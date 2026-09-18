/**
 * 706 -> 707 billing charge token (consumer sub_46AD00).
 *
 * Wire: exactly `str token`. The consumer stores it at `this + 521173`
 * and only uses it to gate the CHARGE banner UI; the empty string is
 * the proven inert value and the only one this server can emit without
 * a billing model.
 */

import { Packet } from "../../packet.ts";

export default function GL_BILLTOKEN_ACK(op: number, token: string): Packet {
  return new Packet(op).str(token);
}
