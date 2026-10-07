using FluentValidation;

namespace HRS.Application.Features.Templates.TemplateVersions;

public sealed class UpdateTemplateSchemaCommandValidator : AbstractValidator<UpdateTemplateSchemaCommand>
{
    public UpdateTemplateSchemaCommandValidator()
    {
        RuleFor(x => x.ReportTypeId).NotEmpty();
        RuleFor(x => x.Schema).Cascade(CascadeMode.Stop).NotNull().SetValidator(new TemplateSchemaValidator());
    }
}
