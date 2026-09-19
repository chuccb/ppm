/**
 * 944 -> 945 GR_RESET_GAMEROOMSLOT_ACK (consumer sub_435E40 via
 * wrapper sub_585F30).
 *
 * This is a count-grammar frame, NOT a status: `u8 count` followed by
 * count slot entries of `{u8 slot, s32 value}`. count == 0 ends
 * consumption after the single byte. A nonzero count has the client
 * tear down and rebuild its GAMEROOM_USERSLOTS registry group from the
 * listed slots, so entries must describe the whole room-slot board —
 * the default empty frame below stays the only honest emission until a
 * room-slot store exists.
 */

import { Packet } from "../../packet.ts";

/** One rebuilt slot row: `{u8 slot, s32 value}`. */
export interface RoomSlotReset {
  readonly slot: number;
  readonly value: number;
}

export default function GR_RESET_GAMEROOMSLOT_ACK(
  op: number,
  entries: readonly RoomSlotReset[] = [],
): Packet {
  const p = new Packet(op).u8(entries.length);
  for (const e of entries) p.u8(e.slot).s32(e.value);
  return p;
}
