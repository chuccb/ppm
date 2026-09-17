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
import userListRequest from "../src/ops/c2s/GL_USERLIST_REQ.ts";

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

  test("preserves every valid native u16 threshold, including ignored values", () => {
    expect(reread(buildPacket("GL_ACCOUNTCONNSUCC", 0x2581)).u16()).toBe(0x2581);
    expect(reread(buildPacket("GL_ACCOUNTCONNSUCC", 0)).u16()).toBe(0);
    expect(() => buildPacket("GL_ACCOUNTCONNSUCC", 0x10000)).toThrow(RangeError);
    expect(() => buildPacket("GL_ACCOUNTCONNSUCC", 1.5)).toThrow(RangeError);
  });
});

describe("105 — user-list request", () => {
  test("accepts only the value the native writer can emit", () => {
    const replies: string[] = [];
    const connection = {
      reply: (name: string) => replies.push(name),
    } as unknown as Parameters<typeof userListRequest>[1];
    const packet = (value: number) => new Packet(opcodeFor("GL_USERLIST_REQ")).u8(value);

    userListRequest(reread(packet(1)), connection);
    expect(replies).toEqual(["GL_USERLIST_ACK"]);
    expect(() => userListRequest(reread(packet(0)), connection)).toThrow(/native writer emits/);
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

  test("rejects source and raw fingerprint combinations the native builder cannot emit", () => {
    const sourceZero = new Packet(opcodeFor("GL_LOGIN_REQ"))
      .str("a")
      .str("b")
      .u64(0xf1e1ab0en)
      .u8(0)
      .zeros(23)
      .u8(1);
    expect(() => readCredentials(reread(sourceZero))).toThrow(/source 0/);

    const sourceMac = new Packet(opcodeFor("GL_LOGIN_REQ"))
      .str("a")
      .str("b")
      .u64(0xf1e1ab0en)
      .u8(1)
      .zeros(6)
      .zeros(17)
      .u8(1);
    expect(() => readCredentials(reread(sourceMac))).toThrow(/source 1/);

    const sourceStorage = new Packet(opcodeFor("GL_LOGIN_REQ"))
      .str("a")
      .str("b")
      .u64(0xf1e1ab0en)
      .u8(2)
      .zeros(23)
      .u8(1);
    expect(() => readCredentials(reread(sourceStorage))).toThrow(/source 2/);
  });
});

describe("681 — login ack", () => {
  const servers: GameServer[] = [
    {
      serverId: 1,
      name: "PaperMan",
      host: "127.0.0.1",
      port: 40201,
      flag: 0,
      group: 0,
      channelGroups: [
        { maxUsers: 100, channel: { type: 1, name: "Channel 1", currentUsers: 0, flag: 0 } },
        { maxUsers: 0 },
        { maxUsers: 0 },
      ],
    },
  ];

  test("failure writes a full raw s32 word", () => {
    const reader = build("GL_LOGIN_ACK", 0x1234_5678);
    expect(reader.opcode).toBe(681);
    expect(reader.s32()).toBe(0x1234_5678);
    expect(reader.remaining).toBe(0);
  });

  test("keeps the named bad-credentials code available", () => {
    const reader = build("GL_LOGIN_ACK", Result.BadCredentials);
    expect(reader.s32()).toBe(2);
    expect(reader.remaining).toBe(0);
  });

  test("rejects a result outside native s32", () => {
    expect(() => buildPacket("GL_LOGIN_ACK", 0x8000_0000)).toThrow(/result/);
    expect(() => buildPacket("GL_LOGIN_ACK", -0x8000_0001)).toThrow(/result/);
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
    expect(reader.u16()).toBe(40201); // selected-server TCP endpoint port
    expect(reader.u8()).toBe(0); // flag
    expect(reader.s16()).toBe(0); // group

    expect(reader.s16()).toBe(100); // max_users / USERS denominator
    expect(reader.u8()).toBe(1); // ch_type
    expect(reader.str()).toBe("Channel 1");
    expect(reader.s16()).toBe(0); // current_users / USERS numerator
    expect(reader.u8()).toBe(0); // ch_flag
    expect(reader.s16()).toBe(0); // group 1 max_users, empty
    expect(reader.s16()).toBe(0); // group 2 max_users, empty

    expect(reader.s32()).toBe(0); // billing_first
    expect(reader.s32()).toBe(0); // billing_second
    expect(reader.remaining).toBe(0);
  });

  test("writes exactly one raw extension tuple when its positive gate is explicit", () => {
    const reader = build("GL_LOGIN_ACK", {
      userNo: 7,
      rawExtension: { gate: 2, s32First: -11, s32Second: 0x1234_5678, featureFlag: 1 },
      servers,
    });

    reader.s32(); // result
    reader.s32(); // user_no
    reader.s32(); // n100
    expect(reader.s32()).toBe(2); // positive gate is not a tuple count
    expect(reader.s32()).toBe(-11);
    expect(reader.s32()).toBe(0x1234_5678);
    expect(reader.u8()).toBe(1);
    expect(reader.s16()).toBe(1); // server_count follows the one tuple
  });

  test("validates the raw extension tuple without changing the safe default", () => {
    const defaultReader = build("GL_LOGIN_ACK", { userNo: 7, servers });
    defaultReader.s32();
    defaultReader.s32();
    defaultReader.s32();
    expect(defaultReader.s32()).toBe(0); // no extension tuple follows

    const negativeGateReader = build("GL_LOGIN_ACK", {
      userNo: 7,
      rawExtension: { gate: -1, s32First: 0, s32Second: 0, featureFlag: 0 },
      servers,
    });
    negativeGateReader.s32();
    negativeGateReader.s32();
    negativeGateReader.s32();
    expect(negativeGateReader.s32()).toBe(-1); // <= 0 also has no tuple
    expect(negativeGateReader.s16()).toBe(1); // server_count is next

    expect(() => buildPacket("GL_LOGIN_ACK", {
      userNo: 7,
      rawExtension: { gate: 1, s32First: 0x8000_0000, s32Second: 0, featureFlag: 0 },
      servers,
    })).toThrow(/s32First/);
    expect(() => buildPacket("GL_LOGIN_ACK", {
      userNo: 7,
      rawExtension: { gate: 1, s32First: 0, s32Second: 0, featureFlag: 0x100 },
      servers,
    })).toThrow(/featureFlag/);
  });

  test("a type-3 channel carries the extra byte", () => {
    const reader = build("GL_LOGIN_ACK", {
      userNo: 1,
      servers: [
        {
          ...servers[0]!,
          channelGroups: [
            { maxUsers: 100, channel: { type: 3, name: "AI", currentUsers: 0, flag: 0, extra: 9 } },
            { maxUsers: 0 },
            { maxUsers: 0 },
          ],
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
    expect(reader.s16()).toBe(100); // max_users
    expect(reader.u8()).toBe(3); // ch_type
    expect(reader.str()).toBe("AI");
    expect(reader.s16()).toBe(0); // current_users
    expect(reader.u8()).toBe(0);
    expect(reader.u8()).toBe(9); // extra, only present for type 3
  });

  test("defaults type-3 extra and ignores extra on other channel types", () => {
    const missingExtra = build("GL_LOGIN_ACK", {
      userNo: 1,
      servers: [{
        ...servers[0]!,
        channelGroups: [
          { maxUsers: 100, channel: { type: 3, name: "AI", currentUsers: 0, flag: 0 } },
          { maxUsers: 0 },
          { maxUsers: 0 },
        ],
      }],
    });
    for (let i = 0; i < 4; i++) missingExtra.s32();
    missingExtra.s16();
    missingExtra.s16();
    missingExtra.str();
    missingExtra.str();
    missingExtra.s16();
    missingExtra.u8();
    missingExtra.s16();
    expect(missingExtra.s16()).toBe(100);
    expect(missingExtra.u8()).toBe(3);
    expect(missingExtra.str()).toBe("AI");
    expect(missingExtra.s16()).toBe(0);
    expect(missingExtra.u8()).toBe(0);
    expect(missingExtra.u8()).toBe(0);

    const ignoredExtra = build("GL_LOGIN_ACK", {
      userNo: 1,
      servers: [{
        ...servers[0]!,
        channelGroups: [
          { maxUsers: 100, channel: { type: 1, name: "Normal", currentUsers: 0, flag: 0, extra: 9 } },
          { maxUsers: 0 },
          { maxUsers: 0 },
        ],
      }],
    });
    for (let i = 0; i < 4; i++) ignoredExtra.s32();
    ignoredExtra.s16();
    ignoredExtra.s16();
    ignoredExtra.str();
    ignoredExtra.str();
    ignoredExtra.s16();
    ignoredExtra.u8();
    ignoredExtra.s16();
    expect(ignoredExtra.s16()).toBe(100);
    expect(ignoredExtra.u8()).toBe(1);
    expect(ignoredExtra.str()).toBe("Normal");
    expect(ignoredExtra.s16()).toBe(0);
    expect(ignoredExtra.u8()).toBe(0);
    expect(ignoredExtra.s16()).toBe(0); // no extra byte for type 1
  });

  test("emits three group slots without requiring an exact input length", () => {
    const reader = build("GL_LOGIN_ACK", {
      userNo: 1,
      servers: [{
        ...servers[0]!,
        channelGroups: [
          { maxUsers: 0 },
          { maxUsers: 0 },
          { maxUsers: 0 },
          { maxUsers: 100, channel: { type: 1, name: "ignored", currentUsers: 0, flag: 0 } },
        ],
      }],
    });
    for (let i = 0; i < 4; i++) reader.s32();
    reader.s16();
    reader.s16();
    reader.str();
    reader.str();
    reader.s16();
    reader.u8();
    reader.s16();
    expect(reader.s16()).toBe(0);
    expect(reader.s16()).toBe(0);
    expect(reader.s16()).toBe(0);
    expect(reader.s32()).toBe(0);
    expect(reader.s32()).toBe(0);
    expect(reader.remaining).toBe(0);
  });

  test("keeps operational raw2 counts signed and width-safe", () => {
    const reader = build("GL_LOGIN_ACK", {
      userNo: 1,
      servers: [{
        ...servers[0]!,
        serverId: 0xffff,
        group: 0xffff,
        channelGroups: [
          { maxUsers: -1 },
          { maxUsers: 1, channel: { type: 1, name: "Signed", currentUsers: -2, flag: 0 } },
          { maxUsers: 0 },
        ],
      }],
    });
    for (let i = 0; i < 4; i++) reader.s32();
    reader.s16();
    expect(reader.u16()).toBe(0xffff); // native raw2 server_id
    reader.str();
    reader.str();
    reader.u16();
    reader.u8();
    expect(reader.u16()).toBe(0xffff); // native raw2 group
    expect(reader.s16()).toBe(-1); // operational max_users remains signed
    expect(reader.s16()).toBe(1);
    expect(reader.u8()).toBe(1);
    expect(reader.str()).toBe("Signed");
    expect(reader.s16()).toBe(-2);
    expect(reader.u8()).toBe(0);
    expect(reader.s16()).toBe(0);
    expect(reader.s32()).toBe(0);
    expect(reader.s32()).toBe(0);
    expect(reader.remaining).toBe(0);
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
          channelGroups: [
            { maxUsers: 1, channel: { type: 1, name: "c".repeat(50), currentUsers: 0, flag: 0 } },
            { maxUsers: 0 },
            { maxUsers: 0 },
          ],
        }],
      }),
    ).toThrow(/channel name/);
  });

  test("keeps the s32 login words unmasked", () => {
    const reader = build("GL_LOGIN_ACK", { userNo: 7, n100: 0x1234_5678, servers });
    reader.s32();
    reader.s32();
    expect(reader.s32()).toBe(0x1234_5678);
    expect(() => buildPacket("GL_LOGIN_ACK", { userNo: 0x8000_0000, servers })).toThrow(/user_no/);
    expect(() => buildPacket("GL_LOGIN_ACK", { userNo: 1, n100: 0x8000_0000, servers })).toThrow(/n100/);
    expect(() => buildPacket("GL_LOGIN_ACK", {
      userNo: 1,
      servers: [{ ...servers[0]!, serverId: Number.NaN }],
    })).toThrow(/server_id/);
  });

  test("requires a channel body when the native group gate is positive", () => {
    expect(() =>
      buildPacket("GL_LOGIN_ACK", {
        userNo: 1,
        servers: [{
          ...servers[0]!,
          channelGroups: [{ maxUsers: 100 }, { maxUsers: 0 }, { maxUsers: 0 }],
        }],
      }),
    ).toThrow(/positive channel group needs a channel body/);
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
