/**
 * The client's answer to the heartbeat. Proof of life, nothing more.
 *
 * Liveness is already recorded when the bytes arrive, so there is nothing to
 * do here. Crucially, do **not** reply: the client builds this packet in
 * response to the heartbeat, so answering it would loop forever.
 */

export default function (): void {
  // Intentionally empty — see above.
}
