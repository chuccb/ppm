/**
 * 720 GR_START_VOTING (S2C kick-vote start broadcast; consumer case 720
 * in `IVotingNetwork::sub_9BF430`, line level 2026-09-19).
 *
 * Native case reads — in this order:
 *   s32 v17, s32 v19, s32 v20, raw4 v18 (via `sub_592AC0`), u8 v16
 * and forwards (v17, v20, v19, v18, v16) to the vtable+8 room UI
 * handler. PACKETS.md §2723 names the positions target / reason /
 * initiator / duration / team by arg order — the dump proves wire
 * widths and the forward tuple, not the business names, so the wire
 * order below is kept verbatim and the guessed names stay comments.
 *
 * This server never starts votes; the module exists so the grammar is
 * complete, not because any flow can emit it today.
 */

import { Packet } from "../../packet.ts";

export interface VotingStart {
  readonly target: number;    // v17 (wire 1st, forwarded 1st)
  readonly initiator: number; // v19 (wire 2nd, forwarded 3rd)
  readonly reason: number;    // v20 (wire 3rd, forwarded 2nd)
  /** raw4 via sub_592AC0: forwarded 4th; duration-like, semantic unproven. */
  readonly durationRaw: number;
  readonly team: number;      // v16 (u8, forwarded 5th)
}

export default function GR_START_VOTING(op: number, vote: VotingStart): Packet {
  return new Packet(op)
    .s32(vote.target)
    .s32(vote.initiator)
    .s32(vote.reason)
    .s32(vote.durationRaw) // raw4 wire; s32 is the width projection
    .u8(vote.team);
}
