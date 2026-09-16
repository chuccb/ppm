/**
 * 246 -> 247 public MyInfo profile.
 *
 * 247 shares the 198 basic-data block, then carries only one character
 * appearance record instead of the complete private loadout projection.
 */

import { Packet } from "../../packet.ts";
import type { MyInfo } from "../../store.ts";
import { writeCharacterAppearance, writeMyInfoBasicData } from "./GL_MYINFO_ACK.ts";

export default function GL_CLIENTINFO_ACK(op: number, myInfo: MyInfo | null): Packet {
  // Native 247 success unconditionally consumes one character record. Do not
  // fabricate a type-1 record when the target has no character; use its one-
  // byte failure projection instead.
  if (!myInfo || myInfo.characters.length === 0) return new Packet(op).u8(0);

  // 247's first byte is the serialized character-list index, not the
  // persistent slot id. The native 198/247 basic block carries the same index.
  const characterIndex = myInfo.selectedCharIndex;
  const character = myInfo.characters[characterIndex] ?? myInfo.characters[0]!;
  const p = new Packet(op).u8(1);
  writeMyInfoBasicData(p, myInfo);
  p.u8(characterIndex).u8(character.charType);
  writeCharacterAppearance(p, character.equip);
  return p;
}
