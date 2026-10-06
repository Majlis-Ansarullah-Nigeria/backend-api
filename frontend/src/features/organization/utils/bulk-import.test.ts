import { describe, expect, it } from "vitest";
import type { BulkImportRow } from "@/lib/api/generated/hRSApiV1.schemas";
import { BULK_IMPORT_OUTCOME, errorReportCsv, filterRowsByTab, outcomeLabel } from "./bulk-import";

function row(overrides: Partial<BulkImportRow>): BulkImportRow {
  return {
    rowNumber: 1,
    outcome: BULK_IMPORT_OUTCOME.New,
    level: "Zone",
    code: "Z01",
    name: "Zone 1",
    parentCode: "",
    headEmail: "",
    issue: null,
    ...overrides,
  };
}

const NEW_ROW = row({ rowNumber: 1, outcome: BULK_IMPORT_OUTCOME.New });
const UPDATE_ROW = row({ rowNumber: 2, outcome: BULK_IMPORT_OUTCOME.Update, code: "Z02" });
const WARNING_ROW = row({ rowNumber: 3, outcome: BULK_IMPORT_OUTCOME.Warning, code: "Z03", issue: "No head; unit will be vacant." });
const ERROR_ROW = row({ rowNumber: 4, outcome: BULK_IMPORT_OUTCOME.Error, code: "Z04", issue: "Invalid email." });

const ALL_ROWS = [NEW_ROW, UPDATE_ROW, WARNING_ROW, ERROR_ROW];

describe("filterRowsByTab", () => {
  it("'all' returns every row", () => {
    expect(filterRowsByTab(ALL_ROWS, "all")).toEqual(ALL_ROWS);
  });

  it("'errors' returns only Error rows", () => {
    expect(filterRowsByTab(ALL_ROWS, "errors")).toEqual([ERROR_ROW]);
  });

  it("'warnings' returns only Warning rows", () => {
    expect(filterRowsByTab(ALL_ROWS, "warnings")).toEqual([WARNING_ROW]);
  });

  it("'updates' returns only Update rows", () => {
    expect(filterRowsByTab(ALL_ROWS, "updates")).toEqual([UPDATE_ROW]);
  });
});

describe("outcomeLabel", () => {
  it("labels each outcome", () => {
    expect(outcomeLabel(BULK_IMPORT_OUTCOME.New)).toBe("Ready");
    expect(outcomeLabel(BULK_IMPORT_OUTCOME.Update)).toBe("Update");
    expect(outcomeLabel(BULK_IMPORT_OUTCOME.Warning)).toBe("Warning");
    expect(outcomeLabel(BULK_IMPORT_OUTCOME.Error)).toBe("Error");
  });
});

describe("errorReportCsv", () => {
  it("includes only Error rows, with a header row", () => {
    const csv = errorReportCsv(ALL_ROWS);
    const lines = csv.split("\n");

    expect(lines[0]).toBe("Row,Result,Level,Code,Name,Parent Code,Head Email,Issue");
    expect(lines).toHaveLength(2);
    expect(lines[1]).toContain("Z04");
    expect(lines[1]).toContain("Invalid email.");
  });

  it("quotes fields containing commas", () => {
    const csv = errorReportCsv([row({ outcome: BULK_IMPORT_OUTCOME.Error, issue: "Parent code \"A,B\" not found." })]);

    expect(csv).toContain('"Parent code ""A,B"" not found."');
  });
});
