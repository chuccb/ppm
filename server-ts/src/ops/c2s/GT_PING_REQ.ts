/**
 * The client's answer to the heartbeat. Proof of life, nothing more.
 *
 * Liveness is already recorded when the bytes arrive, so there is nothing to
 * do here. Crucially, do **not** reply: the client builds this packet in
 * response to the heartbeat, so answering it would loop forever.
 */

import type { Reader } from "../../packet.ts";

export default function GT_PING_REQ(r: Reader): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 101`);
  // Intentionally no reply — see above.
}
