/**
 * End-to-end over a real TCP socket: connect, receive 694, send 682, get 681.
 * This is the exchange a real client performs on startup.
 */
import { afterAll, beforeAll, describe, expect, test } from "bun:test";
import { FrameStream, encodeFrame } from "../src/codec/frame.ts";
import { PacketWriter, type PacketReader } from "../src/codec/packet.ts";
import { Op } from "../src/codec/opcodes.ts";
import { Store } from "../src/db/schema.ts";
import { listen } from "../src/net/listener.ts";
import { LoginResult, type ServerEntry } from "../src/handlers/login.ts";

const servers: readonly ServerEntry[] = [
  {
    id: 1,
    name: "PaperMan",
    host: "127.0.0.1",
    port: 40201,
    flag: 0,
    group: 0,
    channelGroups: [[{ type: 1, name: "Channel 1", port: 40201, flag: 0 }], [], []],
  },
];

let store: Store;
let listener: ReturnType<typeof listen>;
let port: number;

beforeAll(async () => {
  store = new Store();
  await store.createAccount("alice", "hunter2");
  listener = listen({
    hostname: "127.0.0.1",
    port: 0, // ephemeral
    store,
    servers,
    log: () => {},
  });
  port = listener.port;
});

afterAll(() => {
  listener.stop(true);
  store.close();
});

/** Minimal client: collects decoded packets, lets a test await the next one. */
function connectClient() {
  const stream = new FrameStream();
  const inbox: PacketReader[] = [];
  let notify: (() => void) | null = null;

  const ready = Bun.connect({
    hostname: "127.0.0.1",
    port,
    socket: {
      data(_socket, chunk) {
        stream.push(new Uint8Array(chunk));
        for (const reader of stream.drain()) inbox.push(reader);
        notify?.();
      },
    },
  });

  const next = async (): Promise<PacketReader> => {
    for (let waited = 0; waited < 2000; waited += 10) {
      const reader = inbox.shift();
      if (reader) return reader;
      await new Promise<void>((resolve) => {
        notify = resolve;
        setTimeout(resolve, 10);
      });
      notify = null;
    }
    throw new Error("timed out waiting for a packet");
  };

  return { ready, next };
}

function loginRequest(account: string, password: string): PacketWriter {
  const high = BigInt((811034967 ^ 0xb1a9d7c7) >>> 0);
  return new PacketWriter(Op.GL_LOGIN_REQ)
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
    expect(greeting.opcode).toBe(Op.GL_ACCOUNTCONNSUCC);
    expect(greeting.u16()).toBe(0x2580);
    socket.end();
  });

  test("valid credentials produce a success ack", async () => {
    const client = connectClient();
    const socket = await client.ready;
    await client.next(); // 694

    socket.write(encodeFrame(loginRequest("alice", "hunter2")));
    const ack = await client.next();
    expect(ack.opcode).toBe(Op.GL_LOGIN_ACK);
    expect(ack.s32()).toBe(LoginResult.Success);
    expect(ack.s32()).toBeGreaterThan(0); // user_no
    socket.end();
  });

  test("bad credentials produce a rejection", async () => {
    const client = connectClient();
    const socket = await client.ready;
    await client.next();

    socket.write(encodeFrame(loginRequest("alice", "wrong")));
    const ack = await client.next();
    expect(ack.opcode).toBe(Op.GL_LOGIN_ACK);
    expect(ack.s32()).toBe(LoginResult.BadCredentials);
    socket.end();
  });

  test("two frames written together are both handled", async () => {
    const client = connectClient();
    const socket = await client.ready;
    await client.next();

    const first = encodeFrame(loginRequest("alice", "wrong"));
    const second = encodeFrame(loginRequest("alice", "hunter2"));
    const merged = new Uint8Array(first.length + second.length);
    merged.set(first);
    merged.set(second, first.length);
    socket.write(merged);

    expect((await client.next()).s32()).toBe(LoginResult.BadCredentials);
    expect((await client.next()).s32()).toBe(LoginResult.Success);
    socket.end();
  });
});
