using System.Text.RegularExpressions;
using HRS.Domain.Notifications;

namespace HRS.Application.Features.Notifications;

/// <summary>FR-NOT-003's placeholder substitution — plain {{key}} tokens, no expression language
/// (that's TemplateVersion's computed-field evaluator, a different concern entirely). An unknown
/// token is left as-is rather than throwing: a stale {{placeholder}} in Secretariat-edited wording
/// shouldn't block delivery of an otherwise-fine email.</summary>
public static partial class NotificationTemplateRenderer
{
    public static (string Subject, string Body) Render(NotificationTemplate template, IReadOnlyDictionary<string, string> placeholders) =>
        (Substitute(template.Subject, placeholders), Substitute(template.Body, placeholders));

    private static string Substitute(string text, IReadOnlyDictionary<string, string> placeholders) =>
        TokenPattern().Replace(text, match => placeholders.GetValueOrDefault(match.Groups[1].Value, match.Value));

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex TokenPattern();
}
