/**
 * 419 -> 420 add-message result (sub_559810 consumer).
 *
 * Native wire: `{str toNick, u8 xRaw, u8 resultRaw}`. The reader switches
 * exclusively on xRaw {0,1,2,3,4,5,10}; inside xRaw 0 and 3, resultRaw
 * adds draft-type arms. Any other xRaw falls into the default branch and
 * loads the generic error resource — that default arm is the only reply
 * this server can emit without fabricating mail/recipient state.
 */

import { Packet } from "../../packet.ts";

/** Native 419 sender-side recipient-name bound (n24 <= 24 bytes). */
export const MSG_ADD_TARGET_MAX_BYTES = 24;

export default function GL_MSG_ADD_ACK(
  op: number,
  toNick: string,
  xRaw: number,
  resultRaw: number,
): Packet {
  return new Packet(op)
    .strMax(toNick, MSG_ADD_TARGET_MAX_BYTES)
    .u8(xRaw)
    .u8(resultRaw);
}
