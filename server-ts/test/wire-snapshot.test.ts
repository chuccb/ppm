import { describe, expect, test } from "bun:test";
import GL_CLIENTINFO_ACK from "../src/ops/s2c/GL_CLIENTINFO_ACK.ts";
import GL_INVENIN_ACK from "../src/ops/s2c/GL_INVENIN_ACK.ts";
import GL_MYINFO_ACK from "../src/ops/s2c/GL_MYINFO_ACK.ts";
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

  test("255 retains the common prefix and five raw 32-byte profiles", () => {
    const payload = GL_INVENIN_ACK(255, info.userId, 0x7a, snapshot).payload();
    expect(hex(payload)).toBe(compact(`
      01403020107A000100000000000000000000000000000000000000000000000000000000000000006908A800792FA8003D52A8004D79A8005DA0A80021C3A80022C3A80078563412000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
    `));
  });
});
