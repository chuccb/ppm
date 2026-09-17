/**
 * Claims the short-lived login admission on the second TCP connection.
 *
 * Native sends `str identity, s32 n100, u8 1, s32 extCount`. The identity
 * writer is unresolved, so it is logged but never used as an account key.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";
import { Result } from "../s2c/PM_UDPSTART_ACK.ts";

export const IDENTITY_MAX_BYTES = 23;

export interface Handoff {
  readonly identity: string;
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

function denied(connection: Connection): void {
  connection.reply("PM_UDPSTART_ACK", {
    result: Result.UnauthorisedId,
    channelName: connection.config.channel.name,
  });
}

export default function PM_UDPSTART_REQ(r: Reader, connection: Connection): void {
  if (connection.authenticated) {
    connection.reply("PM_UDPSTART_ACK", {
      result: Result.AlreadyConnected,
      channelName: connection.config.channel.name,
    });
    return;
  }

  let handoff: Handoff;
  try {
    handoff = read(r);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    connection.log(`malformed channel handoff — ${message}`);
    denied(connection);
    return;
  }

  const admission = connection.config.admissions.claim(
    handoff.n100,
    handoff.extCount,
    connection.remoteIp,
  );
  if (!admission) {
    connection.log(`channel handoff ${JSON.stringify(handoff.identity)} -> rejected`);
    denied(connection);
    return;
  }

  connection.bindAccount(admission.accountId);
  connection.log(`channel handoff ${JSON.stringify(handoff.identity)} -> account ${admission.accountId}`);
  connection.reply("PM_UDPSTART_ACK", {
    result: Result.Success,
    channelName: connection.config.channel.name,
  });
}
