import { describe, expect, it } from "vitest";
import { isoToJalali, jalaliToIso } from "./jalali";

describe("jalali", () => {
  it("converts known dates both ways", () => {
    expect(isoToJalali("2026-11-24")).toBe("1405/09/03");
    expect(isoToJalali("2025-03-21")).toBe("1404/01/01");
    expect(jalaliToIso("۱۴۰۵/۹/۳")).toBe("2026-11-24");
    expect(jalaliToIso("1404-01-01")).toBe("2025-03-21");
  });

  it("rejects invalid input", () => {
    expect(jalaliToIso("1405/13/01")).toBeNull();
    expect(jalaliToIso("1404/12/30")).toBeNull(); // ۱۴۰۴ کبیسه نیست
    expect(jalaliToIso("abc")).toBeNull();
  });
});
