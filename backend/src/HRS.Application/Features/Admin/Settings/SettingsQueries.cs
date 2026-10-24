using System.Text;
using FluentValidation;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Domain.Access;
using HRS.Domain.Admin;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Admin.Settings;

public sealed record OrganizationSettingsDto(
    string Name,
    bool HasLogo,
    string? LogoVersion,
    BrandColor BrandColor,
    string BrandPrimary,
    string BrandDark,
    string TimeZone,
    DateFormatStyle DateFormat,
    int FiscalYearStartMonth,
    DateTimeOffset? UpdatedAt,
    bool CanEdit);

public sealed record TimeZoneOption(string Id, string Label);

internal static class SettingsAccess
{
    /// <summary>SRS §3.2 "Manage system settings, integrations": the system administrator only.</summary>
    public static Task<bool> CanEditAsync(IAccessPolicy accessPolicy, Guid userId, CancellationToken cancellationToken) =>
        accessPolicy.HasRoleAsync(userId, RoleName.SysAdmin, cancellationToken);

    public static readonly Error Forbidden = Error.Forbidden("FORBIDDEN", "Only the system administrator can change settings.");

    public static async Task<OrganizationSettings> LoadOrCreateAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var settings = await db.OrganizationSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = OrganizationSettings.CreateDefault(OrganizationBranding.DefaultName);
            db.OrganizationSettings.Add(settings);
        }

        return settings;
    }

    public static OrganizationSettingsDto ToDto(OrganizationSettings settings, bool canEdit)
    {
        var palette = BrandColors.For(settings.BrandColor);
        return new OrganizationSettingsDto(
            settings.Name,
            settings.LogoStorageKey is not null,
            settings.LogoStorageKey is null ? null : (settings.UpdatedAt ?? DateTimeOffset.UnixEpoch).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            settings.BrandColor,
            palette.Primary,
            palette.Dark,
            settings.TimeZone,
            settings.DateFormat,
            settings.FiscalYearStartMonth,
            settings.UpdatedAt,
            canEdit);
    }
}

/// <summary>FR-ADM-001's settings. Every signed-in user reads them — the app shell shows the
/// name, logo and colour, and dates follow the time zone and format.</summary>
public sealed record GetOrganizationSettingsQuery(Guid UserId) : IRequest<Result<OrganizationSettingsDto>>;

public sealed class GetOrganizationSettingsQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<GetOrganizationSettingsQuery, Result<OrganizationSettingsDto>>
{
    public async ValueTask<Result<OrganizationSettingsDto>> Handle(GetOrganizationSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await db.OrganizationSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? OrganizationSettings.CreateDefault(OrganizationBranding.DefaultName);
        return Result.Success(SettingsAccess.ToDto(settings, await SettingsAccess.CanEditAsync(accessPolicy, request.UserId, cancellationToken)));
    }
}

/// <summary>The time zones the settings page offers, by IANA id with their current UTC offset.</summary>
public sealed record GetTimeZoneOptionsQuery : IRequest<Result<IReadOnlyList<TimeZoneOption>>>;

public sealed class GetTimeZoneOptionsQueryHandler(IClock clock) : IRequestHandler<GetTimeZoneOptionsQuery, Result<IReadOnlyList<TimeZoneOption>>>
{
    public ValueTask<Result<IReadOnlyList<TimeZoneOption>>> Handle(GetTimeZoneOptionsQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var options = TimeZoneInfo.GetSystemTimeZones()
            .Select(zone => zone.HasIanaId ? zone.Id : TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : null)
            .Where(id => id is not null && id.Contains('/', StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Select(id => new TimeZoneOption(id!, $"{id} ({OrganizationBranding.OffsetLabel(OrganizationBranding.Resolve(id!), now)})"))
            .OrderBy(o => o.Id, StringComparer.Ordinal)
            .ToList();
        return ValueTask.FromResult(Result.Success<IReadOnlyList<TimeZoneOption>>(options));
    }
}

public sealed record UpdateOrganizationSettingsCommand(
    Guid UserId, string Name, BrandColor BrandColor, string TimeZone, DateFormatStyle DateFormat, int FiscalYearStartMonth)
    : IRequest<Result<OrganizationSettingsDto>>;

public sealed class UpdateOrganizationSettingsCommandValidator : AbstractValidator<UpdateOrganizationSettingsCommand>
{
    public UpdateOrganizationSettingsCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(OrganizationSettings.MaxNameLength);
        RuleFor(x => x.BrandColor).IsInEnum();
        RuleFor(x => x.DateFormat).IsInEnum();
        RuleFor(x => x.FiscalYearStartMonth).InclusiveBetween(1, 12);
        RuleFor(x => x.TimeZone)
            .Must(id => id is not null && id.Contains('/', StringComparison.Ordinal) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _))
            .WithMessage("Choose a time zone from the list.");
    }
}

/// <summary>FR-ADM-001: changes the settings. System administrator only; audited with before/after.</summary>
public sealed class UpdateOrganizationSettingsCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IAuditLog auditLog)
    : IRequestHandler<UpdateOrganizationSettingsCommand, Result<OrganizationSettingsDto>>
{
    public async ValueTask<Result<OrganizationSettingsDto>> Handle(UpdateOrganizationSettingsCommand request, CancellationToken cancellationToken)
    {
        if (!await SettingsAccess.CanEditAsync(accessPolicy, request.UserId, cancellationToken))
        {
            return Result.Failure<OrganizationSettingsDto>(SettingsAccess.Forbidden);
        }

        var settings = await SettingsAccess.LoadOrCreateAsync(db, cancellationToken);
        var before = Snapshot(settings);
        settings.Update(request.Name, request.BrandColor, request.TimeZone, request.DateFormat, request.FiscalYearStartMonth, request.UserId, clock.UtcNow);
        auditLog.Add(new AuditEntry(AuditActions.SettingsUpdated, request.UserId, "settings", settings.Id.ToString(), "Changed organization settings",
            Before: before, After: Snapshot(settings)));
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(SettingsAccess.ToDto(settings, canEdit: true));
    }

    private static object Snapshot(OrganizationSettings s) =>
        new { s.Name, BrandColor = s.BrandColor.ToString(), s.TimeZone, DateFormat = s.DateFormat.ToString(), s.FiscalYearStartMonth };
}

