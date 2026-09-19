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
import { type NewSkillProfileSnapshot, type MyInfo } from "../../store.ts";

export default function GL_MYINFO_ACK(
  op: number,
  myInfo: MyInfo | null,
  snapshot?: NewSkillProfileSnapshot,
): Packet {
  if (!myInfo) return new Packet(op).u8(0);
  const p = new Packet(op).u8(1).s32(myInfo.userId);
  writeMyInfoBasicData(p, myInfo);

  const characters = myInfo.characters;
  p.u8(characters.length);
  for (const character of characters) {
    p.u8(character.charType);
    writeCharacterAppearance(p, character.appearance);
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
  // unresolved. A missing snapshot writes seven empty puzzle slots, which
  // keeps packet-only callers useful.
  const selectedProfile = snapshot?.profiles[snapshot.selectedProfile];
  p.u8(5);
  for (let i = 0; i < 7; i++) {
    p.s32(selectedProfile?.puzzleItemIds[i] ?? 0);
  }

  return p.u16(0).s32(myInfo.gamePoints).u8(0);
}

/** The shared sub_523BF0 basic-data block used by 198 and 247. */
export function writeMyInfoBasicData(packet: Packet, myInfo: MyInfo): Packet {
  const { stats } = myInfo;
  return packet
    // sub_46F450 copies exactly 0x18 bytes at +60, then copies +84 separately; native CClientData char[24]
    .str(myInfo.nickname)

    .u8(myInfo.selectedCharIndex)
    .s32(myInfo.level)
    .s32(myInfo.experience)
    // sub_523BF0 reads this post-exp wire word into native +108. The
    // derived class/level at +100 is recomputed from exp and is not itself
    // read from this packet. Native sub_9252D0 later consumes +108 for its
    // condition-1 input, but its server/stat owner is unresolved.
    .s32(0)
    // sub_523BF0 order: [34..36] are reserved, then the native UI consumers'
    // direct order: wins/losses, kills/deaths, headshots, air-combo, hearts,
    // then wire slots +180/+184/+176 (double/triple/critical — see below),
    // and the multi/ultra/z/k/dd counters.
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
    // Native wire order is +172, +180, +184, +176 — NOT ascending offsets:
    // both the reader sub_523BF0 and the mirror writer sub_523E10 read slot
    // 45 (+180) and slot 46 (+184) before slot 44 (+176). Slot semantics are
    // pinned by the sub_5206F0 record-window label bindings:
    //   +44 (+176) = "CRITCALSHOT", +45 (+180) = "DOUBLEKILL",
    //   +46 (+184) = "TRIPLEKILL".
    .s32(stats.doubleKill)
    .s32(stats.tripleKill)
    .s32(stats.criticals)
    .s32(stats.multiKill)
    .s32(stats.ultraKill)
    .s32(stats.zKill)
    .s32(stats.kKill)
    .s32(stats.ddKill)
    // this+304/305/306 flags: read and stored by sub_523BF0 but no read
    // site exists anywhere in the client image — reserved zeros.
    .u8(0)
    .u8(0)
    .u8(0)
    .s32(myInfo.cash)
    // this+112: coupon balance — the native COUPON label's `%10d` source
    // (sub_45FE60, `*(this+41188) = *dword_EE8D1C`); shop checks
    // `price <= dword_EE8D1C`. Zero = no coupon balance.
    .s32(myInfo.coupon ?? 0)
    // this+116: wire word with no proven consumer anywhere in the
    // client image — reserved to the zero the reader stores but never reads
    .s32(0)
    // Native [52] is the cumulative play-time task counter; [53..60]
    // are per-mode counters still without a TS session model (zero = no
    // recorded play in those modes); [61..63] have no proven consumer.
    .s32(stats.playTimeSeconds)
    .zeros(44)
    .u8(myInfo.selectedCharIndex);
}

export function writeCharacterAppearance(packet: Packet, appearance: readonly number[]): Packet {
  for (let i = 0; i < 12; i++) {
    packet.u16(appearance[i] ?? 0);
  }
  return packet;
}
