using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.ReportTypes;

public record ReportTypeDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string Code { get; init; } = default!;
    public string? Description { get; init; }
    public string Frequency { get; init; } = default!;
    public bool IsActive { get; init; }
    public bool CodeLocked { get; init; }
    public int TemplateCount { get; init; }
}

public record SaveReportTypeRequest
{
    public string Name { get; init; } = default!;
    public string Code { get; init; } = default!;
    public string? Description { get; init; }
    public ReportFrequency Frequency { get; init; } = ReportFrequency.Monthly;
    public bool IsActive { get; init; } = true;
}

public record GetReportTypesQuery : IRequest<List<ReportTypeDto>>;

public record CreateReportTypeCommand(SaveReportTypeRequest Request) : IRequest<Result<Guid>>;

public record UpdateReportTypeCommand(Guid Id, SaveReportTypeRequest Request) : IRequest<Result>;

/// <summary>Links a template to a report type, so its reports use the type's code.</summary>
public record AssignTemplateReportTypeCommand(Guid TemplateId, Guid ReportTypeId) : IRequest<Result>;

public class SaveReportTypeRequestValidator : AbstractValidator<SaveReportTypeRequest>
{
    public SaveReportTypeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(6);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class CreateReportTypeCommandValidator : AbstractValidator<CreateReportTypeCommand>
{
    public CreateReportTypeCommandValidator() => RuleFor(x => x.Request).SetValidator(new SaveReportTypeRequestValidator());
}

public class UpdateReportTypeCommandValidator : AbstractValidator<UpdateReportTypeCommand>
{
    public UpdateReportTypeCommandValidator() => RuleFor(x => x.Request).SetValidator(new SaveReportTypeRequestValidator());
}

public class ReportTypeHandlers :
    IRequestHandler<GetReportTypesQuery, List<ReportTypeDto>>,
    IRequestHandler<CreateReportTypeCommand, Result<Guid>>,
    IRequestHandler<UpdateReportTypeCommand, Result>,
    IRequestHandler<AssignTemplateReportTypeCommand, Result>
{
    private const string Forbidden = "FORBIDDEN: Only the National Secretariat can manage report types";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ReportTypeHandlers(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<ReportTypeDto>> Handle(GetReportTypesQuery request, CancellationToken cancellationToken)
    {
        var types = await _context.ReportTypes.AsNoTracking().OrderBy(t => t.Name).ToListAsync(cancellationToken);
        var templateCounts = await _context.ReportTemplates
            .Where(t => t.ReportTypeId != null)
            .GroupBy(t => t.ReportTypeId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var locked = await LockedTypeIdsAsync(cancellationToken);

        return types.Select(t => new ReportTypeDto
        {
            Id = t.Id,
            Name = t.Name,
            Code = t.Code,
            Description = t.Description,
            Frequency = t.Frequency.ToString(),
            IsActive = t.IsActive,
            CodeLocked = locked.Contains(t.Id),
            TemplateCount = templateCounts.GetValueOrDefault(t.Id)
        }).ToList();
    }

    public async Task<Result<Guid>> Handle(CreateReportTypeCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result<Guid>.Failure(Forbidden);
        }

        try
        {
            var r = request.Request;
            var type = new ReportType(r.Name, r.Code, r.Frequency, r.Description);
            if (await _context.ReportTypes.AnyAsync(t => t.Code == type.Code, cancellationToken))
            {
                return Result<Guid>.Failure($"The code {type.Code} is already used by another report type");
            }

            if (!r.IsActive)
            {
                type.Deactivate();
            }

            _context.ReportTypes.Add(type);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(type.Id, "Report type created");
        }
        catch (ArgumentException ex)
        {
            return Result<Guid>.Failure(ex.Message);
        }
    }

    public async Task<Result> Handle(UpdateReportTypeCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result.Failure(Forbidden);
        }

        var type = await _context.ReportTypes.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (type is null)
        {
            return Result.Failure("Report type not found");
        }

        try
        {
            var r = request.Request;
            var code = ReportType.NormalizeCode(r.Code);
            if (await _context.ReportTypes.AnyAsync(t => t.Id != type.Id && t.Code == code, cancellationToken))
            {
                return Result.Failure($"The code {code} is already used by another report type");
            }

            var locked = (await LockedTypeIdsAsync(cancellationToken)).Contains(type.Id);
            type.Update(r.Name, code, r.Frequency, r.Description, locked);
            if (r.IsActive) type.Activate(); else type.Deactivate();

            // Keep the templates' display name in step with the type
            var templates = await _context.ReportTemplates.Where(t => t.ReportTypeId == type.Id).ToListAsync(cancellationToken);
            templates.ForEach(t => t.AssignReportType(type));

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success("Report type updated");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> Handle(AssignTemplateReportTypeCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result.Failure(Forbidden);
        }

        var template = await _context.ReportTemplates.FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);
        var type = await _context.ReportTypes.FirstOrDefaultAsync(t => t.Id == request.ReportTypeId, cancellationToken);
        if (template is null || type is null)
        {
            return Result.Failure("Template or report type not found");
        }

        var hasSubmitted = await _context.ReportSubmissions
            .AnyAsync(s => s.ReportTemplateId == template.Id && s.ReferenceNumber != null, cancellationToken);
        if (hasSubmitted && template.ReportTypeId != type.Id)
        {
            return Result.Failure("Reports have already been submitted with this template, so its report type cannot change");
        }

        template.AssignReportType(type);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success("Report type set");
    }

    // A type's code is locked once any report of that type has a reference number.
    private async Task<HashSet<Guid>> LockedTypeIdsAsync(CancellationToken cancellationToken) =>
        (await _context.ReportSubmissions
            .Where(s => s.ReferenceNumber != null && s.ReportTemplate.ReportTypeId != null)
            .Select(s => s.ReportTemplate.ReportTypeId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken))
        .ToHashSet();
}
