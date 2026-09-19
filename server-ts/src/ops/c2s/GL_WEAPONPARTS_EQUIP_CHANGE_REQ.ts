/**
 * 912 GL_WEAPONPARTS_EQUIP_CHANGE_REQ — change equipped weapon parts
 * (builder sub_95AEF0 @619208; three send arms; the payload words go
 * through sub_592AA0, the generic 4-byte caller-defined writer):
 *   raw0 = 0 or 1: `u8 raw0, raw4 raw1, raw4 raw2`  (9 bytes)
 *   raw0 = 2:      extra trailing raw4 raw3          (13 bytes)
 * Domain meanings of the branches stay UNRESOLVED per documentation.
 *
 * Native 913 consumer sub_95B180: reads `u8 errorRaw`; when 0 it goes
 * on reading the 912-shaped body (u8 raw0, 2-3 x raw4) and BOUNDS-CHECKS
 * the final word against the native item tables; nonzero errorRaw ends
 * consumption after the single byte.
 *
 * TS policy: no weapon-parts model exists, so echoing ids into the
 * bounds-checked pipeline (errorRaw = 0) is deliberately avoided; the
 * answer is always errorRaw = 1 — the one-byte "generic failure" arm
 * (wire "01").
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_WEAPONPARTS_EQUIP_CHANGE_REQ(r: Reader, connection: Connection): void {
  const raw0 = r.u8();
  if (raw0 !== 0 && raw0 !== 1 && raw0 !== 2) {
    throw new RangeError(`912 raw0 ${raw0} is not a native send arm (0/1/2)`);
  }
  const want = raw0 === 2 ? 12 : 8;
  if (r.remaining !== want) {
    throw new RangeError(`912 raw0=${raw0} expects ${1 + want} total bytes, tail mismatch (${r.remaining})`);
  }
  r.s32(); // raw4 raw1 (sub_592AA0 caller-defined 4-byte word)
  r.s32(); // raw4 raw2
  if (raw0 === 2) r.s32(); // raw4 raw3
  connection.reply("GL_WEAPONPARTS_EQUIP_CHANGE_ACK");
}
