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
  readonly selectedGroup: number;
  readonly selectedChannel: number;
  readonly replay: number;
}

export function read(r: Reader): Selection {
  const selection = {
    selectedGroup: r.u8(),
    selectedChannel: r.u8(),
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
    connection.log(`malformed channel selection — ${errorMessage(error)}`);
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
      channelIndex: selection.selectedChannel,
    });
    return;
  }

  const group = connection.config.channelGroupIndex ?? 0;
  const channel = connection.config.channelIndex ?? 0;
  const channelType = connection.config.channelType ?? 0;
  const accepted =
    connection.authenticated &&
    channelType !== 3 &&
    selection.selectedGroup === group &&
    selection.selectedChannel === channel;

  if (!accepted) {
    connection.log(`channel selection ${selection.selectedGroup}/${selection.selectedChannel} -> rejected`);
    connection.reply("GC_ENTERCHANNEL_ACK", {
      result: Result.GenericError4,
      channelId: connection.config.channelId ?? 1,
      channelIndex: selection.selectedChannel,
    });
    return;
  }

  const entry = {
    result: Result.Success,
    channelId: connection.config.channelId ?? 1,
    channelIndex: selection.selectedChannel,
    endpoint: {
      host: connection.config.udpHost ?? "127.0.0.1",
      port: connection.config.udpPort ?? 40202,
    },
    endpointOpaqueByte: connection.config.endpointOpaqueByte ?? 0,
    channelType: connection.config.channelType ?? 0,
    clientFlags: connection.config.clientFlags ?? 0,
    clientDefaultValue: connection.config.clientDefaultValue ?? 5,
  } as const;

  connection.reply("GC_ENTERCHANNEL_ACK", entry);
  connection.completeChannelEntry();
  connection.log(`channel selection ${selection.selectedGroup}/${selection.selectedChannel} -> accepted`);
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
