using System.Text.RegularExpressions;
using FluentValidation;
using HRS.Domain.Templates;
using HRS.Domain.Templates.Expressions;

namespace HRS.Application.Features.Templates.TemplateVersions;

/// <summary>Structural validation only — does the schema have a well-formed shape (unique
/// keys, the right companion fields for each type). A computed field's expression is parsed
/// (via ExpressionEvaluator, M3.3) to catch syntax errors at save time, but never evaluated
/// against real answers here. showIf's trigger and aggregation roll-ups are never evaluated
/// either; that's M3.4's job against this same shape.</summary>
public sealed class TemplateSchemaValidator : AbstractValidator<TemplateSchema>
{
    private static readonly Regex StableKeyPattern = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    public TemplateSchemaValidator()
    {
        RuleFor(x => x.Sections).NotNull();
        RuleForEach(x => x.Sections).SetValidator(new TemplateSectionValidator());

        RuleFor(x => x.Sections)
            .Must(sections => sections.Select(s => s.Key).Distinct().Count() == sections.Count)
            .WithMessage("Section keys must be unique.")
            .When(x => x.Sections is not null);

        RuleFor(x => x.Sections)
            .Custom((sections, context) =>
            {
                if (sections is null)
                {
                    return;
                }

                var allFields = sections.SelectMany(s => s.Fields ?? []).ToList();
                var keys = allFields.Select(f => f.Key).ToList();

                if (keys.Distinct().Count() != keys.Count)
                {
                    context.AddFailure("Field keys must be unique across the whole schema.");
                }

                var keySet = keys.ToHashSet();
                foreach (var field in allFields.Where(f => f.ShowIf is not null))
                {
                    var targetKey = field.ShowIf!.FieldKey;
                    if (targetKey == field.Key)
                    {
                        context.AddFailure($"Field '{field.Key}' can't show itself based on its own answer.");
                    }
                    else if (!keySet.Contains(targetKey))
                    {
                        context.AddFailure($"Field '{field.Key}' has a showIf referencing unknown field '{targetKey}'.");
                    }
                }
            });
    }

    internal static bool IsStableKey(string key) => StableKeyPattern.IsMatch(key);
}

public sealed class TemplateSectionValidator : AbstractValidator<TemplateSection>
{
    public TemplateSectionValidator()
    {
        RuleFor(x => x.Key).NotEmpty().Must(TemplateSchemaValidator.IsStableKey)
            .WithMessage("Section key must start with a lowercase letter and contain only lowercase letters, digits and underscores.");
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.Fields).NotNull();
        RuleForEach(x => x.Fields).SetValidator(new TemplateFieldValidator());
    }
}

public sealed class TemplateFieldValidator : AbstractValidator<TemplateField>
{
    private static readonly TemplateFieldType[] NumericTypes =
        [TemplateFieldType.Number, TemplateFieldType.Currency, TemplateFieldType.Percentage];

    private static readonly TemplateFieldType[] ChoiceTypes =
        [TemplateFieldType.SingleChoice, TemplateFieldType.MultipleChoice, TemplateFieldType.Dropdown];

    // A parse-only check: field values don't matter here, only whether the expression is
    // syntactically well-formed (ExpressionEvaluator never fails on missing/empty field data).
    private static readonly IReadOnlyDictionary<string, object?> EmptyFieldValues =
        new Dictionary<string, object?>();

    public TemplateFieldValidator()
    {
        RuleFor(x => x.Key).NotEmpty().Must(TemplateSchemaValidator.IsStableKey)
            .WithMessage("Field key must start with a lowercase letter and contain only lowercase letters, digits and underscores.");
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();

        RuleFor(x => x.Min)
            .LessThanOrEqualTo(x => x.Max!.Value)
            .WithMessage("Min must not be greater than Max.")
            .When(x => x.Min.HasValue && x.Max.HasValue);

        RuleFor(x => x.Regex)
            .Must(IsValidRegex)
            .WithMessage("Regex is not a valid pattern.")
            .When(x => x.Regex is not null);

        RuleFor(x => x.Aggregation).IsInEnum();
        RuleFor(x => x.Aggregation)
            .Must((field, aggregation) => aggregation == AggregationRule.None || NumericTypes.Contains(field.Type))
            .WithMessage("Aggregation only applies to Number, Currency or Percentage fields.");

        RuleForEach(x => x.VisibleForLevels).IsInEnum();

        RuleFor(x => x.Options)
            .NotEmpty()
            .WithMessage("Choice fields need at least one option.")
            .When(x => ChoiceTypes.Contains(x.Type));
        RuleFor(x => x.Options)
            .Empty()
            .WithMessage("Only choice fields may have options.")
            .When(x => !ChoiceTypes.Contains(x.Type));
        RuleForEach(x => x.Options).SetValidator(new TemplateFieldOptionValidator());
        RuleFor(x => x.Options)
            .Must(options => options is null || options.Select(o => o.Value).Distinct().Count() == options.Count)
            .WithMessage("Option values must be unique within the field.")
            .When(x => x.Options is not null);

        RuleFor(x => x.Columns)
            .NotEmpty()
            .WithMessage("A repeatable table needs at least one column.")
            .When(x => x.Type == TemplateFieldType.RepeatableTable);
        RuleFor(x => x.Columns)
            .Empty()
            .WithMessage("Only repeatable-table fields may have columns.")
            .When(x => x.Type != TemplateFieldType.RepeatableTable);
        // Recursive self-reference (not `new TemplateFieldValidator()`, which would recurse
        // infinitely at construction time, before any instance exists to break the cycle).
        RuleForEach(x => x.Columns).SetValidator(this);
        RuleFor(x => x.Columns)
            .Must(columns => columns is null || columns.All(c => c.Type != TemplateFieldType.RepeatableTable))
            .WithMessage("A repeatable table's columns can't themselves be repeatable tables.")
            .When(x => x.Columns is not null);
        RuleFor(x => x.Columns)
            .Must(columns => columns is null || columns.Select(c => c.Key).Distinct().Count() == columns.Count)
            .WithMessage("Column keys must be unique within the table.")
            .When(x => x.Columns is not null);

        RuleFor(x => x.ComputedExpression)
            .NotEmpty()
            .WithMessage("A computed field needs an expression.")
            .When(x => x.Type == TemplateFieldType.Computed);
        RuleFor(x => x.ComputedExpression)
            .Must(expression => ExpressionEvaluator.Evaluate(expression!, EmptyFieldValues).IsSuccess)
            .WithMessage("Expression is not valid.")
            .When(x => x.Type == TemplateFieldType.Computed && !string.IsNullOrEmpty(x.ComputedExpression));
        RuleFor(x => x.ComputedExpression)
            .Empty()
            .WithMessage("Only computed fields may have an expression.")
            .When(x => x.Type != TemplateFieldType.Computed);
    }

    private static bool IsValidRegex(string? pattern)
    {
        if (pattern is null)
        {
            return true;
        }

        try
        {
            _ = new Regex(pattern);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

public sealed class TemplateFieldOptionValidator : AbstractValidator<TemplateFieldOption>
{
    public TemplateFieldOptionValidator()
    {
        RuleFor(x => x.Value).NotEmpty();
        RuleFor(x => x.Label).NotEmpty();
    }
}
