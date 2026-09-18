/**
 * Channel admission result.
 *
 * The client reads every field before it branches on `result` (`sub_555D50`),
 * so all of them must be present even on failure. (docs/PACKETS.md §3.15d)
 *
 * Note the client's second-level handler ignores `result` entirely and sends
 * the enter-channel request regardless, so rejecting here is not enough on its
 * own — the follow-up must also be refused.
 */

import { Packet } from "../../packet.ts";

export const Result = {
  Success: 1,
  /** Alternate success mode. */
  SuccessAlternate: 2,
  VersionMismatch: 3,
  AlreadyConnected: 4,
  UnauthorisedId: 5,
  ChannelLevelTooLow: 6,
  ChannelKdrTooLow: 7,
  LightServerRestricted: 8,
  BeginnerServerRestricted: 9,
  IntermediateServerRestricted: 10,
} as const;

/** Native result is an opaque u8 at the wire boundary; constants above cover known UI branches. */
export type Result = number;

export interface Admission {
  readonly result: Result;
  /** Native local char[40]; at most 39 ASCII bytes. The reader does not recover a semantic consumer for this string. */
  readonly channelName: string;
  /** With `rank > 10` the client refuses the server. */
  readonly rankRestricted?: boolean;
  /** Shown as "today's login confirmed, %d PG awarded" when positive. */
  readonly dailyLoginRewardPg?: number;
  /** The `%d` in the level-restriction messages. */
  readonly restrictionLevel?: number;
  /** The `%.1f` in the K/D-restriction messages. */
  readonly restrictionKdr?: number;
}

export default function PM_UDPSTART_ACK(op: number, admission: Admission): Packet {
  const {
    result,
    channelName,
    rankRestricted = false,
    dailyLoginRewardPg = 0,
    restrictionLevel = 0,
    restrictionKdr = 0,
  } = admission;

  return new Packet(op)
    .u8(result)
    .u8(rankRestricted ? 1 : 0)
    .s32(dailyLoginRewardPg)
    .str(channelName) // native v71 local char[40]
    .s32(0) // read then unused
    .s32(0) // read then unused
    .s32(restrictionLevel)
    .f32(restrictionKdr)
    .u32(0) // client_request_context: echoed into later requests, meaning unproven
    .u8(0); // has_net_cafe_info: 0 = omit the trailing block
}
