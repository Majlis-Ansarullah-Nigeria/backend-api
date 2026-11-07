using System.Net.Mail;
using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Domain.Entities;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;
using ManagementApi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Profile;

// My profile: the signed-in member's own record (private fields included), their account,
// password and email changes, and "report a mistake" which goes to their Muqam head.

public record PositionDto(string Title, string Level, string? UnitName);

public record MyProfileDto
{
    public string ChandaNo { get; init; } = default!;
    public string FullName { get; init; } = default!;
    public string? Title { get; init; }
    public string? Surname { get; init; }
    public string? FirstName { get; init; }
    public string? MiddleName { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string? PhoneNo { get; init; }
    public string? Address { get; init; }
    public string? MaritalStatus { get; init; }
    public string? PhotoUrl { get; init; }
    public string? JamaatName { get; init; }
    public string? MuqamName { get; init; }
    public string? DilaName { get; init; }
    public string? ZoneName { get; init; }
    public DateTime? RecordUpdatedAt { get; init; }
    public bool HasMemberRecord { get; init; }
    public List<PositionDto> Positions { get; init; } = new();
    public string UserName { get; init; } = default!;
    public string? Email { get; init; }
    public List<string> Roles { get; init; } = new();
    public string? PendingEmail { get; init; }
    public DateTime? PendingEmailRequestedAt { get; init; }
}

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ChangeMyEmailRequest(string NewEmail, string Password);
public record ReportMistakeRequest(string Field, string Correction);

public record GetMyProfileQuery : IRequest<Result<MyProfileDto>>;
public record ChangeMyPasswordCommand(string CurrentPassword, string NewPassword) : IRequest<Result>;
public record ChangeMyEmailCommand(string NewEmail, string Password) : IRequest<Result>;
public record CancelMyEmailChangeCommand : IRequest<Result>;
public record ReportRecordMistakeCommand(string Field, string Correction) : IRequest<Result>;

public class ChangeMyPasswordCommandValidator : AbstractValidator<ChangeMyPasswordCommand>
{
    public ChangeMyPasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Enter your current password");
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(3).WithMessage("Use at least 3 characters");
    }
}

public class ChangeMyEmailCommandValidator : AbstractValidator<ChangeMyEmailCommand>
{
    public ChangeMyEmailCommandValidator()
    {
        RuleFor(x => x.NewEmail).NotEmpty().MaximumLength(256)
            .Must(e => MailAddress.TryCreate(e, out _)).WithMessage("Enter a valid email address");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Enter your password");
    }
}

public class ReportRecordMistakeCommandValidator : AbstractValidator<ReportRecordMistakeCommand>
{
    public ReportRecordMistakeCommandValidator()
    {
        RuleFor(x => x.Field).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Correction).NotEmpty().WithMessage("Say what the correct value is").MaximumLength(1000);
    }
}

