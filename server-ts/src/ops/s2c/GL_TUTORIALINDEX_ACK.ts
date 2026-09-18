/**
 * 686 GL_TUTORIALINDEX_ACK (consumer sub_55C790 -> sub_4422B0 on the
 * tutorial controller dword_E9FE70).
 *
 * Audit 2026-09-19: sub_4422B0 compares the stored marker against the
 * literal 145 — equality HIDES the lobby "TUTO_NEW" badge, anything
 * else SHOWS it. The client-side global boots as 255; the server
 * packet overwrites it. This server keeps every account at the fresh
 * state 0, so the new-player badge stays visible; a completed marker
 * is available for a future store-backed tutorial progress model.
 */

import { Packet } from "../../packet.ts";

/** sub_4422B0 sentinel: the tutorial is done, hide the TUTO_NEW badge. */
export const TUTORIAL_COMPLETED = 145;

export default function GL_TUTORIALINDEX_ACK(op: number, tutorialIndex: number): Packet {
  return new Packet(op).s32(tutorialIndex);
}
