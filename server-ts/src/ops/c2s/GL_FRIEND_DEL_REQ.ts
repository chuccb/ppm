/**
 * 431 asks the server to delete one friend by nickname.
 *
 * Native builder `sub_55AD00`: wire is exactly `str key`, sent only when
 * the nickname is non-empty with `strlen <= 23`.
 *
 * TS policy: with no friend table there is nothing to remove; every
 * request answers the native failure arm status 2 (resource `0x1EE`,
 * friend-list registration failure) with the key echoed back.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";
const FRIEND_OP_KEY_MAX_BYTES = 23; // native 24-byte ACK read local

export default function GL_FRIEND_DEL_REQ(r: Reader, connection: Connection): void {
  const key = r.str();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 431`);
  if (key.length === 0) throw new RangeError("431 nickname must be non-empty (native send gate)");
  if (key.length > FRIEND_OP_KEY_MAX_BYTES) {
    throw new RangeError("431 nickname exceeds the native 23-byte send gate");
  }
  connection.reply("GL_FRIEND_DEL_ACK", 2, key);
}
