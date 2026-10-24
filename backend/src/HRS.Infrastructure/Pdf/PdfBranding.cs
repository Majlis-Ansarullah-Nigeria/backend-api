using HRS.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HRS.Infrastructure.Pdf;

/// <summary>FR-ADM-001's branding at the top of every PDF: the logo (PNG or SVG) and the
/// organization's name in its brand colour.</summary>
internal static class PdfBranding
{
    public static void Header(IContainer container, OrganizationBranding branding) =>
        container.Row(row =>
        {
            if (branding.Logo is { } logo)
            {
                var item = row.ConstantItem(36).Height(36).PaddingRight(8);
                if (branding.LogoContentType == "image/svg+xml")
                {
                    item.Svg(System.Text.Encoding.UTF8.GetString(logo)).FitArea();
                }
                else
                {
                    item.Image(logo).FitArea();
                }
            }

            row.RelativeItem().AlignMiddle().Text(branding.Name).FontSize(12).SemiBold().FontColor(branding.PrimaryColor);
        });
}
