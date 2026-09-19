/**
 * 1002 -> 1003 GL_BLOCKME_LIST_ACK (sub_567BD0 consumer).
 *
 * Wire: `{u16 headerRaw, str headerStr, s32 count, count×str nickname}`.
 *
 * Same read-but-unused header pair as 1001 (`sub_592A00(&v20)` /
 * `sub_592730(&v12)` fill locals that have no consumers). After the header
 * the client clears the blocked-by table (`sub_53A6E0(&unk_EE8C9C)`) and then
 * inserts every nickname via `sub_539360(CMyData, &Source)`, so the snapshot
 * is always a full replacement.
 *
 * Direction semantics (established in the LAYOUTS 黑單家族 rows): MY list is
 * the 1000 -> 1001 pair (with per-entry flags); the "who has blocked me"
 * view is this 1002 -> 1003 pair (nicknames only).
 */

import { Packet } from "../../packet.ts";

/** The client's read-but-unused header pair (same protocol shape as 1001). */
export const BLOCKME_LIST_ACK_HEADER_U16 = 0;
export const BLOCKME_LIST_ACK_HEADER_STRING = "";

export default function GL_BLOCKME_LIST_ACK(op: number, nicknames: readonly string[]): Packet {
  const packet = new Packet(op)
    .u16(BLOCKME_LIST_ACK_HEADER_U16)
    .str(BLOCKME_LIST_ACK_HEADER_STRING)
    .s32(nicknames.length);
  for (const nickname of nicknames) packet.str(nickname);
  return packet;
}
