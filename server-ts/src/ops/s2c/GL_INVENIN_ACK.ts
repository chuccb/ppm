/**
 * 254 -> 255 local-user NewSkill profile snapshot.
 *
 * The client has another mode-0 branch for remote-user preview: after the
 * common mode/uid/context/unknown prefix it reads two more u8 values and one
 * s32 lookup value. 254's native request is always the local inventory-enter
 * path, so this server emits the proven mode-1 self snapshot only.
 */

import { Packet } from "../../packet.ts";
import {
  NEW_SKILL_PROFILE_COUNT,
  NEW_SKILL_PUZZLE_SLOT_COUNT,
  type NewSkillProfileSnapshot,
} from "../../store.ts";

export default function GL_INVENIN_ACK(
  op: number,
  uid: number,
  contextRaw: number,
  snapshot: NewSkillProfileSnapshot,
): Packet {
  if (!Number.isSafeInteger(uid) || uid <= 0 || uid > 0x7fff_ffff) {
    throw new RangeError("255 uid must be a positive s32");
  }
  if (!Number.isInteger(contextRaw) || contextRaw < 0 || contextRaw > 0xff) {
    throw new RangeError("255 contextRaw must fit u8");
  }
  if (!Number.isSafeInteger(snapshot.selectedProfile) || snapshot.selectedProfile < 0 || snapshot.selectedProfile >= NEW_SKILL_PROFILE_COUNT) {
    throw new RangeError("255 selected profile must be an integer in 0..4");
  }
  if (snapshot.profiles.length !== NEW_SKILL_PROFILE_COUNT) {
    throw new RangeError("255 requires exactly five NewSkill profiles");
  }

  const p = new Packet(op)
    .u8(1) // mode 1: local user snapshot
    .s32(uid)
    .u8(contextRaw)
    .u8(0) // unknownHeaderRaw: read by the client, semantic unresolved
    .u8(snapshot.selectedProfile);

  for (const profile of snapshot.profiles) {
    if (profile.puzzleItemIds.length !== NEW_SKILL_PUZZLE_SLOT_COUNT) {
      throw new RangeError("255 profile requires exactly seven puzzle item ids");
    }
    for (const itemId of profile.puzzleItemIds) p.s32(itemId);
    p.s32(profile.expiresAtPackedMinute);
  }
  return p;
}
