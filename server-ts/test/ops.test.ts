import { describe, expect, test } from "bun:test";
import { Packet, decode } from "../src/packet.ts";
import { opcodeFor } from "../src/opcodes.ts";
import {
  build as buildPacket,
  handlerFor,
  summary,
  type OutboundArgs,
  type OutboundName,
} from "../src/ops/registry.ts";
import { Result, type GameServer } from "../src/ops/s2c/GL_LOGIN_ACK.ts";
import { read as readCredentials } from "../src/ops/c2s/GL_LOGIN_REQ.ts";

const build = <N extends OutboundName>(name: N, ...args: OutboundArgs<N>) =>
  decode(buildPacket(name, ...args).encode());

// Keep the type-only contract honest without executing invalid calls.
if (false) {
  // @ts-expect-error c2s names are not outbound builders
  buildPacket("GL_LOGIN_REQ");
  // @ts-expect-error GL_LOGIN_ACK requires its result argument
  buildPacket("GL_LOGIN_ACK");
  // @ts-expect-error the result argument is not a string
  buildPacket("GL_LOGIN_ACK", "not-a-result");
}

/** Build a 682 exactly as the client's builder does. */
function clientLoginRequest(account: string, password: string, dataRevision = 811034967) {
  const low = 0xf1e1ab0en;
  const high = BigInt((dataRevision ^ 0xb1a9d7c7) >>> 0);
  return new Packet(opcodeFor("GL_LOGIN_REQ"))
    .str(account)
    .str(password)
    .u64((high << 32n) | low)
    .u8(2)
    .zeros(24);
}

const reread = (packet: Packet) => decode(packet.encode());

describe("694 — compression threshold and login trigger", () => {
  test("carries the threshold as a u16", () => {
    const reader = build("GL_ACCOUNTCONNSUCC");
    expect(reader.opcode).toBe(694);
    expect(reader.u16()).toBe(0x2580); // compression disabled
  });

  test("rejects a threshold the client would ignore", () => {
    expect(() => buildPacket("GL_ACCOUNTCONNSUCC", 0x2581)).toThrow(RangeError);
    expect(() => buildPacket("GL_ACCOUNTCONNSUCC", 0)).toThrow(RangeError);
  });
});

describe("682 — login request", () => {
  test("parses the exact client shape", () => {
    const reader = reread(clientLoginRequest("alice", "hunter2"));
    const request = readCredentials(reader);
    expect(request.account).toBe("alice");
    expect(request.passwordOrToken).toBe("hunter2");
    expect(request.dataRevision).toBe(811034967);
    expect(request.fingerprintSource).toBe(2);
    expect(request.fingerprint).toHaveLength(24);
  });

  test("rejects a bad guard dword", () => {
    const packet = new Packet(opcodeFor("GL_LOGIN_REQ"))
      .str("a")
      .str("b")
      .u64(0n) // guard absent
      .u8(0)
      .zeros(24);
    expect(() => readCredentials(reread(packet))).toThrow(/guard mismatch/);
  });

  test("rejects trailing bytes", () => {
    const packet = clientLoginRequest("alice", "pw").u8(0xff);
    expect(() => readCredentials(reread(packet))).toThrow(/trailing/);
  });

  test("rejects a truncated fingerprint", () => {
    const packet = new Packet(opcodeFor("GL_LOGIN_REQ"))
      .str("a")
      .str("b")
      .u64(0xf1e1ab0en)
      .u8(0)
      .zeros(8);
    expect(() => readCredentials(reread(packet))).toThrow(RangeError);
  });
});

