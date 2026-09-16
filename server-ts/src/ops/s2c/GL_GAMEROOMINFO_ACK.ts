/**
 * 107 -> 108 empty room list.
 *
 * Mode 0 is the ordinary list branch; with count zero the client reads no room
 * records. Room state is intentionally not synthesized before a Room model
 * exists in server-ts.
 */

import { Packet } from "../../packet.ts";

export default function GL_GAMEROOMINFO_ACK(op: number): Packet {
  return new Packet(op)
    .u8(0) // ordinary room-list mode, not tournament mode
    .u8(0); // room record count; therefore no room fields follow
}
