/**
 * 721 GR_DO_VOTING — cast a vote (builder IVotingNetwork::sub_A192B0
 * @701061: ctor(721) -> sub_5928E0 one byte via the s8 write accessor
 * (1 = yes, 2 = no) -> room-socket send; wire = exactly 1 byte).
 *
 * The vote-result push 722 (shared dispatcher case 722 in
 * sub_9BF430, `s32 target, s8 result`) is server-initiated. Since this
 * server never starts a vote (718 always answers status 0), no legal
 * 721 can pair with an active session, so the vote byte is parsed and
 * silently dropped (no 722 is ever emitted).
 *
 * Related pushes 720 (broadcast start) and 723 (GR_END_RESULT,
 * `s8, s32`) stay unimplemented send-side records.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GR_DO_VOTING(r: Reader, _connection: Connection): void {
  if (r.remaining !== 1) {
    throw new RangeError(`721 expects exactly 1 byte (s8 vote), got ${r.remaining}`);
  }
  r.s8(); // vote: silently dropped, there is no active session
}
