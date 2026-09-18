/**
 * 421 asks the server to delete one mailbox entry by its string key.
 *
 * Native builder `sub_55A1E0` sends `{str key}` and only after two local
 * gates: the key is non-empty with `strlen <= 20` bytes, and
 * `sub_537DE0(byte_EE8968, key)` proves the key exists in the local mail
 * table. Wire shape is therefore exactly one ANSI string.
 *
 * TS cap policy: the 426 key store and the 422 ACK read local are both
 * char[20]-class slots, so this server's 426 record provider never issues
 * keys longer than 19 bytes. A key this server never issued cannot be
 * deleted; the empty-mailbox projection replies `statusRaw = 0` (the
 * native failure arm shows the client's own resource dialog for it).
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";
import { MSG_KEY_MAX_BYTES } from "../s2c/GL_MSG_RECVLIST_ACK.ts";

export default function GL_MSG_DEL_REQ(r: Reader, connection: Connection): void {
  const key = r.str();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 421`);
  if (key.length === 0) throw new RangeError("421 key must be non-empty (native send gate)");
  if (key.length > MSG_KEY_MAX_BYTES) {
    throw new RangeError("421 key exceeds the native char[20] mailbox-key slot");
  }
  connection.reply("GL_MSG_DEL_ACK", 0, key);
}
