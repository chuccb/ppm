#!/usr/bin/env bun
/**
 * Validate the operation registry before tests.
 *
 * There are intentionally no generated barrel files: registry.ts discovers
 * these modules with Bun.Glob and loads them with import.meta.require. Keeping
 * `bun run sync` as a compatibility command makes the old pre-test workflow
 * useful while delegating validation to the same startup path as the server.
 */

import { summary } from "../src/ops/registry.ts";

console.log(`runtime registry check passed: ${summary()}`);
