/**
 * 421 -> 422 delete-one-message result (sub_55A310 consumer).
 *
 * Wire: `{s8/bool statusRaw, str key}`. The consumer's first read
 * is `sub_592900` (s8/bool), not the u8 pair — line-level in
 * PaperMan.exe.c. When the status byte is nonzero the
 * client removes the key from its local mail table (`sub_537A80`) and
 * refreshes the delete-path UI; when zero it shows its own resource
 * dialog (`0x1E3`). The complete status enum is not recovered — this
 * module only proves the zero/nonzero split, never inventing codes.
 *
 * The key echo fits the same native char[20] mailbox-key slot as the
 * 426/421 path (19 bytes max + NUL).
 */

import { Packet } from "../../packet.ts";

export default function GL_MSG_DEL_ACK(op: number, statusRaw: number, key: string): Packet {
  return new Packet(op)
    .s8(statusRaw) // sub_592900 = s8/bool status read
    .str(key); // sub_5378C0 stride-20 mailbox-key slot
}
