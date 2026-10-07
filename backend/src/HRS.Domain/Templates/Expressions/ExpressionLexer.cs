using System.Globalization;

namespace HRS.Domain.Templates.Expressions;

internal enum TokenKind
{
    Number,
    Identifier,
    Plus,
    Minus,
    Star,
    Slash,
    LParen,
    RParen,
    Comma,
    Eq,
    Neq,
    Gt,
    Lt,
    Gte,
    Lte,
    Eof,
}

internal readonly record struct Token(TokenKind Kind, string Text, double NumberValue = 0);

/// <summary>Turns expression text into tokens. Identifiers use the same character class as a
/// field's stable key (TemplateSchemaValidator.IsStableKey) — lowercase start, then lowercase
/// letters/digits/underscore — so a field reference and a function name are lexically identical;
/// the parser tells them apart by whether a '(' follows.</summary>
internal static class ExpressionLexer
{
    public static List<Token> Tokenize(string expression)
    {
        var tokens = new List<Token>();
        var i = 0;

        while (i < expression.Length)
        {
            var c = expression[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            switch (c)
            {
                case '+':
                    tokens.Add(new Token(TokenKind.Plus, "+"));
                    i++;
                    continue;
                case '-':
                    tokens.Add(new Token(TokenKind.Minus, "-"));
                    i++;
                    continue;
                case '*':
                    tokens.Add(new Token(TokenKind.Star, "*"));
                    i++;
                    continue;
                case '/':
                    tokens.Add(new Token(TokenKind.Slash, "/"));
                    i++;
                    continue;
                case '(':
                    tokens.Add(new Token(TokenKind.LParen, "("));
                    i++;
                    continue;
                case ')':
                    tokens.Add(new Token(TokenKind.RParen, ")"));
                    i++;
                    continue;
                case ',':
                    tokens.Add(new Token(TokenKind.Comma, ","));
                    i++;
                    continue;
                case '=':
                    tokens.Add(new Token(TokenKind.Eq, "="));
                    i++;
                    continue;
                case '!' when i + 1 < expression.Length && expression[i + 1] == '=':
                    tokens.Add(new Token(TokenKind.Neq, "!="));
                    i += 2;
                    continue;
                case '>' when i + 1 < expression.Length && expression[i + 1] == '=':
                    tokens.Add(new Token(TokenKind.Gte, ">="));
                    i += 2;
                    continue;
                case '<' when i + 1 < expression.Length && expression[i + 1] == '=':
                    tokens.Add(new Token(TokenKind.Lte, "<="));
                    i += 2;
                    continue;
                case '>':
                    tokens.Add(new Token(TokenKind.Gt, ">"));
                    i++;
                    continue;
                case '<':
                    tokens.Add(new Token(TokenKind.Lt, "<"));
                    i++;
                    continue;
            }

            if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < expression.Length && char.IsAsciiDigit(expression[i + 1])))
            {
                var start = i;
                while (i < expression.Length && (char.IsAsciiDigit(expression[i]) || expression[i] == '.'))
                {
                    i++;
                }

                var text = expression[start..i];
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    throw new ExpressionParseException($"'{text}' is not a valid number.");
                }

                tokens.Add(new Token(TokenKind.Number, text, value));
                continue;
            }

            if (char.IsAsciiLetterLower(c))
            {
                var start = i;
                while (i < expression.Length && (char.IsAsciiLetterLower(expression[i]) || char.IsAsciiDigit(expression[i]) || expression[i] == '_'))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Identifier, expression[start..i]));
                continue;
            }

            throw new ExpressionParseException($"Unexpected character '{c}'.");
        }

        tokens.Add(new Token(TokenKind.Eof, string.Empty));
        return tokens;
    }
}
