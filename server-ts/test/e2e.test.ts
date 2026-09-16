/**
 * End-to-end over a real TCP socket: connect, receive 694, send 682, get 681.
 * This is the exchange a real client performs on startup.
 */
import { afterAll, beforeAll, describe, expect, test } from "bun:test";
import { ChannelAdmissionRegistry } from "../src/admission.ts";
import { Packet, PacketStream, type Reader } from "../src/packet.ts";
import { opcodeFor } from "../src/opcodes.ts";
import { Store } from "../src/store.ts";
import { PING_INTERVAL_MS, listen } from "../src/connection.ts";
import { Result, type GameServer } from "../src/ops/s2c/GL_LOGIN_ACK.ts";

const servers: readonly GameServer[] = [
  {
    serverId: 1,
    name: "PaperMan",
    host: "127.0.0.1",
    port: 40201,
    listingFlag: 0,
    group: 0,
    channelGroups: [[{ channelType: 1, name: "Channel 1", port: 40201, listingFlag: 0 }], [], []],
  },
];

let store: Store;
let listener: ReturnType<typeof listen>;
let port: number;
let admissions: ChannelAdmissionRegistry;

beforeAll(async () => {
  store = new Store();
  admissions = new ChannelAdmissionRegistry();
  await store.createAccount("alice", "hunter2");
  listener = listen({
    role: "login",
    hostname: "127.0.0.1",
    port: 0, // ephemeral
    store,
    servers,
    log: () => {},
    admissions,
    channelName: "Channel 1",
  });
  port = listener.port;
});

afterAll(() => {
  listener.stop(true);
  store.close();
});

/**
 * Minimal client: decodes inbound packets into a queue and lets a test await
 * the next one.
 *
 * Waiters are parked in a list rather than a single `notify` slot -- with one
 * slot, a packet arriving while nobody is waiting would fire and clear the
 * callback, losing the wakeup and making the test flaky.
 */
function connectClient() {
  const stream = new PacketStream();
  const inbox: Reader[] = [];
  const waiters: ((reader: Reader) => void)[] = [];

  const ready = Bun.connect({
    hostname: "127.0.0.1",
    port,
    socket: {
      data(_socket, chunk) {
        stream.push(new Uint8Array(chunk));
        for (const reader of stream.drain()) {
          const waiter = waiters.shift();
          if (waiter) waiter(reader);
          else inbox.push(reader);
        }
      },
    },
  });

  const next = (timeoutMs = 2000): Promise<Reader> => {
    const buffered = inbox.shift();
    if (buffered) return Promise.resolve(buffered);

    return new Promise<Reader>((resolve, reject) => {
      const timer = setTimeout(() => {
        const at = waiters.indexOf(settle);
        if (at >= 0) waiters.splice(at, 1);
        reject(new Error("timed out waiting for a packet"));
      }, timeoutMs);

      const settle = (reader: Reader): void => {
        clearTimeout(timer);
        resolve(reader);
      };
      waiters.push(settle);
    });
  };

  /** Assert nothing arrives within `ms`. */
  const expectSilence = async (ms: number): Promise<void> => {
    await Bun.sleep(ms);
    if (inbox.length > 0) {
      throw new Error(`expected silence but received opcode ${inbox[0]!.opcode}`);
    }
  };

  return { ready, next, expectSilence };
}

function loginRequest(account: string, password: string): Packet {
  const high = BigInt((811034967 ^ 0xb1a9d7c7) >>> 0);
  return new Packet(opcodeFor("GL_LOGIN_REQ"))
    .str(account)
    .str(password)
    .u64((high << 32n) | 0xf1e1ab0en)
    .u8(2)
    .zeros(24);
}

describe("live login over TCP", () => {
  test("server greets with 694 before any request", async () => {
    const client = connectClient();
    const socket = await client.ready;
    const greeting = await client.next();
    expect(greeting.opcode).toBe(opcodeFor("GL_ACCOUNTCONNSUCC"));
    expect(greeting.u16()).toBe(0x2580);
    socket.end();
  });

  test("valid credentials produce a success ack", async () => {
    const client = connectClient();
    const socket = await client.ready;
    await client.next(); // 694

    socket.write(loginRequest("alice", "hunter2").encode());
    const ack = await client.next();
    expect(ack.opcode).toBe(opcodeFor("GL_LOGIN_ACK"));
    expect(ack.s32()).toBe(Result.Success);
    expect(ack.s32()).toBeGreaterThan(0); // user_no
    socket.end();
  });

  test("bad credentials produce a rejection", async () => {
    const client = connectClient();
    const socket = await client.ready;
    await client.next();

    socket.write(loginRequest("alice", "wrong").encode());
    const ack = await client.next();
    expect(ack.opcode).toBe(opcodeFor("GL_LOGIN_ACK"));
    expect(ack.s32()).toBe(Result.BadCredentials);
    socket.end();
  });

  test("an inbound 101 is never answered with 102", async () => {
    // Replying to the client's ping reply would loop both sides forever.
    const client = connectClient();
    const socket = await client.ready;
    await client.next(); // 694

    socket.write(new Packet(opcodeFor("GT_PING_REQ")).encode());

    // The heartbeat is far off, so any traffic now would be a wrong reply.
    expect(PING_INTERVAL_MS).toBeGreaterThan(1000);
    await client.expectSilence(200);

    // Only a real request should produce traffic.
    socket.write(loginRequest("alice", "hunter2").encode());
    expect((await client.next()).opcode).toBe(opcodeFor("GL_LOGIN_ACK"));
    socket.end();
  });

  test("two frames written together are both handled", async () => {
    const client = connectClient();
    const socket = await client.ready;
    await client.next();

    const first = loginRequest("alice", "wrong").encode();
    const second = loginRequest("alice", "hunter2").encode();
    const merged = new Uint8Array(first.length + second.length);
    merged.set(first);
    merged.set(second, first.length);
    socket.write(merged);

    expect((await client.next()).s32()).toBe(Result.BadCredentials);
    expect((await client.next()).s32()).toBe(Result.Success);
    socket.end();
  });

  test("replies keep request order under concurrent handlers", async () => {
    // Handlers are async (argon2 verify), and a wrong password resolves on a
    // different path from a right one. Dispatching concurrently let the second
    // reply overtake the first; the client pairs replies to requests by order.
    // A successful 681 ends the login conversation, so the batch stops there.
    const client = connectClient();
    const socket = await client.ready;
    await client.next();

    const sequence = ["wrong", "hunter2"] as const;
    const batch = sequence.map((pw) => loginRequest("alice", pw).encode());
    const merged = new Uint8Array(batch.reduce((n, f) => n + f.length, 0));
    let at = 0;
    for (const frame of batch) {
      merged.set(frame, at);
      at += frame.length;
    }
    socket.write(merged);

    for (const pw of sequence) {
      const expected = pw === "hunter2" ? Result.Success : Result.BadCredentials;
      expect((await client.next()).s32()).toBe(expected);
    }
    socket.end();
  });
});
