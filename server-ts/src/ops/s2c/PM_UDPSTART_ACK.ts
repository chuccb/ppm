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
  /** Native char[40]; at most 39 ANSI bytes. */
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

  if (!Number.isSafeInteger(result) || result < 0 || result > 0xff) {
    throw new RangeError("144 result must fit u8");
  }
  if (typeof channelName !== "string") {
    throw new TypeError("144 channel_name must be a string");
  }
  if (typeof rankRestricted !== "boolean") {
    throw new TypeError("144 rank_restricted_server_flag must be boolean");
  }
  if (channelName.length > CHANNEL_NAME_MAX_BYTES) {
    throw new RangeError(`channel name longer than ${CHANNEL_NAME_MAX_BYTES} bytes`);
  }
  if (!Number.isSafeInteger(dailyLoginRewardPg) || dailyLoginRewardPg < -0x8000_0000 || dailyLoginRewardPg > 0x7fff_ffff) {
    throw new RangeError("144 daily_login_reward_pg must fit s32");
  }
  if (!Number.isSafeInteger(restrictionLevel) || restrictionLevel < -0x8000_0000 || restrictionLevel > 0x7fff_ffff) {
    throw new RangeError("144 channel_restriction_level must fit s32");
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
