/**
 * 939 GR_AI_GO_NEXT_WAVE_REQ — manual wave advance (builder
 * sub_75CE40 @391020: gated on room state, then ctor(939) + send with
 * NO field writers — empty-body wire, verified).
 *
 * 940 consumer sub_7613D0 (full body re-read): reads only `u8
 * next_wave, s32 wave_time` and then UNCONDITIONALLY flips the
 * wave-start state, plays AI3_next.wav and arms a 30s timer — there
 * is NO status/deny gate, so any 940 frame the server emits
 * immediately starts a wave on the client. Fabricating such a frame
 * without real PVE wave state is impossible; the only honest policy
 * is parse-and-silence. This handler therefore never replies, and 940
 * is deliberately not registered on the s2c side.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GR_AI_GO_NEXT_WAVE_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) {
    throw new RangeError(`939 builder writes no fields (empty body), got ${r.remaining} byte(s)`);
  }
  void connection;
}
