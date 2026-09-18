/**
 * 912 -> 913 GL_WEAPONPARTS_EQUIP_CHANGE_ACK (consumer sub_95B180).
 *
 * Wire: `u8 errorRaw`. errorRaw = 0 makes the client continue reading
 * the 912-shaped body and BOUNDS-CHECK its final s32 against native
 * item tables — a pipeline this server cannot feed without a parts
 * model. Nonzero errorRaw ends consumption after the single byte, so
 * this server always emits errorRaw = 1 (wire "01").
 */

import { Packet } from "../../packet.ts";

export default function GL_WEAPONPARTS_EQUIP_CHANGE_ACK(op: number, errorRaw: 1 = 1): Packet {
  if (errorRaw !== 1) {
    throw new RangeError("913 errorRaw != 1 would enter the bounds-checked echo body this server cannot provide");
  }
  return new Packet(op).u8(errorRaw);
}
