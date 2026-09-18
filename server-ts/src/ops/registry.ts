/**
 * The small, explicit operation registry.
 *
 * A packet module is imported once, in the direction in which it is used:
 *
 *   c2s  reader + Connection -> void
 *   s2c  opcode + fields     -> Packet
 *
 * This is intentionally not a dynamic import registry. Static imports make the
 * actual 15/16 runtime surface visible to TypeScript, while the directory
 * check below still fails startup if a packet file is added and forgotten.
 */

import { Glob } from "bun";
import { Packet, type Reader } from "../packet.ts";
import { opcodeFor, opcodeName } from "../opcodes.ts";
import type { Connection } from "../connection.ts";

import GC_ENTERCHANNEL_REQ from "./c2s/GC_ENTERCHANNEL_REQ.ts";
import GL_CLIENTINFO_REQ from "./c2s/GL_CLIENTINFO_REQ.ts";
import GL_DATA_RECV_COMPLETED_REQ from "./c2s/GL_DATA_RECV_COMPLETED_REQ.ts";
import GL_FRIEND_LIST_REQ from "./c2s/GL_FRIEND_LIST_REQ.ts";
import GL_GAMEROOMINFO_REQ from "./c2s/GL_GAMEROOMINFO_REQ.ts";
import GL_INVENIN_REQ from "./c2s/GL_INVENIN_REQ.ts";
import GL_LOBBYIN_REQ from "./c2s/GL_LOBBYIN_REQ.ts";
import GL_LOGIN_REQ from "./c2s/GL_LOGIN_REQ.ts";
import GL_MSG_RECVLIST_REQ from "./c2s/GL_MSG_RECVLIST_REQ.ts";
import GL_MYINFO_REQ from "./c2s/GL_MYINFO_REQ.ts";
import GL_MYITEM_REQ from "./c2s/GL_MYITEM_REQ.ts";
import GL_SHOPIN_REQ from "./c2s/GL_SHOPIN_REQ.ts";
import GL_USERLIST_REQ from "./c2s/GL_USERLIST_REQ.ts";
import GP_ENTER_PEPACHI_REQ from "./c2s/GP_ENTER_PEPACHI_REQ.ts";
import GP_PEPACHI_LIST_REQ from "./c2s/GP_PEPACHI_LIST_REQ.ts";
import GP_START_GAME_REQ from "./c2s/GP_START_GAME_REQ.ts";
import GS_CAPSULEMACHINE_START_REQ from "./c2s/GS_CAPSULEMACHINE_START_REQ.ts";
import GT_PING_REQ from "./c2s/GT_PING_REQ.ts";
import PM_UDPSTART_REQ from "./c2s/PM_UDPSTART_REQ.ts";

import GC_ENTERCHANNEL_ACK from "./s2c/GC_ENTERCHANNEL_ACK.ts";
import GL_ACCOUNTCONNSUCC from "./s2c/GL_ACCOUNTCONNSUCC.ts";
import GL_CLIENTINFO_ACK from "./s2c/GL_CLIENTINFO_ACK.ts";
import GL_DATA_RECV_COMPLETED_ACK from "./s2c/GL_DATA_RECV_COMPLETED_ACK.ts";
import GL_FRIEND_LIST_ACK from "./s2c/GL_FRIEND_LIST_ACK.ts";
import GL_GAMEROOMINFO_ACK from "./s2c/GL_GAMEROOMINFO_ACK.ts";
import GL_INVENIN_ACK from "./s2c/GL_INVENIN_ACK.ts";
import GL_LOGIN_ACK from "./s2c/GL_LOGIN_ACK.ts";
import GL_MSG_RECVLIST_ACK from "./s2c/GL_MSG_RECVLIST_ACK.ts";
import GL_MYINFO_ACK from "./s2c/GL_MYINFO_ACK.ts";
import GL_MYITEM_ACK from "./s2c/GL_MYITEM_ACK.ts";
import GL_SHOPIN_ACK from "./s2c/GL_SHOPIN_ACK.ts";
import GL_TCPCONNSUCC from "./s2c/GL_TCPCONNSUCC.ts";
import GL_USERLIST_ACK from "./s2c/GL_USERLIST_ACK.ts";
import GT_PING_ACK from "./s2c/GT_PING_ACK.ts";
import GP_ENTER_PEPACHI_ACK from "./s2c/GP_ENTER_PEPACHI_ACK.ts";
import GP_PEPACHI_LIST_ACK from "./s2c/GP_PEPACHI_LIST_ACK.ts";
import GP_START_GAME_ACK from "./s2c/GP_START_GAME_ACK.ts";
import GS_CAPSULEMACHINE_START_ACK from "./s2c/GS_CAPSULEMACHINE_START_ACK.ts";
import PM_UDPSTART_ACK from "./s2c/PM_UDPSTART_ACK.ts";

