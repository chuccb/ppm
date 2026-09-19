/**
 * 706 -> 707 billing charge token (consumer sub_46AD00).
 *
 * Wire: exactly `str token`.
 *
 * Line-level acquittal (2026-09-19): `sub_46AD00` is an opcode
 * re-dispatch switch on `sub_591EE0(v121)`; case 707 performs exactly
 * ONE read, `sub_592730(v121, this+521173)`. Every other accessor call
 * in that function belongs to sibling cases (699/703/...). The consumer
 * stores the token at `this + 521173`
 * and only uses it to gate the CHARGE banner UI; the empty string is
 * the proven inert value and the only one this server can emit without
 * a billing model.
 */

import { Packet } from "../../packet.ts";

export default function GL_BILLTOKEN_ACK(op: number, token: string): Packet {
  return new Packet(op).str(token);
}
