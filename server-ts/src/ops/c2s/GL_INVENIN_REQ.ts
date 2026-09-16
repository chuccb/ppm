/**
 * 254 enters the inventory/NewSkill scene with one opaque context byte.
 *
 * The byte is echoed structurally in 255; its UI/entity meaning is unresolved.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_INVENIN_REQ(r: Reader, connection: Connection): void {
  const requestContextRaw = r.u8();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 254`);
  if (connection.accountId === null) throw new Error("254 requires an authenticated account");

  const myInfo = connection.config.store.ensurePlayerIdentity(connection.accountId);
  if (!myInfo) throw new Error("254 account has no user profile");

  const uid = myInfo.userId;
  const snapshot = connection.config.store.getNewSkillProfileSnapshot(uid);
  connection.reply("GL_INVENIN_ACK", uid, requestContextRaw, snapshot);
}
