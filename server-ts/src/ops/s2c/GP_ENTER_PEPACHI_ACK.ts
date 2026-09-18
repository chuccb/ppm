/**
 * 698 -> 699: consumer-safe Pepachi entry ACK.
 *
 * Sub-84A490 reads `{u8 status, s32, s32}` and only status 1 opens the reel
 * UI. Emitting status 1 would fabricate entry a wallet/present-box state the
 * server cannot justify, so this builder exposes no success arm at all.
 */

import { Packet } from "../../packet.ts";

export default function GP_ENTER_PEPACHI_ACK(op: number): Packet {
  // status 0: native client leaves the UI untouched; both s32 tails stay read-but-zero.
  return new Packet(op).u8(0).s32(0).s32(0);
}