/// <summary>FR-ADM-001's logo: PNG or SVG, at most MaxBytes. The bytes are checked, not the name or
/// declared type. Replacing a logo removes the old file.</summary>
public sealed record UploadLogoCommand(Guid UserId, byte[] Content) : IRequest<Result<OrganizationSettingsDto>>;

public sealed class UploadLogoCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IFileStorage fileStorage, IClock clock, IAuditLog auditLog)
    : IRequestHandler<UploadLogoCommand, Result<OrganizationSettingsDto>>
{
    public const int MaxBytes = 512 * 1024;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public async ValueTask<Result<OrganizationSettingsDto>> Handle(UploadLogoCommand request, CancellationToken cancellationToken)
    {
        if (!await SettingsAccess.CanEditAsync(accessPolicy, request.UserId, cancellationToken))
        {
            return Result.Failure<OrganizationSettingsDto>(SettingsAccess.Forbidden);
        }

        if (request.Content.Length is 0 or > MaxBytes)
        {
            return Result.Failure<OrganizationSettingsDto>(Error.Validation("LOGO_TOO_LARGE", "The logo must be at most 512 KB."));
        }

        var contentType = DetectContentType(request.Content);
        if (contentType is null)
        {
            return Result.Failure<OrganizationSettingsDto>(Error.Validation("LOGO_TYPE", "The logo must be a PNG or SVG image."));
        }

        var settings = await SettingsAccess.LoadOrCreateAsync(db, cancellationToken);
        var oldKey = settings.LogoStorageKey;
        var key = $"branding/logo-{Guid.CreateVersion7():N}{(contentType == "image/png" ? ".png" : ".svg")}";
        await fileStorage.UploadAsync(key, request.Content, contentType, cancellationToken);

        settings.SetLogo(key, contentType, request.UserId, clock.UtcNow);
        auditLog.Add(new AuditEntry(AuditActions.SettingsUpdated, request.UserId, "settings", settings.Id.ToString(), "Uploaded a new logo",
            After: new { Logo = contentType, Bytes = request.Content.Length }));
        await db.SaveChangesAsync(cancellationToken);

        if (oldKey is not null)
        {
            await fileStorage.DeleteAsync(oldKey, cancellationToken);
        }

        return Result.Success(SettingsAccess.ToDto(settings, canEdit: true));
    }

    public static string? DetectContentType(byte[] content)
    {
        if (content.AsSpan().StartsWith(PngSignature))
        {
            return "image/png";
        }

        var head = Encoding.UTF8.GetString(content, 0, Math.Min(content.Length, 1024)).TrimStart('﻿', ' ', '\t', '\r', '\n');
        return (head.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)
                || (head.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && head.Contains("<svg", StringComparison.OrdinalIgnoreCase)))
            ? "image/svg+xml"
            : null;
    }
}

public sealed record RemoveLogoCommand(Guid UserId) : IRequest<Result<OrganizationSettingsDto>>;

public sealed class RemoveLogoCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IFileStorage fileStorage, IClock clock, IAuditLog auditLog)
    : IRequestHandler<RemoveLogoCommand, Result<OrganizationSettingsDto>>
{
    public async ValueTask<Result<OrganizationSettingsDto>> Handle(RemoveLogoCommand request, CancellationToken cancellationToken)
    {
        if (!await SettingsAccess.CanEditAsync(accessPolicy, request.UserId, cancellationToken))
        {
            return Result.Failure<OrganizationSettingsDto>(SettingsAccess.Forbidden);
        }

        var settings = await SettingsAccess.LoadOrCreateAsync(db, cancellationToken);
        var oldKey = settings.LogoStorageKey;
        settings.RemoveLogo(request.UserId, clock.UtcNow);
        auditLog.Add(new AuditEntry(AuditActions.SettingsUpdated, request.UserId, "settings", settings.Id.ToString(), "Removed the logo"));
        await db.SaveChangesAsync(cancellationToken);

        if (oldKey is not null)
        {
            await fileStorage.DeleteAsync(oldKey, cancellationToken);
        }

        return Result.Success(SettingsAccess.ToDto(settings, canEdit: true));
    }
}

public sealed record LogoFile(byte[] Content, string ContentType);

/// <summary>The logo's bytes, for the app shell. Null result when no logo is set.</summary>
public sealed record GetLogoQuery : IRequest<Result<LogoFile>>;

public sealed class GetLogoQueryHandler(IOrganizationBrandingProvider brandingProvider) : IRequestHandler<GetLogoQuery, Result<LogoFile>>
{
    public async ValueTask<Result<LogoFile>> Handle(GetLogoQuery request, CancellationToken cancellationToken)
    {
        var branding = await brandingProvider.GetAsync(cancellationToken, includeLogo: true);
        return branding is { Logo: { } bytes, LogoContentType: { } type }
            ? Result.Success(new LogoFile(bytes, type))
            : Result.Failure<LogoFile>(Error.NotFound("NO_LOGO", "No logo has been uploaded."));
    }
}
