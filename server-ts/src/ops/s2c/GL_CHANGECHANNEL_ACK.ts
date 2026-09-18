/**
 * 370 -> 371 GL_CHANGECHANNEL_ACK (consumer sub_570100).
 *
 * Wire: `u8 status`; under status == 1 the client additionally reads
 * `u8 channel_id, str host_ip, s32 host_port, u8 extra` and hands the
 * endpoint to sub_596E60. status 0/2/3 are failure banners, >= 4 is a
 * silent reset. This deployment exposes a single channel, so this
 * server always emits status = 0 (wire "00"); a successful redirect
 * would require a second channel endpoint this server does not run.
 */

import { Packet } from "../../packet.ts";

export default function GL_CHANGECHANNEL_ACK(op: number, status: 0 = 0): Packet {
  if (status !== 0) {
    throw new RangeError("371 status != 0 requires a multi-channel endpoint this server does not provide");
  }
  return new Packet(op).u8(status);
}
