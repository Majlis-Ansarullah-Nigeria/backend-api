// Mirrors backend/src/HRS.Domain/Templates/Expressions/*.cs exactly — both evaluators must
// agree on every case in shared/expression-fixtures.json (FR-TPL-005). Deliberately closed: a
// fixed function set (sum, average, percentage, if), no string literals, no variables beyond
// field references — "safe" means there's nothing here that could do anything but compute a
// number. See the backend's ExpressionEvaluator.cs for the full rationale on each coercion rule.

type TokenKind =
  | "number"
  | "identifier"
  | "plus"
  | "minus"
  | "star"
  | "slash"
  | "lparen"
  | "rparen"
  | "comma"
  | "eq"
  | "neq"
  | "gt"
  | "lt"
  | "gte"
  | "lte"
  | "eof";

interface Token {
  kind: TokenKind;
  text: string;
  numberValue?: number;
}

export class ExpressionParseError extends Error {}

export type ExpressionResult = { success: true; value: number } | { success: false; error: string };

export function evaluateExpression(expression: string, fieldValues: Record<string, unknown>): ExpressionResult {
  try {
    const node = parseExpressionString(expression);
    return { success: true, value: evaluateNode(node, fieldValues) };
  } catch (error) {
    if (error instanceof ExpressionParseError) {
      return { success: false, error: error.message };
    }
    throw error;
  }
}

// ---- Lexer ----

function isAsciiDigit(c: string | undefined): c is string {
  return c !== undefined && c >= "0" && c <= "9";
}

function isAsciiLowerLetter(c: string | undefined): c is string {
  return c !== undefined && c >= "a" && c <= "z";
}

const SINGLE_CHAR_TOKENS: Record<string, TokenKind> = {
  "+": "plus",
  "-": "minus",
  "*": "star",
  "/": "slash",
  "(": "lparen",
  ")": "rparen",
  ",": "comma",
  "=": "eq",
};

function tokenize(expression: string): Token[] {
  const tokens: Token[] = [];
  let i = 0;

  while (i < expression.length) {
    const c = expression[i]!;

    if (/\s/.test(c)) {
      i++;
      continue;
    }

    if (c === "!" && expression[i + 1] === "=") {
      tokens.push({ kind: "neq", text: "!=" });
      i += 2;
      continue;
    }
    if (c === ">" && expression[i + 1] === "=") {
      tokens.push({ kind: "gte", text: ">=" });
      i += 2;
      continue;
    }
    if (c === "<" && expression[i + 1] === "=") {
      tokens.push({ kind: "lte", text: "<=" });
      i += 2;
      continue;
    }
    if (c === ">") {
      tokens.push({ kind: "gt", text: ">" });
      i++;
      continue;
    }
    if (c === "<") {
      tokens.push({ kind: "lt", text: "<" });
      i++;
      continue;
    }
    if (c in SINGLE_CHAR_TOKENS) {
      tokens.push({ kind: SINGLE_CHAR_TOKENS[c]!, text: c });
      i++;
      continue;
    }

    if (isAsciiDigit(c) || (c === "." && isAsciiDigit(expression[i + 1]))) {
      const start = i;
      while (i < expression.length && (isAsciiDigit(expression[i]) || expression[i] === ".")) {
        i++;
      }
      const text = expression.slice(start, i);
      const value = Number(text);
      if (Number.isNaN(value)) {
        throw new ExpressionParseError(`'${text}' is not a valid number.`);
      }
      tokens.push({ kind: "number", text, numberValue: value });
      continue;
    }

    if (isAsciiLowerLetter(c)) {
      const start = i;
      while (
        i < expression.length &&
        (isAsciiLowerLetter(expression[i]) || isAsciiDigit(expression[i]) || expression[i] === "_")
      ) {
        i++;
      }
      tokens.push({ kind: "identifier", text: expression.slice(start, i) });
      continue;
    }

    throw new ExpressionParseError(`Unexpected character '${c}'.`);
  }

  tokens.push({ kind: "eof", text: "" });
  return tokens;
}

