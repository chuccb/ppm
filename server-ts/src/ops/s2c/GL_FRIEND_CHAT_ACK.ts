/**
 * 439 -> 440 friend-whisper result (sub_55B660 consumer).
 *
 * Wire (native Fact): `{u8 statusRaw, str nick1, str nick2}` and only
 * when `statusRaw == 2` an extra `str comment`. Reader locals are 24B,
 * 24B and 200B respectively.
 *
 * Display branches: 0 -> resource `0x1EF` (character name does not
 * exist), 1 -> `0x1F0` (offline), 2 -> `0x1D9` (the whisper display
 * path that also reads the comment), 3 -> `0x1D8` (could not find).
 * The wire nicks only advance the cursor in every branch — rendering
 * uses the global `sub_401B20` state — so extra string semantics beyond
 * "status 2 carries the comment" are not invented.
 */

import { Packet } from "../../packet.ts";

export default function GL_FRIEND_CHAT_ACK(
  op: number,
  statusRaw: number,
  nick1: string,
  nick2: string,
  comment?: string,
): Packet {
  const p = new Packet(op)
    .u8(statusRaw)
    .strMax(nick1, 23) // native v19[6] 24-byte read local
    .strMax(nick2, 23); // native v17[6] 24-byte read local
  if (statusRaw === 2) {
    p.strMax(comment ?? "", 199); // native v20[200] comment read local
  }
  return p;
}
