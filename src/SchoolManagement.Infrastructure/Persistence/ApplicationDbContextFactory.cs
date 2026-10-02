using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SchoolManagement.Application.Common.Models;

namespace SchoolManagement.Infrastructure.Persistence;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        
        string connectionString = "Server=.;Database=SchoolManagementDb;User Id=sa;Password=out2sol;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        optionsBuilder.UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));

        var tenantContext = new TenantContext();
        // At design time (migrations), treat as SuperAdmin so all entities and schemas map cleanly
        tenantContext.SetTenant(null, null, isSuperAdmin: true);

        return new ApplicationDbContext(optionsBuilder.Options, tenantContext);
    }
}
