using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Enums;
using TaskStatus = TaskTracker.Domain.Enums.TaskStatus;

namespace TaskTracker.Infrastructure.Persistence.Configurations;

// ── User Configuration ────────────────────────────────────────────────────────
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);
        builder.Property(u => u.Salt).IsRequired().HasMaxLength(256);
        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(100);

        builder.Property(u => u.Role)
            .HasConversion<string>()   // Stored as "Admin", "Developer", "Viewer"
            .HasMaxLength(20);

        builder.Property(u => u.IsActive).HasDefaultValue(true);
        builder.Property(u => u.FailedLoginCount).HasDefaultValue(0);

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.PasswordResetTokens)
            .WithOne(pr => pr.User)
            .HasForeignKey(pr => pr.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.AssignedTasks)
            .WithOne(t => t.AssignedTo)
            .HasForeignKey(t => t.AssignedToId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.TimeLogs)
            .WithOne(tl => tl.User)
            .HasForeignKey(tl => tl.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// ── RefreshToken Configuration ────────────────────────────────────────────────
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.TokenHash).IsRequired().HasMaxLength(128);
        builder.HasIndex(rt => rt.TokenHash).IsUnique();
        builder.Property(rt => rt.IpAddress).HasMaxLength(45); // IPv6 max length
        builder.Property(rt => rt.IsRevoked).HasDefaultValue(false);
    }
}

// ── PasswordResetToken Configuration ─────────────────────────────────────────
public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens");
        builder.HasKey(pr => pr.Id);

        builder.Property(pr => pr.TokenHash).IsRequired().HasMaxLength(128);
        builder.HasIndex(pr => pr.TokenHash).IsUnique();
        builder.Property(pr => pr.IpAddress).HasMaxLength(45);
    }
}

// ── Project Configuration ─────────────────────────────────────────────────────
public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.IsActive).HasDefaultValue(true);

        builder.HasMany(p => p.Tasks)
            .WithOne(t => t.Project)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Sprints)
            .WithOne(s => s.Project)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

// ── Sprint Configuration ──────────────────────────────────────────────────────
public sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable("Sprints");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.IsActive).HasDefaultValue(false);

        builder.HasMany(s => s.Tasks)
            .WithOne(t => t.Sprint)
            .HasForeignKey(t => t.SprintId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

// ── TaskItem Configuration ────────────────────────────────────────────────────
public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).IsRequired().HasMaxLength(500);
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.EstimatedMinutes).HasDefaultValue(60);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(TaskStatus.Pending);

        builder.Property(t => t.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(TaskPriority.Medium);

        // Index for common dashboard queries
        builder.HasIndex(t => t.AssignedToId);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.CompletedAt);

        builder.HasMany(t => t.TimeLogs)
            .WithOne(tl => tl.Task)
            .HasForeignKey(tl => tl.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

// ── TimeLog Configuration ─────────────────────────────────────────────────────
public sealed class TimeLogConfiguration : IEntityTypeConfiguration<TimeLog>
{
    public void Configure(EntityTypeBuilder<TimeLog> builder)
    {
        builder.ToTable("TimeLogs");
        builder.HasKey(tl => tl.Id);
        builder.Property(tl => tl.Notes).HasMaxLength(500);
        builder.Property(tl => tl.DurationMinutes).IsRequired();

        // Index for velocity queries (by date)
        builder.HasIndex(tl => tl.LoggedAt);
        builder.HasIndex(tl => tl.UserId);
    }
}
