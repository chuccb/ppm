/**
 * 254 -> 255 local-user NewSkill profile snapshot (consumer
 * `sub_574270`; branch facts in docs/PACKETS.md around §1660).
 *
 * The client has another mode-0 branch for remote-user preview: after the
 * common mode/uid/context/unknown prefix it reads two more u8 values and one
 * s32 lookup value. 254's native request is always the local inventory-enter
 * path, so this server emits the proven mode-1 self snapshot only.
 */

import { Packet } from "../../packet.ts";
import { type NewSkillProfileSnapshot } from "../../store.ts";

export default function GL_INVENIN_ACK(
  op: number,
  uid: number,
  contextRaw: number,
  snapshot: NewSkillProfileSnapshot,
): Packet {
  const p = new Packet(op)
    .u8(1) // mode 1: local user snapshot
    .s32(uid)
    .u8(contextRaw)
    .u8(0) // unknownHeaderRaw (v11): audited — parked under mode 1, only the room-relay (mode 0) arm consumes it
    .u8(snapshot.selectedProfile);

  for (const profile of snapshot.profiles) {
    for (const itemId of profile.puzzleItemIds) {
      p.s32(itemId);
    }
    p.s32(profile.expiresAtPackedMinute);
  }
  return p;
}
