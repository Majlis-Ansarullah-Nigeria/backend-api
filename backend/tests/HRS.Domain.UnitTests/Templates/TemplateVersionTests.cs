using HRS.Domain.Templates;

namespace HRS.Domain.UnitTests.Templates;

public class TemplateVersionTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateDraftStartsAsDraftWithNoPublishedOrRetiredTimestamps()
    {
        var version = TemplateVersion.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), 1, "{}", Now);

        Assert.Equal(TemplateVersionStatus.Draft, version.Status);
        Assert.Equal(1, version.VersionNumber);
        Assert.Null(version.PublishedAt);
        Assert.Null(version.RetiredAt);
    }

    [Fact]
    public void UpdateDraftSchemaReplacesTheSchemaWhileStillADraft()
    {
        var version = TemplateVersion.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), 1, "{}", Now);

        version.UpdateDraftSchema("{\"sections\":[]}");

        Assert.Equal("{\"sections\":[]}", version.SchemaJson);
    }

    [Fact]
    public void UpdateDraftSchemaThrowsOncePublished()
    {
        var version = TemplateVersion.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), 1, "{}", Now);
        version.Publish(Now);

        Assert.Throws<InvalidOperationException>(() => version.UpdateDraftSchema("{\"sections\":[]}"));
    }

    [Fact]
    public void PublishSetsStatusAndTimestamp()
    {
        var version = TemplateVersion.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), 1, "{}", Now);

        version.Publish(Now);

        Assert.Equal(TemplateVersionStatus.Published, version.Status);
        Assert.Equal(Now, version.PublishedAt);
    }

    [Fact]
    public void PublishThrowsWhenNotADraft()
    {
        var version = TemplateVersion.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), 1, "{}", Now);
        version.Publish(Now);

        Assert.Throws<InvalidOperationException>(() => version.Publish(Now));
    }

    [Fact]
    public void RetireSetsStatusAndTimestamp()
    {
        var version = TemplateVersion.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), 1, "{}", Now);
        version.Publish(Now);

        version.Retire(Now);

        Assert.Equal(TemplateVersionStatus.Retired, version.Status);
        Assert.Equal(Now, version.RetiredAt);
    }

    [Fact]
    public void RetireThrowsWhenNotPublished()
    {
        var version = TemplateVersion.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), 1, "{}", Now);

        Assert.Throws<InvalidOperationException>(() => version.Retire(Now));
    }
}
