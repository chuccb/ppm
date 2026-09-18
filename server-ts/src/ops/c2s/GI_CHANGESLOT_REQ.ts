/**
 * 312 GI_CHANGESLOT_REQ — character-list selection (CHARSLOT):
 * sub_884160 forwards CClientData+88 (selected_char_list_index) as
 * this packet's single byte (builder sub_573270; wire = exactly
 * `u8 slot_no`). This is NOT an inventory tab switch — the historical
 * comment naming it that way was wrong; the 312 byte is the same field
 * MyInfo consumes for its selected-character projection.
 *
 * Native 313 consumer sub_573320 reads NOTHING from the packet — it
 * only runs the UI refresh sub_538470(byte_EE8968, a2, 0). So the ACK
 * carries no error arm; this server echoes the requested slot.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GI_CHANGESLOT_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 1) {
    throw new RangeError(`312 expects exactly 1 byte (u8 slot_no), got ${r.remaining}`);
  }
  const slotNo = r.u8();
  if (connection.accountId != null) {
    const myInfo = connection.config.store.ensurePlayerIdentity(connection.accountId);
    if (myInfo) connection.config.store.setCurrentCharacter(myInfo.userId, slotNo);
  }
  connection.reply("GI_CHANGESLOT_ACK", slotNo);
}
