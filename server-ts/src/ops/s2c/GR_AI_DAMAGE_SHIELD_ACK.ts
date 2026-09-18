/**
 * 922 -> 923 GR_AI_DAMAGE_SHIELD_ACK (consumer sub_761710).
 *
 * Wire: `s16 shield_id, s16 damage, s16 remain, s32 raw32` (10 bytes).
 * raw32 is carried verbatim — natively it is the sign-extended LOW
 * BYTE of the builder's float scale argument (sub_592B20/SLOBYTE),
 * so a verbatim echo is byte-identical to what a native server would
 * broadcast. The consumer only acts when a room object matches the
 * (id, damage) pair and remain is 4 or 7; the echo itself is inert.
 */

import { Packet } from "../../packet.ts";

export default function GR_AI_DAMAGE_SHIELD_ACK(
  op: number,
  shieldId: number,
  damage: number,
  remain: number,
  raw32: number,
): Packet {
  return new Packet(op).s16(shieldId).s16(damage).s16(remain).s32(raw32);
}
