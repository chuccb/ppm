/**
 * 218 GI_CHANGEDATA_REQ — dirty character-record push (builder
 * sub_572FC0): `u8 selected_slot(from CClientData+88), u8 count(<=0x14),
 * count x {u8 slot, u8 charType, 12 x u16 appearance}` = slot-record
 * layout shared with 198 (sub_5244E0 per dirty row; the wire's second
 * byte is the persistent char_type read position, verified against the
 * 198 mirror reader).
 *
 * The client has ALREADY applied these rows optimistically when it
 * sends 218 (sub_525450 emits only changed slots); dropping them while
 * ACKing success would fabricate a divergence — the next login would
 * undo the appearance change the player already sees. The honest
 * answer requires persistence, which is exactly what applyCharacterData
 * now provides transactionally.
 *
 * 219 sub_573230 -> sub_4BCF00 semantics: status == 1 advances the
 * local state machine (+1160: 2->3 / 4->5 / 6->7 / 8->9); status == 0
 * simply does not transition. Nothing else is read.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

const NATIVE_CHARACTER_SLOT_COUNT = 20;
const APPEARANCE_WORD_COUNT = 12;

export default function GI_CHANGEDATA_REQ(r: Reader, connection: Connection): void {
  const selectedSlot = r.u8();
  const count = r.u8();
  if (count > NATIVE_CHARACTER_SLOT_COUNT) {
    throw new RangeError(`218 native builder caps count at 0x14 (${NATIVE_CHARACTER_SLOT_COUNT}), got ${count}`);
  }
  const rows = [];
  for (let i = 0; i < count; i++) {
    const slot = r.u8();
    const characterType = r.u8();
    const appearance: number[] = [];
    for (let w = 0; w < APPEARANCE_WORD_COUNT; w++) appearance.push(r.u16());
    rows.push({ slot, characterType, appearance });
  }
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 218`);

  if (connection.accountId == null) {
    throw new RangeError("218 requires a bound account identity");
  }
  const myInfo = connection.config.store.ensurePlayerIdentity(connection.accountId);
  if (!myInfo) throw new RangeError("218 identity bootstrap unexpectedly empty");

  const store = connection.config.store;
  store.setCurrentCharacter(myInfo.userId, selectedSlot); // CClientData+88 echoes the selection; false = unowned slot, drop silently
  const applied = store.applyCharacterData(myInfo.userId, rows);
  connection.reply("GI_CHANGEDATA_ACK", applied ? 1 : 0);
}
