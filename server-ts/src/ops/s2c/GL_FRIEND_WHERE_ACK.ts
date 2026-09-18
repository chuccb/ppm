/**
 * 441 -> 442 friend-where result (sub_55B9F0 consumer).
 *
 * Wire (native Fact): `u8 statusRaw`; only when `statusRaw == 1` the
 * triple `{u8 whereType, u8 channel, u8 roomNo}` follows. Branches:
 * 1 -> tutorial when whereType == 11 (resource `0x314`), room-join /
 * channel-switch flow when whereType == 9 or 10, otherwise the lobby
 * notice (`0x21E`); 2 -> `0x21D` plus the extra `0x3AF` line; any other
 * value, including 0, -> `0x21D` alone, and no further bytes are read.
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
