/**
 * 423 -> 424 mark-one-message-read result (sub_55A4F0 consumer).
 *
 * Wire: `{u8 statusRaw, str key}`. When the status byte is nonzero the
 * client writes the read-state marker `89` for that key
 * (`sub_537D20`); when zero it shows its own resource dialog (`0x1E4`).
 * The complete status enum is not recovered — this module only proves
 * the zero/nonzero split, never inventing codes.
 */

import { Packet } from "../../packet.ts";
import { MSG_KEY_MAX_BYTES } from "./GL_MSG_RECVLIST_ACK.ts";

export default function GL_MSG_READ_ACK(op: number, statusRaw: number, key: string): Packet {
  return new Packet(op)
    .u8(statusRaw)
    .strMax(key, MSG_KEY_MAX_BYTES); // native char[20] mailbox-key slot
}
