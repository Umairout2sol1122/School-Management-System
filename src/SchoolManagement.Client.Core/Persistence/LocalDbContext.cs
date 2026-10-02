using Microsoft.EntityFrameworkCore;
using SchoolManagement.Client.Core.Models;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Entities.Tenant;

namespace SchoolManagement.Client.Core.Persistence;

public class LocalDbContext : DbContext
{
    private readonly string _dbPath;

    public LocalDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    public LocalDbContext(DbContextOptions<LocalDbContext> options) : base(options)
    {
        _dbPath = string.Empty;
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<SyncQueueItem> SyncQueue => Set<SyncQueueItem>();
    public DbSet<LocalSyncState> SyncStates => Set<LocalSyncState>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured && !string.IsNullOrEmpty(_dbPath))
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Organization>().HasKey(e => e.Id);
        modelBuilder.Entity<Branch>().HasKey(e => e.Id);
        modelBuilder.Entity<User>().HasKey(e => e.Id);
        modelBuilder.Entity<SyncQueueItem>().HasKey(e => e.Id);
        modelBuilder.Entity<LocalSyncState>().HasKey(e => e.Id);

        modelBuilder.Entity<SyncQueueItem>()
            .HasIndex(e => e.ClientSyncId)
            .IsUnique();

        modelBuilder.Entity<SyncQueueItem>()
            .HasIndex(e => new { e.OrganizationId, e.SyncStatus });
    }
}
