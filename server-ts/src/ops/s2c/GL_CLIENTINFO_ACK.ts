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
  // Do not fall back to another character: that would pair a valid index with
  // the wrong appearance and hide an unresolved Store-slot mapping.
  const characterIndex = myInfo.selectedCharIndex;
  if (!Number.isSafeInteger(characterIndex) || characterIndex < 0 || characterIndex >= 20) {
    return new Packet(op).u8(0);
  }
  const character = myInfo.characters[characterIndex];
  if (!character) return new Packet(op).u8(0);
  const p = new Packet(op).u8(1);
  writeMyInfoBasicData(p, myInfo);
  p.u8(characterIndex).label("247 char_type expected").u8(character.charType);
  writeCharacterAppearance(p, character.appearance);
  return p;
}
