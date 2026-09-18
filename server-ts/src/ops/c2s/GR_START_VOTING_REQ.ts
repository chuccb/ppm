/**
 * 718 GR_START_VOTING_REQ — initiate a room vote (builder
 * IVotingNetwork::sub_A191D0 @701039, gated by the local voting
 * state machine): ctor(718) -> three sub_592A20 4-byte writes; wire =
 * exactly `s32 target_slot, s32 reason, s32 initiator_slot` = 12
 * bytes on the room socket (sub_58D7D0 sender).
 *
 * Native 719 consumer (shared dispatcher IVotingNetwork::sub_9BF430,
 * case 719): reads `u8 status` and forwards it to the initiator
 * callback (vtable+12) with no arm split.
 *
 * TS policy: no voting-session model — the request is parsed and the
 * initiator gets status = 0 (votes never start). Broadcast 720 /
 * result 722 are server-initiated pushes this server never emits.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GR_START_VOTING_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 12) {
    throw new RangeError(`718 expects exactly 3 x s32 (12 bytes), got ${r.remaining}`);
  }
  r.s32(); // target_slot
  r.s32(); // reason
  r.s32(); // initiator_slot
  connection.reply("GR_START_VOTING_ACK", 0);
}
