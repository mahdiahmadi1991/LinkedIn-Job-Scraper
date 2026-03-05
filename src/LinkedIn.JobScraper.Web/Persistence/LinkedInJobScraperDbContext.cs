using LinkedIn.JobScraper.Web.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkedIn.JobScraper.Web.Persistence;

public sealed class LinkedInJobScraperDbContext : DbContext
{
    public LinkedInJobScraperDbContext(DbContextOptions<LinkedInJobScraperDbContext> options)
        : base(options)
    {
    }

    public DbSet<AiBehaviorSettingsRecord> AiBehaviorSettings => Set<AiBehaviorSettingsRecord>();

    public DbSet<AiGlobalShortlistRunCandidateRecord> AiGlobalShortlistRunCandidates => Set<AiGlobalShortlistRunCandidateRecord>();

    public DbSet<AiGlobalShortlistItemRecord> AiGlobalShortlistItems => Set<AiGlobalShortlistItemRecord>();

    public DbSet<AiGlobalShortlistRunRecord> AiGlobalShortlistRuns => Set<AiGlobalShortlistRunRecord>();

    public DbSet<AppUserRecord> AppUsers => Set<AppUserRecord>();

    public DbSet<JobRecord> Jobs => Set<JobRecord>();

    public DbSet<JobStatusHistoryRecord> JobStatusHistory => Set<JobStatusHistoryRecord>();

    public DbSet<LinkedInSearchSettingsRecord> LinkedInSearchSettings => Set<LinkedInSearchSettingsRecord>();

    public DbSet<LinkedInSessionRecord> LinkedInSessions => Set<LinkedInSessionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobRecord>(
            entity =>
            {
                entity.ToTable("Jobs");
                entity.HasKey(static job => job.Id);

                entity.Property(static job => job.LinkedInJobId).HasMaxLength(64).IsRequired();
                entity.Property(static job => job.LinkedInJobPostingUrn).HasMaxLength(256).IsRequired();
                entity.Property(static job => job.LinkedInJobCardUrn).HasMaxLength(256);
                entity.Property(static job => job.Title).HasMaxLength(512).IsRequired();
                entity.Property(static job => job.CompanyName).HasMaxLength(256);
                entity.Property(static job => job.LocationName).HasMaxLength(256);
                entity.Property(static job => job.EmploymentStatus).HasMaxLength(128);
                entity.Property(static job => job.CompanyApplyUrl).HasMaxLength(2048);
                entity.Property(static job => job.DetailContentFingerprint).HasMaxLength(128);
                entity.Property(static job => job.AiLabel).HasMaxLength(64);
                entity.Property(static job => job.RowVersion).IsRowVersion();

                entity.Property(static job => job.CurrentStatus)
                    .HasConversion<string>()
                    .HasMaxLength(32);

                entity.HasIndex(static job => job.LinkedInJobId).IsUnique();
                entity.HasIndex(static job => job.LinkedInJobPostingUrn).IsUnique();
                entity.HasIndex(static job => job.CurrentStatus);
                entity.HasIndex(static job => job.AiLabel);
                entity.HasIndex(static job => job.AiScore);
                entity.HasIndex(static job => job.LastSeenAtUtc);
                entity.HasIndex(static job => job.LastDetailSyncedAtUtc);
                entity.HasIndex(static job => job.ListedAtUtc);
                entity.HasIndex(static job => job.LinkedInUpdatedAtUtc);
            });

        modelBuilder.Entity<AppUserRecord>(
            entity =>
            {
                entity.ToTable("AppUsers");
                entity.HasKey(static user => user.Id);

                entity.Property(static user => user.UserName).HasMaxLength(128).IsRequired();
                entity.Property(static user => user.DisplayName).HasMaxLength(256).IsRequired();
                entity.Property(static user => user.PasswordHash).HasMaxLength(1024).IsRequired();

                entity.HasIndex(static user => user.UserName).IsUnique();
                entity.HasIndex(static user => user.IsActive);
                entity.HasIndex(static user => user.ExpiresAtUtc);
            });

