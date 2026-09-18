/**
 * Compression threshold, and the login trigger.
 *
 * Carries the LZ threshold, and in the same client handler invokes the login
 * request builder — so this packet is what makes the client send credentials.
 * It must be sent exactly once per connection: sending it again after login
 * makes the client resend credentials forever. (docs/PACKETS.md §1.4)
 *
 * The client only lowers its own threshold when the value is strictly below
 * 0x2580, so sending 0x2580 leaves compression off in both directions.
 */

import { COMPRESSION_DISABLED, Packet } from "../../packet.ts";

export default function GL_ACCOUNTCONNSUCC(op: number, threshold = COMPRESSION_DISABLED): Packet {
  return new Packet(op).u16(threshold);
}
