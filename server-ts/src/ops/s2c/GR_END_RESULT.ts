/**
 * 723 GR_END_RESULT (S2C vote-flow end signal; consumer case 723 in
 * `IVotingNetwork::sub_9BF430`, line level 2026-09-19).
 *
 * Native case reads `{s8 v12, s32 v13}` and forwards both to the
 * vtable+20 handler. The semantic split of the two words is not
 * recovered beyond the widths (PACKETS.md case-723 note); names below
 * stay neutral.
 *
 * This server never runs votes; the module completes the grammar only.
 */

import { Packet } from "../../packet.ts";

export default function GR_END_RESULT(
  op: number,
  /** v12: leading s8 slot; meaning unresolved. */
  flagRaw: number,
  /** v13: trailing s32 slot; meaning unresolved. */
  wordRaw: number,
): Packet {
  return new Packet(op).s8(flagRaw).s32(wordRaw);
}
