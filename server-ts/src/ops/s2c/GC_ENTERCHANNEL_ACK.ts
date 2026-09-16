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

/** Native result is an opaque u8 at the wire boundary; constants cover known UI branches. */
export type Result = number;
type FailureResult = number;

export interface Endpoint {
  readonly host: string;
  readonly port: number;
}

export interface FailureEntry {
  readonly result: FailureResult;
  readonly channelId: number;
  readonly channelIndex: number;
}

export interface SuccessEntry {
  readonly result: typeof Result.Success;
  readonly channelId: number;
  readonly channelIndex: number;
  readonly endpoint: Endpoint;
  readonly endpointOpaque?: number;
  readonly channelType?: number;
  readonly clientFlags?: number;
  readonly clientDefault?: number;
}

export type Entry = FailureEntry | SuccessEntry;

export default function GC_ENTERCHANNEL_ACK(op: number, entry: Entry): Packet {
  if (!Number.isSafeInteger(entry.result) || entry.result < 0 || entry.result > 0xff) {
    throw new RangeError("196 result must fit u8");
  }

  const p = new Packet(op)
    .u8(entry.result)
    .s32(entry.channelId)
    .u8(entry.channelIndex);

  if (entry.result !== Result.Success || !("endpoint" in entry)) return p;
  if ((entry.channelType ?? 0) === 3) {
    throw new RangeError("196 channel type 3 requires the unrecovered AI tail");
  }

  if (entry.endpoint.host.length === 0 || entry.endpoint.host.length > 19) {
    throw new RangeError("196 endpoint host must fit the native char[20]");
  }
  if (entry.endpoint.port < 1 || entry.endpoint.port > 0xffff) {
    throw new RangeError("196 endpoint port must fit an unsigned 16-bit value");
  }

  return p
    .str(entry.endpoint.host)
    .s32(entry.endpoint.port)
    .u8(entry.endpointOpaque ?? 0)
    .u8(entry.channelType ?? 0)
    .u32(entry.clientFlags ?? 0)
    .u8(entry.clientDefault ?? 5);
}
