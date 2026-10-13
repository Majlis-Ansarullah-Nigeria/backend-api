namespace HRS.Domain.Notifications;

/// <summary>FR-NOT-006: "broadcasts a message to all heads or a level." Department officers and
/// Leadership aren't heads and aren't addressable here — out of scope for the mockup's "Send to"
/// dropdown, which only ever shows head audiences.</summary>
public enum AnnouncementAudience
{
    AllHeads,
    ZoneHeads,
    DilaHeads,
    MuqamHeads,
}
