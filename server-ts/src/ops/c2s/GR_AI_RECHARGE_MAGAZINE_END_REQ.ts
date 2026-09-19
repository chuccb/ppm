/**
 * 926 GR_AI_RECHARGE_MAGAZINE_END_REQ (builder sub_5586B0 @153814):
 * `u8 slot, u8 kind, s8 status` — sub_592920 x2 + sub_5928E0 (all
 * one-byte accessors; sub_5928E0 width re-verified). Wire = 3 bytes.
 *
 * 927 consumer sub_558880 (full body): reads `u8 slot, u8 team,
 * s8/bool unk (sub_592900, 1B verified), u8 statusRaw`; when statusRaw
 * != 0 the consumer stops there and runs the denial sub_763510;
 * statusRaw == 0 continues +u8+s32 into the refuel-end logic.
 * TS: no ammo model -> always the 4-byte terminating denial arm
 * `u8 slot, u8 team, s8/bool unk = 0 (no request counterpart; unread on
 * the denial arm), u8 statusRaw = 1`.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GR_AI_RECHARGE_MAGAZINE_END_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 3) {
    throw new RangeError(`926 expects exactly 3 bytes (u8 slot, u8 kind, s8 status), got ${r.remaining}`);
  }
  const slot = r.u8();
  const kind = r.u8();
  /* const status = */ r.s8();
  connection.reply("GR_AI_RECHARGE_MAGAZINE_END_ACK", slot, kind);
}
