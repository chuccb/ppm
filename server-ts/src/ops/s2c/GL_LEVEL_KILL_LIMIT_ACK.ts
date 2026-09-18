/**
 * 704 -> 705 level/kill-limit configuration (sub_55C9B0 consumer).
 *
 * Wire (native Fact): `{s32 killLimit, f32 expRate, s32 maxLevelLimit}`
 * (12 bytes). killLimit == 0 is the proven silent arm — the client
 * skips the notice-text branch entirely. Nonzero values select
 * localized notices the server cannot substantiate here, so only the
 * all-zero frame is emitted.
 */

import { Packet } from "../../packet.ts";

export default function GL_LEVEL_KILL_LIMIT_ACK(
  op: number,
  killLimit: number,
  expRate: number,
  maxLevelLimit: number,
): Packet {
  return new Packet(op).s32(killLimit).f32(expRate).s32(maxLevelLimit);
}
