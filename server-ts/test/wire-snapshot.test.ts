import { describe, expect, test } from "bun:test";
import GL_CLIENTINFO_ACK from "../src/ops/s2c/GL_CLIENTINFO_ACK.ts";
import GL_EXPIRE_PARTSUP_ACK from "../src/ops/s2c/GL_EXPIRE_PARTSUP_ACK.ts";
import GL_FRIEND_LIST_ACK from "../src/ops/s2c/GL_FRIEND_LIST_ACK.ts";
import GL_FRIEND_ADD_ACK from "../src/ops/s2c/GL_FRIEND_ADD_ACK.ts" ;
import GL_MSG_ADD_ACK from "../src/ops/s2c/GL_MSG_ADD_ACK.ts";
import GL_NEW_MSG_COUNT_ACK from "../src/ops/s2c/GL_NEW_MSG_COUNT_ACK.ts";
import GL_BILLTOKEN_ACK from "../src/ops/s2c/GL_BILLTOKEN_ACK.ts";
import GL_RACKINGWEB_TOKEN_ACK from "../src/ops/s2c/GL_RACKINGWEB_TOKEN_ACK.ts";
import GQ_QUEST_ACCEPT_DAILY_ACK from "../src/ops/s2c/GQ_QUEST_ACCEPT_DAILY_ACK.ts";
import GQ_QUEST_USER_COMPLETE_HONOR_ACK from "../src/ops/s2c/GQ_QUEST_USER_COMPLETE_HONOR_ACK.ts";
import GI_CHANGEDATA_ACK from "../src/ops/s2c/GI_CHANGEDATA_ACK.ts";
import GI_CHANGESLOT_ACK from "../src/ops/s2c/GI_CHANGESLOT_ACK.ts";
import GL_CHANGECHANNEL_ACK from "../src/ops/s2c/GL_CHANGECHANNEL_ACK.ts";
import GI_CHANGE_SKILLITEMSLOT_ACK from "../src/ops/s2c/GI_CHANGE_SKILLITEMSLOT_ACK.ts";
import GG_GAMECENTER_GAME_END_ACK from "../src/ops/s2c/GG_GAMECENTER_GAME_END_ACK.ts";
import GG_GAMECENTER_GAME_START_ACK from "../src/ops/s2c/GG_GAMECENTER_GAME_START_ACK.ts";
import GG_GAMECENTER_GAME_START_OK_ACK from "../src/ops/s2c/GG_GAMECENTER_GAME_START_OK_ACK.ts";
import GL_GET_GAMEROOM_PROGRESSTIME_ACK from "../src/ops/s2c/GL_GET_GAMEROOM_PROGRESSTIME_ACK.ts";
import GR_START_VOTING_ACK from "../src/ops/s2c/GR_START_VOTING_ACK.ts";
import GL_WEAPONPARTS_EQUIP_CHANGE_ACK from "../src/ops/s2c/GL_WEAPONPARTS_EQUIP_CHANGE_ACK.ts";
import GR_AI_GET_REWARD_ITEM_ACK from "../src/ops/s2c/GR_AI_GET_REWARD_ITEM_ACK.ts";
import GR_AI_DAMAGE_SHIELD_ACK from "../src/ops/s2c/GR_AI_DAMAGE_SHIELD_ACK.ts";
import GR_AI_RECHARGE_MAGAZINE_START_ACK from "../src/ops/s2c/GR_AI_RECHARGE_MAGAZINE_START_ACK.ts";
import GR_AI_RECHARGE_MAGAZINE_END_ACK from "../src/ops/s2c/GR_AI_RECHARGE_MAGAZINE_END_ACK.ts";
import GR_AI_CONTINUE_START_ACK from "../src/ops/s2c/GR_AI_CONTINUE_START_ACK.ts";
import GR_AI_FEVER_START_ACK from "../src/ops/s2c/GR_AI_FEVER_START_ACK.ts";
import GR_RESET_GAMEROOMSLOT_ACK from "../src/ops/s2c/GR_RESET_GAMEROOMSLOT_ACK.ts";
import GG_GAMECENTER_RANKING_ACK from "../src/ops/s2c/GG_GAMECENTER_RANKING_ACK.ts";
import GL_GAMECENTER_REC_ACK from "../src/ops/s2c/GL_GAMECENTER_REC_ACK.ts";
import GS_DELETEGIFT_ACK from "../src/ops/s2c/GS_DELETEGIFT_ACK.ts";
import GS_BUYCHAR_ACK from "../src/ops/s2c/GS_BUYCHAR_ACK.ts";
import GR_FORCEOUT_ACK from "../src/ops/s2c/GR_FORCEOUT_ACK.ts";
import GL_LEVEL_KILL_LIMIT_ACK from "../src/ops/s2c/GL_LEVEL_KILL_LIMIT_ACK.ts";
import GL_TUTORIALINDEX_ACK from "../src/ops/s2c/GL_TUTORIALINDEX_ACK.ts";
import GL_VOICEITEMSLOT_ACK from "../src/ops/s2c/GL_VOICEITEMSLOT_ACK.ts";
import GL_FRIEND_CHAT_ACK from "../src/ops/s2c/GL_FRIEND_CHAT_ACK.ts";
import GL_FRIEND_DEL_ACK from "../src/ops/s2c/GL_FRIEND_DEL_ACK.ts";
import GL_FRIEND_INFO_ACK from "../src/ops/s2c/GL_FRIEND_INFO_ACK.ts";
import GL_FRIEND_WHERE_ACK from "../src/ops/s2c/GL_FRIEND_WHERE_ACK.ts";
import GL_MSG_DEL_ACK from "../src/ops/s2c/GL_MSG_DEL_ACK.ts";
import GL_MSG_READ_ACK from "../src/ops/s2c/GL_MSG_READ_ACK.ts";
import GL_MSG_RECVLIST_ACK from "../src/ops/s2c/GL_MSG_RECVLIST_ACK.ts";
import GL_INVENIN_ACK from "../src/ops/s2c/GL_INVENIN_ACK.ts";
import GL_MYINFO_ACK from "../src/ops/s2c/GL_MYINFO_ACK.ts";
import GL_MYPARTSUP_ACK from "../src/ops/s2c/GL_MYPARTSUP_ACK.ts";
import PM_CONNECT_ACK, { packCalendar } from "../src/ops/s2c/PM_CONNECT_ACK.ts";
import type { MyInfo, NewSkillProfileSnapshot } from "../src/store.ts";

