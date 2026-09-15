/**
 * Filename-driven packet registry.
 *
 * Every module in `src/wire/` is named after the opcode it implements, and
 * that filename is the only place the name appears — the module itself never
 * repeats it, and gets its opcode injected instead.
 *
 *   src/wire/GL_LOGIN_REQ.ts   *_REQ  -> inbound handler, default (reader, session)
 *   src/wire/GL_LOGIN_ACK.ts   others -> outbound builder, default (op, ...args)
 *
 * `wire/index.ts` is a one-line-per-module barrel. It exists so the *names*
 * are known to the type checker: `session.reply("GL_LOGON_ACK")` is then a
 * compile error rather than a runtime surprise. The barrel is checked against
 * the directory at startup, so adding a file without listing it fails loudly.
 */

import { Glob } from "bun";
import type { Packet, Reader } from "./packet.ts";
import { opcodeFor, opcodeName } from "./opcodes.ts";
import type { Session } from "./session.ts";
import { modules } from "./wire/index.ts";

/** A `*_REQ` module: reads an inbound packet and acts on it. */
export type Handler = (reader: Reader, session: Session) => void | Promise<void>;

/** Any other module: builds an outbound packet from its own opcode. */
export type Builder = (op: number, ...args: never[]) => Packet;

type Modules = typeof modules;

/** Builder names, and the arguments each one takes after its injected opcode. */
export type BuilderName = {
  [K in keyof Modules]: K extends `${string}_REQ` ? never : K;
}[keyof Modules] &
  string;

export type BuilderArgs<N extends BuilderName> = Modules[N] extends {
  default: (op: number, ...args: infer A) => Packet;
}
  ? A
  : never;

const WIRE_DIR = new URL("./wire/", import.meta.url).pathname;

export class Registry {
  readonly #handlers = new Map<number, Handler>();
  readonly #builders = new Map<string, { op: number; build: Builder }>();

  static load(): Registry {
    const registry = new Registry();

    // The barrel is the source of names; the directory is the source of truth.
    // Cross-check them so neither can drift.
    const onDisk = new Set(
      [...new Glob("*.ts").scanSync({ cwd: WIRE_DIR })]
        .filter((file) => file !== "index.ts")
        .map((file) => file.slice(0, -3)),
    );
    const listed = new Set(Object.keys(modules));

    for (const name of onDisk) {
      if (!listed.has(name)) throw new Error(`src/wire/${name}.ts is not listed in wire/index.ts`);
    }
    for (const name of listed) {
      if (!onDisk.has(name)) throw new Error(`wire/index.ts lists ${name}, which has no module`);
    }

    for (const [name, module] of Object.entries(modules)) {
      const op = opcodeFor(name); // throws if the filename is not a real opcode
      if (name.endsWith("_REQ")) {
        registry.#handlers.set(op, module.default as Handler);
      } else {
        registry.#builders.set(name, { op, build: module.default as Builder });
      }
    }
    return registry;
  }

  handlerFor(opcode: number): Handler | undefined {
    return this.#handlers.get(opcode);
  }

  build<N extends BuilderName>(name: N, ...args: BuilderArgs<N>): Packet {
    const entry = this.#builders.get(name);
    if (!entry) throw new Error(`no builder in src/wire/ for ${name}`);
    return (entry.build as (op: number, ...rest: unknown[]) => Packet)(entry.op, ...args);
  }

  get summary(): string {
    const handlers = [...this.#handlers.keys()].map(opcodeName).sort();
    const builders = [...this.#builders.keys()].sort();
    return (
      `${handlers.length} handlers (${handlers.join(", ")}), ` +
      `${builders.length} builders (${builders.join(", ")})`
    );
  }
}
