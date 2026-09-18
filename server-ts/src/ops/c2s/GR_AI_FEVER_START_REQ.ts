/**
 * 935 GR_AI_FEVER_START_REQ — request fever/burst mode (builder
 * sub_7622C0 @393717): gated send with ctor(935) and NO field
 * writers — wire is the empty body (0 bytes).
 *
 * 936 consumer sub_7623A0 (full body): fixed 7-byte read — `u8
 * status, u8 flag, s32 duration_ms, u8 type`. status != 0 applies the
 * fever start ONLY when `s32 duration_ms` equals the client's own
 * dword_EE8CB4 baseline, else error-log; status == 0 is the declined
 * arm (state=2 + UI broadcast with the flag, zero reads remain).
 * TS: no fever model -> the designated declined arms is the only
 * frame: `u8 0, u8 0, s32 0, u8 0` (wire 7 zero bytes).
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GR_AI_FEVER_START_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) {
    throw new RangeError(`935 builder writes no fields (empty body), got ${r.remaining} byte(s)`);
  }
  connection.reply("GR_AI_FEVER_START_ACK");
}
