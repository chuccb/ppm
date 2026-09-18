/**
 * 466 GI_CHANGE_SKILLITEMSLOT_REQ — NewSkill accessory slot upload
 * (builder sub_5738A0 @167578, sole wrapper sub_4AA480 forwarded from
 * the NEWSKILL_ACCESSORY UI @77877).
 *
 * Native wire (builder + blob writer sub_527BA0 bodies re-read;
 * sub_592920 = 1 byte, sub_527BA0 = 0x1C bytes):
 *   u8 raw0
 *   u8 raw1
 *   [raw1 != 0 only] u8 raw2, 28-byte bulk (7 x s32 accessory ids)
 * Domain semantics of raw0/raw1/raw2 stay UNRESOLVED on purpose — the
 * .c exposes only their structure, so the fields keep these raw names.
 *
 * Native 467 consumer sub_573A70: reads `u8 resultRaw,
 * u8 unknownHeaderRaw, u8 count`, then per row `u8 profile` and — only
 * when the client-side accessory store dword_E650B0 exists — a 32-byte
 * row payload. The two header bytes are read but never used.
 *
 * TS policy: no NewSkill accessory store, so rows are always empty:
 * reply `resultRaw = 0, unknownHeaderRaw = 0, count = 0` (wire "000000").
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GI_CHANGE_SKILLITEMSLOT_REQ(r: Reader, connection: Connection): void {
  if (r.remaining < 2) {
    throw new RangeError(`466 expects at least 2 bytes (u8 raw0, u8 raw1), got ${r.remaining}`);
  }
  r.u8(); // raw0
  const raw1 = r.u8();
  const want = raw1 !== 0 ? 29 : 0;
  if (r.remaining !== want) {
    throw new RangeError(`466 expects ${2 + want} total payload bytes for raw1=${raw1}, tail mismatch (${r.remaining})`);
  }
  if (raw1 !== 0) {
    r.u8(); // raw2
    for (let i = 0; i < 7; i++) r.s32(); // accessory ids bulk
  }
  connection.reply("GI_CHANGE_SKILLITEMSLOT_ACK", 0);
}
