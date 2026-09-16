#!/usr/bin/env bun
/**
 * Check the operation folders used by the runtime registry.
 *
 * There are intentionally no generated barrel files: registry.ts discovers
 * these modules with Bun.Glob and loads them with import.meta.require. Keep
 * `bun run sync` as the cheap, explicit pre-test check for filenames that are
 * not real entries in db/packets.tsv.
 */

import { Glob } from "bun";
import { opcodeFor } from "../src/opcodes.ts";

const OPS = new URL("../src/ops/", import.meta.url).pathname;
let total = 0;

for (const dir of ["c2s", "s2c"] as const) {
  const names = [...new Glob("*.ts").scanSync({ cwd: `${OPS}${dir}` })]
    .map((file) => file.slice(0, -3))
    .sort();

  for (const name of names) {
    opcodeFor(name);
    total += 1;
  }
  console.log(`src/ops/${dir}: ${names.length} runtime modules`);
}

console.log(`runtime registry check passed (${total} modules)`);
