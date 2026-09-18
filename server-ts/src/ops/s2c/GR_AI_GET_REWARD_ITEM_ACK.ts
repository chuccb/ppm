/**
 * 918 -> 919 GR_AI_GET_REWARD_ITEM_ACK (consumer sub_761B20).
 *
 * Wire under the TS policy: `u8 idx, u8 statusRaw = 1, u8 0xFF` — the
 * failure arm terminated by the sentinel 0xFF (anything else there
 * keeps the client reading the slot/type/count trail). With no loot
 * model this is the only frame this server emits.
 */

import { Packet } from "../../packet.ts";

export default function GR_AI_GET_REWARD_ITEM_ACK(
  op: number,
  idx: number,
): Packet {
  return new Packet(op).u8(idx).u8(1).u8(0xff);
}
