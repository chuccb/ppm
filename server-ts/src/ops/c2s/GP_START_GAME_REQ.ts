/**
 * 700 — Pepachi spin request (native builder `sub_8458D0`, line level
 * re-read 2026-09-19).
 *
 * Wire: `{u8 n5, s32 key}` where
 *   - n5        = outer machine/panel index (byte_D7AC88 family caller
 *                 sub_8459C0; matches the pepachi page's selected machine)
 *   - key       = `0x12FA660 (= 19,900,000) + (sub_525790(activeChar) % 100000)`
 *
 * `sub_525790` returns the active character's roster byte at
 * `+158 + 13*selectedSlot` (a data-driven, ITEMDB-indexed bat/group
 * code). The key constant is one element of the client's room-object
 * address family — the serializer v17[0..N] writes the N slot codes under
 * the bases 19,900,000 / 10,000,000 / 10,100,000 / 10,200,000 / ... and
 * the decoder `n20 = a2 - 19,900,000` unwraps slot 0 again. So the s32 is
 * the slot-0 key of the player's active character, i.e. WHICH bat/group
 * this character owns — the "machine index + character key" pair, not a
 * coin type. The request is still fully consumed without action: the safe
 * ACK carries no reel result and no wallet movement.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GP_START_GAME_REQ(r: Reader, connection: Connection): void {
  const machine = r.u8();              // n5: panel/machine index
  const characterKey = r.s32();        // 0x12FA660 + charKey%100000 (slot-0 object key)
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 700`);
  void machine;
  void characterKey;
  connection.reply("GP_START_GAME_ACK");
}
