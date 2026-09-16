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
  if (!myInfo) return new Packet(op).u8(0);

  const character = myInfo.characters.find(
    ({ slotNo }) => slotNo === myInfo.currentChar,
  ) ?? myInfo.characters[0];
  const p = new Packet(op).u8(1);
  writeMyInfoBasicData(p, myInfo);
  p.u8(character?.slotNo ?? 0).u8(character?.charType ?? 1);
  writeCharacterAppearance(p, character?.equip ?? []);
  return p;
}
