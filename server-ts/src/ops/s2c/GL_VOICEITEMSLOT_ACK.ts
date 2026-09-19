/**
 * 792 GL_VOICEITEMSLOT_ACK (CMyVoiceCustomize::sub_876B00, audited
 * 2026-09-19): `u8 soundPack, s16 topVoiceA, s16 topVoiceB,
 * 27 x {s16 voiceItem, u8 flag}` = 86 bytes (docs/LAYOUTS.md row 792:
 * `u8 s16 s16 27x(s16 u8)`).
 *
 * Per-field meaning:
 * - `soundPack` indexes the sub_876DB0 side-record table; 0 is the
 *   local character's own pack.
 * - `topVoiceA/B` are the featured voice ids shown at the top of the
 *   customize pane; 0 = none featured.
 * - each slot: `voiceItem` routes through vtbl+8 with
 *   `&unk_EAFC40 + id` when nonzero, ELSE 0 — so a zero voice id is
 *   literally "use the character's native (non-customized) voice";
 *   `flag` 0 is the "未自訂/預設" slot marker (PACKETS §3.15voice).
 *
 * The default frame is therefore the semantic empty state "no voice
 * customization owned anywhere", not filler; real owned sets can be
 * passed slot-by-slot via `slots`.
 */

import { Packet } from "../../packet.ts";

/** Local character sound pack (sub_876DB0 selector). */
export const OWN_CHARACTER_SOUND_PACK = 0;
export const SLOT_COUNT = 27;

/** One owned voice-customization slot; id 0 keeps the native voice, flag 0 means default. */
export interface VoiceItemSlot {
  readonly voiceItem: number;
  readonly flag: number;
}

export default function GL_VOICEITEMSLOT_ACK(
  op: number,
  soundPack = OWN_CHARACTER_SOUND_PACK,
  topVoiceA = 0,
  topVoiceB = 0,
  slots: readonly VoiceItemSlot[] = [],
): Packet {
  const p = new Packet(op).u8(soundPack).s16(topVoiceA).s16(topVoiceB);
  if (slots.length > SLOT_COUNT) {
    throw new RangeError(`native table holds exactly ${SLOT_COUNT} voice slots`);
  }
  for (const s of slots) p.s16(s.voiceItem).u8(s.flag);
  for (let i = slots.length; i < SLOT_COUNT; i += 1) p.s16(0).u8(0);
  return p;
}