const hex = (bytes: Uint8Array): string => Buffer.from(bytes).toString("hex").toUpperCase();
const compact = (text: string): string => text.replace(/\s/g, "");

const info = {
  userId: 0x1020_3040,
  nickname: "target",
  selectedCharIndex: 1,
  level: 0x1122_3344,
  experience: -2,
  gamePoints: 7,
  cash: 8,
  stats: {
    wins: 10,
    losses: 11,
    kills: 12,
    deaths: 13,
    headshots: 14,
    combos: 15,
    hearts: 16,
    criticals: 17,
    doubleKill: 18,
    tripleKill: 19,
    multiKill: 20,
    ultraKill: 21,
    zKill: 22,
    kKill: 23,
    ddKill: 24,
    playTimeSeconds: 25,
  },
  characters: [
    { slotNo: 9, charType: 1, appearance: [] },
    {
      slotNo: 7,
      charType: 0x2a,
      appearance: [1, 0x1234, 3, 4, 5, 6, 7, 8, 9, 10, 11, 0xffff],
    },
  ],
} satisfies MyInfo;

const profileIds = [11_012_201, 11_022_201, 11_031_101, 11_041_101, 11_051_101, 11_060_001, 11_060_002];
const snapshot = {
  selectedProfile: 1,
  profiles: Array.from({ length: 5 }, (_, index) => ({
    puzzleItemIds: index === 1 ? profileIds : [0, 0, 0, 0, 0, 0, 0],
    expiresAtPackedMinute: index === 1 ? 0x1234_5678 : 0,
  })),
} satisfies NewSkillProfileSnapshot;


