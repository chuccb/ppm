/**
 * 370 -> 371 GL_CHANGECHANNEL_ACK (consumer sub_570100).
 *
 * Wire: `u8 status`; under status == 1 the client additionally reads
 * `u8 channel_id, str host_ip, s32 host_port, u8 extra` and hands the
 * endpoint to sub_596E60. status 0/2/3 are failure banners, >= 4 is a
 * silent reset. This deployment exposes a single channel, so this
 * server always emits status = 0 (wire "00").
 */

import { Packet } from "../../packet.ts";

export default function GL_CHANGECHANNEL_ACK(op: number): Packet {
  return new Packet(op).u8(0);
}
