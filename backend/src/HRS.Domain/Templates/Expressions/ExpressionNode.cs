namespace HRS.Domain.Templates.Expressions;

internal enum BinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Equal,
    NotEqual,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
}

internal abstract record ExpressionNode;

internal sealed record NumberNode(double Value) : ExpressionNode;

internal sealed record IdentifierNode(string Name) : ExpressionNode;

internal sealed record UnaryMinusNode(ExpressionNode Operand) : ExpressionNode;

internal sealed record BinaryNode(ExpressionNode Left, BinaryOperator Operator, ExpressionNode Right) : ExpressionNode;

internal sealed record FunctionCallNode(string Name, IReadOnlyList<ExpressionNode> Arguments) : ExpressionNode;
