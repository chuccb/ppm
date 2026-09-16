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
import type { NewSkillProfileSnapshot, MyInfo } from "../../store.ts";

export default function GL_MYINFO_ACK(
  op: number,
  myInfo: MyInfo | null,
  snapshot?: NewSkillProfileSnapshot,
): Packet {
  if (!myInfo) return new Packet(op).u8(0);

  const p = new Packet(op).u8(1).s32(myInfo.userId);
  writeMyInfoBasicData(p, myInfo);

  p.u8(Math.min(myInfo.characters.length, 20));
  for (const character of myInfo.characters.slice(0, 20)) {
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
  const selectedProfile = snapshot?.profiles[snapshot.selectedProfile];
  p.u8(5);
  for (let i = 0; i < 7; i++) p.s32(selectedProfile?.puzzleItemIds[i] ?? 0);

  return p.u16(0).s32(myInfo.gamePoints).u8(0);
}

/** The shared sub_523BF0 basic-data block used by 198 and 247. */
export function writeMyInfoBasicData(packet: Packet, myInfo: MyInfo): Packet {
  const { stats } = myInfo;
  return packet
    .str(myInfo.nickname)
    .u8(myInfo.currentChar)
    .s32(myInfo.level)
    .s32(myInfo.experience)
    .s32(0)
    .s32(stats.playCount)
    .s32(stats.roundCount)
    .s32(stats.criticals)
    .s32(stats.wins)
    .s32(stats.losses)
    .s32(stats.kills)
    .s32(stats.deaths)
    .s32(stats.disconnects)
    .s32(stats.hearts)
    .s32(stats.headshots)
    .s32(stats.doubleKill)
    .s32(stats.tripleKill)
    .s32(stats.combos)
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
    .zeros(48)
    .u8(myInfo.currentChar);
}

export function writeCharacterAppearance(packet: Packet, equip: readonly number[]): Packet {
  for (let i = 0; i < 12; i++) packet.u16(equip[i] ?? 0);
  return packet;
}