// ---- Parser ----

type BinaryOperator = "add" | "subtract" | "multiply" | "divide" | "eq" | "neq" | "gt" | "lt" | "gte" | "lte";

type ExpressionNode =
  | { type: "number"; value: number }
  | { type: "identifier"; name: string }
  | { type: "unaryMinus"; operand: ExpressionNode }
  | { type: "binary"; left: ExpressionNode; operator: BinaryOperator; right: ExpressionNode }
  | { type: "call"; name: string; args: ExpressionNode[] };

const COMPARISON_OPERATORS: Partial<Record<TokenKind, BinaryOperator>> = {
  eq: "eq",
  neq: "neq",
  gt: "gt",
  lt: "lt",
  gte: "gte",
  lte: "lte",
};

const FUNCTION_ARITY: Record<string, { min: number; max: number }> = {
  sum: { min: 1, max: Infinity },
  average: { min: 1, max: Infinity },
  percentage: { min: 2, max: 2 },
  if: { min: 3, max: 3 },
};

function validateFunctionCall(name: string, argCount: number): void {
  const arity = FUNCTION_ARITY[name];
  if (!arity) {
    throw new ExpressionParseError(`Unknown function '${name}'.`);
  }
  if (argCount < arity.min || argCount > arity.max) {
    const expected = arity.min === arity.max ? `${arity.min}` : `at least ${arity.min}`;
    throw new ExpressionParseError(`'${name}' takes ${expected} argument(s), got ${argCount}.`);
  }
}

class Parser {
  private position = 0;

  constructor(private readonly tokens: Token[]) {}

  static parseAll(expression: string): ExpressionNode {
    const parser = new Parser(tokenize(expression));
    const node = parser.parseExpression();
    parser.expect("eof");
    return node;
  }

  private parseExpression(): ExpressionNode {
    return this.parseComparison();
  }

  private parseComparison(): ExpressionNode {
    const left = this.parseAdditive();
    const operator = COMPARISON_OPERATORS[this.current.kind];
    if (!operator) {
      return left;
    }
    this.advance();
    const right = this.parseAdditive();
    return { type: "binary", left, operator, right };
  }

  private parseAdditive(): ExpressionNode {
    let left = this.parseMultiplicative();
    for (;;) {
      if (this.match("plus")) {
        left = { type: "binary", left, operator: "add", right: this.parseMultiplicative() };
      } else if (this.match("minus")) {
        left = { type: "binary", left, operator: "subtract", right: this.parseMultiplicative() };
      } else {
        return left;
      }
    }
  }

  private parseMultiplicative(): ExpressionNode {
    let left = this.parseUnary();
    for (;;) {
      if (this.match("star")) {
        left = { type: "binary", left, operator: "multiply", right: this.parseUnary() };
      } else if (this.match("slash")) {
        left = { type: "binary", left, operator: "divide", right: this.parseUnary() };
      } else {
        return left;
      }
    }
  }

  private parseUnary(): ExpressionNode {
    if (this.match("minus")) {
      return { type: "unaryMinus", operand: this.parseUnary() };
    }
    return this.parsePrimary();
  }

  private parsePrimary(): ExpressionNode {
    const token = this.current;

    if (token.kind === "number") {
      this.advance();
      return { type: "number", value: token.numberValue! };
    }

    if (token.kind === "identifier") {
      this.advance();
      if (!this.match("lparen")) {
        return { type: "identifier", name: token.text };
      }
      const args = this.parseArgumentList();
      this.expect("rparen");
      validateFunctionCall(token.text, args.length);
      return { type: "call", name: token.text, args };
    }

    if (token.kind === "lparen") {
      this.advance();
      const inner = this.parseExpression();
      this.expect("rparen");
      return inner;
    }

    throw new ExpressionParseError(
      token.kind === "eof" ? "Unexpected end of expression." : `Unexpected token '${token.text}'.`,
    );
  }

