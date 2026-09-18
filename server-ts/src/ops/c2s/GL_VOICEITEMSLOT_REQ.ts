/**
 * 791 asks for the voice-customize slot table (builder `sub_885590`:
 * empty; the requested context `(this+296, this+292)` stays client-side
 * and is never sent).
 *
 * Native 792 consumer chain: `case 792u` -> `sub_885D00` ->
 * `CMyVoiceCustomize::sub_876B00`, which reads `u8 page` then 2 `s16`
 * and 27 `{s16 id, u8 flag}` slots into the page-selected slot table
 * (`sub_876DB0(this, page)`); nonzero ids resolve `unk_EAFC40 + id`.
 *
 * TS policy: no voice-item system exists here, so the whole table is
 * all-zero — the honest "nothing configured" frame (every slot clears
 * to the null widget path, exactly like the native zero arm) — on the
 * proven page index 0.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_VOICEITEMSLOT_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 791`);
  connection.reply("GL_VOICEITEMSLOT_ACK", 0);
}
