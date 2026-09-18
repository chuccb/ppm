/**
 * 142 PM_CONNECT_ACK — endpoint re-confirm + server wall clock.
 *
 * Wire (consumer sub_5565D0, validated line by line):
 *   str endpoint_host              local char[20]; at most 19 bytes + NUL
 *   s32 endpoint_port              only the low u16 reaches sub_596E60
 *   u8 active_channel_index        stored via sub_417D00
 *   u32 packed_calendar            sub_534F20 unpacks:
 *                                    bits 24-31: year - 2000
 *                                    bits 19-23: month (1-12)
 *                                    bits 13-18: day of month
 *                                    bits  7-12: hour
 *                                    bits  0- 6: minute
 *   (seconds are not on the wire; the consumer zeroes them.)
 *
 * The wire carries no timezone, and native never documents one — the
 * obvious reading is the server's own wall clock, so TS encodes the
 * process-local time (deployment TZ is pinned by the operator).
 */

import { Packet } from "../../packet.ts";

export interface ConnectInfo {
  readonly endpoint: { readonly host: string; readonly port: number };
  readonly activeChannelIndex: number;
  readonly serverTime: Date;
}

/** `(year-2000)<<24 | month<<19 | day<<13 | hour<<7 | minute` — inverse of sub_534F20. */
export function packCalendar(date: Date): number {
  return (
    ((date.getFullYear() - 2000) << 24) |
    ((date.getMonth() + 1) << 19) |
    (date.getDate() << 13) |
    (date.getHours() << 7) |
    date.getMinutes()
  ) >>> 0;
}

export default function PM_CONNECT_ACK(op: number, info: ConnectInfo): Packet {
  return new Packet(op)
    .str(info.endpoint.host)
    .s32(info.endpoint.port)
    .u8(info.activeChannelIndex)
    .u32(packCalendar(info.serverTime));
}
