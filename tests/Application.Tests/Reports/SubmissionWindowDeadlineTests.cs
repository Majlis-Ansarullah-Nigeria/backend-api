using FluentAssertions;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;
using Xunit;

namespace ManagementApi.Application.Tests.Reports;

public class SubmissionWindowDeadlineTests
{
    private static readonly DateTime Opens = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MuqamDue = new(2026, 11, 10, 23, 59, 0, DateTimeKind.Utc);
    private static readonly DateTime DilaDue = new(2026, 11, 15, 23, 59, 0, DateTimeKind.Utc);
    private static readonly DateTime Closes = new(2026, 11, 20, 23, 59, 0, DateTimeKind.Utc);
    private static readonly Guid Muqam = Guid.NewGuid();

    private static SubmissionWindow Window()
    {
        var window = new SubmissionWindow(Guid.NewGuid(), "November", Opens, Closes);
        window.SetDueDates(MuqamDue, DilaDue, null);
        return window;
    }

    [Fact]
    public void On_time_before_the_level_due_date_late_after_it_locked_after_close()
    {
        var window = Window();

        window.IsLateFor(MuqamDue.AddMinutes(-1), OrganizationLevel.Muqam, Muqam).Should().BeFalse();
        window.IsLateFor(MuqamDue.AddMinutes(1), OrganizationLevel.Muqam, Muqam).Should().BeTrue();
        window.IsOpenFor(MuqamDue.AddMinutes(1), OrganizationLevel.Muqam, Muqam).Should().BeTrue();
        window.IsOpenFor(Closes.AddMinutes(1), OrganizationLevel.Muqam, Muqam).Should().BeFalse();
        window.DueAt(OrganizationLevel.Zone).Should().Be(Closes);
    }

    [Fact]
    public void Extension_moves_due_and_close_dates_for_one_unit_only()
    {
        var window = Window();
        window.GrantExtension(OrganizationLevel.Muqam, Muqam, MuqamDue.AddDays(5), Closes.AddDays(3), "Flooding");

        window.IsLateFor(MuqamDue.AddDays(2), OrganizationLevel.Muqam, Muqam).Should().BeFalse();
        window.IsOpenFor(Closes.AddDays(2), OrganizationLevel.Muqam, Muqam).Should().BeTrue();
        window.IsOpenFor(Closes.AddDays(2), OrganizationLevel.Muqam, Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Due_dates_must_be_inside_the_window_and_parents_due_first_is_warned()
    {
        var window = new SubmissionWindow(Guid.NewGuid(), "November", Opens, Closes);

        var outside = () => window.SetDueDates(Closes.AddDays(1), null, null);
        outside.Should().Throw<ArgumentException>();

        window.SetDueDates(DilaDue, MuqamDue, null);
        window.DueDateWarnings().Should().ContainSingle(w => w.Contains("Dilaat are due before their Muqamaat"));
    }
}
