/**
 * 689 stores one tutorial step (builder sub_55C7D0: `s32 tutorialIndex`
 * via the 4-byte write accessor sub_592A20).
 *
 * Native dispatcher has NO `case 690`, and the docs' old
 * `sub_582530` anchor does not exist in this dump either, so the client
 * never parses a 690 answer: the TS handler validates the frame and
 * deliberately stays silent.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_TUTORIAL_INDEX_SET_REQ(r: Reader, _connection: Connection): void {
  const tutorialIndex = r.s32();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 689`);
  void tutorialIndex; // parse-only: no native ACK exists
}