describe("142 connect-ack payload snapshot", () => {
  // Fixed wall clock: 2026-09-19 21:30 local.
  // packed = 26<<24 | 9<<19 | 19<<13 | 21<<7 | 30 = 0x1A4A6A9E (sub_534F20 inverse).
  const wire = PM_CONNECT_ACK(142, {
    endpoint: { host: "127.0.0.1", port: 40_202 },
    activeChannelIndex: 0,
    serverTime: new Date(2026, 8, 19, 21, 30),
  }).payload();

  test("grammar str/s32/u8/u32 with the packed calendar word", () => {
    expect(hex(wire)).toBe(compact(`
      31 32 37 2E 30 2E 30 2E 31 00
      0A 9D 00 00
      00
      9E 6A 4A 1A
    `));
  });

  test("packCalendar round-trips through the sub_534F20 decode masks", () => {
    const packed = packCalendar(new Date(2026, 0, 1, 0, 0));
    expect((packed >>> 24) + 2000).toBe(2026);
    expect((packed & 0xf8_0000) >>> 19).toBe(1);
    expect((packed & 0x7_e000) >>> 13).toBe(1);
    expect((packed & 0x1f_80) >>> 7).toBe(0);
    expect(packed & 0x7f).toBe(0);
    expect(() => packCalendar(new Date(2300, 0, 1))).toThrow(/2000..2255/);
  });

  test("validation: host length, port range, index range", () => {
    const base = { endpoint: { host: "h", port: 1 }, activeChannelIndex: 1, serverTime: new Date(2026, 0, 2, 3, 4) };
    expect(() => PM_CONNECT_ACK(142, { ...base, endpoint: { host: "x".repeat(20), port: 1 } })).toThrow(/19 bytes/);
    expect(() => PM_CONNECT_ACK(142, { ...base, endpoint: { host: "h", port: 65_536 } })).toThrow(/u16/);
    expect(() => PM_CONNECT_ACK(142, { ...base, activeChannelIndex: 256 })).toThrow(/u8/);
  });
});

