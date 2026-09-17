/**
 * 197 -> 198 MyInfo bootstrap.
 *
 * The field order is the native sub_570550 reader order documented in
 * docs/PACKETS.md §3.2. The Store deliberately supplies only a canonical type-1
 * character and zero-valued optional projections until their data models are
 * implemented; zero item/loadout IDs are valid empty values and avoid inventing
 * catalog entries.
 */

import { Packet } from "../../packet.ts";
import {
  NEW_SKILL_PROFILE_COUNT,
  NEW_SKILL_PUZZLE_SLOT_COUNT,
  type NewSkillProfile,
  type NewSkillProfileSnapshot,
  type MyInfo,
} from "../../store.ts";

function requireS32(name: string, value: number): void {
  if (!Number.isSafeInteger(value) || value < -0x8000_0000 || value > 0x7fff_ffff) {
    throw new RangeError(`198 ${name} must fit s32`);
  }
}

export function requireU8(name: string, value: number): void {
  if (!Number.isSafeInteger(value) || value < 0 || value > 0xff) {
    throw new RangeError(`${name} must fit u8`);
  }
}

const NATIVE_CHARACTER_SLOT_COUNT = 20;

function requireCharacterIndex(name: string, value: number): void {
  requireU8(name, value);
  if (value >= NATIVE_CHARACTER_SLOT_COUNT) {
    throw new RangeError(`198 ${name} must be a native character-list index in 0..19`);
  }
}

function requireU16(name: string, value: number): void {
  if (!Number.isSafeInteger(value) || value < 0 || value > 0xffff) {
    throw new RangeError(`198 ${name} must fit u16`);
  }
}

// sub_46F450 copies exactly 0x18 bytes at +60, then copies +84 separately.
const NICKNAME_MAX_BYTES = 23; // native CClientData char[24], including NUL
const NEW_SKILL_RANGES = [
  [11_010_001, 11_020_000],
  [11_020_001, 11_030_000],
  [11_030_001, 11_040_000],
  [11_040_001, 11_050_000],
  [11_050_001, 11_060_000],
  [11_060_001, 11_070_000],
] as const;

/** Resource/native ordinal family; zero is the empty puzzle slot. */
export function requireNewSkillPuzzleId(value: number, slot: number, opcode: 198 | 255): void {
  requireS32(`puzzle[${slot}]`, value);
  if (value === 0) return;
  const range = NEW_SKILL_RANGES[Math.min(slot, NEW_SKILL_RANGES.length - 1)]!;
  if (value < range[0] || value > range[1]) {
    throw new RangeError(`${opcode} puzzle[${slot}] is outside its native resource range`);
  }
}

export default function GL_MYINFO_ACK(
  op: number,
  myInfo: MyInfo | null,
  snapshot?: NewSkillProfileSnapshot,
): Packet {
  if (!myInfo) return new Packet(op).u8(0);
  requireS32("user_id", myInfo.userId);
  requireS32("game_point", myInfo.gamePoints);

  const p = new Packet(op).u8(1).s32(myInfo.userId);
  writeMyInfoBasicData(p, myInfo);

  if (myInfo.characters.length > NATIVE_CHARACTER_SLOT_COUNT) {
    throw new RangeError("198 supports at most 20 character records");
  }
  const characters = myInfo.characters;
  p.u8(characters.length);
  for (const character of characters) {
    requireU8("char_type", character.charType);
    p.u8(character.charType);
    writeCharacterAppearance(p, character.equip);
  }

  // Four empty weapon groups are the native-compatible no-loadout projection.
  p.u8(4);
  for (let group = 0; group < 4; group++) {
    p.u8(group).u16(0);
    if (group !== 3) p.u16(0).u16(0).u16(0);
  }

  // 9 UI-item slots; no nonzero ID is emitted without catalog validation.
  for (let i = 0; i < 9; i++) p.s32(0);

  // NewSkill profile selector and the selected profile's seven puzzle IDs.
  // `n5=5` is the recovered native-compatible raw convention; its semantic is
  // unresolved. A missing snapshot is kept useful for packet-only callers.
  let selectedProfile: NewSkillProfile | undefined;
  if (snapshot) {
    if (!Number.isSafeInteger(snapshot.selectedProfile) || snapshot.selectedProfile < 0 || snapshot.selectedProfile >= NEW_SKILL_PROFILE_COUNT) {
      throw new RangeError("198 selected profile must be an integer in 0..4");
    }
    if (snapshot.profiles.length !== NEW_SKILL_PROFILE_COUNT) {
      throw new RangeError("198 requires exactly five NewSkill profiles");
    }
    selectedProfile = snapshot.profiles[snapshot.selectedProfile];
    if (!selectedProfile || selectedProfile.puzzleItemIds.length !== NEW_SKILL_PUZZLE_SLOT_COUNT) {
      throw new RangeError("198 selected profile requires exactly seven puzzle item ids");
    }
  }
  p.u8(5);
  for (let i = 0; i < 7; i++) {
    const itemId = selectedProfile?.puzzleItemIds[i] ?? 0;
    requireNewSkillPuzzleId(itemId, i, 198);
    p.s32(itemId);
  }

  return p.u16(0).s32(myInfo.gamePoints).u8(0);
}

