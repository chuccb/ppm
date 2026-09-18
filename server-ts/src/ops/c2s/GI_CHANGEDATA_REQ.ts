/**
 * 218 GI_CHANGEDATA_REQ — client uploads its locally changed inventory
 * slots (builder sub_572FC0 @167338).
 *
 * Native wire (builder + per-slot serializer sub_5244E0 both re-read
 * line-level; write widths proven by accessor bodies sub_592920 = 1B,
 * sub_5929E0 = 2B):
 *
 *   u8  char_slot         selected character slot
 *   u8  count             changed-slot count (builder aborts > 0x14)
 *   count x 26B record {
 *     u8    slot,
 *     u8    flagRaw,      gate byte from the paired client table
 *     12 x  u16 rawWords  the 13-word slot-data table diff
 *   }
 *
 * The paired-word table semantices stay unresolved (values table at
 * this+13*slot+158..169 words), so the fields keep raw names — exactly
 * what the .c exposes.
 *
 * Native 219 consumer sub_573230 just forwards `u8 status` into the
 * inventory-sync state machine sub_4BCF00: status == 1 flips the
 * pending -> applied transitions (0xC additionally refreshes the UI),
 * status == 0 takes the abort-sync arm, other values are literal no-ops.
 *
 * TS policy: no per-account inventory-slot store exists, so the upload
 * is structurally parsed (count capped at the native 0x14, trailing
 * rejected) and answered with status = 1 — the proven success arm that
 * lets the client state machine settle.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GI_CHANGEDATA_REQ(r: Reader, connection: Connection): void {
  r.u8(); // char_slot
  const count = r.u8();
  if (count > 0x14) {
    throw new RangeError(`218 count ${count} exceeds the native 0x14 cap`);
  }
  if (r.remaining !== count * 26) {
    throw new RangeError(`218 expects count x 26 byte records (${count * 26} bytes), got ${r.remaining}`);
  }
  for (let i = 0; i < count; i++) {
    r.u8(); // slot
    r.u8(); // flagRaw
    for (let w = 0; w < 12; w++) r.u16(); // rawWords
  }
  connection.reply("GI_CHANGEDATA_ACK", 1);
}