describe("native 198/247/255 payload snapshots", () => {
  test("198 keeps the shared basic block and private tail byte-for-byte", () => {
    const payload = GL_MYINFO_ACK(198, info, snapshot).payload();
    expect(hex(payload)).toBe(compact(`
      0140302010746172676574000144332211FEFFFFFF000000000000000000000000000000000A0000000B0000000C0000000D0000000E0000000F0000001000000012000000130000001100000014000000150000001600000017000000180000000000000800000000000000000000001900000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000102010000000000000000000000000000000000000000000000002A0100341203000400050006000700080009000A000B00FFFF04000000000000000000010000000000000000020000000000000000030000000000000000000000000000000000000000000000000000000000000000000000000000056908A800792FA8003D52A8004D79A8005DA0A80021C3A80022C3A80000000700000000
    `));
  });

  test("247 stops after one native character appearance record", () => {
    const payload = GL_CLIENTINFO_ACK(247, info).payload();
    expect(hex(payload)).toBe(compact(`
      01746172676574000144332211FEFFFFFF000000000000000000000000000000000A0000000B0000000C0000000D0000000E0000000F00000010000000120000001300000011000000140000001500000016000000170000001800000000000008000000000000000000000019000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001012A0100341203000400050006000700080009000A000B00FFFF
    `));
  });

  test("201 PartsUp push emits the exact 17-wire-byte record frame", () => {
    expect(hex(GL_MYPARTSUP_ACK(201).payload())).toBe("00000000");
    const entries = [
      { key0: 0x0102_0304, key1: -2, kind: 0x80, value: 0x7fff_ffff, period: 86_400 },
      { key0: -1, key1: 0, kind: 0, value: 0, period: 0 },
    ];
    expect(hex(GL_MYPARTSUP_ACK(201, entries).payload())).toBe(compact(`
      02000000
      04030201 FEFFFFFF 80 FFFFFF7F 80510100
      FFFFFFFF 00000000 00 00000000 00000000
    `));
  });

  test("202 expiry shares the 201 frame and 17-byte records", () => {
    expect(hex(GL_EXPIRE_PARTSUP_ACK(202).payload())).toBe("00000000");
    expect(
      hex(GL_EXPIRE_PARTSUP_ACK(202, [{ key0: 0x0102_0304, key1: -2, kind: 0x80, value: 0x7fff_ffff, period: 86_400 }]).payload()),
    ).toBe(compact("01000000 04030201 FEFFFFFF 80 FFFFFF7F 80510100"));
  });

  test("PartsUp builders reject out-of-domain wire values", () => {
    const bad = (kind: number) => () =>
      GL_MYPARTSUP_ACK(201, [{ key0: 0, key1: 0, kind, value: 0, period: 0 }]);
    expect(bad(0x100)).toThrow(RangeError);
    expect(bad(-1)).toThrow(RangeError);
  });

  test("434 emits context plus the native {str nickname, s32 stateRaw} rows", () => {
    expect(hex(GL_FRIEND_LIST_ACK(434).payload())).toBe(compact("0000 00 00"));
    const rows = [
      { nickname: "alice", stateRaw: 0x010203 },
      { nickname: "bob", stateRaw: -1 },
    ];
    expect(hex(GL_FRIEND_LIST_ACK(434, "ctx", rows).payload())).toBe(compact(`
      0000 63747800
      02
      616C69636500 03020100
      626F6200 FFFFFFFF
    `));
  });

  test("434 caps rows at the native 100-entry friend table", () => {
    const rows = Array.from({ length: 101 }, () => ({ nickname: "a", stateRaw: 0 }));
    expect(() => GL_FRIEND_LIST_ACK(434, "", rows)).toThrow(RangeError);
    expect(() => GL_FRIEND_LIST_ACK(434, "", [{ nickname: "a".repeat(21), stateRaw: 0 }])).toThrow(
      RangeError,
    );
  });

  test("426 emits the full native message row in wire order", () => {
    expect(hex(GL_MSG_RECVLIST_ACK(426).payload())).toBe(compact("0000 00 00"));
    const rows = [
      { key: "sender", kind: 2, name: "subj", extraRaw: 0x12345678, body: "hello", selector: "F", flagRaw: -2 },
    ];
    expect(hex(GL_MSG_RECVLIST_ACK(426, "ctx", rows).payload())).toBe(compact(`
      0000 63747800
      01
      73656E64657200 02 7375626A00 78563412 68656C6C6F00 4600 FEFF
    `));
  });

  test("426 caps rows at 10 and strings at their native strides", () => {
    const base = { key: "k", kind: 0, name: "n", extraRaw: 0, body: "b", selector: "", flagRaw: 0 };
    expect(() => GL_MSG_RECVLIST_ACK(426, "", Array.from({ length: 11 }, () => base))).toThrow(
      RangeError,
    );
    expect(() => GL_MSG_RECVLIST_ACK(426, "", [{ ...base, key: "k".repeat(20) }])).toThrow(RangeError);
    expect(() => GL_MSG_RECVLIST_ACK(426, "", [{ ...base, name: "n".repeat(21) }])).toThrow(RangeError);
    expect(() => GL_MSG_RECVLIST_ACK(426, "", [{ ...base, body: "b".repeat(201) }])).toThrow(RangeError);
    expect(() => GL_MSG_RECVLIST_ACK(426, "", [{ ...base, selector: "FM" }])).toThrow(RangeError);
  });

  test("422/424 emit the exact {u8 statusRaw, str key} frames", () => {
    expect(hex(GL_MSG_ADD_ACK(420, "nick1", 6, 0).payload())).toBe("6E69636B31000600");
    expect(hex(GL_TUTORIALINDEX_ACK(686, 0).payload())).toBe("00000000");
    expect(hex(GL_LEVEL_KILL_LIMIT_ACK(705, 0, 0, 0).payload())).toBe("000000000000000000000000");
    expect(hex(GL_BILLTOKEN_ACK(707, "").payload())).toBe("00");
    expect(hex(GL_BILLTOKEN_ACK(707, "TOK").payload())).toBe("544F4B00");
    expect(hex(GL_RACKINGWEB_TOKEN_ACK(788).payload())).toBe("00");
    expect(() => GL_RACKINGWEB_TOKEN_ACK(788, 1 as never)).toThrow(RangeError);
    expect(hex(GQ_QUEST_ACCEPT_DAILY_ACK(877).payload())).toBe("0000000000");
    expect(() => GQ_QUEST_ACCEPT_DAILY_ACK(877, 1 as never)).toThrow(RangeError);
    expect(hex(GQ_QUEST_USER_COMPLETE_HONOR_ACK(879).payload())).toBe("01");
    expect(() => GQ_QUEST_USER_COMPLETE_HONOR_ACK(879, 0 as never)).toThrow(RangeError);
    expect(hex(GR_FORCEOUT_ACK(132).payload())).toBe("00");
    expect(() => GR_FORCEOUT_ACK(132, 1 as never)).toThrow(RangeError);
    expect(hex(GI_CHANGEDATA_ACK(219).payload())).toBe("01");
    expect(hex(GI_CHANGEDATA_ACK(219, 0).payload())).toBe("00");
    expect(hex(GS_BUYCHAR_ACK(311).payload())).toBe("00000000000000000000");
    expect(() => GS_BUYCHAR_ACK(311, 1 as never)).toThrow(RangeError);
    expect(hex(GI_CHANGESLOT_ACK(313, 7).payload())).toBe("07");
    expect(() => GI_CHANGESLOT_ACK(313, -1)).toThrow(RangeError);
    expect(hex(GL_CHANGECHANNEL_ACK(371).payload())).toBe("00");
    expect(() => GL_CHANGECHANNEL_ACK(371, 1 as never)).toThrow(RangeError);
    expect(hex(GS_DELETEGIFT_ACK(454).payload())).toBe("000000000000000000");
    expect(() => GS_DELETEGIFT_ACK(454, 1 as never)).toThrow(RangeError);
    expect(hex(GI_CHANGE_SKILLITEMSLOT_ACK(467).payload())).toBe("000000");
    expect(() => GI_CHANGE_SKILLITEMSLOT_ACK(467, 1 as never)).toThrow(RangeError);
    expect(hex(GL_GAMECENTER_REC_ACK(473, 7).payload())).toBe("0700" + "0".repeat(72));
    expect(() => GL_GAMECENTER_REC_ACK(473, 0x10000)).toThrow(RangeError);
    expect(hex(GG_GAMECENTER_GAME_START_ACK(475, 7, 1).payload())).toBe("01070001");
    expect(hex(GG_GAMECENTER_GAME_END_ACK(477, 3).payload())).toBe("0300" + "0".repeat(270));
    expect(hex(GG_GAMECENTER_RANKING_ACK(481, 7).payload())).toBe("0700" + "0".repeat(18));
    expect(hex(GG_GAMECENTER_GAME_START_OK_ACK(484, 7).payload())).toBe("070001070000000000");
    expect(hex(GL_GET_GAMEROOM_PROGRESSTIME_ACK(486).payload())).toBe("03");
    expect(() => GL_GET_GAMEROOM_PROGRESSTIME_ACK(486, 0 as never)).toThrow(RangeError);
    expect(hex(GR_START_VOTING_ACK(719).payload())).toBe("00");
    expect(hex(GL_WEAPONPARTS_EQUIP_CHANGE_ACK(913).payload())).toBe("01");
    expect(() => GL_WEAPONPARTS_EQUIP_CHANGE_ACK(913, 0 as never)).toThrow(RangeError);
    expect(hex(GR_AI_GET_REWARD_ITEM_ACK(919, 2).payload())).toBe("0201FF");
    expect(() => GR_AI_GET_REWARD_ITEM_ACK(919, 0x200)).toThrow(RangeError);
    expect(hex(GR_AI_DAMAGE_SHIELD_ACK(923, 5, -3, 7, 0x40).payload())).toBe("0500FDFF070040000000");
    expect(hex(GR_AI_RECHARGE_MAGAZINE_START_ACK(925, 1, 0).payload())).toBe("010002");
    expect(hex(GR_AI_RECHARGE_MAGAZINE_END_ACK(927, 1, 0).payload())).toBe("01000001");
    expect(hex(GR_AI_CONTINUE_START_ACK(929).payload())).toBe("00");
    expect(() => GR_AI_CONTINUE_START_ACK(929, 1 as never)).toThrow(RangeError);
    expect(hex(GR_AI_FEVER_START_ACK(936).payload())).toBe("00000000000000");
    expect(hex(GR_RESET_GAMEROOMSLOT_ACK(945).payload())).toBe("00");
    expect(() => GR_RESET_GAMEROOMSLOT_ACK(945, 1 as never)).toThrow(RangeError);
    expect(hex(GL_NEW_MSG_COUNT_ACK(784, 0).payload())).toBe("00000000");
    expect(hex(GL_VOICEITEMSLOT_ACK(792, 0).payload())).toBe("0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");
    expect(hex(GL_FRIEND_WHERE_ACK(442, 0).payload())).toBe("00");
    expect(hex(GL_FRIEND_WHERE_ACK(442, 1, 10, 2, 5).payload())).toBe("010A0205");
    expect(hex(GL_FRIEND_INFO_ACK(436, [
      { nickname: "frndA", statusRaw: 0 },
      { nickname: "frndB", statusRaw: 0 },
    ]).payload())).toBe("0266726E6441000066726E64420000");
    expect(hex(GL_FRIEND_INFO_ACK(436, [
      { nickname: "hero", statusRaw: 1, channelText: "ch1", raw: 3 },
    ]).payload())).toBe("016865726F00016368310003");
    expect(hex(GL_FRIEND_INFO_ACK(436, []).payload())).toBe("00");
    expect(hex(GL_FRIEND_CHAT_ACK(440, 3, "me", "you").payload())).toBe(
      "036D6500796F7500",
    );
    expect(hex(GL_FRIEND_CHAT_ACK(440, 2, "me", "you", "hi").payload())).toBe(
      "026D6500796F7500686900",
    );
    expect(hex(GL_FRIEND_ADD_ACK(430, 1, "frnd").payload())).toBe("0166726E6400");
    expect(hex(GL_FRIEND_DEL_ACK(432, 2, "frnd").payload())).toBe("0266726E6400");
    expect(hex(GL_MSG_DEL_ACK(422, 1, "mail1").payload())).toBe("016D61696C3100");
    expect(hex(GL_MSG_DEL_ACK(422, 0, "").payload())).toBe("0000");
    expect(hex(GL_MSG_READ_ACK(424, 1, "mail1").payload())).toBe("016D61696C3100");
    expect(hex(GL_MSG_READ_ACK(424, 0, "").payload())).toBe("0000");
    expect(() => GL_MSG_DEL_ACK(422, 1, "k".repeat(20))).toThrow(RangeError);
    expect(() => GL_MSG_READ_ACK(424, 1, "k".repeat(20))).toThrow(RangeError);
  });

test("255 retains the common prefix and five raw 32-byte profiles", () => {
    const payload = GL_INVENIN_ACK(255, info.userId, 0x7a, snapshot).payload();
    expect(hex(payload)).toBe(compact(`
      01403020107A000100000000000000000000000000000000000000000000000000000000000000006908A800792FA8003D52A8004D79A8005DA0A80021C3A80022C3A80078563412000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
    `));
  });
});
