/**
 * The one-time handoff from a successful login TCP connection to a new
 * channel TCP connection.
 *
 * The native 143 request contains a String[24], but its writer is still
 * unresolved. It is therefore deliberately absent from the lookup key. The
 * only safe key available here is the source IP plus the two values echoed
 * from 681. A match is consumed exactly once; an ambiguous match is rejected
 * instead of guessing which account the client meant.
 */

export interface ChannelAdmission {
  readonly accountId: number;
  readonly n100: number;
  readonly extensionCount: number;
  readonly remoteIp: string;
  readonly expiresAt: number;
}

export class ChannelAdmissionRegistry {
  readonly #byAccount = new Map<number, ChannelAdmission>();

  issue(
    accountId: number,
    n100: number,
    extensionCount: number,
    remoteIp: string,
    lifetimeMs: number,
    now = Date.now(),
  ): void {
    if (!Number.isSafeInteger(accountId) || accountId <= 0) {
      throw new RangeError("accountId must be a positive safe integer");
    }
    if (!Number.isInteger(n100) || n100 < -128 || n100 > 127) {
      throw new RangeError("n100 must fit the signed byte used by 143");
    }
    if (!Number.isSafeInteger(extensionCount) || extensionCount < 0) {
      throw new RangeError("extensionCount must be a non-negative safe integer");
    }
    if (!Number.isSafeInteger(lifetimeMs) || lifetimeMs <= 0) {
      throw new RangeError("admission lifetime must be a positive safe integer");
    }
    if (remoteIp.length === 0) throw new RangeError("remoteIp must not be empty");

    this.prune(now);
    this.#byAccount.set(accountId, {
      accountId,
      n100,
      extensionCount,
      remoteIp,
      expiresAt: now + lifetimeMs,
    });
  }

  /**
   * Atomically claim the only unexpired admission with these echoed values.
   * `null` means no match or more than one match.
   */
  claim(
    n100: number,
    extensionCount: number,
    remoteIp: string,
    now = Date.now(),
  ): ChannelAdmission | null {
    this.prune(now);

    let match: ChannelAdmission | undefined;
    for (const admission of this.#byAccount.values()) {
      if (
        admission.n100 !== n100 ||
        admission.extensionCount !== extensionCount ||
        admission.remoteIp !== remoteIp
      ) {
        continue;
      }
      if (match) return null; // The native request has no proven account key.
      match = admission;
    }

    if (!match) return null;
    this.#byAccount.delete(match.accountId);
    return match;
  }

  prune(now = Date.now()): void {
    for (const [accountId, admission] of this.#byAccount) {
      if (admission.expiresAt <= now) this.#byAccount.delete(accountId);
    }
  }
}
