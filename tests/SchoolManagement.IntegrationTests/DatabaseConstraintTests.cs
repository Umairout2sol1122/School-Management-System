using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Audit;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.IntegrationTests;

public class DatabaseConstraintTests
{
    private ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var tenantContext = new TenantContext();
        tenantContext.SetTenant(null, null, isSuperAdmin: true);

        return new ApplicationDbContext(options, tenantContext);
    }

    [Fact]
    public async Task AuditLog_ShouldPersistDetailsAndBeQueryable()
    {
        // Arrange
        string dbName = $"AuditDb_{Guid.NewGuid()}";
        using var context = CreateContext(dbName);
        var orgId = Guid.NewGuid();

        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Action = AuditAction.Create,
            Module = "Tenant",
            EntityName = "Organization",
            EntityId = orgId.ToString(),
            Details = "Created organization Alpha Grammar School",
            UserEmail = "superadmin@pak2source.com"
        };

        // Act
        context.AuditLogs.Add(audit);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.AuditLogs.FirstOrDefaultAsync(a => a.Id == audit.Id);
        saved.Should().NotBeNull();
        saved!.Details.Should().Contain("Alpha Grammar School");
        saved.Action.Should().Be(AuditAction.Create);
    }

    [Fact]
    public async Task SyncQueueItem_ShouldPersistWithPendingStatus()
    {
        // Arrange
        string dbName = $"SyncDb_{Guid.NewGuid()}";
        using var context = CreateContext(dbName);
        var orgId = Guid.NewGuid();

        var item = new SyncQueueItem
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ClientSyncId = Guid.NewGuid(),
            EntityName = "Student",
            EntityId = Guid.NewGuid(),
            Action = SyncAction.Insert,
            PayloadJson = "{\"FirstName\":\"Bilal\",\"LastName\":\"Khan\"}",
            SyncStatus = SyncStatus.Pending
        };

        // Act
        context.SyncQueue.Add(item);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.SyncQueue.FirstOrDefaultAsync(q => q.Id == item.Id);
        saved.Should().NotBeNull();
        saved!.SyncStatus.Should().Be(SyncStatus.Pending);
        saved.EntityName.Should().Be("Student");
    }
}
