/**
 * Channel selection result, read by `CLobbyChannel` rather than the main
 * dispatcher (`sub_4179D0`). The endpoint tail exists only for result 1.
 *
 * The endpoint is the private UDP control address established by the native
 * client after 196. This module only writes its source-proven wire shape; it
 * does not assign a meaning to the opaque byte, flags, or final default byte.
 * (docs/PACKETS.md §3.15d5)
 */

import { Packet } from "../../packet.ts";

export const Result = {
  ChannelFull: 0,
  Success: 1,
  RankRestricted: 2,
  ClanRequired: 3,
  GenericError4: 4,
  GenericError5: 5,
  Error6: 6,
  GenericError7: 7,
  Error8: 8,
  GenericError9: 9,
} as const;

export type Result = (typeof Result)[keyof typeof Result];

export interface Endpoint {
  host: string;
  port: number;
}

export interface Entry {
  result: Result;
  channelId: number;
  channelIndex: number;
  endpoint?: Endpoint;
  endpointOpaqueByte?: number;
  channelType?: number;
  clientFlags?: number;
  clientDefaultValue?: number;
}

export default function GC_ENTERCHANNEL_ACK(op: number, entry: Entry): Packet {
  const p = new Packet(op)
    .u8(entry.result)
    .s32(entry.channelId)
    .u8(entry.channelIndex);

  if (entry.result !== Result.Success) {
    if (entry.endpoint !== undefined) {
      throw new RangeError("a non-success 196 cannot carry an endpoint tail");
    }
    return p;
  }

  const endpoint = entry.endpoint;
  if (!endpoint) throw new RangeError("a successful 196 requires an endpoint tail");
  if (endpoint.host.length === 0 || endpoint.host.length > 19) {
    throw new RangeError("196 endpoint host must fit the native char[20]");
  }
  if (endpoint.port < 1 || endpoint.port > 0xffff) {
    throw new RangeError("196 endpoint port must fit an unsigned 16-bit value");
  }

  return p
    .str(endpoint.host)
    .s32(endpoint.port)
    .u8(entry.endpointOpaqueByte ?? 0)
    .u8(entry.channelType ?? 0)
    .u32(entry.clientFlags ?? 0)
    .u8(entry.clientDefaultValue ?? 5);
}