/** The shared sub_523BF0 basic-data block used by 198 and 247. */
export function writeMyInfoBasicData(packet: Packet, myInfo: MyInfo): Packet {
  if (typeof myInfo.nickname !== "string") throw new TypeError("198 nickname must be a string");
  if (myInfo.nickname.length > NICKNAME_MAX_BYTES) {
    throw new RangeError(`198 nickname must fit native char[24]`);
  }
  requireCharacterIndex("selected_char_index", myInfo.selectedCharIndex);
  const { stats } = myInfo;
  const words: readonly [string, number][] = [
    ["level", myInfo.level],
    ["experience", myInfo.experience],
    ["wins", stats.wins],
    ["losses", stats.losses],
    ["kills", stats.kills],
    ["deaths", stats.deaths],
    ["headshots", stats.headshots],
    ["combos", stats.combos],
    ["hearts", stats.hearts],
    ["criticals", stats.criticals],
    ["doubleKill", stats.doubleKill],
    ["tripleKill", stats.tripleKill],
    ["multiKill", stats.multiKill],
    ["ultraKill", stats.ultraKill],
    ["zKill", stats.zKill],
    ["kKill", stats.kKill],
    ["ddKill", stats.ddKill],
    ["cash", myInfo.cash],
    ["playTimeSeconds", stats.playTimeSeconds],
  ];
  for (const [name, value] of words) requireS32(name, value);
  return packet
    .str(myInfo.nickname)
    .u8(myInfo.selectedCharIndex)
    .s32(myInfo.level)
    .s32(myInfo.experience)
    // sub_523BF0 reads this post-exp wire word into native +108. The
    // derived class/level at +100 is recomputed from exp and is not itself
    // read from this packet. Native sub_9252D0 later consumes +108 for its
    // condition-1 threshold, but its server/stat owner is unresolved.
    .s32(0)
    // sub_523BF0 order: [34..36] are reserved, then the native UI consumers'
    // direct order: wins/losses, kills/deaths, headshots, air-combo, hearts,
    // criticals, and the double/triple/multi/ultra/z/k/dd counters.
    // Store disconnects/playCount/roundCount have no proven wire owner.
    .s32(0)
    .s32(0)
    .s32(0)
    .s32(stats.wins)
    .s32(stats.losses)
    .s32(stats.kills)
    .s32(stats.deaths)
    .s32(stats.headshots)
    .s32(stats.combos)
    .s32(stats.hearts)
    .s32(stats.criticals)
    .s32(stats.doubleKill)
    .s32(stats.tripleKill)
    .s32(stats.multiKill)
    .s32(stats.ultraKill)
    .s32(stats.zKill)
    .s32(stats.kKill)
    .s32(stats.ddKill)
    .u8(0)
    .u8(0)
    .u8(0)
    .s32(myInfo.cash)
    .s32(0)
    .s32(0)
    // Native [52] is the cumulative play-time task counter; [53..63]
    // are mode counters/reserved words without a TS data model yet.
    .s32(stats.playTimeSeconds)
    .zeros(44)
    .u8(myInfo.selectedCharIndex);
}

export function writeCharacterAppearance(packet: Packet, equip: readonly number[]): Packet {
  for (let i = 0; i < 12; i++) {
    const value = equip[i] ?? 0;
    requireU16(`appearance[${i}]`, value);
    packet.u16(value);
  }
  return packet;
}
