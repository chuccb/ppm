/**
 * 705 GL_LEVEL_KILL_LIMIT_ACK — anti-addiction restriction panel
 * (consumer sub_55C9B0: pure global hydration `n11_0 / flt_BEFEE8 /
 * dword_BEFEE0`, then CLobbyChannel::sub_415F90 switches on n11_0).
 *
 * Audit 2026-09-19 of the switch arms:
 * - kind 0   -> the switch never runs; NO warning panel is shown. The
 *   exp-rate/level-cap locals are only read inside kinds 6..12, so
 *   zero values there are dead cells under this frame, not fabrications.
 * - kind 6..12 -> lobby shows resource 795/796/813/801/820/828/827
 *   populated with the cap/rate fields.
 * This server: unrestricted play -> kind = 0 with dead 0.0/0 tails.
 */

import { Packet } from "../../packet.ts";

/** n11_0 == 0: sub_415F90 skips the switch; no restriction panel. */
export const NO_RESTRICTION = 0;

export default function GL_LEVEL_KILL_LIMIT_ACK(
  op: number,
  restrictionKind = NO_RESTRICTION,
  expRate = 0,
  maxLevelLimit = 0,
): Packet {
  return new Packet(op).s32(restrictionKind).f32(expRate).s32(maxLevelLimit);
}
