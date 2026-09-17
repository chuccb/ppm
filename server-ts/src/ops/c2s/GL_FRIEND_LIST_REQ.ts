/** 433 is an empty request for the current user's friend list. */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_FRIEND_LIST_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 433`);
  const myInfo = connection.accountId === null
    ? null
    : connection.config.store.ensurePlayerIdentity(connection.accountId);
  // 434 stores this otherwise-unused compatibility string in native char[21].
  const context = myInfo?.nickname;
  connection.reply("GL_FRIEND_LIST_ACK", context && context.length <= 20 ? context : "");
}
