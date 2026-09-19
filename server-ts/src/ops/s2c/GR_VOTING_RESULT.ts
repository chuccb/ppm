/**
 * 722 GR_VOTING_RESULT (S2C kick-vote result; consumer case 722 in
 * `IVotingNetwork::sub_9BF430`, line level 2026-09-19).
 *
 * Native case reads `{s32 v15 target, s8 v14 result}` (note: the result
 * byte goes through the s8 accessor `sub_592900`, not the u8 one) and
 * forwards both to the vtable+16 handler. Result semantics per
 * PACKETS.md: 1 = kick passed, 0 = rejected; no other value has a
 * recovered branch.
 *
 * This server never runs votes; the module completes the grammar only.
 */

import { Packet } from "../../packet.ts";

export default function GR_VOTING_RESULT(
  op: number,
  target: number,
  /** Native s8 result slot; 1 passed / 0 rejected are the proven meanings. */
  result: number,
): Packet {
  return new Packet(op).s32(target).s8(result);
}
