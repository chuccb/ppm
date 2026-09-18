/**
 * 685 -> 686 tutorial index (sub_55C790 consumer).
 *
 * Wire: exactly `s32 tutorialIndex`; the client stores it into the
 * global tutorial slot (n145_0) and refreshes the tutorial UI. With no
 * per-account progression model this server only ever emits 0 — the
 * proven blank-board value.
 */

import { Packet } from "../../packet.ts";

export default function GL_TUTORIALINDEX_ACK(op: number, tutorialIndex: number): Packet {
  return new Packet(op).s32(tutorialIndex);
}
