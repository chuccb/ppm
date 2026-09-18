/**
 * 429 asks the server to add one friend by nickname.
 *
 * Native builder `sub_55A860`: wire is exactly `str key`, sent only when
 * the nickname is non-empty with `strlen <= 23` and the client-side self
 * (`sub_537740`) / duplicate (`sub_538200`) checks were passed locally
 * (those show `0x1E5/0x1E6` before any send).
 *
 * TS policy: there is no friend-table model, so the operation never
 * succeeds. The self-nickname attempt answers status 1 (native self
 * arm); every other name answers the native `else` arm, status 5, whose
 * branch shows the "account not registered" resource (`0x1EC`). No other
 * status is fabricated, and the key is echoed for the ACK/UI pairing.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";
const FRIEND_OP_KEY_MAX_BYTES = 23; // native 24-byte ACK read local

export default function GL_FRIEND_ADD_REQ(r: Reader, connection: Connection): void {
  const key = r.str();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 429`);
  if (key.length === 0) throw new RangeError("429 nickname must be non-empty (native send gate)");
  if (key.length > FRIEND_OP_KEY_MAX_BYTES) {
    throw new RangeError("429 nickname exceeds the native 23-byte send gate");
  }
  const selfNickname = connection.accountId === null
    ? null
    : (connection.config.store.ensurePlayerIdentity(connection.accountId)?.nickname ?? null);
  connection.reply("GL_FRIEND_ADD_ACK", key === selfNickname ? 1 : 5, key);
}
