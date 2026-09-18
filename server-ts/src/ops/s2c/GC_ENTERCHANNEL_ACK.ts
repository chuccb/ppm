/**
 * Channel selection result, read by `CLobbyChannel` rather than the main
 * dispatcher (`sub_4179D0`). The endpoint tail exists only for result 1.
 *
 * The endpoint is the private UDP control address established by the native
 * client after 196. This module only writes its source-proven wire shape; it
 * does not assign a meaning to the opaque byte, flags, or final default byte.
 * Type 3 enters the recovered `sub_875680` continuation (header0-only gate
 * arm included). (docs/PACKETS.md §3.15d5; docs/S2C_NATIVE_AUDITS.md)
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
  /** Native wire field is a complete s32; endpoint policy is external. */
  readonly port: number;
}

export interface FailureEntry {
  readonly result: FailureResult;
  readonly channelId: number;
  readonly channelIndex: number;
}

export interface Type3SmallRecord {
  readonly u8_0: number;
  readonly u8_1: number;
  readonly u8_2: number;
  readonly u8_3: number;
  readonly s32_0: number;
  readonly s32_1: number;
  readonly raw4_0: number;
  readonly raw4_1: number;
  readonly u8_4: number;
}

export interface Type3StageRecord {
  readonly s32_0: number;
  readonly raw4_0: number;
  readonly hasName0: number;
  /** Written only when hasName0 != 0; at most 31 bytes (native 32-byte copy). */
  readonly name0: string;
  readonly s32_1: number;
  readonly s32_2: number;
  readonly s32_3: number;
  readonly s32_4: number;
  readonly s32_5: number;
  readonly s32_6: number;
  readonly hasName1: number;
  /** Written only when hasName1 != 0; at most 25 bytes (native 26-byte copy). */
  readonly name1: string;
}

/**
 * Raw projection of every field sub_875680 consumes after channel_type=3.
 * Names intentionally describe only width/order; native consumers do not
 * provide a trustworthy business schema for this continuation.
 */
/** The native continuation may end after header0 when header0 <= 0. */
export interface Type3TailGate {
  readonly header0: number;
}

export interface Type3TailFull {
  readonly header0: number;
  readonly header1: number;
  readonly name: string;
  readonly raw4_0: number;
  readonly raw4_1: number;
  readonly raw4_2: number;
  readonly raw4_3: number;
  readonly u8_0: number;
  readonly u8_1: number;
  readonly u8_2: number;
  readonly listCount: number;
  readonly listValues: readonly number[];
  readonly smallRecordCount: number;
  readonly smallRecordMode: number;
  readonly smallRecords: readonly Type3SmallRecord[];
  readonly u8_3: number;
  readonly stageCount: number;
  readonly stageRecords: readonly Type3StageRecord[];
  readonly raw4Final: number;
}

export type Type3Tail = Type3TailGate | Type3TailFull;

export interface SuccessEntry {
  readonly result: typeof Result.Success;
  readonly channelId: number;
  readonly channelIndex: number;
  readonly endpoint: Endpoint;
  readonly endpointOpaque?: number;
  readonly channelType?: number;
  readonly clientFlags?: number;
  readonly clientDefault?: number;
  readonly type3Tail?: Type3Tail;
}

export type Entry = FailureEntry | SuccessEntry;

function isFullType3Tail(tail: Type3Tail): tail is Type3TailFull {
  return "header1" in tail;
}

function writeType3Tail(p: Packet, tail: Type3Tail): void {
  p.s32(tail.header0);
  if (!isFullType3Tail(tail)) return; // native allows the header0-only gate arm
  p.s32(tail.header1)
    .str(tail.name) // native 68-byte storage
    .u32(tail.raw4_0).u32(tail.raw4_1).u32(tail.raw4_2).u32(tail.raw4_3)
    .u8(tail.u8_0).u8(tail.u8_1).u8(tail.u8_2).s32(tail.listCount);
  for (const value of tail.listValues) p.s32(value);
  p.u8(tail.smallRecordCount).u8(tail.smallRecordMode);
  for (const record of tail.smallRecords) {
    p.u8(record.u8_0).u8(record.u8_1).u8(record.u8_2).u8(record.u8_3)
      .s32(record.s32_0).s32(record.s32_1).u32(record.raw4_0).u32(record.raw4_1).u8(record.u8_4);
  }
  p.u8(tail.u8_3).u8(tail.stageCount);
  for (const record of tail.stageRecords) {
    p.s32(record.s32_0).u32(record.raw4_0).u8(record.hasName0);
    if (record.hasName0 !== 0) p.str(record.name0); // native 32-byte copy
    p.s32(record.s32_1).s32(record.s32_2).s32(record.s32_3).s32(record.s32_4)
      .s32(record.s32_5).s32(record.s32_6).u8(record.hasName1);
    if (record.hasName1 !== 0) p.str(record.name1); // native 26-byte copy
  }
  p.u32(tail.raw4Final);
}

export default function GC_ENTERCHANNEL_ACK(op: number, entry: Entry): Packet {
  const p = new Packet(op)
    .u8(entry.result)
    .s32(entry.channelId)
    .u8(entry.channelIndex);

  if (entry.result !== Result.Success) return p;

  const successEntry = entry as SuccessEntry;
  const endpointOpaque = successEntry.endpointOpaque ?? 0;
  const channelType = successEntry.channelType ?? 0;
  const clientFlags = successEntry.clientFlags ?? 0;
  const clientDefault = successEntry.clientDefault ?? 5;

  p.str(successEntry.endpoint.host) // native char[20]
    .s32(successEntry.endpoint.port)
    .u8(endpointOpaque)
    .u8(channelType)
    .u32(clientFlags) // native raw4
    .u8(clientDefault);
  if (channelType === 3 && successEntry.type3Tail !== undefined) {
    writeType3Tail(p, successEntry.type3Tail);
  }
  return p;
}