describe("681 — login ack", () => {
  const servers: GameServer[] = [
    {
      serverId: 1,
      name: "PaperMan",
      host: "127.0.0.1",
      port: 40201, // > 32767: valid, the s16 bit pattern is reused as u_short
      flag: 0,
      group: 0,
      channelGroups: [[{ type: 1, name: "Channel 1", port: 40301, flag: 0 }], [], []],
    },
  ];

  test("failure writes a full s32 word", () => {
    const reader = build("GL_LOGIN_ACK", Result.BadCredentials);
    expect(reader.opcode).toBe(681);
    expect(reader.s32()).toBe(2);
    expect(reader.remaining).toBe(0);
  });

  test("success round-trips in the documented field order", () => {
    const reader = build("GL_LOGIN_ACK", { userNo: 7, servers });

    expect(reader.s32()).toBe(Result.Success);
    expect(reader.s32()).toBe(7); // user_no
    expect(reader.s32()).toBe(0); // charge mode
    expect(reader.s32()).toBe(0); // ext_count

    expect(reader.s16()).toBe(1); // server_count
    expect(reader.s16()).toBe(1); // server_id
    expect(reader.str()).toBe("PaperMan");
    expect(reader.str()).toBe("127.0.0.1");
    expect(reader.s16() & 0xffff).toBe(40201); // u_short bit pattern
    expect(reader.u8()).toBe(0); // flag
    expect(reader.s16()).toBe(0); // group

    expect(reader.s16()).toBe(1); // group 0 channel count
    expect(reader.u8()).toBe(1); // ch_type
    expect(reader.str()).toBe("Channel 1");
    expect(reader.s16() & 0xffff).toBe(40301);
    expect(reader.u8()).toBe(0); // ch_flag
    expect(reader.s16()).toBe(0); // group 1 is empty
    expect(reader.s16()).toBe(0); // group 2 is empty

    expect(reader.s32()).toBe(0); // billing_first
    expect(reader.s32()).toBe(0); // billing_second
    expect(reader.remaining).toBe(0);
  });

  test("a type-3 channel carries the extra byte", () => {
    const reader = build("GL_LOGIN_ACK", {
      userNo: 1,
      servers: [
        {
          ...servers[0]!,
          channelGroups: [[{ type: 3, name: "AI", port: 1, flag: 0, extra: 9 }], [], []],
        },
      ],
    });
    for (let i = 0; i < 4; i++) reader.s32();
    reader.s16();
    reader.s16();
    reader.str();
    reader.str();
    reader.s16();
    reader.u8();
    reader.s16();
    expect(reader.s16()).toBe(1);
    expect(reader.u8()).toBe(3); // ch_type
    expect(reader.str()).toBe("AI");
    expect(reader.s16()).toBe(1);
    expect(reader.u8()).toBe(0);
    expect(reader.u8()).toBe(9); // extra, only present for type 3
  });

  test("insists on exactly three channel groups", () => {
    expect(() =>
      buildPacket("GL_LOGIN_ACK", {
        userNo: 1,
        servers: [{ ...servers[0]!, channelGroups: [[]] }],
      }),
    ).toThrow(/three channel groups/);
  });

  test("rejects names that overrun native fixed buffers", () => {
    expect(() =>
      buildPacket("GL_LOGIN_ACK", {
        userNo: 1,
        servers: [{ ...servers[0]!, name: "s".repeat(50) }],
      }),
    ).toThrow(/server name/);
    expect(() =>
      buildPacket("GL_LOGIN_ACK", {
        userNo: 1,
        servers: [{
          ...servers[0]!,
          channelGroups: [[{ type: 1, name: "c".repeat(50), port: 1, flag: 0 }], [], []],
        }],
      }),
    ).toThrow(/channel name/);
  });

  test("rejects out-of-range login words before masking them", () => {
    expect(() => buildPacket("GL_LOGIN_ACK", { userNo: 0x8000_0000, servers })).toThrow(/user_no/);
    expect(() => buildPacket("GL_LOGIN_ACK", { userNo: 1, n100: 128, servers })).toThrow(/n100/);
  });

  test("rejects a multi-entry group that the native reader cannot consume", () => {
    expect(() =>
      buildPacket("GL_LOGIN_ACK", {
        userNo: 1,
        servers: [{
          ...servers[0]!,
          channelGroups: [[
            { type: 1, name: "one", port: 1, flag: 0 },
            { type: 1, name: "two", port: 2, flag: 0 },
          ], [], []],
        }],
      }),
    ).toThrow(/at most one channel/);
  });

});

describe("registry", () => {
  test("direction comes from the folder, not the REQ/ACK suffix", () => {
    // GT_PING_ACK is an _ACK the server sends; GT_PING_REQ is a _REQ it
    // receives. A suffix rule would get both backwards.
    expect(handlerFor(opcodeFor("GT_PING_REQ"))).toBeDefined();
    expect(handlerFor(opcodeFor("GT_PING_ACK"))).toBeUndefined();
    expect(() => buildPacket("GT_PING_ACK")).not.toThrow();
  });

  test("the registry discovers both operation folders at startup", () => {
    expect(summary()).toMatch(/^c2s 15 \(/);
    expect(summary()).toMatch(/\), s2c 16 \(/);
    expect(summary()).toContain("GL_LOGIN_ACK");
    expect(summary()).toContain("GL_LOGIN_REQ");
  });
});
