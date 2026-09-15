/**
 * Keepalive: GT_PING_ACK(102) out, GT_PING_REQ(101) back.
 *
 * The direction is the opposite of what the REQ/ACK names suggest, and the
 * binary is unambiguous (docs/PACKETS.md §3.15pre):
 *
 *   - dispatcher `case 102u` -> `sub_58D6F0`, which builds `Packet(101)` with
 *     an empty payload and sends it immediately.
 *   - The client has no handler for 101 and no builder for 102.
 *
 * So the server drives the heartbeat: send 102 periodically, treat an inbound
 * 101 as proof of life. Never reply to 101 with 102 — that loops forever.
 */

import { Packet } from "./packet.ts";
import { Op } from "./opcodes.ts";

/** How often to poll. Not evidenced by the client; a server-side choice. */
export const PING_INTERVAL_MS = 15_000;

/** Drop a connection that has not answered for this long. */
export const PING_TIMEOUT_MS = 60_000;

/** GT_PING_ACK(102): server-initiated heartbeat, empty payload. */
export function GT_PING_ACK(): Packet {
  return new Packet(Op.GT_PING_ACK, 0);
}