public static class MemberPositions
{
    public static async Task<Dictionary<Guid, List<PositionDto>>> ForMembersAsync(
        IApplicationDbContext context, IReadOnlyCollection<Guid> memberIds, CancellationToken ct)
    {
        var positions = await context.MemberPositions.AsNoTracking()
            .Where(p => memberIds.Contains(p.MemberId) && p.IsActive)
            .Select(p => new { p.MemberId, p.PositionTitle, p.OrganizationLevel, p.OrganizationEntityId })
            .ToListAsync(ct);
        var ids = positions.Where(p => p.OrganizationEntityId.HasValue).Select(p => p.OrganizationEntityId!.Value).Distinct().ToList();
        var names = new Dictionary<Guid, string>();
        if (ids.Count > 0)
        {
            foreach (var m in await context.Muqams.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct)) names[m.Id] = m.Name;
            foreach (var d in await context.Dilas.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct)) names[d.Id] = d.Name;
            foreach (var z in await context.Zones.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct)) names[z.Id] = z.Name;
        }

        return positions
            .GroupBy(p => p.MemberId)
            .ToDictionary(g => g.Key, g => g.Select(p => new PositionDto(
                p.PositionTitle,
                p.OrganizationLevel.ToString(),
                p.OrganizationLevel == OrganizationLevel.National ? "National"
                    : p.OrganizationEntityId is { } id && names.TryGetValue(id, out var n) ? n : null)).ToList());
    }

    public static string FullName(Member m) =>
        string.Join(' ', new[] { m.Title, m.FirstName, m.MiddleName, m.Surname }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public class MyProfileHandlers :
    IRequestHandler<GetMyProfileQuery, Result<MyProfileDto>>,
    IRequestHandler<ChangeMyPasswordCommand, Result>,
    IRequestHandler<ChangeMyEmailCommand, Result>,
    IRequestHandler<CancelMyEmailChangeCommand, Result>,
    IRequestHandler<ReportRecordMistakeCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAccountRecoveryService _accounts;
    private readonly INotificationService _notifications;
    private readonly TimeProvider _clock;

    public MyProfileHandlers(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IAccountRecoveryService accounts,
        INotificationService notifications,
        TimeProvider clock)
    {
        _context = context;
        _currentUser = currentUser;
        _accounts = accounts;
        _notifications = notifications;
        _clock = clock;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private async Task<ApplicationUser?> CurrentUserAsync(CancellationToken ct) =>
        await _context.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);

    private async Task<Member?> MemberOfAsync(ApplicationUser user, CancellationToken ct)
    {
        var query = _context.Members.AsNoTracking().Include(m => m.Muqam).ThenInclude(q => q!.Dila).ThenInclude(d => d!.Zone);
        if (user.MemberId is { } memberId)
        {
            return await query.FirstOrDefaultAsync(m => m.Id == memberId, ct);
        }

        return string.IsNullOrEmpty(user.ChandaNo) ? null : await query.FirstOrDefaultAsync(m => m.ChandaNo == user.ChandaNo, ct);
    }

    public async Task<Result<MyProfileDto>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Result<MyProfileDto>.Failure("Account not found");
        }

        var member = await MemberOfAsync(user, cancellationToken);
        var pending = await _context.EmailChangeRequests.AsNoTracking()
            .Where(e => e.UserId == user.Id && e.Status == EmailChangeRequestStatus.Pending)
            .OrderByDescending(e => e.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var roles = await (from ur in _context.UserRoles
                           join r in _context.Roles on ur.RoleId equals r.Id
                           where ur.UserId == user.Id
                           select r.Name!).ToListAsync(cancellationToken);

        var positions = member is null
            ? new List<PositionDto>()
            : (await MemberPositions.ForMembersAsync(_context, new[] { member.Id }, cancellationToken)).GetValueOrDefault(member.Id) ?? new();
        string? jamaatName = null;
        if (member?.JamaatId is { } jamaatId)
        {
            jamaatName = await _context.Jamaats.AsNoTracking().Where(j => j.JamaatId == jamaatId).Select(j => j.Name).FirstOrDefaultAsync(cancellationToken);
        }

        return Result<MyProfileDto>.Success(new MyProfileDto
        {
            ChandaNo = member?.ChandaNo ?? user.ChandaNo ?? string.Empty,
            FullName = member is null ? $"{user.FirstName} {user.LastName}".Trim() : MemberPositions.FullName(member),
            Title = member?.Title,
            Surname = member?.Surname ?? user.LastName,
            FirstName = member?.FirstName ?? user.FirstName,
            MiddleName = member?.MiddleName,
            DateOfBirth = member?.DateOfBirth,
            PhoneNo = member?.PhoneNo ?? user.PhoneNumber,
            Address = member?.Address,
            MaritalStatus = member?.MaritalStatus,
            PhotoUrl = member?.PhotoUrl ?? user.ImageUrl,
            JamaatName = jamaatName,
            MuqamName = member?.Muqam?.Name,
            DilaName = member?.Muqam?.Dila?.Name,
            ZoneName = member?.Muqam?.Dila?.Zone?.Name,
            RecordUpdatedAt = member?.LastModifiedOn ?? member?.CreatedOn,
            HasMemberRecord = member is not null,
            Positions = positions,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            Roles = roles,
            PendingEmail = pending?.RequestedEmail,
            PendingEmailRequestedAt = pending?.RequestedAt,
        });
    }

    public Task<Result> Handle(ChangeMyPasswordCommand request, CancellationToken cancellationToken) =>
        _accounts.ChangePasswordAsync(_currentUser.UserId, request.CurrentPassword, request.NewPassword);

    public async Task<Result> Handle(ChangeMyEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Result.Failure("Account not found");
        }

        if (!await _accounts.CheckPasswordAsync(user.Id, request.Password))
        {
            return Result.Failure("WRONG_PASSWORD: Your password is wrong.");
        }

        if (string.Equals(user.Email, request.NewEmail.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("This is already the email on your account.");
        }

        var pending = await _context.EmailChangeRequests
            .Where(e => e.UserId == user.Id && e.Status == EmailChangeRequestStatus.Pending)
            .ToListAsync(cancellationToken);
        pending.ForEach(p => p.Supersede(Now));

        // The Secretariat approves every email change before it takes effect.
        _context.EmailChangeRequests.Add(new EmailChangeRequest(
            user.Id, user.ChandaNo ?? string.Empty, user.Email, request.NewEmail, user.PhoneNumber, "Requested from My profile", Now));
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success("Request sent. The National Secretariat will review it.");
    }

    public async Task<Result> Handle(CancelMyEmailChangeCommand request, CancellationToken cancellationToken)
    {
        var pending = await _context.EmailChangeRequests
            .Where(e => e.UserId == _currentUser.UserId && e.Status == EmailChangeRequestStatus.Pending)
            .ToListAsync(cancellationToken);
        if (pending.Count == 0)
        {
            return Result.Failure("You have no pending email change.");
        }

        pending.ForEach(p => p.Cancel(Now));
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success("Email change cancelled.");
    }

    public async Task<Result> Handle(ReportRecordMistakeCommand request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Result.Failure("Account not found");
        }

        var member = await MemberOfAsync(user, cancellationToken);
        var muqamId = member?.MuqamId ?? user.MuqamId;
        var name = member is null ? $"{user.FirstName} {user.LastName}".Trim() : MemberPositions.FullName(member);

        // The Muqam head keeps the record; when the Muqam has no head account, the National admins get it.
        var recipients = muqamId is null
            ? new List<ApplicationUser>()
            : await _context.Users.AsNoTracking()
                .Where(u => u.IsActive && u.MuqamId == muqamId && u.OrganizationLevel == OrganizationLevel.Muqam && u.Id != user.Id)
                .ToListAsync(cancellationToken);
        if (recipients.Count == 0)
        {
            recipients = await (from u in _context.Users
                                join ur in _context.UserRoles on u.Id equals ur.UserId
                                join r in _context.Roles on ur.RoleId equals r.Id
                                where u.IsActive && r.Name == Shared.Authorization.Roles.NationalAdmin
                                select u).Distinct().ToListAsync(cancellationToken);
        }

        var message = $"{name} ({member?.ChandaNo ?? user.ChandaNo}) says their {request.Field} is wrong. Correct value: {request.Correction.Trim()}";
        foreach (var r in recipients)
        {
            await _notifications.SendNotificationAsync(
                NotificationType.RecordCorrection, r.Id, $"{r.FirstName} {r.LastName}".Trim(),
                "Membership record correction", message, NotificationPriority.Normal,
                member?.Id, "Member", cancellationToken);
        }

        return Result.Success("Sent. Your Muqam head will check and update your record.");
    }
}
