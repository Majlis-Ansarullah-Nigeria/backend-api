using HRS.Domain.Organization;

namespace HRS.Application.Features.Windows.SubmissionWindows;

/// <summary>
/// FR-WIN-003/hrs-domain's "warn when a parent level is due before its children" — advisory
/// only, never blocks saving (a Secretariat may have a real reason to set it that way). Walks
/// the roll-up chain Muqam -&gt; Dila -&gt; Zone -&gt; National; Department is excluded since it
/// doesn't roll up into that chain the same way.
/// </summary>
public static class ParentBeforeChildWarnings
{
    private static readonly UnitLevelCode[] RollUpChain =
        [UnitLevelCode.Muqam, UnitLevelCode.Dila, UnitLevelCode.Zone, UnitLevelCode.National];

    public static IReadOnlyList<string> Detect(IReadOnlyList<WindowDueDate> dueDates)
    {
        var byLevel = dueDates.ToDictionary(d => d.Level, d => d.DueAt);
        var warnings = new List<string>();

        for (var i = 0; i < RollUpChain.Length - 1; i++)
        {
            var childLevel = RollUpChain[i];
            var parentLevel = RollUpChain[i + 1];

            if (!byLevel.TryGetValue(childLevel, out var childDueAt) || !byLevel.TryGetValue(parentLevel, out var parentDueAt))
            {
                continue;
            }

            if (parentDueAt < childDueAt)
            {
                warnings.Add(
                    $"{parentLevel} is due before {childLevel} — {parentLevel} heads won't be able to pull complete {childLevel} figures.");
            }
        }

        return warnings;
    }
}
