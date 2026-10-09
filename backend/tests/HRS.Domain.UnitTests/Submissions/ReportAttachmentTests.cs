using HRS.Domain.Submissions;

namespace HRS.Domain.UnitTests.Submissions;

public class ReportAttachmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static ReportAttachment CreateAttachment() =>
        ReportAttachment.Create(
            Guid.NewGuid(), Guid.NewGuid(), "photos", "site.jpg", "image/jpeg", 12_345, "reports/x/y/site.jpg", Guid.NewGuid(), Now);

    [Fact]
    public void CreateStartsPending()
    {
        var attachment = CreateAttachment();

        Assert.Equal(ReportAttachmentStatus.Pending, attachment.Status);
        Assert.Equal(12_345, attachment.SizeBytes);
        Assert.Equal(Now, attachment.CreatedAt);
    }

    [Fact]
    public void MarkCleanMovesToCleanAndUpdatesTheRealMeasuredSize()
    {
        var attachment = CreateAttachment();

        attachment.MarkClean(12_000);

        Assert.Equal(ReportAttachmentStatus.Clean, attachment.Status);
        Assert.Equal(12_000, attachment.SizeBytes);
    }

    [Fact]
    public void MarkCleanTwiceThrows()
    {
        var attachment = CreateAttachment();
        attachment.MarkClean(12_000);

        Assert.Throws<InvalidOperationException>(() => attachment.MarkClean(12_000));
    }
}