  private parseArgumentList(): ExpressionNode[] {
    const args: ExpressionNode[] = [];
    if (this.current.kind === "rparen") {
      return args;
    }
    args.push(this.parseExpression());
    while (this.match("comma")) {
      args.push(this.parseExpression());
    }
    return args;
  }

  private get current(): Token {
    return this.tokens[this.position]!;
  }

  private advance(): void {
    this.position++;
  }

  private match(kind: TokenKind): boolean {
    if (this.current.kind !== kind) {
      return false;
    }
    this.advance();
    return true;
  }

  private expect(kind: TokenKind): void {
    if (!this.match(kind)) {
      throw new ExpressionParseError(`Expected '${kind}' but found '${this.current.text}'.`);
    }
  }
}

function parseExpressionString(expression: string): ExpressionNode {
  return Parser.parseAll(expression);
}

// ---- Evaluator ----

function evaluateNode(node: ExpressionNode, values: Record<string, unknown>): number {
  switch (node.type) {
    case "number":
      return node.value;
    case "identifier":
      return coerceToNumber(values[node.name]);
    case "unaryMinus":
      return -evaluateNode(node.operand, values);
    case "binary":
      return evaluateBinary(node.left, node.operator, node.right, values);
    case "call":
      return evaluateCall(node.name, node.args, values);
  }
}

function evaluateBinary(
  leftNode: ExpressionNode,
  operator: BinaryOperator,
  rightNode: ExpressionNode,
  values: Record<string, unknown>,
): number {
  const left = evaluateNode(leftNode, values);
  const right = evaluateNode(rightNode, values);

  switch (operator) {
    case "add":
      return left + right;
    case "subtract":
      return left - right;
    case "multiply":
      return left * right;
    case "divide":
      return right === 0 ? 0 : left / right;
    case "eq":
      return left === right ? 1 : 0;
    case "neq":
      return left !== right ? 1 : 0;
    case "gt":
      return left > right ? 1 : 0;
    case "lt":
      return left < right ? 1 : 0;
    case "gte":
      return left >= right ? 1 : 0;
    case "lte":
      return left <= right ? 1 : 0;
  }
}

function evaluateCall(name: string, args: ExpressionNode[], values: Record<string, unknown>): number {
  switch (name) {
    case "sum":
      return args.reduce((total, arg) => total + evaluateNode(arg, values), 0);
    case "average": {
      const present = args.filter((arg) => isPresent(arg, values));
      return present.length === 0 ? 0 : present.reduce((total, arg) => total + evaluateNode(arg, values), 0) / present.length;
    }
    case "percentage": {
      const numerator = evaluateNode(args[0]!, values);
      const denominator = evaluateNode(args[1]!, values);
      return denominator === 0 ? 0 : (numerator / denominator) * 100;
    }
    case "if": {
      const condition = evaluateNode(args[0]!, values);
      return condition !== 0 ? evaluateNode(args[1]!, values) : evaluateNode(args[2]!, values);
    }
    default:
      throw new ExpressionParseError(`Unknown function '${name}'.`);
  }
}

/** A bare field reference counts as "present" only if the field has an answer. Any other kind
 * of argument (a literal, a nested call, arithmetic) always has a value, so it's always counted. */
function isPresent(node: ExpressionNode, values: Record<string, unknown>): boolean {
  return node.type !== "identifier" || (node.name in values && values[node.name] !== null && values[node.name] !== undefined);
}

function coerceToNumber(value: unknown): number {
  if (value === null || value === undefined) {
    return 0;
  }
  if (typeof value === "number") {
    return Number.isFinite(value) ? value : 0;
  }
  if (typeof value === "boolean") {
    return value ? 1 : 0;
  }
  if (typeof value === "string") {
    if (value.trim() === "") {
      return 0;
    }
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }
  return 0;
}
