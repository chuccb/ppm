/**
 * 924 -> 925 GR_AI_RECHARGE_MAGAZINE_START_ACK (consumer sub_558550,
 * full body): status == 0 reads +u8+s32 and applies the refuel;
 * status == 1 reads +u8+u16 and enters the sub_764170 trail; any
 * other status is the terminating denial sub_763510 with zero extra
 * reads. With no ammo model only the denial arm is emitted:
 * `u8 slot, u8 team, u8 status = 2` (wire 3B).
 */

import { Packet } from "../../packet.ts";

export default function GR_AI_RECHARGE_MAGAZINE_START_ACK(
  op: number,
  slot: number,
  team: number,
): Packet {
  return new Packet(op).u8(slot).u8(team).u8(2);
}
