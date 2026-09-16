import { afterAll, beforeAll, describe, expect, test } from "bun:test";
import { Packet, PacketStream, decode, type Reader } from "../src/packet.ts";
import { opcodeFor } from "../src/opcodes.ts";
import { Registry, type OutboundArgs, type OutboundName } from "../src/ops/registry.ts";
import { Store } from "../src/store.ts";
import { listen } from "../src/session.ts";
import { read as readHandoff } from "../src/ops/c2s/PM_UDPSTART_REQ.ts";
import { Result } from "../src/ops/s2c/PM_UDPSTART_ACK.ts";

const ops = Registry.load();
const build = <N extends OutboundName>(name: N, ...args: OutboundArgs<N>) =>
  decode(ops.build(name, ...args).encode());

/** Build PM_UDPSTART_REQ exactly as sub_555C60 does. */
function handoff(identity: string, chargeMode = 0, extCount = 0, literal = 1): Packet {
  return new Packet(opcodeFor("PM_UDPSTART_REQ"))
    .str(identity)
    .s32(chargeMode)
    .u8(literal)
    .s32(extCount);
}

describe("GL_TCPCONNSUCC", () => {
  test("is an empty greeting, like its login-server counterpart", () => {
    const r = build("GL_TCPCONNSUCC");
    expect(r.opcode).toBe(opcodeFor("GL_TCPCONNSUCC"));
    expect(r.remaining).toBe(0);
  });
});

describe("PM_UDPSTART_REQ", () => {
  test("parses the shape sub_555C60 emits", () => {
    const parsed = readHandoff(decode(handoff("abc123", 7, 2).encode()));
    expect(parsed).toEqual({ identity: "abc123", chargeMode: 7, extCount: 2 });
  });

  test("rejects a middle byte that is not the hardcoded 1", () => {
    expect(() => readHandoff(decode(handoff("a", 0, 0, 2).encode()))).toThrow(/literal 1/);
  });

  test("rejects an identity longer than the client's String[24]", () => {
    const tooLong = "x".repeat(24);
    expect(() => readHandoff(decode(handoff(tooLong).encode()))).toThrow(/23-byte/);
  });

  test("rejects trailing bytes", () => {
    const padded = handoff("a").u8(0);
    expect(() => readHandoff(decode(padded.encode()))).toThrow(/trailing/);
  });
});

describe("PM_UDPSTART_ACK", () => {
  test("writes every field, in the order sub_555D50 reads them", () => {
    const r = build("PM_UDPSTART_ACK", {
      result: Result.Success,
      channelName: "Channel 1",
      dailyLoginRewardPg: 500,
      restrictionLevel: 3,
      restrictionKdr: 1.5,
    });

    expect(r.u8()).toBe(Result.Success);
    expect(r.u8()).toBe(0); // rank restricted
    expect(r.s32()).toBe(500); // daily login reward
    expect(r.str()).toBe("Channel 1");
    expect(r.s32()).toBe(0); // read then unused
    expect(r.s32()).toBe(0); // read then unused
    expect(r.s32()).toBe(3); // restriction level
    expect(r.f32()).toBe(1.5); // restriction K/D
    expect(r.u32()).toBe(0); // client request context
    expect(r.u8()).toBe(0); // no netcafe block
    expect(r.remaining).toBe(0);
  });

  test("all fields are present even on failure, because the client reads first", () => {
    const r = build("PM_UDPSTART_ACK", {
      result: Result.VersionMismatch,
      channelName: "x",
    });
    expect(r.u8()).toBe(Result.VersionMismatch);
    r.u8();
    r.s32();
    r.str();
    r.s32();
    r.s32();
    r.s32();
    r.f32();
    r.u32();
    expect(r.u8()).toBe(0);
    expect(r.remaining).toBe(0);
  });

  test("rejects a channel name longer than the client's char[40]", () => {
    expect(() =>
      ops.build("PM_UDPSTART_ACK", {
        result: Result.Success,
        channelName: "y".repeat(40),
      }),
    ).toThrow(/39 bytes/);
  });
});

describe("live channel handshake", () => {
  let store: Store;
  let listener: ReturnType<typeof listen>;

  beforeAll(() => {
    store = new Store();
    listener = listen({
      role: "channel",
      hostname: "127.0.0.1",
      port: 0,
      store,
      servers: [],
      ops,
      log: () => {},
      channelName: "Test Channel",
    });
  });

  afterAll(() => {
    listener.stop(true);
    store.close();
  });

  test("greets with GL_TCPCONNSUCC, then admits on the handoff", async () => {
    const stream = new PacketStream();
    const inbox: Reader[] = [];
    const waiters: ((r: Reader) => void)[] = [];

    const socket = await Bun.connect({
      hostname: "127.0.0.1",
      port: listener.port,
      socket: {
        data(_s, chunk) {
          stream.push(new Uint8Array(chunk));
          for (const r of stream.drain()) {
            const waiter = waiters.shift();
            if (waiter) waiter(r);
            else inbox.push(r);
          }
        },
      },
    });

    const next = (): Promise<Reader> => {
      const buffered = inbox.shift();
      if (buffered) return Promise.resolve(buffered);
      return new Promise((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error("timed out")), 2000);
        waiters.push((r) => {
          clearTimeout(timer);
          resolve(r);
        });
      });
    };

    // The channel greeting differs from the login server's.
    const greeting = await next();
    expect(greeting.opcode).toBe(opcodeFor("GL_TCPCONNSUCC"));

    socket.write(handoff("player-1", 0, 0).encode());
    const ack = await next();
    expect(ack.opcode).toBe(opcodeFor("PM_UDPSTART_ACK"));
    expect(ack.u8()).toBe(Result.Success);
    ack.u8();
    ack.s32();
    expect(ack.str()).toBe("Test Channel");

    socket.end();
  });
});