        modelBuilder.Entity<JobStatusHistoryRecord>(
            entity =>
            {
                entity.ToTable("JobStatusHistory");
                entity.HasKey(static status => status.Id);

                entity.Property(static status => status.Status)
                    .HasConversion<string>()
                    .HasMaxLength(32);

                entity.Property(static status => status.Notes).HasMaxLength(1000);

                entity.HasIndex(static status => new { status.JobRecordId, status.ChangedAtUtc });

                entity.HasOne(static status => status.JobRecord)
                    .WithMany(static job => job.StatusHistory)
                    .HasForeignKey(static status => status.JobRecordId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

        modelBuilder.Entity<LinkedInSessionRecord>(
            entity =>
            {
                entity.ToTable("LinkedInSessions");
                entity.HasKey(static session => session.Id);

                entity.Property(static session => session.SessionKey).HasMaxLength(128).IsRequired();
                entity.Property(static session => session.Source).HasMaxLength(128).IsRequired();
                entity.Property(static session => session.RequestHeadersJson).IsRequired();

                entity.HasIndex(static session => session.SessionKey).IsUnique();
            });

        modelBuilder.Entity<AiBehaviorSettingsRecord>(
            entity =>
            {
                entity.ToTable("AiBehaviorSettings");
                entity.HasKey(static settings => settings.Id);

                entity.Property(static settings => settings.ProfileName).HasMaxLength(128).IsRequired();
                entity.Property(static settings => settings.OutputLanguageCode).HasMaxLength(8).IsRequired();
                entity.Property(static settings => settings.RowVersion).IsRowVersion();
            });

        modelBuilder.Entity<LinkedInSearchSettingsRecord>(
            entity =>
            {
                entity.ToTable("LinkedInSearchSettings");
                entity.HasKey(static settings => settings.Id);

                entity.Property(static settings => settings.ProfileName).HasMaxLength(128).IsRequired();
                entity.Property(static settings => settings.Keywords).HasMaxLength(512).IsRequired();
                entity.Property(static settings => settings.LocationInput).HasMaxLength(256);
                entity.Property(static settings => settings.LocationDisplayName).HasMaxLength(256);
                entity.Property(static settings => settings.LocationGeoId).HasMaxLength(32);
                entity.Property(static settings => settings.WorkplaceTypeCodesCsv).HasMaxLength(128).IsRequired();
                entity.Property(static settings => settings.JobTypeCodesCsv).HasMaxLength(128).IsRequired();
                entity.Property(static settings => settings.RowVersion).IsRowVersion();
            });

        modelBuilder.Entity<AiGlobalShortlistRunRecord>(
            entity =>
            {
                entity.ToTable("AiGlobalShortlistRuns");
                entity.HasKey(static run => run.Id);

                entity.Property(static run => run.Status).HasMaxLength(32).IsRequired();
                entity.Property(static run => run.PromptVersion).HasMaxLength(64);
                entity.Property(static run => run.ModelName).HasMaxLength(128);
                entity.Property(static run => run.Summary).HasMaxLength(2000);

                entity.HasIndex(static run => run.CreatedAtUtc);
                entity.HasIndex(static run => run.CompletedAtUtc);
                entity.HasIndex(static run => run.Status);
            });

        modelBuilder.Entity<AiGlobalShortlistRunCandidateRecord>(
            entity =>
            {
                entity.ToTable("AiGlobalShortlistRunCandidates");
                entity.HasKey(static candidate => candidate.Id);

                entity.Property(static candidate => candidate.Status).HasMaxLength(32).IsRequired();

                entity.HasIndex(static candidate => candidate.RunId);
                entity.HasIndex(static candidate => candidate.JobRecordId);
                entity.HasIndex(static candidate => new { candidate.RunId, candidate.SequenceNumber }).IsUnique();
                entity.HasIndex(static candidate => new { candidate.RunId, candidate.JobRecordId }).IsUnique();

                entity.HasOne(static candidate => candidate.Run)
                    .WithMany(static run => run.Candidates)
                    .HasForeignKey(static candidate => candidate.RunId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(static candidate => candidate.JobRecord)
                    .WithMany(static job => job.GlobalShortlistRunCandidates)
                    .HasForeignKey(static candidate => candidate.JobRecordId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

        modelBuilder.Entity<AiGlobalShortlistItemRecord>(
            entity =>
            {
                entity.ToTable("AiGlobalShortlistItems");
                entity.HasKey(static item => item.Id);

                entity.Property(static item => item.Decision).HasMaxLength(32).IsRequired();
                entity.Property(static item => item.PromptVersion).HasMaxLength(64);
                entity.Property(static item => item.ModelName).HasMaxLength(128);
                entity.Property(static item => item.ErrorCode).HasMaxLength(128);
                entity.Property(static item => item.RecommendationReason).HasMaxLength(2000);
                entity.Property(static item => item.Concerns).HasMaxLength(2000);

                entity.HasIndex(static item => item.RunId);
                entity.HasIndex(static item => item.JobRecordId);
                entity.HasIndex(static item => new { item.RunId, item.Decision });
                entity.HasIndex(static item => new { item.RunId, item.Rank }).IsUnique();
                entity.HasIndex(static item => new { item.RunId, item.JobRecordId }).IsUnique();

                entity.HasOne(static item => item.Run)
                    .WithMany(static run => run.Items)
                    .HasForeignKey(static item => item.RunId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(static item => item.JobRecord)
                    .WithMany(static job => job.GlobalShortlistItems)
                    .HasForeignKey(static item => item.JobRecordId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
    }
}
