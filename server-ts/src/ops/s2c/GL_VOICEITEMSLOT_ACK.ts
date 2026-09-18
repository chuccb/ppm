/**
 * 791 -> 792 voice-customize slot table (CMyVoiceCustomize::sub_876B00
 * wire reader).
 *
 * Wire (native Fact): `u8 page`, `u16`, `u16`, then 27 fixed
 * `{u16 id, u8 flag}` slot pairs (86 bytes total). Each nonzero id
 * resolves against `unk_EAFC40`; zero selects the null path per slot.
 * Slot/flag semantics beyond that zero/nonzero split are UNRESOLVED
 * and not invented.
 */

import { Packet } from "../../packet.ts";

/** Native proof: 27 fixed slot pairs in the 3x9 voice widget grid. */
export const VOICE_SLOT_PAIRS = 27;

export default function GL_VOICEITEMSLOT_ACK(op: number, page: number): Packet {
  const p = new Packet(op).u8(page).u16(0).u16(0);
  for (let i = 0; i < VOICE_SLOT_PAIRS; i += 1) p.u16(0).u8(0);
  return p;
}
