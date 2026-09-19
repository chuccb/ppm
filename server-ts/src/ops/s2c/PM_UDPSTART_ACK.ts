/**
 * Channel admission result.
 *
 * The client reads every field before it branches on `result` (`sub_555D50`),
 * so all of them must be present even on failure. (docs/PACKETS.md §3.15d;
 * full-body re-read 2026-09-19: v72/v68 stored-only, restriction `%d`/`%.1f`
 * sites at result 6..10, net-cafe tail = {4 x u8, 8 x s32}.)
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
    // v72/v68: line-proven stored-only in sub_555D50 (read into locals,
    // never referenced afterwards) — zero is the honest value.
    .s32(0)
    .s32(0)
    .s32(restrictionLevel)
    .f32(restrictionKdr)
    // Native reads this word via sub_592AC0 and stores it into the
    // session-context global dword_F2A684 (sole write-site), which the
    // 834/119/125/419/439 request builders echo back verbatim
    // (sub_592AA0 writes; line-level 2026-09-19). It is a server-owned
    // session correlation token: this server correlates nothing, so
    // 0 = no token is the honest emission.
    .u32(0)
    // has_net_cafe_info: non-zero makes the client read the trailing
    // block {4 x u8, 8 x s32} into sub_A1C800 — not emittable without a
    // net-cafe model, so 0.
    .u8(0);
}
