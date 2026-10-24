namespace HRS.Domain.Admin;

/// <summary>FR-ADM-001's brand colours — a fixed set rather than any hex value, so every choice
/// keeps white text on the accent and the accent as text on white at WCAG AA (≥ 4.5:1).</summary>
public enum BrandColor
{
    Green,
    Teal,
    Blue,
    Purple,
    Orange,
}

public static class BrandColors
{
    /// <summary>Primary is the accent fill (buttons); Dark is the accent as text (links, active marks).</summary>
    public sealed record Palette(string Primary, string Dark);

    public static Palette For(BrandColor color) =>
        color switch
        {
            BrandColor.Teal => new("#0E7490", "#155E75"),
            BrandColor.Blue => new("#1D4ED8", "#1E40AF"),
            BrandColor.Purple => new("#7C3AED", "#6D28D9"),
            BrandColor.Orange => new("#B45309", "#92400E"),
            _ => new("#0B7A4B", "#075C39"),
        };
}
