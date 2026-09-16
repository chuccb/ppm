/** 197 is an empty request; the Store owns the 198 MyInfo projection. */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_MYINFO_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 197`);
  const myInfo = connection.accountId === null
    ? null
    : connection.config.store.ensurePlayerIdentity(connection.accountId);
  const snapshot = myInfo
    ? connection.config.store.getNewSkillProfileSnapshot(myInfo.userId)
    : undefined;
  connection.reply("GL_MYINFO_ACK", myInfo, snapshot);
}
