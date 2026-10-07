namespace HRS.Domain.Organization;

/// <summary>
/// A node in the organization tree (FR-ORG-001). Code is a short local segment unique among
/// siblings (e.g. "Z03"), not the full hyphenated identifier the SRS shows as an example
/// ("Z03-D12-M04") — that's derived from the path/ancestor chain for display, never stored,
/// so it can't drift from the real hierarchy. Path is a plain materialized path
/// ("nat.z03.d12.m04", per PLAN.md — not the Postgres ltree extension).
/// </summary>
public sealed class Unit
{
    private Unit()
    {
    }

    private Unit(
        Guid id,
        string code,
        string name,
        Guid levelId,
        Guid? parentId,
        string path,
        string? address,
        string? contactPhone,
        string? contactEmail,
        double? latitude,
        double? longitude,
        DateTimeOffset createdAt)
    {
        Id = id;
        Code = code;
        Name = name;
        LevelId = levelId;
        ParentId = parentId;
        Path = path;
        Address = address;
        ContactPhone = contactPhone;
        ContactEmail = contactEmail;
        Latitude = latitude;
        Longitude = longitude;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public Guid LevelId { get; private set; }

    public Guid? ParentId { get; private set; }

    public string Path { get; private set; } = string.Empty;

    public string? Address { get; private set; }

    public string? ContactPhone { get; private set; }

    public string? ContactEmail { get; private set; }

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    /// <summary>The single seeded root (National). Every other unit is created via <see cref="Create"/>.</summary>
    public static Unit CreateRoot(Guid id, string code, string name, Guid levelId, DateTimeOffset now) =>
        new(id, NormalizeCode(code), name, levelId, parentId: null, BuildPath(null, code), null, null, null, null, null, now);

    public static Unit Create(
        Guid id,
        string code,
        string name,
        Guid levelId,
        Unit parent,
        string? address,
        string? contactPhone,
        string? contactEmail,
        double? latitude,
        double? longitude,
        DateTimeOffset now) =>
        new(
            id,
            NormalizeCode(code),
            name,
            levelId,
            parent.Id,
            BuildPath(parent.Path, code),
            address,
            contactPhone,
            contactEmail,
            latitude,
            longitude,
            now);

    public void Rename(string name) => Name = name;

    public void UpdateContact(string? address, string? contactPhone, string? contactEmail, double? latitude, double? longitude)
    {
        Address = address;
        ContactPhone = contactPhone;
        ContactEmail = contactEmail;
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>Moves this unit under a new parent (FR-ORG-005). Descendants keep their own
    /// ParentId but need their Path rebased too — see <see cref="RebasePath"/>.</summary>
    public void MoveTo(Unit newParent)
    {
        ParentId = newParent.Id;
        Path = BuildPath(newParent.Path, Code);
    }

    /// <summary>Updates a descendant's materialized path after an ancestor moved. ParentId is
    /// unchanged — only the prefix contributed by the moved ancestor shifts.</summary>
    public void RebasePath(string newPath) => Path = newPath;

    public void Archive(DateTimeOffset now) => ArchivedAt = now;

    public void Restore() => ArchivedAt = null;

    /// <summary>The hyphenated display identifier (e.g. "Z03-D12-M04") derived from the path — never stored.</summary>
    public string DisplayCode() =>
        string.Join('-', Path.Split('.').Skip(1).Select(segment => segment.ToUpperInvariant()));

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static string BuildPath(string? parentPath, string code)
    {
        var segment = NormalizeCode(code).ToLowerInvariant();
        return parentPath is null ? segment : $"{parentPath}.{segment}";
    }
}
