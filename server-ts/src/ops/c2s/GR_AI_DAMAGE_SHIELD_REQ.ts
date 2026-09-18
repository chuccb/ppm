/**
 * 922 GR_AI_DAMAGE_SHIELD_REQ — defence-core damage report (builder
 * sub_761580 @393240, client-state gated; send body:
 *   sub_5929E0 (2B) x 3  -> s16 shield_id, s16 damage, s16 remain
 *   sub_592B20 (4B) with SLOBYTE(*a4) — the client transmits only the
 *   LOW BYTE of the float SHIELD_SCALE argument, sign-extended into
 *   4 bytes. Wire = 10 bytes.
 *
 * 923 consumer sub_761710 mirror-reads u16/u16/u16 + f32 (10B) and only
 * then consults state; a4-affects only sub_75E3A0's transform of the
 * echoed 4-byte field. It is a room-broadcast sync: this server has no
 * defence-core model, so it echoes the received frame verbatim
 * (byte-identical s16 x3 + raw s32) by replying with the parsed fields.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GR_AI_DAMAGE_SHIELD_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 10) {
    throw new RangeError(`922 expects exactly 10 bytes (s16 x3 + s32 low-byte float), got ${r.remaining}`);
  }
  const shieldId = r.s16();
  const damage = r.s16();
  const remain = r.s16();
  const scaleLowRaw = r.s32();
  connection.reply("GR_AI_DAMAGE_SHIELD_ACK", shieldId, damage, remain, scaleLowRaw);
}
