using System.Net;

namespace ManagementApi.Application.Auth.Recovery;

/// <summary>Plain, branded HTML emails for password reset and email changes.</summary>
public static class AccountEmails
{
    private const string Green = "#1B5E20";

    public static (string Subject, string Html, string Text) ResetPassword(string name, string link) => (
        "Reset your password",
        Layout($"""
            <p>Salam {Encode(name)},</p>
            <p>We received a request to reset the password for your Majlis Ansarullah Nigeria portal account.</p>
            <p style="text-align:center;margin:28px 0">
              <a href="{Encode(link)}" style="background:{Green};color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:600">Reset password</a>
            </p>
            <p>This link expires in 1 hour and can be used once.</p>
            <p>If you didn't ask for this, ignore this email. Your password will not change.</p>
            """),
        $"Salam {name},\n\nReset your portal password here (expires in 1 hour, works once):\n{link}\n\nIf you didn't ask for this, ignore this email.");

    public static (string Subject, string Html, string Text) PasswordChanged(string name, DateTime changedAtUtc) => (
        "Your password was changed",
        Layout($"""
            <p>Salam {Encode(name)},</p>
            <p>The password for your portal account was changed on {Encode(Lagos(changedAtUtc))}.</p>
            <p>If this wasn't you, contact the National Secretariat straight away.</p>
            """),
        $"Salam {name},\n\nYour portal password was changed on {Lagos(changedAtUtc)}.\nIf this wasn't you, contact the National Secretariat straight away.");

    public static (string Subject, string Html, string Text) EmailUpdated(string name, string newEmail, DateTime changedAtUtc) => (
        "Your email address was updated",
        Layout($"""
            <p>Salam {Encode(name)},</p>
            <p>The email address on your portal account was changed to <strong>{Encode(newEmail)}</strong> on {Encode(Lagos(changedAtUtc))}, after a request approved by the National Secretariat.</p>
            <p>If this wasn't you, contact the National Secretariat straight away.</p>
            """),
        $"Salam {name},\n\nThe email on your portal account was changed to {newEmail} on {Lagos(changedAtUtc)}.\nIf this wasn't you, contact the National Secretariat straight away.");

    private static string Layout(string body) => $"""
        <div style="font-family:Inter,Arial,sans-serif;max-width:560px;margin:0 auto;color:#1f2937">
          <div style="background:{Green};color:#fff;padding:16px 24px;font-size:18px;font-weight:700">Majlis Ansarullah Nigeria</div>
          <div style="padding:24px;border:1px solid #e5e7eb;border-top:none;line-height:1.5">{body}</div>
          <p style="font-size:12px;color:#6b7280;padding:0 24px">This is an automated message from the MAAN portal.</p>
        </div>
        """;

    private static string Lagos(DateTime utc)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Lagos");
            return TimeZoneInfo.ConvertTimeFromUtc(utc, zone).ToString("d MMM yyyy, HH:mm") + " (Lagos time)";
        }
        catch (TimeZoneNotFoundException)
        {
            return utc.AddHours(1).ToString("d MMM yyyy, HH:mm") + " (Lagos time)";
        }
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
