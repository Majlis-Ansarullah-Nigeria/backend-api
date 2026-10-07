namespace HRS.Domain.Windows;

/// <summary>A level's position relative to its window right now (BR-WIN-01..08). "Closed" covers
/// both a level whose own grace period ended and the window's hard close — whichever comes
/// first; M4.4's worker is what turns "Closed" into an actual Missed report, this just reports
/// the state.</summary>
public enum WindowLevelStatus
{
    BeforeOpen,
    OnTime,
    Late,
    Closed,
}
