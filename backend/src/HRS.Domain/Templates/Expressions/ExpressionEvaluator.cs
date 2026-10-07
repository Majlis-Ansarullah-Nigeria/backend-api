using System.Globalization;
using HRS.Domain.Common;

namespace HRS.Domain.Templates.Expressions;

/// <summary>
/// Evaluates a computed-field expression (FR-TPL-005) against a unit's answers. Every value —
/// field lookups, sub-expressions, the whole result — is a double; missing or non-numeric field
/// values coerce to 0 rather than failing, and dividing by 0 (percentage, /) yields 0 rather than
/// throwing. That coercion is what "safe" means here: nothing a report's answers can contain
/// makes this throw. The only failure mode is a malformed expression STRING, which
/// TemplateSchemaValidator already rejects before a template can be saved (see Application's
/// TemplateSchemaValidator — it parses every ComputedExpression at validation time), so by the
/// time this runs against real answers the expression is already known to be well-formed.
/// </summary>
public static class ExpressionEvaluator
{
    public static Result<double> Evaluate(string expression, IReadOnlyDictionary<string, object?> fieldValues)
    {
        try
        {
            var node = ExpressionParser.Parse(expression);
            return Result.Success(EvaluateNode(node, fieldValues));
        }
        catch (ExpressionParseException ex)
        {
            return Result.Failure<double>(Error.Validation("EXPRESSION_PARSE_ERROR", ex.Message));
        }
    }

    private static double EvaluateNode(ExpressionNode node, IReadOnlyDictionary<string, object?> values) => node switch
    {
        NumberNode number => number.Value,
        IdentifierNode identifier => CoerceToDouble(values.GetValueOrDefault(identifier.Name)),
        UnaryMinusNode unary => -EvaluateNode(unary.Operand, values),
        BinaryNode binary => EvaluateBinary(binary, values),
        FunctionCallNode call => EvaluateFunction(call, values),
        _ => throw new ExpressionParseException("Unrecognized expression node."),
    };

    private static double EvaluateBinary(BinaryNode node, IReadOnlyDictionary<string, object?> values)
    {
        var left = EvaluateNode(node.Left, values);
        var right = EvaluateNode(node.Right, values);

        return node.Operator switch
        {
            BinaryOperator.Add => left + right,
            BinaryOperator.Subtract => left - right,
            BinaryOperator.Multiply => left * right,
            BinaryOperator.Divide => right == 0 ? 0 : left / right,
            BinaryOperator.Equal => left == right ? 1 : 0,
            BinaryOperator.NotEqual => left != right ? 1 : 0,
            BinaryOperator.GreaterThan => left > right ? 1 : 0,
            BinaryOperator.LessThan => left < right ? 1 : 0,
            BinaryOperator.GreaterThanOrEqual => left >= right ? 1 : 0,
            BinaryOperator.LessThanOrEqual => left <= right ? 1 : 0,
            _ => throw new ExpressionParseException("Unrecognized operator."),
        };
    }

    private static double EvaluateFunction(FunctionCallNode node, IReadOnlyDictionary<string, object?> values)
    {
        switch (node.Name)
        {
            case "sum":
                return node.Arguments.Sum(arg => EvaluateNode(arg, values));

            case "average":
                var present = node.Arguments.Where(arg => IsPresent(arg, values)).ToList();
                return present.Count == 0 ? 0 : present.Sum(arg => EvaluateNode(arg, values)) / present.Count;

            case "percentage":
                var numerator = EvaluateNode(node.Arguments[0], values);
                var denominator = EvaluateNode(node.Arguments[1], values);
                return denominator == 0 ? 0 : numerator / denominator * 100;

            case "if":
                var condition = EvaluateNode(node.Arguments[0], values);
                return condition != 0
                    ? EvaluateNode(node.Arguments[1], values)
                    : EvaluateNode(node.Arguments[2], values);

            default:
                throw new ExpressionParseException($"Unknown function '{node.Name}'.");
        }
    }

    /// <summary>A bare field reference counts as "present" only if the field has an answer.
    /// Any other kind of argument (a literal, a nested call, arithmetic) always has a value, so
    /// it's always counted — "missing" only makes sense for a direct field lookup.</summary>
    private static bool IsPresent(ExpressionNode node, IReadOnlyDictionary<string, object?> values) =>
        node is not IdentifierNode identifier || (values.TryGetValue(identifier.Name, out var value) && value is not null);

    private static double CoerceToDouble(object? value) => value switch
    {
        null => 0,
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        decimal m => (double)m,
        bool b => b ? 1 : 0,
        string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => 0,
    };
}
