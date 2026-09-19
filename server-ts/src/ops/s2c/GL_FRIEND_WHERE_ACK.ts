/**
 * 441 -> 442 friend-where result (sub_55B9F0 consumer).
 *
 * Wire (native Fact, sub_55B9F0 re-read line level 2026-09-19):
 * `u8 statusRaw`; only when `statusRaw == 1` the triple
 * `{u8 whereType, u8 channel, u8 roomNo}` follows. Branches:
 * - 11 -> tutorial notice (resource `0x314` formatted with the
 *   nickname cell), then all where-fields cleared.
 * - 9/10 -> chat window is closed first (sub_9A9CF0/sub_9AA050), then:
 *   `channel == *sub_417D00()` (already on the friend's channel) goes
 *   the direct room-join path (`dword_EA131C` vtbl+64 with roomNo, or
 *   the `sub_405EB0` fallback), otherwise `sub_570030(channel)` does a
 *   channel switch first.
 * - any other whereType -> lobby notice `0x21E`, then cleared.
 * statusRaw 2 -> `0x21D` plus the extra `0x3AF` line; any other value,
 * including 0, -> `0x21D` alone — no further bytes are read either way.
 */

import { Packet } from "../../packet.ts";

export default function GL_FRIEND_WHERE_ACK(
  op: number,
  statusRaw: number,
  whereType?: number,
  channel?: number,
  roomNo?: number,
): Packet {
  const p = new Packet(op).u8(statusRaw);
  if (statusRaw === 1) {
    p.u8(whereType ?? 0).u8(channel ?? 0).u8(roomNo ?? 0);
  }
  return p;
}
