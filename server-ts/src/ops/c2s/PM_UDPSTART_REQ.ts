/**
 * The client's answer to the channel greeting: a handoff claim from login.
 *
 * `str identity, s32 n100, u8 1, s32 extCount` (`sub_555C60`). The
 * middle byte is a hardcoded literal 1, and the two integers are echoed back
 * from the login reply.
 *
 * `identity` comes from a native `String[24]`, so at most 23 ANSI bytes. Its
 * writer has not been located, so it is **not** treated as an account or
 * nickname key. The handoff is accepted only when the shared, recent login
 * admission matches the source IP and echoed values. (docs/PACKETS.md §3.15d)
 */

import type { Reader } from "../../packet.ts";
import type { Connection } from "../../connection.ts";
import { Result } from "../s2c/PM_UDPSTART_ACK.ts";

/** Native `String[24]`, so 23 bytes plus the NUL. */
export const IDENTITY_MAX_BYTES = 23;

export interface Handoff {
  readonly identity: string;
  /** Echoed from the login reply; opaque billing/charge UI mode. */
  readonly n100: number;
  readonly extCount: number;
}

export function read(r: Reader): Handoff {
  const identity = r.str("euc-kr", IDENTITY_MAX_BYTES);
  const n100 = r.s32();
  const literal = r.u8();
  const extCount = r.s32();

  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes`);
  if (literal !== 1) throw new RangeError(`expected the literal 1, got ${literal}`);

  return { identity, n100, extCount };
}

export default function PM_UDPSTART_REQ(r: Reader, connection: Connection): void {
  if (connection.authenticated) {
    connection.reply("PM_UDPSTART_ACK", {
      result: Result.AlreadyConnected,
      channelName: connection.config.channelName,
    });
    return;
  }

  let handoff: Handoff;
  try {
    handoff = read(r);
  } catch (error) {
    connection.log(`malformed channel handoff — ${errorMessage(error)}`);
    connection.reply("PM_UDPSTART_ACK", {
      result: Result.UnauthorisedId,
      channelName: connection.config.channelName,
    });
    return;
  }

  const admission = connection.config.admissions.claim(
    handoff.n100,
    handoff.extCount,
    connection.remoteIp,
  );
  if (!admission) {
    connection.log(`channel handoff ${JSON.stringify(handoff.identity)} -> rejected`);
    connection.reply("PM_UDPSTART_ACK", {
      result: Result.UnauthorisedId,
      channelName: connection.config.channelName,
    });
    return;
  }

  connection.bindAccount(admission.accountId);
  connection.log(`channel handoff ${JSON.stringify(handoff.identity)} -> account ${admission.accountId}`);
  connection.reply("PM_UDPSTART_ACK", {
    result: Result.Success,
    channelName: connection.config.channelName,
  });
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
