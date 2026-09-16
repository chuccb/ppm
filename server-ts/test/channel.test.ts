import { afterAll, beforeAll, describe, expect, test } from "bun:test";
import { ChannelAdmissionRegistry } from "../src/admission.ts";
import { Packet, PacketStream, decode, type Reader } from "../src/packet.ts";
import { opcodeFor } from "../src/opcodes.ts";
import { build as buildPacket, type OutboundArgs, type OutboundName } from "../src/ops/registry.ts";
import { Store } from "../src/store.ts";
import { listen } from "../src/connection.ts";
import { read as readHandoff } from "../src/ops/c2s/PM_UDPSTART_REQ.ts";
import { Result as EnterResult } from "../src/ops/s2c/GC_ENTERCHANNEL_ACK.ts";
import { Result } from "../src/ops/s2c/PM_UDPSTART_ACK.ts";

const build = <N extends OutboundName>(name: N, ...args: OutboundArgs<N>) =>
  decode(buildPacket(name, ...args).encode());

/** Build PM_UDPSTART_REQ exactly as sub_555C60 does. */
function handoff(identity: string, n100 = 0, extCount = 0, literal = 1): Packet {
  return new Packet(opcodeFor("PM_UDPSTART_REQ"))
    .str(identity)
    .s32(n100)
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
    expect(parsed).toEqual({ identity: "abc123", n100: 7, extCount: 2 });
  });

  test("rejects a middle byte that is not the hardcoded 1", () => {
    expect(() => readHandoff(decode(handoff("a", 0, 0, 2).encode()))).toThrow(/literal 1/);
  });

  test("rejects an identity longer than the client's String[24]", () => {
    const tooLong = "x".repeat(24);
    expect(() => readHandoff(decode(handoff(tooLong).encode()))).toThrow(/23 bytes/);
  });

  test("rejects trailing bytes", () => {
    const padded = handoff("a").u8(0);
    expect(() => readHandoff(decode(padded.encode()))).toThrow(/trailing/);
  });
});

describe("GC_ENTERCHANNEL_ACK", () => {
  test("writes only the three-field failure prefix", () => {
    const r = build("GC_ENTERCHANNEL_ACK", {
      result: EnterResult.GenericError4,
      channelId: 7,
      channelIndex: 2,
    });

    expect(r.u8()).toBe(EnterResult.GenericError4);
    expect(r.s32()).toBe(7);
    expect(r.u8()).toBe(2);
    expect(r.remaining).toBe(0);
  });

  test("preserves an unknown native u8 failure code", () => {
    const r = build("GC_ENTERCHANNEL_ACK", {
      result: 0xfe,
      channelId: 7,
      channelIndex: 2,
    });
    expect(r.u8()).toBe(0xfe);
    expect(r.s32()).toBe(7);
    expect(r.u8()).toBe(2);
    expect(r.remaining).toBe(0);
    expect(() =>
      buildPacket("GC_ENTERCHANNEL_ACK", { result: 0x100, channelId: 7, channelIndex: 2 }),
    ).toThrow(/u8/);
  });

  test("writes the endpoint tail only for success", () => {
    const r = build("GC_ENTERCHANNEL_ACK", {
      result: EnterResult.Success,
      channelId: 1,
      channelIndex: 0,
      endpoint: { host: "127.0.0.1", port: 40202 },
      endpointOpaque: 3,
      channelType: 1,
      clientFlags: 1,
      clientDefault: 5,
    });

    expect(r.u8()).toBe(EnterResult.Success);
    expect(r.s32()).toBe(1);
    expect(r.u8()).toBe(0);
    expect(r.str()).toBe("127.0.0.1");
    expect(r.s32()).toBe(40202);
    expect(r.u8()).toBe(3);
    expect(r.u8()).toBe(1);
    expect(r.u32()).toBe(1);
    expect(r.u8()).toBe(5);
    expect(r.remaining).toBe(0);
  });

  test("keeps the success tail and numeric widths exact", () => {
    expect(() =>
      buildPacket("GC_ENTERCHANNEL_ACK", {
        result: EnterResult.Success,
        channelId: 1,
        channelIndex: 0,
        endpoint: { host: "127.0.0.1", port: Number.NaN },
      }),
    ).toThrow(/endpoint port/);
    expect(() =>
      buildPacket("GC_ENTERCHANNEL_ACK", {
        result: EnterResult.Success,
        channelId: 1,
        channelIndex: 0,
        endpoint: { host: "127.0.0.1", port: 40202 },
        clientFlags: 0x1_0000_0000,
      }),
    ).toThrow(/client_flags/);
    expect(() =>
      buildPacket("GC_ENTERCHANNEL_ACK", {
        result: EnterResult.GenericError4,
        channelId: Number.NaN,
        channelIndex: 0,
      }),
    ).toThrow(/channel_id/);
  });

  test("does not emit an incomplete type-3 AI response", () => {
    expect(() =>
      buildPacket("GC_ENTERCHANNEL_ACK", {
        result: EnterResult.Success,
        channelId: 1,
        channelIndex: 0,
        endpoint: { host: "127.0.0.1", port: 40202 },
        channelType: 3,
      }),
    ).toThrow(/AI tail/);
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

  test("preserves an unknown native u8 result and keeps the fixed prefix", () => {
    const r = build("PM_UDPSTART_ACK", {
      result: 0xfe,
      channelName: "x",
    });
    expect(r.u8()).toBe(0xfe);
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
    expect(() =>
      buildPacket("PM_UDPSTART_ACK", { result: 0x100, channelName: "x" }),
    ).toThrow(/u8/);
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
      buildPacket("PM_UDPSTART_ACK", {
        result: Result.Success,
        channelName: "y".repeat(40),
      }),
    ).toThrow(/39 bytes/);
  });
});

describe("live channel handshake", () => {
  let store: Store;
  let listener: ReturnType<typeof listen>;
  let admissions: ChannelAdmissionRegistry;

  beforeAll(() => {
    store = new Store();
    admissions = new ChannelAdmissionRegistry();
    admissions.issue(1, 0, 0, "127.0.0.1", 10_000);
    listener = listen({
      role: "channel",
      hostname: "127.0.0.1",
      port: 0,
      store,
      servers: [],
      log: () => {},
      admissions,
      channelName: "Test Channel",
      channelType: 1,
      udpHost: "127.0.0.1",
      udpPort: 40202,
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

    socket.write(new Packet(opcodeFor("GC_ENTERCHANNEL_REQ")).u8(0).u8(0).u8(0).encode());
    const entry = await next();
    expect(entry.opcode).toBe(opcodeFor("GC_ENTERCHANNEL_ACK"));
    expect(entry.u8()).toBe(EnterResult.Success);
    expect(entry.s32()).toBe(1);
    expect(entry.u8()).toBe(0);
    expect(entry.str()).toBe("127.0.0.1");
    expect(entry.s32()).toBe(40202);
    expect(entry.u8()).toBe(0); // opaque byte
    expect(entry.u8()).toBe(1); // normal channel type
    expect(entry.u32()).toBe(0); // client flags
    expect(entry.u8()).toBe(5); // native initial default
    expect(entry.remaining).toBe(0);

    socket.write(new Packet(opcodeFor("GC_ENTERCHANNEL_REQ")).u8(0).u8(0).u8(0).encode());
    const repeated = await next();
    expect(repeated.u8()).toBe(EnterResult.GenericError4);
    expect(repeated.s32()).toBe(1);
    expect(repeated.u8()).toBe(0);
    expect(repeated.remaining).toBe(0);

    socket.end();
  });
});
