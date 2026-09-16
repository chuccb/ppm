/**
 * 250 is a client-local lobby transition notice. The recovered protocol has
 * no 251 consumer, so the server must not reply.
 */

import type { Reader } from "../../packet.ts";

export default function GL_LOBBYIN_REQ(r: Reader): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 250`);
}
