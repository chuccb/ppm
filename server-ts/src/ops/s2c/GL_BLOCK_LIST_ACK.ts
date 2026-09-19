/**
 * 1000 -> 1001 GL_BLOCK_LIST_ACK (sub_567D50 consumer).
 *
 * Wire: `{u16 headerRaw, str headerStr, s32 count, count×{s32 flags, str nickname}}`.
 *
 * The native handler reads the two leading fields and then *never touches
 * them again*: `sub_592A00(&v23)` and `sub_592730(&v14)` write into stack
 * locals with zero consumers before the list refill begins (protocol-header
 * carry-overs). Our honest projection sends the minimal encoding (`0`, `""`)
 * and documents it here instead of fabricating content.
 *
 * Per entry the handler calls `sub_5395E0(CMyData, nick, flags)`, storing the
 * s32 flag word next to each nickname in the MY-blocklist table
 * (`dword_EE8C90` side), then refreshes the list UI (`dword_EA131C` vtable
 * +132 with (rowCount, 50, 1)). Recovery shows the flag space can carry an
 * `0x80000000` mark bit; its renderer-side consumer is not located in this
 * build, so an unmarked entry (`0`) is the honest wire value — deletion
 * policy is enforced by the 998 -> 999 flow itself, which the client does
 * evaluate.
 */

import { Packet } from "../../packet.ts";

/** The client's read-but-unused header pair; keep the wire shape minimal. */
export const BLOCK_LIST_ACK_HEADER_U16 = 0;
export const BLOCK_LIST_ACK_HEADER_STRING = "";

/** Plain list entry: the 0x80000000 mark bit has no proven consumer. */
export const BLOCK_LIST_ENTRY_FLAG_NONE = 0;

export interface BlockListRow {
  flags: number;
  nickname: string;
}

export default function GL_BLOCK_LIST_ACK(op: number, rows: readonly BlockListRow[]): Packet {
  const packet = new Packet(op)
    .u16(BLOCK_LIST_ACK_HEADER_U16)
    .str(BLOCK_LIST_ACK_HEADER_STRING)
    .s32(rows.length);
  for (const row of rows) packet.s32(row.flags).str(row.nickname);
  return packet;
}
