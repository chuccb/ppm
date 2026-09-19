/**
 * 487 -> 488 GL_MYROOMCHANGE_ACK (sub_5861C0 consumer).
 *
 * Wire: `u8 status, [u8 slot if status == 1]`. The client has exactly three
 * arms (line-read):
 *   - status 1      -> reads `u8 slot` (defaults to 0) and stores it into
 *                      `*sub_417D00()` (+0 of the CLobbyChannel singleton: the
 *                      selected my-room slot byte);
 *   - status 11     -> no-op (skips the restore arm entirely);
 *   - anything else -> `sub_44C1D0(dword_E9FE70)` restores the MyRoom window
 *                      from `*(this+4252)` (the current room index) via the
 *                      `L"MYROOM_%d"` label, i.e. the change is rejected and
 *                      the UI is rolled back.
 *
 * TS chooses status 0 for the rejection arm: every non-1/non-11 code behaves
 * identically in the client, so 0 is the honest "rejected" value and nothing
 * else is fabricated. Status 11 is never emitted (its only proven behaviour
 * is "leave the UI untouched", which a rejection must not do).
 */

import { Packet } from "../../packet.ts";

/** 488 status arms, named after the proven client behaviours. */
export const MyRoomChangeStatus = {
  /** Accepted: client stores the echoed slot and keeps the UI. */
  Success: 1,
  /** Rejected: client rolls the MyRoom UI back to the current room. */
  Rejected: 0,
} as const;
export type MyRoomChangeStatusValue = typeof MyRoomChangeStatus[keyof typeof MyRoomChangeStatus];

export default function GL_MYROOMCHANGE_ACK(op: number, statusRaw: number, slot: number): Packet {
  const packet = new Packet(op).u8(statusRaw);
  if (statusRaw === MyRoomChangeStatus.Success) packet.u8(slot);
  return packet;
}
