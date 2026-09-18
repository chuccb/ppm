/**
 * 218 -> 219 GI_CHANGEDATA_ACK (consumer sub_573230 ->
 * state machine sub_4BCF00).
 *
 * Wire: `u8 status`. status == 1 flips the client's inventory-sync
 * pending -> applied transitions (sub_522440 additionally refreshes the
 * UI); status == 0 takes the abort-sync arm (no transition, player
 * simply does not get the applied state). No other value has any
 * recovered branch, so the domain is exactly {0,1}.
 *
 * Since 2026-09-19 the handler behind 218 persists rows into
 * player_character transactionally: success reports status 1, an
 * unknown-slot rollback reports status 0.
 */

import { Packet } from "../../packet.ts";

export default function GI_CHANGEDATA_ACK(op: number, status: 0 | 1 = 1): Packet {
  return new Packet(op).u8(status);
}
