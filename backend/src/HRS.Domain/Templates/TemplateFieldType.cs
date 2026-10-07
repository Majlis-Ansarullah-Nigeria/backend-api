namespace HRS.Domain.Templates;

/// <summary>The field-type catalog (FR-TPL-002).</summary>
public enum TemplateFieldType
{
    ShortText,
    LongText,
    Number,
    Currency,
    Percentage,
    Date,
    DateRange,
    YesNo,
    SingleChoice,
    MultipleChoice,
    Dropdown,
    Rating,
    FileUpload,
    RepeatableTable,
    MemberList,
    Location,
    Computed,
}
