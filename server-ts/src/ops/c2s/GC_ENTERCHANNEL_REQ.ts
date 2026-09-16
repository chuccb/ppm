/**
 * Selects the group and channel advertised by 681.
 *
 * The client sends this after every 144, even when 144 reported failure; an
 * unauthenticated or wrong selection therefore receives an explicit non-
 * success 196 and never gains lobby authority. (`sub_56FF40`, `sub_4179D0`)
 */

import type { Reader } from "../../packet.ts";
import type { Connection } from "../../connection.ts";
import { Result } from "../s2c/GC_ENTERCHANNEL_ACK.ts";

export interface Selection {
  readonly group: number;
  readonly channel: number;
  readonly replay: number;
}

export function read(r: Reader): Selection {
  const selection = {
    group: r.u8(),
    channel: r.u8(),
    replay: r.u8(),
  };
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes`);
  return selection;
}

export default function GC_ENTERCHANNEL_REQ(r: Reader, connection: Connection): void {
  let selection: Selection;
  try {
    selection = read(r);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    connection.log(`malformed channel selection — ${message}`);
    connection.reply("GC_ENTERCHANNEL_ACK", {
      result: Result.GenericError4,
      channelId: connection.config.channelId ?? 1,
      channelIndex: 0,
    });
    return;
  }

  if (connection.channelEntryCompleted) {
    connection.log("repeated channel selection -> rejected");
    connection.reply("GC_ENTERCHANNEL_ACK", {
      result: Result.GenericError4,
      channelId: connection.config.channelId ?? 1,
      channelIndex: selection.channel,
    });
    return;
  }

  const group = connection.config.group ?? 0;
  const channel = connection.config.channel ?? 0;
  const channelType = connection.config.channelType ?? 0;
  const accepted =
    connection.authenticated &&
    channelType !== 3 &&
    selection.group === group &&
    selection.channel === channel;

  if (!accepted) {
    connection.log(`channel selection ${selection.group}/${selection.channel} -> rejected`);
    connection.reply("GC_ENTERCHANNEL_ACK", {
      result: Result.GenericError4,
      channelId: connection.config.channelId ?? 1,
      channelIndex: selection.channel,
    });
    return;
  }

  const entry = {
    result: Result.Success,
    channelId: connection.config.channelId ?? 1,
    channelIndex: selection.channel,
    endpoint: {
      host: connection.config.udpHost ?? "127.0.0.1",
      port: connection.config.udpPort ?? 40202,
    },
    endpointOpaque: connection.config.endpointOpaque ?? 0,
    channelType: connection.config.channelType ?? 0,
    clientFlags: connection.config.clientFlags ?? 0,
    clientDefault: connection.config.clientDefault ?? 5,
  } as const;

  connection.reply("GC_ENTERCHANNEL_ACK", entry);
  connection.completeChannelEntry();
  connection.log(`channel selection ${selection.group}/${selection.channel} -> accepted`);
}