export type Handler = (reader: Reader, connection: Connection) => void | Promise<void>;

const inbound = {
  GC_ENTERCHANNEL_REQ,
  GL_CLIENTINFO_REQ,
  GL_DATA_RECV_COMPLETED_REQ,
  GL_FRIEND_LIST_REQ,
  GL_GAMEROOMINFO_REQ,
  GL_INVENIN_REQ,
  GL_LOBBYIN_REQ,
  GL_LOGIN_REQ,
  GL_MSG_RECVLIST_REQ,
  GL_MYINFO_REQ,
  GL_MYITEM_REQ,
  GL_SHOPIN_REQ,
  GL_USERLIST_REQ,
  GP_ENTER_PEPACHI_REQ,
  GP_PEPACHI_LIST_REQ,
  GP_START_GAME_REQ,
  GS_CAPSULEMACHINE_START_REQ,
  GT_PING_REQ,
  PM_UDPSTART_REQ,
} satisfies Record<string, Handler>;

const outbound = {
  GC_ENTERCHANNEL_ACK,
  GL_ACCOUNTCONNSUCC,
  GL_CLIENTINFO_ACK,
  GL_DATA_RECV_COMPLETED_ACK,
  GL_FRIEND_LIST_ACK,
  GL_GAMEROOMINFO_ACK,
  GL_INVENIN_ACK,
  GL_LOGIN_ACK,
  GL_MSG_RECVLIST_ACK,
  GL_MYINFO_ACK,
  GL_MYITEM_ACK,
  GL_SHOPIN_ACK,
  GL_TCPCONNSUCC,
  GL_USERLIST_ACK,
  GP_ENTER_PEPACHI_ACK,
  GP_PEPACHI_LIST_ACK,
  GP_START_GAME_ACK,
  GS_CAPSULEMACHINE_START_ACK,
  GT_PING_ACK,
  PM_UDPSTART_ACK,
} as const;

export type OutboundName = keyof typeof outbound;
export type OutboundArgs<N extends OutboundName> =
  Parameters<(typeof outbound)[N]> extends [number, ...infer Args] ? Args : never;

type Operation = (...args: unknown[]) => unknown;

function namesOnDisk(dir: "c2s" | "s2c"): string[] {
  const folder = new URL(`./${dir}/`, import.meta.url).pathname;
  return [...new Glob("*.ts").scanSync({ cwd: folder })]
    .map((file) => file.slice(0, -3))
    .sort();
}

function verifyDirectory(dir: "c2s" | "s2c", names: readonly string[]): void {
  const expected = new Set(names);
  for (const file of namesOnDisk(dir)) {
    if (!expected.has(file)) {
      throw new Error(`src/ops/${dir}/${file}.ts is not registered in ops/registry.ts`);
    }
  }
  for (const name of names) opcodeFor(name);
}

const inboundNames = Object.keys(inbound);
const outboundNames = Object.keys(outbound);
verifyDirectory("c2s", inboundNames);
verifyDirectory("s2c", outboundNames);

const handlers = new Map<number, Handler>();
for (const [name, operation] of Object.entries(inbound)) {
  const opcode = opcodeFor(name);
  if (handlers.has(opcode)) throw new Error(`duplicate c2s opcode ${opcodeName(opcode)}`);
  handlers.set(opcode, operation);
}

export function handlerFor(opcode: number): Handler | undefined {
  return handlers.get(opcode);
}

export function build<N extends OutboundName>(name: N, ...args: OutboundArgs<N>): Packet {
  const operation = outbound[name] as unknown as Operation;
  const packet = operation(opcodeFor(name), ...(args as unknown[]));
  if (!(packet instanceof Packet)) {
    throw new TypeError(`outbound operation ${name} did not return a Packet`);
  }
  return packet;
}

export function summary(): string {
  const c2s = [...inboundNames].sort().join(", ");
  const s2c = [...outboundNames].sort().join(", ");
  return `c2s ${inboundNames.length} (${c2s}), s2c ${outboundNames.length} (${s2c})`;
}
