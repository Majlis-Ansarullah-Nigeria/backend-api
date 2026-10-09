using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Invitations;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Domain.Windows;
using HRS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HRS.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options), IAppDbContext
{
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<ReportAttachment> ReportAttachments => Set<ReportAttachment>();

    public DbSet<ReportInstance> ReportInstances => Set<ReportInstance>();

    public DbSet<ReportType> ReportTypes => Set<ReportType>();

    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    public DbSet<SubmissionWindow> SubmissionWindows => Set<SubmissionWindow>();

    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();

    public DbSet<UnitLevel> UnitLevels => Set<UnitLevel>();

    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<WindowJobRun> WindowJobRuns => Set<WindowJobRun>();

    public DbSet<WindowSchedule> WindowSchedules => Set<WindowSchedule>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Identity's defaults are "AspNetUsers" etc.; rename to match this
        // project's plain snake_case table-naming convention.
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        builder.Entity<UserSession>(session =>
        {
            session.ToTable("user_sessions");
            session.HasKey(s => s.Id);
            session.Property(s => s.RefreshTokenHash).IsRequired();
            session.HasIndex(s => s.RefreshTokenHash).IsUnique();
            session.HasIndex(s => s.UserId);
            session
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IdempotencyRecord>(record =>
        {
            record.ToTable("idempotency_records");
            record.HasKey(r => r.Id);
            record.Property(r => r.Key).IsRequired();
            record.Property(r => r.ResponseJson).HasColumnType("jsonb").IsRequired();
            record.HasIndex(r => new { r.UserId, r.Key }).IsUnique();
        });

        builder.Entity<Invitation>(invitation =>
        {
            invitation.ToTable("invitations");
            invitation.HasKey(i => i.Id);
            invitation.Property(i => i.Email).IsRequired();
            invitation.Property(i => i.Name).IsRequired();
            invitation.Property(i => i.TokenHash).IsRequired();
            invitation.HasIndex(i => i.TokenHash).IsUnique();
            invitation.HasIndex(i => i.Email);
            invitation
                .HasOne<Unit>()
                .WithMany()
                .HasForeignKey(i => i.PendingHeadOfUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RoleAssignment>(assignment =>
        {
            assignment.ToTable("role_assignments");
            assignment.HasKey(a => a.Id);
            assignment.HasIndex(a => a.UserId);
            assignment.HasIndex(a => a.UnitId);
            assignment
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            assignment
                .HasOne<Unit>()
                .WithMany()
                .HasForeignKey(a => a.UnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationUser>(user =>
        {
            user
                .HasOne<RoleAssignment>()
                .WithMany()
                .HasForeignKey(u => u.ActiveRoleAssignmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<UnitLevel>(level =>
        {
            level.ToTable("unit_levels");
            level.HasKey(l => l.Id);
            level.Property(l => l.Name).IsRequired();
            level.Property(l => l.PluralName).IsRequired();
            level.HasIndex(l => l.Code).IsUnique();

            level.HasData(
                new { Id = OrganizationSeedIds.NationalLevelId, Code = UnitLevelCode.National, Name = "National", PluralName = "National", Depth = 0 },
                new { Id = OrganizationSeedIds.ZoneLevelId, Code = UnitLevelCode.Zone, Name = "Zone", PluralName = "Zones", Depth = 1 },
                new { Id = OrganizationSeedIds.DilaLevelId, Code = UnitLevelCode.Dila, Name = "Dila", PluralName = "Dilaat", Depth = 2 },
                new { Id = OrganizationSeedIds.MuqamLevelId, Code = UnitLevelCode.Muqam, Name = "Muqam", PluralName = "Muqamaat", Depth = 3 },
                new { Id = OrganizationSeedIds.DepartmentLevelId, Code = UnitLevelCode.Department, Name = "Department", PluralName = "Departments", Depth = 1 });
        });

        builder.Entity<Unit>(unit =>
        {
            unit.ToTable("units");
            unit.HasKey(u => u.Id);
            unit.Property(u => u.Code).IsRequired();
            unit.Property(u => u.Name).IsRequired();
            unit.Property(u => u.Path).IsRequired();
            unit.HasIndex(u => new { u.ParentId, u.Code }).IsUnique();
            unit.HasIndex(u => u.Path);
            unit
                .HasOne<UnitLevel>()
                .WithMany()
                .HasForeignKey(u => u.LevelId)
                .OnDelete(DeleteBehavior.Restrict);
            unit
                .HasOne<Unit>()
                .WithMany()
                .HasForeignKey(u => u.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            unit.HasData(new
            {
                Id = OrganizationSeedIds.NationalUnitId,
                Code = "NAT",
                Name = "National",
                LevelId = OrganizationSeedIds.NationalLevelId,
                ParentId = (Guid?)null,
                Path = "nat",
                Address = (string?)null,
                ContactPhone = (string?)null,
                ContactEmail = (string?)null,
                Latitude = (double?)null,
                Longitude = (double?)null,
                CreatedAt = OrganizationSeedIds.SeedTimestamp,
                ArchivedAt = (DateTimeOffset?)null,
            });
        });

        builder.Entity<ReportType>(reportType =>
        {
            reportType.ToTable("report_types");
            reportType.HasKey(r => r.Id);
            reportType.Property(r => r.Name).IsRequired();
            // A plain list of enum values, not a relationship to another entity — mapped straight
            // to a Postgres integer[] column rather than a join table (EF Core primitive collections).
            reportType.PrimitiveCollection(r => r.ApplicableLevels)
                .HasField("_applicableLevels")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<TemplateVersion>(version =>
        {
            version.ToTable("template_versions");
            version.HasKey(v => v.Id);
            version.Property(v => v.SchemaJson).HasColumnType("jsonb").IsRequired();
            version.HasIndex(v => v.ReportTypeId);
            version.HasIndex(v => new { v.ReportTypeId, v.VersionNumber }).IsUnique();
            version
                .HasOne<ReportType>()
                .WithMany()
                .HasForeignKey(v => v.ReportTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReportInstance>(instance =>
        {
            instance.ToTable("report_instances");
            instance.HasKey(i => i.Id);
            instance.Property(i => i.AnswersJson).HasColumnType("jsonb").IsRequired();
            // Postgres' own xmin system column as the concurrency token — no real column is
            // added by a migration, since xmin already exists on every table.
            instance.Property(i => i.RowVersion).HasColumnName("xmin").IsRowVersion();
            // The report-instance uniqueness rule (ef-migration skill): one instance per unit per
            // window, never two.
            instance.HasIndex(i => new { i.WindowId, i.UnitId }).IsUnique();
            // Postgres' unique index allows any number of NULLs, so this only ever constrains
            // actual (post-submit) reference numbers — never two instances with the same one.
            instance.HasIndex(i => i.ReferenceNumber).IsUnique();
            instance
                .HasOne<SubmissionWindow>()
                .WithMany()
                .HasForeignKey(i => i.WindowId)
                .OnDelete(DeleteBehavior.Restrict);
            instance
                .HasOne<Unit>()
                .WithMany()
                .HasForeignKey(i => i.UnitId)
                .OnDelete(DeleteBehavior.Restrict);
            instance
                .HasOne<TemplateVersion>()
                .WithMany()
                .HasForeignKey(i => i.TemplateVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReportAttachment>(attachment =>
        {
            attachment.ToTable("report_attachments");
            attachment.HasKey(a => a.Id);
            attachment.Property(a => a.FieldKey).IsRequired();
            attachment.Property(a => a.FileName).IsRequired();
            attachment.Property(a => a.ContentType).IsRequired();
            attachment.Property(a => a.StorageKey).IsRequired();
            attachment.HasIndex(a => a.ReportInstanceId);
            attachment
                .HasOne<ReportInstance>()
                .WithMany()
                .HasForeignKey(a => a.ReportInstanceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SubmissionWindow>(window =>
        {
            window.ToTable("submission_windows");
            window.HasKey(w => w.Id);
            window.Property(w => w.PeriodLabel).IsRequired();
            window.HasIndex(w => w.ReportTypeId);
            window
                .HasOne<ReportType>()
                .WithMany()
                .HasForeignKey(w => w.ReportTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            // SetNull, not Restrict/Cascade: a window stands on its own once generated — if its
            // schedule is ever removed, the window just loses its provenance link, not itself.
            window
                .HasOne<WindowSchedule>()
                .WithMany()
                .HasForeignKey(w => w.ScheduleId)
                .OnDelete(DeleteBehavior.SetNull);

            window.OwnsMany(w => w.DueDates, dueDate =>
            {
                dueDate.ToTable("submission_window_due_dates");
                dueDate.WithOwner().HasForeignKey("submission_window_id");
                dueDate.Property<int>("id");
                dueDate.HasKey("id");
                dueDate.HasIndex("submission_window_id", nameof(WindowLevelDueDate.Level)).IsUnique();
            });
            window.Navigation(w => w.DueDates).HasField("_dueDates").UsePropertyAccessMode(PropertyAccessMode.Field);

            // A plain list of unit ids, not a relationship — FR-WIN-004's "hand-picked list of
            // units" (M4.2), mapped straight to a Postgres uuid[] column like ReportType's
            // ApplicableLevels, not a join table.
            window.PrimitiveCollection(w => w.TargetUnitIds)
                .HasField("_targetUnitIds")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            window.OwnsMany(w => w.Extensions, extension =>
            {
                extension.ToTable("window_extensions");
                extension.WithOwner().HasForeignKey("submission_window_id");
                extension.Property<int>("id");
                extension.HasKey("id");
                extension.HasIndex("submission_window_id", nameof(WindowExtension.UnitId));
                // Restrict, not Cascade: units are only ever archived, never hard-deleted, so this
                // can never actually fire — it documents the invariant rather than guarding it.
                extension
                    .HasOne<Unit>()
                    .WithMany()
                    .HasForeignKey(e => e.UnitId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            window.Navigation(w => w.Extensions).HasField("_extensions").UsePropertyAccessMode(PropertyAccessMode.Field);

            window.OwnsMany(w => w.Reopens, reopen =>
            {
                reopen.ToTable("window_reopens");
                reopen.WithOwner().HasForeignKey("submission_window_id");
                reopen.Property<int>("id");
                reopen.HasKey("id");
                reopen.HasIndex("submission_window_id", nameof(WindowReopen.UnitId));
                reopen
                    .HasOne<Unit>()
                    .WithMany()
                    .HasForeignKey(r => r.UnitId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            window.Navigation(w => w.Reopens).HasField("_reopens").UsePropertyAccessMode(PropertyAccessMode.Field);

            // Columns directly on submission_windows, not a child table — one settings value per
            // window, same shape as the "Create window" mockup's Reminders panel.
            window.OwnsOne(w => w.ReminderSettings, settings =>
            {
                settings.Property(s => s.RemindFiveDaysBefore).HasColumnName("remind_five_days_before").HasDefaultValue(true);
                settings.Property(s => s.RemindTwoDaysBefore).HasColumnName("remind_two_days_before").HasDefaultValue(true);
                settings.Property(s => s.RemindOnDueDay).HasColumnName("remind_on_due_day").HasDefaultValue(true);
                settings.Property(s => s.EscalateToParentAfterThreeDays).HasColumnName("escalate_to_parent_after_three_days").HasDefaultValue(true);
                settings.Property(s => s.EscalateToZoneAfterSevenDays).HasColumnName("escalate_to_zone_after_seven_days").HasDefaultValue(false);
            });
            window.Navigation(w => w.ReminderSettings).IsRequired();
        });

        // Its own top-level table, not owned by SubmissionWindow: per-unit, grows without bound,
        // and queried independently by the worker and (later) M8's dispatcher.
        builder.Entity<WindowJobRun>(jobRun =>
        {
            jobRun.ToTable("window_job_runs");
            jobRun.HasKey(r => r.Id);
            jobRun.Property(r => r.EventKey).IsRequired();
            jobRun.HasIndex(r => new { r.WindowId, r.UnitId, r.EventType, r.EventKey }).IsUnique();
            jobRun
                .HasOne<SubmissionWindow>()
                .WithMany()
                .HasForeignKey(r => r.WindowId)
                .OnDelete(DeleteBehavior.Cascade);
            jobRun
                .HasOne<Unit>()
                .WithMany()
                .HasForeignKey(r => r.UnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WindowSchedule>(schedule =>
        {
            schedule.ToTable("window_schedules");
            schedule.HasKey(s => s.Id);
            schedule.HasIndex(s => s.ReportTypeId);
            schedule
                .HasOne<ReportType>()
                .WithMany()
                .HasForeignKey(s => s.ReportTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            schedule.OwnsMany(s => s.DueOffsets, dueOffset =>
            {
                dueOffset.ToTable("window_schedule_due_offsets");
                dueOffset.WithOwner().HasForeignKey("window_schedule_id");
                dueOffset.Property<int>("id");
                dueOffset.HasKey("id");
                dueOffset.HasIndex("window_schedule_id", nameof(ScheduleLevelDueOffset.Level)).IsUnique();
            });
            schedule.Navigation(s => s.DueOffsets).HasField("_dueOffsets").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
