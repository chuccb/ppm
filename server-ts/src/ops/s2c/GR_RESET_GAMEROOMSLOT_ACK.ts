/**
 * 944 -> 945 GR_RESET_GAMEROOMSLOT_ACK (consumer sub_435E40 via
 * wrapper sub_585F30).
 *
 * This is a count-grammar frame, NOT a status: `u8 count` followed by
 * count slot entries (u8, s32 each). count == 0 ends consumption
 * after the single byte — the only frame this server emits, since a
 * nonzero count would have the client tear down and rebuild its
 * GAMEROOM_USERSLOTS registry group from a list this server cannot
 * fabricate. Wire: "00".
 */

import { Packet } from "../../packet.ts";

export default function GR_RESET_GAMEROOMSLOT_ACK(op: number): Packet {
  return new Packet(op).u8(0);
}
