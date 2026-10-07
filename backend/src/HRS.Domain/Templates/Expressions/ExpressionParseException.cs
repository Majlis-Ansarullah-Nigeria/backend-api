namespace HRS.Domain.Templates.Expressions;

/// <summary>Thrown by the lexer/parser for malformed expression text — unknown tokens, unknown
/// function names, wrong argument counts, unbalanced parentheses. Never thrown for runtime data
/// (a missing or non-numeric field value just coerces to 0 — see ExpressionEvaluator); that's
/// what "safe" means in FR-TPL-005. Caught once at ExpressionEvaluator.Evaluate's boundary and
/// turned into a Result failure — callers never need to catch this themselves.</summary>
public sealed class ExpressionParseException(string message) : Exception(message);
