using ManagementApi.Domain.Entities.Reports;

namespace ManagementApi.Infrastructure.Services;

/// <summary>
/// Service for generating HTML email templates
/// </summary>
public interface IEmailTemplateService
{
    string GenerateNotificationEmail(
        string recipientName,
        string title,
        string message,
        NotificationPriority priority,
        string? actionUrl = null,
        string? actionText = null);
}

public class EmailTemplateService : IEmailTemplateService
{
    public string GenerateNotificationEmail(
        string recipientName,
        string title,
        string message,
        NotificationPriority priority,
        string? actionUrl = null,
        string? actionText = null)
    {
        var priorityColor = GetPriorityColor(priority);
        var priorityText = priority.ToString();

        var html = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{title}</title>
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <!-- Header -->
                    <tr>
                        <td style=""padding: 40px 30px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); text-align: center;"">
                            <h1 style=""margin: 0; color: #ffffff; font-size: 24px; font-weight: 600;"">
                                Majlis Ansarullah Nigeria
                            </h1>
                            <p style=""margin: 10px 0 0 0; color: #ffffff; font-size: 14px;"">
                                Report Management System
                            </p>
                        </td>
                    </tr>

                    <!-- Priority Badge -->
                    <tr>
                        <td style=""padding: 20px 30px 0 30px;"">
                            <span style=""display: inline-block; padding: 6px 12px; background-color: {priorityColor}; color: #ffffff; border-radius: 4px; font-size: 12px; font-weight: 600; text-transform: uppercase;"">
                                {priorityText} Priority
                            </span>
                        </td>
                    </tr>

                    <!-- Content -->
                    <tr>
                        <td style=""padding: 20px 30px;"">
                            <h2 style=""margin: 0 0 10px 0; color: #333333; font-size: 20px; font-weight: 600;"">
                                Dear {recipientName},
                            </h2>
                            <h3 style=""margin: 20px 0 10px 0; color: #555555; font-size: 18px; font-weight: 600;"">
                                {title}
                            </h3>
                            <p style=""margin: 0; color: #666666; font-size: 16px; line-height: 1.6;"">
                                {message}
                            </p>
                        </td>
                    </tr>

                    <!-- Action Button -->
                    {(string.IsNullOrEmpty(actionUrl) ? "" : $@"
                    <tr>
                        <td style=""padding: 20px 30px;"">
                            <table role=""presentation"" style=""margin: 0 auto;"">
                                <tr>
                                    <td style=""border-radius: 4px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);"">
                                        <a href=""{actionUrl}"" target=""_blank"" style=""display: inline-block; padding: 14px 30px; color: #ffffff; text-decoration: none; font-size: 16px; font-weight: 600; border-radius: 4px;"">
                                            {actionText ?? "View Details"}
                                        </a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    ")}

                    <!-- Footer -->
                    <tr>
                        <td style=""padding: 30px; background-color: #f8f9fa; border-top: 1px solid #e9ecef;"">
                            <p style=""margin: 0 0 10px 0; color: #666666; font-size: 14px; line-height: 1.5;"">
                                This is an automated notification from the Report Management System.
                                Please do not reply to this email.
                            </p>
                            <p style=""margin: 0; color: #999999; font-size: 12px;"">
                                © {DateTime.UtcNow.Year} Majlis Ansarullah Nigeria. All rights reserved.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        return html;
    }

    private string GetPriorityColor(NotificationPriority priority)
    {
        return priority switch
        {
            NotificationPriority.Low => "#6c757d",      // Gray
            NotificationPriority.Normal => "#0dcaf0",   // Cyan
            NotificationPriority.High => "#fd7e14",     // Orange
            NotificationPriority.Urgent => "#dc3545",   // Red
            _ => "#0dcaf0"
        };
    }
}
