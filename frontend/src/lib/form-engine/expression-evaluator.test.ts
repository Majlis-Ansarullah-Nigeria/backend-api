import { describe, expect, it } from "vitest";
import fixtures from "../../../../shared/expression-fixtures.json";
import { evaluateExpression } from "./expression-evaluator";

interface ExpressionFixture {
  name: string;
  expression: string;
  values: Record<string, unknown>;
  expected?: number;
  expectError?: boolean;
}

describe("evaluateExpression", () => {
  // Drives shared/expression-fixtures.json — the backend's xUnit suite runs the exact same
  // file, so these two test runs are the cross-language parity guarantee FR-TPL-005 asks for.
  it.each(fixtures as ExpressionFixture[])("$name", (fixture) => {
    const result = evaluateExpression(fixture.expression, fixture.values);

    if (fixture.expectError) {
      expect(result.success).toBe(false);
    } else {
      expect(result.success).toBe(true);
      if (result.success) {
        expect(result.value).toBeCloseTo(fixture.expected!, 6);
      }
    }
  });
});
