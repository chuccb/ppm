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

export type Result = (typeof Result)[keyof typeof Result];

export interface Admission {
  result: Result;
  /** Native char[40]; at most 39 ANSI bytes. */
  channelName: string;
  /** With `rank > 10` the client refuses the server. */
  rankRestricted?: boolean;
  /** Shown as "today's login confirmed, %d PG awarded" when positive. */
  dailyLoginRewardPg?: number;
  /** The `%d` in the level-restriction messages. */
  restrictionLevel?: number;
  /** The `%.1f` in the K/D-restriction messages. */
  restrictionKdr?: number;
}

/** Native char[40]. */
export const CHANNEL_NAME_MAX_BYTES = 39;

export default function PM_UDPSTART_ACK(op: number, admission: Admission): Packet {
  const {
    result,
    channelName,
    rankRestricted = false,
    dailyLoginRewardPg = 0,
    restrictionLevel = 0,
    restrictionKdr = 0,
  } = admission;

  if (channelName.length > CHANNEL_NAME_MAX_BYTES) {
    throw new RangeError(`channel name longer than ${CHANNEL_NAME_MAX_BYTES} bytes`);
  }

  return new Packet(op)
    .u8(result)
    .u8(rankRestricted ? 1 : 0)
    .s32(dailyLoginRewardPg)
    .str(channelName)
    .s32(0) // read then unused
    .s32(0) // read then unused
    .s32(restrictionLevel)
    .f32(restrictionKdr)
    .u32(0) // client_request_context: echoed into later requests, meaning unproven
    .u8(0); // has_net_cafe_info: 0 = omit the trailing block
}
