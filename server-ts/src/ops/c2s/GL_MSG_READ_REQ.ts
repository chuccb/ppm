/**
 * 423 asks the server to mark one mailbox entry read, by its string key.
 *
 * Native builder `sub_55A3C0` mirrors 421 (`{str key}` wire) with send
 * gates: non-empty key of `strlen <= 20` bytes, and
 * `sub_537E90(byte_EE8968, key) == 0` — sent only while the entry is not
 * yet marked `89` (unread). Wire shape is exactly one ANSI string.
 *
 * Same cap policy as 421: TS keeps the key within the native char[20]
 * mailbox-key slot (19 bytes max), and with no mailbox model the only
 * honest reply to a key this server never issued is `statusRaw = 0`
 * (native failure arm).
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";
const MSG_KEY_MAX_BYTES = 19; // sub_5378C0 stride-20 slot, including NUL

export default function GL_MSG_READ_REQ(r: Reader, connection: Connection): void {
  const key = r.str();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 423`);
  if (key.length === 0) throw new RangeError("423 key must be non-empty (native send gate)");
  if (key.length > MSG_KEY_MAX_BYTES) {
    throw new RangeError("423 key exceeds the native char[20] mailbox-key slot");
  }
  connection.reply("GL_MSG_READ_ACK", 0, key);
}
