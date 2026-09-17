/**
 * Selects the group and channel advertised by 681.
 *
 * The client sends this after every 144, even after a failed 144. A failed
 * selection therefore gets a real 196 failure and never grants lobby access.
 * The third byte is a native boolean flag; its business meaning is unknown.
 * Type 3 is fail-closed: a success packet needs the complete raw continuation.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";
import { Result } from "../s2c/GC_ENTERCHANNEL_ACK.ts";

export interface Selection {
  readonly group: number;
  readonly channel: number;
  readonly rawFlag: number;
}

export function read(r: Reader): Selection {
  const selection = { group: r.u8(), channel: r.u8(), rawFlag: r.u8() };
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes`);
  if (selection.rawFlag > 1) {
    throw new RangeError(`195 native raw flag is boolean, got ${selection.rawFlag}`);
  }
  return selection;
}

function reject(connection: Connection, channelIndex: number): void {
  connection.reply("GC_ENTERCHANNEL_ACK", {
    result: Result.GenericError4,
    channelId: connection.config.channel.id,
    channelIndex,
  });
}

export default function GC_ENTERCHANNEL_REQ(r: Reader, connection: Connection): void {
  let selection: Selection;
  try {
    selection = read(r);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    connection.log(`malformed channel selection — ${message}`);
    reject(connection, 0);
    return;
  }

  if (connection.channelEntryCompleted) {
    connection.log("repeated channel selection -> rejected");
    reject(connection, selection.channel);
    return;
  }

  const channel = connection.config.channel;
  const type3Ready = channel.type === 3
    ? channel.type3Tail !== undefined && "header1" in channel.type3Tail
    : channel.type3Tail === undefined;
  const accepted = connection.authenticated &&
    type3Ready &&
    selection.group === channel.group &&
    selection.channel === channel.index;

  if (!accepted) {
    connection.log(`channel selection ${selection.group}/${selection.channel} -> rejected`);
    reject(connection, selection.channel);
    return;
  }

  connection.reply("GC_ENTERCHANNEL_ACK", {
    result: Result.Success,
    channelId: channel.id,
    channelIndex: selection.channel,
    endpoint: channel.endpoint,
    endpointOpaque: channel.endpointOpaque,
    channelType: channel.type,
    type3Tail: channel.type3Tail,
    clientFlags: channel.clientFlags,
    clientDefault: channel.clientDefault,
  });
  connection.completeChannelEntry();
  connection.log(`channel selection ${selection.group}/${selection.channel} -> accepted`);
}
