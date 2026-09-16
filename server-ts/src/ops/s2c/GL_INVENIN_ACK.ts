/**
 * 254 -> 255 local-user NewSkill profile snapshot.
 *
 * The client has another mode-0 branch for remote-user preview, but 254's
 * native request is always the local inventory-enter path. This server emits
 * the proven mode-1 self snapshot only.
 */

import { Packet } from "../../packet.ts";
import {
  NEW_SKILL_PROFILE_COUNT,
  NEW_SKILL_PUZZLE_SLOT_COUNT,
  type NewSkillProfileSnapshot,
} from "../../store.ts";

export default function GL_INVENIN_ACK(
  op: number,
  userId: number,
  requestContextRaw: number,
  snapshot: NewSkillProfileSnapshot,
): Packet {
  if (!Number.isSafeInteger(userId) || userId <= 0) {
    throw new RangeError("255 user ID must be a positive safe integer");
  }
  if (!Number.isInteger(requestContextRaw) || requestContextRaw < 0 || requestContextRaw > 0xff) {
    throw new RangeError("255 request context must fit u8");
  }
  if (snapshot.selectedProfile < 0 || snapshot.selectedProfile >= NEW_SKILL_PROFILE_COUNT) {
    throw new RangeError("255 selected profile must be in 0..4");
  }
  if (snapshot.profiles.length !== NEW_SKILL_PROFILE_COUNT) {
    throw new RangeError("255 requires exactly five NewSkill profiles");
  }

  const p = new Packet(op)
    .u8(1) // mode 1: local user snapshot
    .s32(userId)
    .u8(requestContextRaw)
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
