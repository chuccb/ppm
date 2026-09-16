/**
 * Server-initiated heartbeat. Empty payload.
 *
 * The direction is the opposite of what the REQ/ACK names suggest, and the
 * binary is unambiguous (docs/PACKETS.md §3.15pre): the client's dispatcher
 * handles this packet by building the ping request and sending it straight
 * back (`sub_58D6F0`). It has no handler for that request and no builder for
 * this one. So the server polls, and an inbound request is proof of life —
 * answering one would loop both sides forever.
 */

import { Packet } from "../../packet.ts";

export default function GT_PING_ACK(op: number): Packet {
  return new Packet(op);
}
