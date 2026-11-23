import { describe, expect, it } from "vitest";
import { formatDate, toLatinDigits } from "./format";

describe("format", () => {
  it("shows API dates in the Persian calendar", () => {
    expect(formatDate("2026-11-24")).toBe("۱۴۰۵/۰۹/۰۳");
  });

  it("reads zone-less timestamps as UTC and shows the Tehran day", () => {
    // ۲۲:۳۸ UTC روز ۲۳ نوامبر = ۰۲:۰۸ بامداد ۲۴ نوامبر در تهران
    expect(formatDate("2026-11-23T22:38:15.4687")).toBe("۱۴۰۵/۰۹/۰۳");
    expect(formatDate("2026-11-23T22:38:15Z")).toBe("۱۴۰۵/۰۹/۰۳");
  });

  it("shows a dash for missing dates", () => {
    expect(formatDate(null)).toBe("—");
  });

  it("converts Persian and Arabic digits to Latin", () => {
    expect(toLatinDigits("۱۰۰۲")).toBe("1002");
    expect(toLatinDigits("٣٤")).toBe("34");
  });
});
