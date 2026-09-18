/**
 * 421 -> 422 delete-one-message result (sub_55A310 consumer).
 *
 * Wire: `{u8 statusRaw, str key}`. When the status byte is nonzero the
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
    .u8(statusRaw)
    .str(key); // sub_5378C0 stride-20 mailbox-key slot
}
