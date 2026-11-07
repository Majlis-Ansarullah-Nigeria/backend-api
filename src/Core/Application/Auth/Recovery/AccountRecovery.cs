using System.Net.Mail;
using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ManagementApi.Application.Auth.Recovery;

// Forgot password:
// 1. Lookup: the member enters their membership number and sees their email masked (or none).
// 2a. Send the reset link to that email, or
// 2b. ask for a new email; the National Secretariat approves it, then the link goes to the new email.
// 3. Reset: the link opens the reset page, which sets the new password.
// Anonymous steps never reveal whether a membership number exists.

public record ForgotPasswordLookupRequest(string ChandaNo);
public record ForgotPasswordLookupResult(bool HasEmail, string? MaskedEmail);
/// <param name="Identifier">The email address on the account, or the Membership Number (ChandaNo).</param>
public record ForgotPasswordRequest(string Identifier);
public record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);
public record RequestEmailChangeRequest(string ChandaNo, string NewEmail, string? PhoneNumber, string? Note);
public record RejectEmailChangeRequest(string? Reason);

public record EmailChangeRequestDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string MemberName { get; init; } = default!;
    public string ChandaNo { get; init; } = default!;
    public string? Unit { get; init; }
    public string? CurrentEmailMasked { get; init; }
    public string RequestedEmail { get; init; } = default!;
    public string? PhoneNumber { get; init; }
    public string? Note { get; init; }
    public string Status { get; init; } = default!;
    public DateTime RequestedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? ReviewedByName { get; init; }
    public string? RejectionReason { get; init; }
}

public record ForgotPasswordLookupQuery(ForgotPasswordLookupRequest Request) : IRequest<Result<ForgotPasswordLookupResult>>;
public record SendPasswordResetLinkCommand(ForgotPasswordRequest Request) : IRequest<Result>;
public record ResetPasswordCommand(ResetPasswordRequest Request) : IRequest<Result>;
public record RequestEmailChangeCommand(RequestEmailChangeRequest Request) : IRequest<Result>;
public record GetEmailChangeRequestsQuery(EmailChangeRequestStatus? Status) : IRequest<Result<List<EmailChangeRequestDto>>>;
public record GetPendingEmailChangeCountQuery : IRequest<Result<int>>;
public record ApproveEmailChangeCommand(Guid Id) : IRequest<Result>;
public record RejectEmailChangeCommand(Guid Id, RejectEmailChangeRequest Request) : IRequest<Result>;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Request.UserId).NotEmpty();
        RuleFor(x => x.Request.Token).NotEmpty();
        RuleFor(x => x.Request.NewPassword).NotEmpty().MinimumLength(3).WithMessage("Password must be at least 3 characters");
    }
}

public class RequestEmailChangeCommandValidator : AbstractValidator<RequestEmailChangeCommand>
{
    public RequestEmailChangeCommandValidator()
    {
        RuleFor(x => x.Request.ChandaNo).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Request.NewEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Request.PhoneNumber).MaximumLength(30);
        RuleFor(x => x.Request.Note).MaximumLength(500);
    }
}

