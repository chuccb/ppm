/**
 * 718 -> 719 GR_START_VOTING_ACK (shared dispatcher case 719 in
 * IVotingNetwork::sub_9BF430; single `u8 status` byte forwarded to the
 * initiator callback with no arm split).
 *
 * This server never starts votes, so status is always 0.
 */

import { Packet } from "../../packet.ts";

export default function GR_START_VOTING_ACK(op: number): Packet {
  return new Packet(op).u8(0);
}
