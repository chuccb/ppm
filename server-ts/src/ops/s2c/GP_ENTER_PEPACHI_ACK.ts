/**
 * 698 -> 699: consumer-safe Pepachi entry ACK (consumer
 * `CLobbyShop::sub_46AD00`, case 699 — sub_84A490 belongs to sibling
 * case 701, the attribution here was corrected 2026-09-19).
 *
 * Line-level: the case reads `{s8/bool status, s32, s32}`
 * (`sub_592900` then two `sub_592A40`) and only status 1 routes into
 * `sub_469CF0` (Pepachi scene enter). Emitting status 1 would fabricate
 * a wallet/present-box state the server cannot justify, so this builder
 * exposes no success arm at all.
 */

import { Packet } from "../../packet.ts";

export default function GP_ENTER_PEPACHI_ACK(op: number): Packet {
  // s8/bool status 0: native client routes to sub_46A1E0 and leaves the
  // reel UI untouched; both s32 tails stay read-but-zero.
  return new Packet(op).s8(0).s32(0).s32(0);
}
