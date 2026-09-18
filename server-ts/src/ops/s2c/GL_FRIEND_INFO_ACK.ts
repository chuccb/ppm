/**
 * 435 -> 436 friend-info result (sub_55B2C0 consumer).
 *
 * Wire (native Fact): `u8 count`, then `count` rows of
 * `{str nickname, u8 statusRaw}`; when `statusRaw == 1` the row is
 * followed by `{str channelText, u8 raw}`. Each row is handed to
 * `sub_5382D0(table, nickname, statusRaw, channelText, raw + 1)` and the
 * UI virtual call receives `(matchedOnlineCount, count, 1)`.
 *
 * Rows with `statusRaw != 1` therefore only echo the nickname. The only
 * status distinction the binary proves is "== 1 carries extra context";
 * no richer meaning is invented here.
 */

import { Packet } from "../../packet.ts";

export interface FriendInfoRow {
  nickname: string;
  statusRaw: number;
  /** Present only when statusRaw == 1 (native conditional pair). */
  channelText?: string;
  /** Present only when statusRaw == 1. */
  raw?: number;
}

export default function GL_FRIEND_INFO_ACK(op: number, rows: FriendInfoRow[]): Packet {
  const p = new Packet(op).u8(rows.length);
  rows.forEach(({ nickname, statusRaw, channelText, raw }) => {
    p.str(nickname).u8(statusRaw);
    if (statusRaw === 1) {
      p.str(channelText ?? "").u8(raw ?? 0); // native locals: v5[5]=20B text + raw
    }
  });
  return p;
}
