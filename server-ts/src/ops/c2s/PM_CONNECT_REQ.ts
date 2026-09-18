/**
 * 141 PM_CONNECT_REQ — endpoint re-confirm request after the client's own
 * private-UDP probe.
 *
 * Native client builder sub_556530 sends a bare opcode the moment its
 * private-UDP op18 probe latches (docs/PACKETS.md §3.15d). The 142 reply
 * consumer sub_5565D0 reads `str endpoint_host` into a local char[20],
 * `s32 endpoint_port` (only the low u16 is used), `u8 active_channel_index`
 * (stored via sub_417D00), and `u32 packed_calendar` which sub_534F20
 * unpacks into the client's server-clock words.
 *
 * TS policy: the UDP control listener is already bound by the time any TCP
 * client can reach this handler, so always answer with the live advertised
 * endpoint — just like native, which uses this exchange as a second chance
 * to publish the endpoint on top of the 693/144 admission flow.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function PM_CONNECT_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) {
    throw new RangeError(`141 expects an empty payload, got ${r.remaining} bytes`);
  }
  connection.reply("PM_CONNECT_ACK", {
    endpoint: connection.config.channel.endpoint,
    activeChannelIndex: connection.config.channel.index,
    serverTime: new Date(),
  });
}
