/**
 * 370 -> 371 GL_CHANGECHANNEL_ACK (consumer sub_570100).
 *
 * Wire: `u8 status`; under status == 1 the client additionally reads
 * `u8 channel_id, str host_ip, s32 host_port, u8 extra` and hands the
 * endpoint to sub_596E60. status 0/2/3 are failure banners, >= 4 is a
 * silent reset.
 *
 * This deployment exposes a single channel, so the default frame is the
 * documented failure banner status = 0 (wire "00"); the success arm is
 * fully shaped here for a future multi-channel store.
 * 2026-09-19 行級加錨：status==1 時 client 重寫與 142/196 **同一組** control endpoint
 * (channel slot 417D00()[0] / opaque byte 1D0CFE4 / sockaddr 1326908 共管三件),
 * 並先關舊裝置 (n15==9/8 → vtable+120/124)。
 */

import { Packet } from "../../packet.ts";

/** Success-arm endpoint the client would switch to (status == 1 only). */
export interface ChannelTarget {
  readonly channelId: number;
  readonly hostIp: string;
  readonly hostPort: number;
  /** Trailing status byte read after the port; domain unresolved, 0 is the inert value. */
  readonly extra?: number;
}

export default function GL_CHANGECHANNEL_ACK(
  op: number,
  status = 0,
  target?: ChannelTarget,
): Packet {
  const p = new Packet(op).u8(status);
  if (status === 1) {
    if (!target) {
      throw new RangeError("status 1 needs the channel endpoint (`target`) — native reads it unconditionally");
    }
    p.u8(target.channelId)
      .str(target.hostIp)
      .s32(target.hostPort)
      .u8(target.extra ?? 0);
  }
  return p;
}
