/**
 * 900 -> 901: capsule-machine start result (consumer sub_9A1A30,
 * full-body line-level re-read 2026-09-19).
 *
 * Line-level arm map of sub_9A1A30:
 *  - `v11` (u8) IS the result switch:
 *      v11 != 0 → FAILURE arm: SetOwningNode + `sub_9A1C90(this, 1)`
 *        (close the gacha compound) + banner via msg `0x4B4` (idle-kick
 *        text, developer reuse — the string is native truth). No award
 *        rows and no wallet words are consulted in this arm.
 *      v11 == 0 → SUCCESS arm: the three tails are mirrored into the
 *        lobby wallet display cells `byte_D7AC88[+104]=CASH /
 *        [+108]=PG / [+112]=COUPON` (labels proven by the three
 *        writers sub_45FD80=CASH, sub_45FCA0=PG, sub_45FE60=COUPON),
 *        then `sub_9A1C30` finalizes the reel — which dereferences the
 *        FIRST award row's class byte (this+44) even at count 0. A
 *        successful arm therefore needs real award rows; fabricating
 *        empty rows would drive the reel off-garbage.
 *  - `n10` (s32) is the draw count AND the spin-10 selector in one:
 *    `*(this+38) = (n10 == 10)`; rows = n10 ×
 *    `{u8 class (this+i+44), s32 item (this+i+14), s32 (this+i+24)}`.
 *  - tails (nonzero-status arm reads but never uses them):
 *    `s32 cash, s32 pg, s32 coupon` — lobby wallet words refreshed on
 *    the success path.
 *
 * Wire (failure arm, 17 bytes): `{u8 1, s32 0, 3×s32 0}` — result 1
 * shows the native failure banner and closes the compound; count 0 and
 * zero wallet words are consumed but inert here. This server has no
 * capsule/wallet model, so the failure arm is the only honest reply.
 */

import { Packet } from "../../packet.ts";

export default function GS_CAPSULEMACHINE_START_ACK(op: number): Packet {
  return new Packet(op).u8(1).s32(0).s32(0).s32(0).s32(0);
}
