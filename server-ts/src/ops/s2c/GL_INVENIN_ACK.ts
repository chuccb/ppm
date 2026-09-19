/**
 * 254 -> 255 local-user NewSkill profile snapshot (consumer
 * `sub_574270`, re-read line level 2026-09-19).
 *
 * Accepted modes are only 0/1 — any other first byte is silently
 * dropped before a single further read.
 *
 * Mode 1 (self, the one this server emits): after
 * `{u8 mode, s32 uid, u8 context(n16), u8 v11}` the client checks
 * `dword_EE8CB4 == uid` (the local-identity global); on match it
 * zero-fills a 5x8-dword struct array, reads the `u8 selectedProfile`
 * (native cap: value must be < 5 or hydration is skipped ENTIRELY),
 * then reads the five profiles as ONE raw 0xA0-byte blob
 * (5 x 32 bytes) and hands it to `sub_4BDD80`.
 *
 * Profile record = 8 dwords (32B). Before the blob read the client
 * pre-zeroes dword index 7 of every record — the slot this server's
 * projection fills with `expiresAtPackedMinute`, so 0 there is the
 * honest "no expiry" and the seven leading dwords are the puzzle ids.
 *
 * Mode 0 (remote preview): re-reads the triple `{u8 v11, u8 n16,
 * s32 uid}` for the TARGET player and resolves n16 through
 * `sub_67D870` (negative result = top bit set => skip; otherwise
 * routes to `sub_47B040`/`sub_4345C0` depending on the n2_0==3 club
 * gate). The native 254 request is always the local path, so this
 * server emits the mode-1 snapshot only.
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
