import { describe, expect, test } from "bun:test";
import { Packet, decode } from "../src/packet.ts";
import { Op } from "../src/opcodes.ts";
import { Store } from "../src/store.ts";
import {
  GL_ACCOUNTCONNSUCC,
  GL_LOGIN_ACK,
  GL_LOGIN_ACK_rejected,
  type GameServer,
  Result,
  login,
  readGL_LOGIN_REQ,
} from "../src/login.ts";

/** Build a 682 exactly as the client's builder does. */
function clientLoginRequest(account: string, password: string, dataRevision = 811034967) {
  const low = 0xf1e1ab0en;
  const high = BigInt((dataRevision ^ 0xb1a9d7c7) >>> 0);
  return new Packet(Op.GL_LOGIN_REQ)
    .str(account)
    .str(password)
    .u64((high << 32n) | low)
    .u8(2)
    .zeros(24);
}

const reread = (packet: Packet) => decode(packet.encode());

describe("694 — compression threshold and login trigger", () => {
  test("carries the threshold as a u16", () => {
    const reader = reread(GL_ACCOUNTCONNSUCC());
    expect(reader.opcode).toBe(694);
    expect(reader.u16()).toBe(0x2580); // compression disabled
  });

  test("rejects a threshold the client would ignore", () => {
    expect(() => GL_ACCOUNTCONNSUCC(0x2581)).toThrow(RangeError);
    expect(() => GL_ACCOUNTCONNSUCC(0)).toThrow(RangeError);
  });
});

describe("682 — login request", () => {
  test("parses the exact client shape", () => {
    const reader = reread(clientLoginRequest("alice", "hunter2"));
    const request = readGL_LOGIN_REQ(reader);
    expect(request.account).toBe("alice");
    expect(request.password).toBe("hunter2");
    expect(request.dataRevision).toBe(811034967);
    expect(request.fingerprintSource).toBe(2);
    expect(request.fingerprint).toHaveLength(24);
  });

  test("rejects a bad guard dword", () => {
    const packet = new Packet(Op.GL_LOGIN_REQ)
      .str("a")
      .str("b")
      .u64(0n) // guard absent
      .u8(0)
      .zeros(24);
    expect(() => readGL_LOGIN_REQ(reread(packet))).toThrow(/guard mismatch/);
  });

  test("rejects trailing bytes", () => {
    const packet = clientLoginRequest("alice", "pw").u8(0xff);
    expect(() => readGL_LOGIN_REQ(reread(packet))).toThrow(/trailing/);
  });

  test("rejects a truncated fingerprint", () => {
    const packet = new Packet(Op.GL_LOGIN_REQ)
      .str("a")
      .str("b")
      .u64(0xf1e1ab0en)
      .u8(0)
      .zeros(8);
    expect(() => readGL_LOGIN_REQ(reread(packet))).toThrow(RangeError);
  });
});

describe("681 — login ack", () => {
  const servers: GameServer[] = [
    {
      id: 1,
      name: "PaperMan",
      host: "127.0.0.1",
      port: 40201, // > 32767: valid, the s16 bit pattern is reused as u_short
      flag: 0,
      group: 0,
      channelGroups: [[{ type: 1, name: "Channel 1", port: 40301, flag: 0 }], [], []],
    },
  ];

  test("failure writes a full s32 word", () => {
    const reader = reread(GL_LOGIN_ACK_rejected(Result.BadCredentials));
    expect(reader.opcode).toBe(681);
    expect(reader.s32()).toBe(2);
    expect(reader.remaining).toBe(0);
  });

  test("success round-trips in the documented field order", () => {
    const reader = reread(
      GL_LOGIN_ACK(7, servers),
    );

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
    const reader = reread(
      GL_LOGIN_ACK(1, [
        {
          ...servers[0]!,
          channelGroups: [[{ type: 3, name: "AI", port: 1, flag: 0, extra: 9 }], [], []],
        },
      ]),
    );
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
      GL_LOGIN_ACK(1, [{ ...servers[0]!, channelGroups: [[]] }]),
    ).toThrow(/three channel groups/);
  });

  test("end-to-end: good and bad credentials", async () => {
    const store = new Store();
    await store.createAccount("alice", "hunter2");

    const good = await login(
      store,
      readGL_LOGIN_REQ(reread(clientLoginRequest("alice", "hunter2"))),
      servers,
    );
    expect(good.accountId).toBeGreaterThan(0);
    expect(reread(good.reply).s32()).toBe(Result.Success);

    const bad = await login(
      store,
      readGL_LOGIN_REQ(reread(clientLoginRequest("alice", "nope"))),
      servers,
    );
    expect(bad.accountId).toBeNull();
    expect(reread(bad.reply).s32()).toBe(Result.BadCredentials);
    store.close();
  });
});
