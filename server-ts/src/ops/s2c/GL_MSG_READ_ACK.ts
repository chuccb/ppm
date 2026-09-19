/**
 * 423 -> 424 mark-one-message-read result (sub_55A4F0 consumer).
 *
 * Wire: `{s8/bool statusRaw, str key}`. The consumer's first read
 * is `sub_592900` (s8/bool), not the u8 pair — line-level in
 * PaperMan.exe.c. When the status byte is nonzero the
 * client writes the read-state marker `89` for that key
 * (`sub_537D20`); when zero it shows its own resource dialog (`0x1E4`).
 * The complete status enum is not recovered — this module only proves
 * the zero/nonzero split, never inventing codes.
 */

import { Packet } from "../../packet.ts";

export default function GL_MSG_READ_ACK(op: number, statusRaw: number, key: string): Packet {
  return new Packet(op)
    .s8(statusRaw) // sub_592900 = s8/bool status read
    .str(key); // sub_5378C0 stride-20 mailbox-key slot
}