public class AccountRecoveryHandlers :
    IRequestHandler<ForgotPasswordLookupQuery, Result<ForgotPasswordLookupResult>>,
    IRequestHandler<SendPasswordResetLinkCommand, Result>,
    IRequestHandler<ResetPasswordCommand, Result>,
    IRequestHandler<RequestEmailChangeCommand, Result>,
    IRequestHandler<GetEmailChangeRequestsQuery, Result<List<EmailChangeRequestDto>>>,
    IRequestHandler<GetPendingEmailChangeCountQuery, Result<int>>,
    IRequestHandler<ApproveEmailChangeCommand, Result>,
    IRequestHandler<RejectEmailChangeCommand, Result>
{
    public const string LinkSentMessage =
        "If an account matches, we've sent a link to reset your password. Check your inbox and spam folder. The link expires in 1 hour.";
    public const string RequestSentMessage =
        "Your request has been sent to the National Secretariat. Once they confirm it's you, we'll send the reset link to your new email.";
    private const string Forbidden = "FORBIDDEN: Only the National Secretariat can review email change requests";

    // One reset email per account per minute, so the form can't be used to flood a member's inbox.
    private static readonly TimeSpan ResendInterval = TimeSpan.FromSeconds(60);

    private readonly IApplicationDbContext _context;
    private readonly IAccountRecoveryService _recovery;
    private readonly IEmailService _email;
    private readonly ICurrentUserService _currentUser;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _clock;
    private readonly ILogger<AccountRecoveryHandlers> _logger;

    public AccountRecoveryHandlers(
        IApplicationDbContext context,
        IAccountRecoveryService recovery,
        IEmailService email,
        ICurrentUserService currentUser,
        IMemoryCache cache,
        TimeProvider clock,
        ILogger<AccountRecoveryHandlers> logger)
    {
        _context = context;
        _recovery = recovery;
        _email = email;
        _currentUser = currentUser;
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public async Task<Result<ForgotPasswordLookupResult>> Handle(ForgotPasswordLookupQuery request, CancellationToken cancellationToken)
    {
        var user = await FindActiveUserAsync(request.Request.ChandaNo, cancellationToken);

        // Unknown numbers look the same as accounts without an email.
        return Result<ForgotPasswordLookupResult>.Success(IsUsableEmail(user?.Email)
            ? new ForgotPasswordLookupResult(true, MaskEmail(user!.Email!))
            : new ForgotPasswordLookupResult(false, null));
    }

    public async Task<Result> Handle(SendPasswordResetLinkCommand request, CancellationToken cancellationToken)
    {
        var identifier = (request.Request.Identifier ?? string.Empty).Trim();

        // An email may belong to more than one account (emails are not unique here): each gets its own link.
        var users = identifier.Contains('@')
            ? await _context.Users
                .Where(u => u.IsActive && u.NormalizedEmail == identifier.ToUpperInvariant())
                .Take(5)
                .ToListAsync(cancellationToken)
            : (await FindActiveUserAsync(identifier, cancellationToken)) is { } byNumber
                ? new List<ApplicationUser> { byNumber }
                : new List<ApplicationUser>();

        foreach (var user in users.Where(u => IsUsableEmail(u.Email) && TryStartResend(u.Id)))
        {
            await SendResetLinkAsync(user, user.Email!, cancellationToken);
        }

        return Result.Success(LinkSentMessage);
    }

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        var result = await _recovery.ResetPasswordAsync(r.UserId, r.Token, r.NewPassword);
        if (!result.Succeeded)
        {
            return result;
        }

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == r.UserId, cancellationToken);
        if (user is not null && IsUsableEmail(user.Email))
        {
            var (subject, html, text) = AccountEmails.PasswordChanged(DisplayName(user), Now);
            await SendQuietlyAsync(user.Email!, DisplayName(user), subject, html, text, cancellationToken);
        }

        return Result.Success("Password changed. You can now sign in.");
    }

    public async Task<Result> Handle(RequestEmailChangeCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        var user = await FindActiveUserAsync(r.ChandaNo, cancellationToken);
        if (user is null)
        {
            return Result.Success(RequestSentMessage); // don't reveal unknown numbers
        }

        // A newer request replaces any pending one, so the Secretariat only sees the latest.
        var pending = await _context.EmailChangeRequests
            .Where(e => e.UserId == user.Id && e.Status == EmailChangeRequestStatus.Pending)
            .ToListAsync(cancellationToken);
        pending.ForEach(p => p.Supersede(Now));

        _context.EmailChangeRequests.Add(new EmailChangeRequest(
            user.Id, user.ChandaNo ?? r.ChandaNo, user.Email, r.NewEmail, r.PhoneNumber, r.Note, Now));
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(RequestSentMessage);
    }

    public async Task<Result<List<EmailChangeRequestDto>>> Handle(GetEmailChangeRequestsQuery request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result<List<EmailChangeRequestDto>>.Failure(Forbidden);
        }

        var query = _context.EmailChangeRequests.AsNoTracking();
        if (request.Status.HasValue)
        {
            query = query.Where(e => e.Status == request.Status.Value);
        }

        var items = await query.OrderByDescending(e => e.RequestedAt).Take(500).ToListAsync(cancellationToken);
        var userIds = items.Select(i => i.UserId).Distinct().ToList();
        var users = await _context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName, u.MuqamId, u.DilaId, u.ZoneId })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var muqamIds = users.Values.Where(u => u.MuqamId != null).Select(u => u.MuqamId!.Value).ToList();
        var dilaIds = users.Values.Where(u => u.DilaId != null).Select(u => u.DilaId!.Value).ToList();
        var zoneIds = users.Values.Where(u => u.ZoneId != null).Select(u => u.ZoneId!.Value).ToList();
        var muqams = await _context.Muqams.Where(m => muqamIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);
        var dilas = await _context.Dilas.Where(d => dilaIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);
        var zones = await _context.Zones.Where(z => zoneIds.Contains(z.Id)).ToDictionaryAsync(z => z.Id, z => z.Name, cancellationToken);

        return Result<List<EmailChangeRequestDto>>.Success(items.Select(i =>
        {
            users.TryGetValue(i.UserId, out var u);
            var unit = new[]
            {
                u?.MuqamId is { } m ? muqams.GetValueOrDefault(m) : null,
                u?.DilaId is { } d ? dilas.GetValueOrDefault(d) : null,
                u?.ZoneId is { } z ? zones.GetValueOrDefault(z) : null,
            }.Where(n => !string.IsNullOrEmpty(n));

            return new EmailChangeRequestDto
            {
                Id = i.Id,
                UserId = i.UserId,
                MemberName = u is null ? i.ChandaNo : ($"{u.FirstName} {u.LastName}".Trim() is { Length: > 0 } n ? n : u.UserName ?? i.ChandaNo),
                ChandaNo = i.ChandaNo,
                Unit = string.Join(" · ", unit) is { Length: > 0 } joined ? joined : null,
                CurrentEmailMasked = IsUsableEmail(i.CurrentEmail) ? MaskEmail(i.CurrentEmail!) : null,
                RequestedEmail = i.RequestedEmail,
                PhoneNumber = i.PhoneNumber,
                Note = i.Note,
                Status = i.Status.ToString(),
                RequestedAt = i.RequestedAt,
                ReviewedAt = i.ReviewedAt,
                ReviewedByName = i.ReviewedByName,
                RejectionReason = i.RejectionReason
            };
        }).ToList());
    }

    public async Task<Result<int>> Handle(GetPendingEmailChangeCountQuery request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result<int>.Failure(Forbidden);
        }

        return Result<int>.Success(await _context.EmailChangeRequests
            .CountAsync(e => e.Status == EmailChangeRequestStatus.Pending, cancellationToken));
    }

    public async Task<Result> Handle(ApproveEmailChangeCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result.Failure(Forbidden);
        }

        var change = await _context.EmailChangeRequests.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (change is null)
        {
            return Result.Failure("Request not found");
        }

        if (change.Status != EmailChangeRequestStatus.Pending)
        {
            return Result.Failure("This request has already been reviewed.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == change.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure("The member's account no longer exists");
        }

        var oldEmail = user.Email;
        var changed = await _recovery.ChangeEmailAsync(user, change.RequestedEmail);
        if (!changed.Succeeded)
        {
            return changed;
        }

        change.Approve(_currentUser.UserId, _currentUser.UserName ?? "National Secretariat", Now);
        await _context.SaveChangesAsync(cancellationToken);

        var name = DisplayName(user);
        var (subject, html, text) = AccountEmails.EmailUpdated(name, change.RequestedEmail, Now);
        await SendQuietlyAsync(change.RequestedEmail, name, subject, html, text, cancellationToken);
        if (IsUsableEmail(oldEmail) && !string.Equals(oldEmail, change.RequestedEmail, StringComparison.OrdinalIgnoreCase))
        {
            await SendQuietlyAsync(oldEmail!, name, subject, html, text, cancellationToken);
        }

        await SendResetLinkAsync(user, change.RequestedEmail, cancellationToken);
        return Result.Success("Email updated and a reset link sent to the new address");
    }

    public async Task<Result> Handle(RejectEmailChangeCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result.Failure(Forbidden);
        }

        var change = await _context.EmailChangeRequests.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (change is null)
        {
            return Result.Failure("Request not found");
        }

        try
        {
            change.Reject(_currentUser.UserId, _currentUser.UserName ?? "National Secretariat", request.Request.Reason, Now);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success("Request rejected");
    }

    /// <summary>"adeyemi@gmail.com" → "a•••••@g••••.com".</summary>
    public static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0)
        {
            return "•••";
        }

        var local = email[..at];
        var domain = email[(at + 1)..];
        var dot = domain.LastIndexOf('.');
        var host = dot > 0 ? domain[..dot] : domain;
        var tld = dot > 0 ? domain[dot..] : string.Empty;

        return $"{local[0]}{new string('•', Math.Max(3, local.Length - 1))}@" +
               $"{(host.Length > 0 ? host[0] : '•')}{new string('•', Math.Max(3, host.Length - 1))}{tld}";
    }

    public static bool IsUsableEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        // Registration gives members without an email a placeholder like "1234@temp.local": not deliverable.
        if (email.Trim().EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            return new MailAddress(email).Address == email.Trim();
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private Task<ApplicationUser?> FindActiveUserAsync(string chandaNo, CancellationToken cancellationToken)
    {
        var number = (chandaNo ?? string.Empty).Trim();
        return number.Length == 0
            ? Task.FromResult<ApplicationUser?>(null)
            : _context.Users.FirstOrDefaultAsync(u => u.ChandaNo == number && u.IsActive, cancellationToken);
    }

    private bool TryStartResend(Guid userId)
    {
        var key = $"password-reset:{userId}";
        if (_cache.TryGetValue(key, out _))
        {
            return false;
        }

        _cache.Set(key, true, ResendInterval);
        return true;
    }

    private async Task SendResetLinkAsync(ApplicationUser user, string toEmail, CancellationToken cancellationToken)
    {
        var link = await _recovery.CreatePasswordResetLinkAsync(user);
        var (subject, html, text) = AccountEmails.ResetPassword(DisplayName(user), link);
        await SendQuietlyAsync(toEmail, DisplayName(user), subject, html, text, cancellationToken);
    }

    // Email failures are logged, never shown: the anonymous responses must look the same either way.
    private async Task SendQuietlyAsync(string to, string name, string subject, string html, string text, CancellationToken cancellationToken)
    {
        try
        {
            await _email.SendEmailAsync(to, name, subject, html, text, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not send '{Subject}' email", subject);
        }
    }

    private static string DisplayName(ApplicationUser user) =>
        $"{user.FirstName} {user.LastName}".Trim() is { Length: > 0 } name ? name : user.UserName ?? "member";
}
