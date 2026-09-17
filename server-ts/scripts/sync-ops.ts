#!/usr/bin/env bun
/**
 * Validate the operation registry before tests.
 *
 * The registry explicitly imports each module and checks the directory with
 * Bun.Glob. Keeping `bun run sync` as a small compatibility command makes the
 * pre-test filename/catalogue check visible in CI.
 */

import { summary } from "../src/ops/registry.ts";

console.log(`runtime registry check passed: ${summary()}`);
