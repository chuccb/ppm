/**
 * 700 -> 701: consumer-safe spin denial.
 *
 * sub_84A490 only opens award/reel decoding when the first byte is 1, so the
 * safe arm ends the frame right after `{u8 0, u8 rawError 0}` — no jackpot/
 * balance words, no reel triplets, nothing to misread as a grant.
 */

import { Packet } from "../../packet.ts";

export default function GP_START_GAME_ACK(op: number): Packet {
  return new Packet(op).u8(0).u8(0);
}
