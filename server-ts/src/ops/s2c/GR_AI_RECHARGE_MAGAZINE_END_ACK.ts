/**
 * 926 -> 927 GR_AI_RECHARGE_MAGAZINE_END_ACK (consumer sub_558880,
 * full body re-read): `u8 slot, u8 team, u8 unk, u8 statusRaw` is the
 * fixed 4-byte head; statusRaw == 0 continues +u8+s32 into the
 * refuel-end logic, nonzero terminates in the denial sub_763510.
 * The TS denial frame therefore is exactly the head with status = 1
 * and the unk byte echoed from the request (wire 4B).
 */

import { Packet } from "../../packet.ts";

export default function GR_AI_RECHARGE_MAGAZINE_END_ACK(
  op: number,
  slot: number,
  team: number,
): Packet {
  return new Packet(op).u8(slot).u8(team).u8(0).u8(1);
}
