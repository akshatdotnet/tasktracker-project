using Microsoft.EntityFrameworkCore;
using TaskTracker.Domain.Common;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Interfaces;

namespace TaskTracker.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext — the single source of truth for database schema.
/// Implements IUnitOfWork so handlers call SaveChangesAsync through the abstraction.
/// Configuration is in separate IEntityTypeConfiguration files (SRP, OCP).
/// </summary>
public sealed class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── DbSets ────────────────────────────────────────────────────────────
    public DbSet<User>                 Users               => Set<User>();
    public DbSet<RefreshToken>         RefreshTokens       => Set<RefreshToken>();
    public DbSet<PasswordResetToken>   PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Project>              Projects            => Set<Project>();
    public DbSet<Sprint>               Sprints             => Set<Sprint>();
    public DbSet<TaskItem>             Tasks               => Set<TaskItem>();
    public DbSet<TimeLog>              TimeLogs            => Set<TimeLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration classes in this assembly automatically
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    /// <summary>
    /// Overrides SaveChangesAsync to automatically set UpdatedAt on modified AuditableEntities.
    /// This eliminates the need for handlers to set UpdatedAt manually.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
