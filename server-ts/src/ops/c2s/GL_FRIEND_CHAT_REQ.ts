/**
 * 439 sends one friend whisper.
 *
 * Native builder `sub_55B510(friendNick, message)`, gated by
 * `strlen(message) <= 180`: wire is exactly
 * `{s32 rawUidContext, str selfNick, str friendNick, str message}`
 * written to the GL lobby socket. `rawUidContext` is `dword_F2A684` —
  * the same propagated raw4 context carried by 834; its server-domain
 * meaning is UNRESOLVED, so it is parsed and deliberately not joined to
 * Store identity. `selfNick` is the client's own nickname from
 * `sub_537740`.
 *
 * TS policy: no friend table and no whisper relay exist here, so the
 * universal proven failure arm is status 3 (resource `0x1D8`
 * 「%sさんを見つけることが出来ませんでした」) with the wire nick pair
 * echoed back. The client displays through its global `sub_401B20`
 * state regardless of the wire nicks (they only advance the cursor), so
 * the echo carries no display risk. Status 0/1 (not-exist / offline)
 * encode claims this server cannot substantiate and are never sent.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

const FRIEND_CHAT_MESSAGE_MAX_BYTES = 180; // native sub_55B510 gate

export default function GL_FRIEND_CHAT_REQ(r: Reader, connection: Connection): void {
  const rawUidContext = r.s32();
  const selfNick = r.str();
  const friendNick = r.str();
  const message = r.str();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 439`);
  if (message.length > FRIEND_CHAT_MESSAGE_MAX_BYTES) {
    throw new RangeError("439 message exceeds the native 180-byte send gate");
  }
  void rawUidContext;
  connection.reply("GL_FRIEND_CHAT_ACK", 3, selfNick, friendNick);
}
