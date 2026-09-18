/**
 * 435 asks per-nick online/location info for the local friend list.
 *
 * Native builder `sub_55B0A0`: wire is exactly ONE `str` with the
 * comma-separated list of the 21-byte friend-table names
 * ("nick1,nick2,..." with no trailing comma), assembled into a
 * `String[1028]` buffer (memset 0x400). When the local friend table is
 * empty the client does NOT send 435 at all (it posts the UI update
 * directly); a empty string is still parsed defensively.
 *
 * TS policy: this server keeps no friend table, so no nickname can ever
 * hold state. Every parsed nickname is echoed back in 436 with
 * `statusRaw = 0` (the native "no extra context" arm), which leaves the
 * client's table mutation with the proven non-online arm. Rows are
 * capped at the native 100-entry friend table and the 20-byte stride.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";
const FRIEND_NICKNAME_MAX_BYTES = 20; // sub_537F60 stride-21 slot, including NUL

/** Native sub_55B0A0 assembles the comma-separated list into String[1028] (0x400 bytes used). */
const FRIEND_INFO_NAMES_MAX_BYTES = 1023;

/** Native friend table capacity mirrored by the 434/435/436 chain (100 entries). */
const FRIEND_INFO_ROWS_MAX = 100;

export default function GL_FRIEND_INFO_REQ(r: Reader, connection: Connection): void {
  const csv = r.str();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 435`);
  if (csv.length > FRIEND_INFO_NAMES_MAX_BYTES) {
    throw new RangeError(`435 nick list exceeds the native char[${FRIEND_INFO_NAMES_MAX_BYTES + 1}] buffer`);
  }
  const rows = csv.length === 0 ? [] : csv.split(",").map((nickname, index) => {
    if (nickname.length === 0) throw new RangeError(`435 row ${index} name must be non-empty`);
    if (nickname.length > FRIEND_NICKNAME_MAX_BYTES) {
      throw new RangeError(`435 row ${index} exceeds the native 20-byte friend-table stride`);
    }
    return { nickname, statusRaw: 0 };
  });
  if (rows.length > FRIEND_INFO_ROWS_MAX) {
    throw new RangeError(`435 lists more than ${FRIEND_INFO_ROWS_MAX} names (native friend-table capacity)`);
  }
  connection.reply("GL_FRIEND_INFO_ACK", rows);
}
