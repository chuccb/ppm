/**
 * 700 — Pepachi spin request.
 *
 * docs/PACKETS.md §3.15d2a: the native builder emits exactly
 * `{u8 machine, s32 coinType}`. The request is fully consumed to keep the
 * frame boundary strict; both fields are unactionated — the safe ACK carries
 * no reel result and no wallet movement.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GP_START_GAME_REQ(r: Reader, connection: Connection): void {
  const machine = r.u8();
  const coinType = r.s32();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 700`);
  void machine;
  void coinType;
  connection.reply("GP_START_GAME_ACK");
}
