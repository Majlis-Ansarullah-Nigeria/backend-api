using System.Globalization;

namespace HRS.Domain.Templates.Expressions;

/// <summary>
/// Recursive-descent parser for the computed-field language (FR-TPL-005): sum, average,
/// percentage, if, plus arithmetic and comparisons (comparisons exist only so `if`'s condition
/// can express a threshold, e.g. "if(attendance &gt; 10, ...)"). Deliberately closed — a fixed
/// function set, no string literals, no variables beyond field references, no loops — "safe"
/// means there's nothing here that could do anything but compute a number.
/// </summary>
internal sealed class ExpressionParser
{
    private static readonly Dictionary<string, (int Min, int Max)> FunctionArity = new()
    {
        ["sum"] = (1, int.MaxValue),
        ["average"] = (1, int.MaxValue),
        ["percentage"] = (2, 2),
        ["if"] = (3, 3),
    };

    private readonly IReadOnlyList<Token> _tokens;
    private int _position;

    private ExpressionParser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
    }

    public static ExpressionNode Parse(string expression)
    {
        var tokens = ExpressionLexer.Tokenize(expression);
        var parser = new ExpressionParser(tokens);
        var node = parser.ParseExpression();
        parser.Expect(TokenKind.Eof);
        return node;
    }

    private ExpressionNode ParseExpression() => ParseComparison();

    private ExpressionNode ParseComparison()
    {
        var left = ParseAdditive();

        var op = Current.Kind switch
        {
            TokenKind.Eq => BinaryOperator.Equal,
            TokenKind.Neq => BinaryOperator.NotEqual,
            TokenKind.Gt => BinaryOperator.GreaterThan,
            TokenKind.Lt => BinaryOperator.LessThan,
            TokenKind.Gte => BinaryOperator.GreaterThanOrEqual,
            TokenKind.Lte => BinaryOperator.LessThanOrEqual,
            _ => (BinaryOperator?)null,
        };

        if (op is null)
        {
            return left;
        }

        Advance();
        var right = ParseAdditive();
        return new BinaryNode(left, op.Value, right);
    }

    private ExpressionNode ParseAdditive()
    {
        var left = ParseMultiplicative();

        while (true)
        {
            if (Match(TokenKind.Plus))
            {
                left = new BinaryNode(left, BinaryOperator.Add, ParseMultiplicative());
            }
            else if (Match(TokenKind.Minus))
            {
                left = new BinaryNode(left, BinaryOperator.Subtract, ParseMultiplicative());
            }
            else
            {
                return left;
            }
        }
    }

    private ExpressionNode ParseMultiplicative()
    {
        var left = ParseUnary();

        while (true)
        {
            if (Match(TokenKind.Star))
            {
                left = new BinaryNode(left, BinaryOperator.Multiply, ParseUnary());
            }
            else if (Match(TokenKind.Slash))
            {
                left = new BinaryNode(left, BinaryOperator.Divide, ParseUnary());
            }
            else
            {
                return left;
            }
        }
    }

    private ExpressionNode ParseUnary() =>
        Match(TokenKind.Minus) ? new UnaryMinusNode(ParseUnary()) : ParsePrimary();

    private ExpressionNode ParsePrimary()
    {
        var token = Current;

        if (token.Kind == TokenKind.Number)
        {
            Advance();
            return new NumberNode(token.NumberValue);
        }

        if (token.Kind == TokenKind.Identifier)
        {
            Advance();
            if (!Match(TokenKind.LParen))
            {
                return new IdentifierNode(token.Text);
            }

            var args = ParseArgumentList();
            Expect(TokenKind.RParen);
            ValidateFunctionCall(token.Text, args.Count);
            return new FunctionCallNode(token.Text, args);
        }

        if (token.Kind == TokenKind.LParen)
        {
            Advance();
            var inner = ParseExpression();
            Expect(TokenKind.RParen);
            return inner;
        }

        throw new ExpressionParseException(
            token.Kind == TokenKind.Eof ? "Unexpected end of expression." : $"Unexpected token '{token.Text}'.");
    }

    private List<ExpressionNode> ParseArgumentList()
    {
        var args = new List<ExpressionNode>();

        if (Current.Kind == TokenKind.RParen)
        {
            return args;
        }

        args.Add(ParseExpression());
        while (Match(TokenKind.Comma))
        {
            args.Add(ParseExpression());
        }

        return args;
    }

    private static void ValidateFunctionCall(string name, int argCount)
    {
        if (!FunctionArity.TryGetValue(name, out var arity))
        {
            throw new ExpressionParseException($"Unknown function '{name}'.");
        }

        if (argCount < arity.Min || argCount > arity.Max)
        {
            var expected = arity.Min == arity.Max
                ? arity.Min.ToString(CultureInfo.InvariantCulture)
                : $"at least {arity.Min}";
            throw new ExpressionParseException($"'{name}' takes {expected} argument(s), got {argCount}.");
        }
    }

    private Token Current => _tokens[_position];

    private void Advance() => _position++;

    private bool Match(TokenKind kind)
    {
        if (Current.Kind != kind)
        {
            return false;
        }

        Advance();
        return true;
    }

    private void Expect(TokenKind kind)
    {
        if (!Match(kind))
        {
            throw new ExpressionParseException($"Expected '{kind}' but found '{Current.Text}'.");
        }
    }
}
