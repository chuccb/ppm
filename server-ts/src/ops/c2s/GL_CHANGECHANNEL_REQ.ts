/**
 * 370 GL_CHANGECHANNEL_REQ — move to another channel (builder
 * sub_570030 @165825: sent only when the target channel differs from
 * the current one; ctor(370) -> sub_592920 one byte -> send; wire =
 * exactly `u8 channel_id`).
 *
 * Native 371 consumer sub_570100 arms:
 *  - status == 1: reads `u8 channel_id, str host_ip, s32 host_port,
 *    u8 extra`, hands the endpoint to sub_596E60 (secondary UDP
 *    address field) and shows the move banner (resource 0x163);
 *  - status == 0 / 2 / 3: failure banners with resources 0xDA / 0x148 /
 *    0x328 and the pending holder reset;
 *  - status >= 4: silent reset only.
 *
 * TS policy: this deployment advertises exactly ONE channel (group 0,
 * the other entries are maxUsers 0), so there is no legal alternative
 * endpoint to hand out; the honest answer is status = 0 (wire "00"),
 * the native "cannot move" failure arm.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_CHANGECHANNEL_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 1) {
    throw new RangeError(`370 expects exactly 1 byte (u8 channel_id), got ${r.remaining}`);
  }
  r.u8(); // channel_id: no alternative channel exists in this deployment
  connection.reply("GL_CHANGECHANNEL_ACK", 0);
}
