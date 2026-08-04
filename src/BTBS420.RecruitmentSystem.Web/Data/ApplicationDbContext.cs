using BTBS420.RecruitmentSystem.Web.Data.Configurations;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Data;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<JobFamily> JobFamilies => Set<JobFamily>();

    public DbSet<Seniority> Seniorities => Set<Seniority>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<JobPosting> JobPostings => Set<JobPosting>();

    public DbSet<Skill> Skills => Set<Skill>();

    public DbSet<Education> Educations => Set<Education>();

    public DbSet<Language> Languages => Set<Language>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<ExperienceRange> ExperienceRanges => Set<ExperienceRange>();

    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();

    public DbSet<CandidateProfileSkill> CandidateProfileSkills => Set<CandidateProfileSkill>();

    public DbSet<CandidateProfileLanguage> CandidateProfileLanguages => Set<CandidateProfileLanguage>();

    public DbSet<CandidateEducation> CandidateEducations => Set<CandidateEducation>();

    public DbSet<CandidateExperience> CandidateExperiences => Set<CandidateExperience>();

    public DbSet<CandidateDocument> CandidateDocuments => Set<CandidateDocument>();

    public DbSet<JobApplication> JobApplications => Set<JobApplication>();

    public DbSet<JobApplicationStatusChange> JobApplicationStatusChanges =>
        Set<JobApplicationStatusChange>();

    public DbSet<ApplicationNote> ApplicationNotes => Set<ApplicationNote>();

    public DbSet<Interview> Interviews => Set<Interview>();

    public DbSet<InterviewParticipant> InterviewParticipants => Set<InterviewParticipant>();

    public DbSet<InterviewEvaluation> InterviewEvaluations => Set<InterviewEvaluation>();

    public DbSet<Offer> Offers => Set<Offer>();

    public override int SaveChanges()
    {
        return SaveChanges(acceptAllChangesOnSuccess: true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureActivityLogsAreAppendOnly();
        EnsureJobApplicationStatusChangesAreAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(
            acceptAllChangesOnSuccess: true,
            cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureActivityLogsAreAppendOnly();
        EnsureJobApplicationStatusChangesAreAppendOnly();
        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .Property(user => user.IsActive)
            .HasDefaultValue(true);

        builder.Entity<ApplicationUser>()
            .HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("EmailIndex")
            .IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL");

        builder.Entity<ApplicationUser>()
            .HasOne(user => user.Department)
            .WithMany()
            .HasForeignKey(user => user.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.ApplyConfiguration(new ActivityLogConfiguration());
        builder.ApplyConfiguration(new NotificationConfiguration());
        builder.ApplyConfiguration(new DepartmentConfiguration());
        builder.ApplyConfiguration(new JobFamilyConfiguration());
        builder.ApplyConfiguration(new SeniorityConfiguration());
        builder.ApplyConfiguration(new PositionConfiguration());
        builder.ApplyConfiguration(new JobPostingConfiguration());
        builder.ApplyConfiguration(new SkillConfiguration());
        builder.ApplyConfiguration(new EducationConfiguration());
        builder.ApplyConfiguration(new LanguageConfiguration());
        builder.ApplyConfiguration(new LocationConfiguration());
        builder.ApplyConfiguration(new ExperienceRangeConfiguration());
        builder.ApplyConfiguration(new CandidateProfileConfiguration());
        builder.ApplyConfiguration(new CandidateProfileSkillConfiguration());
        builder.ApplyConfiguration(new CandidateProfileLanguageConfiguration());
        builder.ApplyConfiguration(new CandidateEducationConfiguration());
        builder.ApplyConfiguration(new CandidateExperienceConfiguration());
        builder.ApplyConfiguration(new CandidateDocumentConfiguration());
        builder.ApplyConfiguration(new JobApplicationConfiguration());
        builder.ApplyConfiguration(new JobApplicationStatusChangeConfiguration());
        builder.ApplyConfiguration(new ApplicationNoteConfiguration());
        builder.ApplyConfiguration(new InterviewConfiguration());
        builder.ApplyConfiguration(new InterviewParticipantConfiguration());
        builder.ApplyConfiguration(new InterviewEvaluationConfiguration());
        builder.ApplyConfiguration(new OfferConfiguration());
    }

    private void EnsureActivityLogsAreAppendOnly()
    {
        var mutatedActivityLog = ChangeTracker
            .Entries<ActivityLog>()
            .FirstOrDefault(
                entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (mutatedActivityLog is not null)
        {
            throw new InvalidOperationException(
                "Aktivite kayıtları yalnızca eklenebilir; güncellenemez veya silinemez.");
        }
    }

    private void EnsureJobApplicationStatusChangesAreAppendOnly()
    {
        var mutatedStatusChange = ChangeTracker
            .Entries<JobApplicationStatusChange>()
            .FirstOrDefault(
                entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (mutatedStatusChange is not null)
        {
            throw new InvalidOperationException(
                "Başvuru durum geçmişi yalnızca eklenebilir; güncellenemez veya silinemez.");
        }
    }
}
