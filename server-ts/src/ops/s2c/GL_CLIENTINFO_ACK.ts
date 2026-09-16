/**
 * 246 -> 247 public player profile.
 *
 * 247 shares the 198 basic-data block, then carries only one character
 * appearance record instead of the complete private loadout projection.
 */

import { Packet } from "../../packet.ts";
import type { PlayerInfo } from "../../store.ts";
import { writeAppearance, writeMyInfoCore } from "./GL_MYINFO_ACK.ts";

export default function GL_CLIENTINFO_ACK(op: number, player: PlayerInfo | null): Packet {
  if (!player) return new Packet(op).u8(0);

  const character = player.characters[player.currentCharacter] ?? player.characters[0];
  const p = new Packet(op).u8(1);
  writeMyInfoCore(p, player);
  p.u8(character?.slot ?? 0).u8(character?.type ?? 1);
  writeAppearance(p, character?.appearance ?? []);
  return p;
}
