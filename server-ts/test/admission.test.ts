import { describe, expect, test } from "bun:test";
import { ChannelAdmissionRegistry } from "../src/admission.ts";

describe("channel admissions", () => {
  test("a matching admission is single-use", () => {
    const admissions = new ChannelAdmissionRegistry();
    admissions.issue(7, 0, 0, "10.0.0.4", 1_000, 100);

    const claimed = admissions.claim(0, 0, "10.0.0.4", 100);
    expect(claimed?.accountId).toBe(7);
    expect(admissions.claim(0, 0, "10.0.0.4", 100)).toBeNull();
  });

  test("matches the source IP and both echoed values", () => {
    const admissions = new ChannelAdmissionRegistry();
    admissions.issue(7, 4, 2, "10.0.0.4", 1_000, 100);

    expect(admissions.claim(4, 2, "10.0.0.5", 100)).toBeNull();
    expect(admissions.claim(4, 1, "10.0.0.4", 100)).toBeNull();
    expect(admissions.claim(4, 2, "10.0.0.4", 100)?.accountId).toBe(7);
  });

  test("rejects ambiguous same-source claims", () => {
    const admissions = new ChannelAdmissionRegistry();
    admissions.issue(7, 0, 0, "10.0.0.4", 1_000, 100);
    admissions.issue(8, 0, 0, "10.0.0.4", 1_000, 100);

    expect(admissions.claim(0, 0, "10.0.0.4", 100)).toBeNull();
    expect(admissions.claim(0, 0, "10.0.0.4", 100)).toBeNull();
  });

  test("expires claims before matching", () => {
    const admissions = new ChannelAdmissionRegistry();
    admissions.issue(7, 0, 0, "10.0.0.4", 100, 100);

    expect(admissions.claim(0, 0, "10.0.0.4", 200)).toBeNull();
  });
});
