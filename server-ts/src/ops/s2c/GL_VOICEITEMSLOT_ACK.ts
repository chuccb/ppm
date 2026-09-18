/**
 * 792 GL_VOICEITEMSLOT_ACK (CMyVoiceCustomize::sub_876B00, audited
 * 2026-09-19): `u8 soundPack, u16 topVoiceA, u16 topVoiceB,
 * 27 x {u16 voiceItem, u8 flag}` = 86 bytes.
 *
 * soundPack indexes sub_876DB0('s side-record table; 0 is the local
 * character. Every u16 voice id routes through vtbl+8 with
 * `&unk_EAFC40 + id` when nonzero, ELSE 0 — so a zero voice id is
 * literally "use the character's native (non-customized) voice", and
 * flag 0 is the "未自訂/預設" slot marker (PACKETS §3.15voice).
 * All zeros therefore say "no voice customization owned anywhere" —
 * the honest empty state, not filler.
 */

import { Packet } from "../../packet.ts";

/** Local character sound pack (sub_876DB0 selector). */
export const OWN_CHARACTER_SOUND_PACK = 0;
const SLOT_COUNT = 27;

export default function GL_VOICEITEMSLOT_ACK(op: number, soundPack = OWN_CHARACTER_SOUND_PACK): Packet {
  const p = new Packet(op).u8(soundPack).u16(0).u16(0);
  for (let i = 0; i < SLOT_COUNT; i += 1) p.u16(0).u8(0);
  return p;
}
