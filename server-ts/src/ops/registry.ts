/**
 * Runtime packet registry, discovered directly from `src/ops/{c2s,s2c}`.
 *
 * Each packet is one file named after its opcode. Bun's `import.meta.require`
 * lets the registry load that directory without generated barrel files:
 *
 *   src/ops/c2s/GL_LOGIN_REQ.ts   the client sends it; we read and handle it
 *   src/ops/s2c/GL_LOGIN_ACK.ts   we send it; we build it
 *
 * Direction comes from the folder, not the `_REQ`/`_ACK` suffix. Those suffixes
 * describe the client's view and do not always match ours: `GT_PING_ACK` is an
 * `_ACK` the server sends, while `GT_PING_REQ` is a `_REQ` it receives.
 * (docs/PACKETS.md §3.15pre)
 *
 * The runtime loader checks every discovered filename against db/packets.tsv and
 * every default export against its filename. A missing, renamed, or unknown
 * operation therefore fails at startup instead of silently disappearing.
 */

import { Glob } from "bun";
import { Packet, type Reader } from "../packet.ts";
import { opcodeFor, opcodeName } from "../opcodes.ts";
import type { Connection } from "../connection.ts";

/** A c2s module: reads an inbound packet and handles it, start to finish. */
export type Handler = (reader: Reader, connection: Connection) => void | Promise<void>;

/**
 * Type-only view of the s2c builders. The imports are erased; runtime loading
 * still comes from the directory below. Keeping this small surface here means
 * removing generated barrel files does not weaken `reply()` or `build()`:
 * names stay a literal union and the opcode argument is stripped from each
 * builder's parameter tuple.
 */
type OutboundModules = {
  GC_ENTERCHANNEL_ACK: typeof import("./s2c/GC_ENTERCHANNEL_ACK.ts").default;
  GL_ACCOUNTCONNSUCC: typeof import("./s2c/GL_ACCOUNTCONNSUCC.ts").default;
  GL_CLIENTINFO_ACK: typeof import("./s2c/GL_CLIENTINFO_ACK.ts").default;
  GL_DATA_RECV_COMPLETED_ACK: typeof import("./s2c/GL_DATA_RECV_COMPLETED_ACK.ts").default;
  GL_FRIEND_LIST_ACK: typeof import("./s2c/GL_FRIEND_LIST_ACK.ts").default;
  GL_GAMEROOMINFO_ACK: typeof import("./s2c/GL_GAMEROOMINFO_ACK.ts").default;
  GL_INVENIN_ACK: typeof import("./s2c/GL_INVENIN_ACK.ts").default;
  GL_LOGIN_ACK: typeof import("./s2c/GL_LOGIN_ACK.ts").default;
  GL_MSG_RECVLIST_ACK: typeof import("./s2c/GL_MSG_RECVLIST_ACK.ts").default;
  GL_MYINFO_ACK: typeof import("./s2c/GL_MYINFO_ACK.ts").default;
  GL_MYITEM_ACK: typeof import("./s2c/GL_MYITEM_ACK.ts").default;
  GL_SHOPIN_ACK: typeof import("./s2c/GL_SHOPIN_ACK.ts").default;
  GL_TCPCONNSUCC: typeof import("./s2c/GL_TCPCONNSUCC.ts").default;
  GL_USERLIST_ACK: typeof import("./s2c/GL_USERLIST_ACK.ts").default;
  GT_PING_ACK: typeof import("./s2c/GT_PING_ACK.ts").default;
  PM_UDPSTART_ACK: typeof import("./s2c/PM_UDPSTART_ACK.ts").default;
};

export type OutboundName = keyof OutboundModules;
export type OutboundArgs<N extends OutboundName> = OutboundModules[N] extends (
  _op: number,
  ...args: infer Args
) => unknown
  ? Args
  : never;

type Operation = (...args: unknown[]) => unknown;
type LoadedOperation = {
  readonly opcode: number;
  readonly operation: Operation;
};
type ImportMetaWithRequire = ImportMeta & {
  require(path: string): unknown;
};

const requireModule = (path: string): unknown =>
  (import.meta as ImportMetaWithRequire).require(path);

function namesOnDisk(dir: "c2s" | "s2c"): string[] {
  const folder = new URL(`./${dir}/`, import.meta.url).pathname;
  return [...new Glob("*.ts").scanSync({ cwd: folder })]
    .map((file) => file.slice(0, -3))
    .sort();
}

function operationFromModule(dir: "c2s" | "s2c", name: string): Operation {
  const namespace = requireModule(`./${dir}/${name}.ts`);
  if (
    typeof namespace !== "object" ||
    namespace === null ||
    !("default" in namespace) ||
    typeof namespace.default !== "function"
  ) {
    throw new TypeError(`src/ops/${dir}/${name}.ts must export a default function`);
  }

  const operation = namespace.default as Operation;
  const actualName = operation.name;
  if (actualName !== name && actualName !== `${name}_default`) {
    throw new Error(
      `src/ops/${dir}/${name}.ts exports a function named ${actualName || "(anonymous)"}`,
    );
  }
  return operation;
}

function loadOperations(dir: "c2s" | "s2c"): ReadonlyMap<string, LoadedOperation> {
  const operations = new Map<string, LoadedOperation>();
  const opcodes = new Map<number, string>();
  for (const name of namesOnDisk(dir)) {
    // opcodeFor is deliberately called during discovery, not only on first use.
    // A typo in a filename must fail while the server starts.
    const opcode = opcodeFor(name);
    const previous = opcodes.get(opcode);
    if (previous) {
      throw new Error(`duplicate ${dir} opcode ${opcodeName(opcode)}: ${previous} and ${name}`);
    }
    opcodes.set(opcode, name);
    operations.set(name, { opcode, operation: operationFromModule(dir, name) });
  }
  return operations;
}

const inboundOperations = loadOperations("c2s");
const outboundOperations = loadOperations("s2c");

/** opcode -> the module that handles it. */
const handlers = new Map<number, Handler>();
for (const { opcode, operation } of inboundOperations.values()) {
  if (handlers.has(opcode)) throw new Error(`duplicate c2s opcode ${opcodeName(opcode)}`);
  handlers.set(opcode, operation as Handler);
}

export function handlerFor(opcode: number): Handler | undefined {
  return handlers.get(opcode);
}

export function build<N extends OutboundName>(name: N, ...args: OutboundArgs<N>): Packet {
  const entry = outboundOperations.get(name);
  if (!entry) throw new Error(`unknown outbound operation ${name}`);

  // The public tuple is checked by TypeScript; the runtime-loaded function is
  // intentionally unknown until its result is checked below.
  const packet = entry.operation(entry.opcode, ...(args as unknown[]));
  if (!(packet instanceof Packet)) {
    throw new TypeError(`outbound operation ${name} did not return a Packet`);
  }
  return packet;
}

/** One line for the startup log. */
export function summary(): string {
  const inbound = [...inboundOperations.values()].map(({ opcode }) => opcodeName(opcode)).sort();
  const outbound = [...outboundOperations.keys()].sort();
  return `c2s ${inbound.length} (${inbound.join(", ")}), s2c ${outbound.length} (${outbound.join(", ")})`;
}
