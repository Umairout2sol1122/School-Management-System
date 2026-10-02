using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedInitialDataAsync(
        ApplicationDbContext context,
        IPasswordHasher passwordHasher,
        CancellationToken cancellationToken = default)
    {
        // 1. Seed Permissions
        var existingPermissionCodes = await context.Permissions
            .IgnoreQueryFilters()
            .Select(p => p.Code)
            .ToListAsync(cancellationToken);

        var permissionsToInsert = new List<Permission>();
        foreach (string code in Permissions.All)
        {
            if (!existingPermissionCodes.Contains(code))
            {
                string[] parts = code.Split('.');
                string module = parts.Length > 0 ? parts[0] : "General";
                string name = parts.Length > 1 ? parts[1] : code;

                permissionsToInsert.Add(new Permission
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    Name = name,
                    Module = module,
                    Description = $"Permission to {name} in {module}"
                });
            }
        }

        if (permissionsToInsert.Any())
        {
            context.Permissions.AddRange(permissionsToInsert);
            await context.SaveChangesAsync(cancellationToken);
        }

        // 2. Seed System Roles
        var existingRoles = await context.Roles
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        var allPermissions = await context.Permissions
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        var systemRoleNames = new[]
        {
            (nameof(UserRoleType.SuperAdmin), "Global Platform Super Administrator"),
            (nameof(UserRoleType.OrganizationAdmin), "Organization School Administrator"),
            (nameof(UserRoleType.Principal), "School Principal / Head of Campus"),
            (nameof(UserRoleType.Teacher), "Academic Teaching Staff"),
            (nameof(UserRoleType.Accountant), "Finance and Accounts Officer"),
            (nameof(UserRoleType.Staff), "Administrative Support Staff"),
            (nameof(UserRoleType.Student), "Enrolled Student"),
            (nameof(UserRoleType.Parent), "Parent or Legal Guardian")
        };

        Role? superAdminRole = null;
        Role? orgAdminRole = null;

        foreach (var (roleName, description) in systemRoleNames)
        {
            var role = existingRoles.FirstOrDefault(r => r.Name == roleName && r.IsSystemRole);
            if (role == null)
            {
                role = new Role
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    Description = description,
                    IsSystemRole = true,
                    OrganizationId = null
                };
                context.Roles.Add(role);
                await context.SaveChangesAsync(cancellationToken);
            }

            if (roleName == nameof(UserRoleType.SuperAdmin))
                superAdminRole = role;
            if (roleName == nameof(UserRoleType.OrganizationAdmin))
                orgAdminRole = role;
        }

        // 3. Grant all permissions to SuperAdmin Role
        if (superAdminRole != null)
        {
            var existingRolePerms = await context.RolePermissions
                .IgnoreQueryFilters()
                .Where(rp => rp.RoleId == superAdminRole.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync(cancellationToken);

            var missingRolePerms = allPermissions
                .Where(p => !existingRolePerms.Contains(p.Id))
                .Select(p => new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = superAdminRole.Id,
                    PermissionId = p.Id
                })
                .ToList();

            if (missingRolePerms.Any())
            {
                context.RolePermissions.AddRange(missingRolePerms);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        // 4. Grant Default Permissions to all standard roles
        async Task AssignRolePermissionsAsync(Role? targetRole, IEnumerable<string> codes)
        {
            if (targetRole == null) return;
            var existingRolePerms = await context.RolePermissions
                .IgnoreQueryFilters()
                .Where(rp => rp.RoleId == targetRole.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync(cancellationToken);

            var codeSet = new HashSet<string>(codes);
            var missingRolePerms = allPermissions
                .Where(p => codeSet.Contains(p.Code) && !existingRolePerms.Contains(p.Id))
                .Select(p => new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = targetRole.Id,
                    PermissionId = p.Id
                })
                .ToList();

            if (missingRolePerms.Any())
            {
                context.RolePermissions.AddRange(missingRolePerms);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        var principalRole = existingRoles.FirstOrDefault(r => r.Name == nameof(UserRoleType.Principal));
        var teacherRole = existingRoles.FirstOrDefault(r => r.Name == nameof(UserRoleType.Teacher));
        var accountantRole = existingRoles.FirstOrDefault(r => r.Name == nameof(UserRoleType.Accountant));
        var staffRole = existingRoles.FirstOrDefault(r => r.Name == nameof(UserRoleType.Staff));
        var studentRole = existingRoles.FirstOrDefault(r => r.Name == nameof(UserRoleType.Student));
        var parentRole = existingRoles.FirstOrDefault(r => r.Name == nameof(UserRoleType.Parent));

        // OrgAdmin & Principal: Everything except platform-level Organizations.Manage
        var orgAdminCodes = allPermissions.Select(p => p.Code).Where(c => c != Permissions.OrganizationsManage);
        await AssignRolePermissionsAsync(orgAdminRole, orgAdminCodes);
        await AssignRolePermissionsAsync(principalRole, orgAdminCodes);

        // Teacher Permissions
        await AssignRolePermissionsAsync(teacherRole, new[]
        {
            Permissions.StudentsView,
            Permissions.AttendanceView, Permissions.AttendanceMark, Permissions.AttendanceEdit,
            Permissions.HomeworkView, Permissions.HomeworkCreate, Permissions.HomeworkReview,
            Permissions.ExamsView, Permissions.ExamsEnterMarks,
            Permissions.TimetableView,
            Permissions.AcademicsView,
            Permissions.CommunicationView, Permissions.CommunicationSend
        });

        // Accountant Permissions
        await AssignRolePermissionsAsync(accountantRole, new[]
        {
            Permissions.FeesView, Permissions.FeesCreateStructure, Permissions.FeesGenerateInvoices,
            Permissions.FeesCollectPayment, Permissions.FeesRefund, Permissions.FeesExport,
            Permissions.StudentsView,
            Permissions.ReportsView, Permissions.ReportsExport
        });

        // Staff Permissions
        await AssignRolePermissionsAsync(staffRole, new[]
        {
            Permissions.AttendanceView, Permissions.AttendanceMark,
            Permissions.CommunicationView
        });

        // Student Permissions
        await AssignRolePermissionsAsync(studentRole, new[]
        {
            Permissions.AttendanceView,
            Permissions.HomeworkView,
            Permissions.ExamsView,
            Permissions.TimetableView,
            Permissions.FeesView
        });

        // Parent Permissions
        await AssignRolePermissionsAsync(parentRole, new[]
        {
            Permissions.StudentsView,
            Permissions.AttendanceView,
            Permissions.HomeworkView,
            Permissions.ExamsView,
            Permissions.FeesView,
            Permissions.FeesCollectPayment,
            Permissions.CommunicationView
        });

        // 5. Seed Demo Organization (Pak2Source Academy) first so valid OrganizationId exists
        var demoOrg = await context.Organizations
            .IgnoreQueryFilters()
            .Include(o => o.Branches)
            .FirstOrDefaultAsync(o => o.Code == "P2S-ACADEMY", cancellationToken);

        Branch? mainBranch = null;

        if (demoOrg == null)
        {
            demoOrg = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Pak2Source International Academy",
                Code = "P2S-ACADEMY",
                Email = "admissions@p2s-academy.edu",
                Phone = "+92 51 111 222 333",
                Address = "Sector F-7/2, Islamabad, Pakistan",
                ThemePrimaryColor = "#924EC0",
                ThemeSecondaryColor = "#5840BA",
                ThemeDarkMode = true,
                IsActive = true
            };

            mainBranch = new Branch
            {
                Id = Guid.NewGuid(),
                OrganizationId = demoOrg.Id,
                Name = "Main Campus Islamabad",
                Code = "BR-MAIN",
                Address = "Sector F-7/2, Islamabad",
                Email = "main@p2s-academy.edu",
                Phone = "+92 51 111 222 333",
                IsMainBranch = true,
                IsActive = true
            };

            demoOrg.Branches.Add(mainBranch);
            context.Organizations.Add(demoOrg);
            context.Branches.Add(mainBranch);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            mainBranch = demoOrg.Branches.FirstOrDefault(b => b.IsMainBranch) ?? demoOrg.Branches.FirstOrDefault();
        }

        // Seed default Organization Modules for all tenant organizations
        var allOrgs = await context.Organizations.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var existingOrgModules = await context.OrganizationModules.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var defaultModuleNames = new[] { "Students", "Teachers", "Academics", "Attendance", "Fees", "Exams", "Timetable", "Homework", "Communication", "Reports" };
        var newModulesToAdd = new List<OrganizationModule>();

        foreach (var org in allOrgs)
        {
            foreach (var modName in defaultModuleNames)
            {
                if (!existingOrgModules.Any(m => m.OrganizationId == org.Id && m.ModuleName.Equals(modName, StringComparison.OrdinalIgnoreCase) && !m.IsDeleted))
                {
                    newModulesToAdd.Add(new OrganizationModule
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = org.Id,
                        ModuleName = modName,
                        IsEnabled = true
                    });
                }
            }
        }

        if (newModulesToAdd.Any())
        {
            context.OrganizationModules.AddRange(newModulesToAdd);
            await context.SaveChangesAsync(cancellationToken);
        }

        // 6. Seed SuperAdmin Platform User
        bool superAdminExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.RoleType == UserRoleType.SuperAdmin, cancellationToken);

        if (!superAdminExists && superAdminRole != null)
        {
            var (hash, salt) = passwordHasher.HashPassword("SuperAdmin@2026!");

            var superAdminUser = new User
            {
                Id = Guid.NewGuid(),
                OrganizationId = demoOrg.Id,
                BranchId = mainBranch?.Id,
                Username = "superadmin",
                Email = "superadmin@pak2source.com",
                NormalizedEmail = "SUPERADMIN@PAK2SOURCE.COM",
                FirstName = "Global",
                LastName = "SuperAdmin",
                PasswordHash = hash,
                PasswordSalt = salt,
                RoleType = UserRoleType.SuperAdmin,
                IsActive = true
            };

            context.Users.Add(superAdminUser);
            context.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = superAdminUser.Id,
                RoleId = superAdminRole.Id
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        // 7. Seed Demo Organization Admin (orgadmin@p2s-academy.edu)
        bool orgAdminExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == "orgadmin@p2s-academy.edu" || u.Email == "admin@p2s-academy.edu", cancellationToken);

        if (!orgAdminExists && orgAdminRole != null)
        {
            var (orgAdminHash, orgAdminSalt) = passwordHasher.HashPassword("Admin@2026!Pak");
            var orgAdminUser = new User
            {
                Id = Guid.NewGuid(),
                OrganizationId = demoOrg.Id,
                BranchId = mainBranch?.Id,
                Username = "orgadmin",
                Email = "orgadmin@p2s-academy.edu",
                NormalizedEmail = "ORGADMIN@P2S-ACADEMY.EDU",
                FirstName = "Principal",
                LastName = "Administrator",
                PasswordHash = orgAdminHash,
                PasswordSalt = orgAdminSalt,
                RoleType = UserRoleType.OrganizationAdmin,
                IsActive = true
            };

            context.Users.Add(orgAdminUser);
            context.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = orgAdminUser.Id,
                RoleId = orgAdminRole.Id
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        // 8. Seed Demo Teacher (fatima.noor@school.edu)
        bool teacherExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == "fatima.noor@school.edu", cancellationToken);

        if (!teacherExists && teacherRole != null)
        {
            var (teacherHash, teacherSalt) = passwordHasher.HashPassword("Teacher@2026!Pak");
            var teacherUser = new User
            {
                Id = Guid.NewGuid(),
                OrganizationId = demoOrg.Id,
                BranchId = mainBranch?.Id,
                Username = "fatima.noor",
                Email = "fatima.noor@school.edu",
                NormalizedEmail = "FATIMA.NOOR@SCHOOL.EDU",
                FirstName = "Fatima",
                LastName = "Noor",
                PasswordHash = teacherHash,
                PasswordSalt = teacherSalt,
                RoleType = UserRoleType.Teacher,
                IsActive = true
            };

            context.Users.Add(teacherUser);
            context.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = teacherUser.Id,
                RoleId = teacherRole.Id
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        // 9. Seed Demo Accountant (accountant@p2s-academy.edu)
        bool accountantExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == "accountant@p2s-academy.edu", cancellationToken);

        if (!accountantExists && accountantRole != null)
        {
            var (accHash, accSalt) = passwordHasher.HashPassword("Accountant@2026!Pak");
            var accUser = new User
            {
                Id = Guid.NewGuid(),
                OrganizationId = demoOrg.Id,
                BranchId = mainBranch?.Id,
                Username = "accountant",
                Email = "accountant@p2s-academy.edu",
                NormalizedEmail = "ACCOUNTANT@P2S-ACADEMY.EDU",
                FirstName = "Kamran",
                LastName = "Akhtar (Accountant)",
                PasswordHash = accHash,
                PasswordSalt = accSalt,
                RoleType = UserRoleType.Accountant,
                IsActive = true
            };

            context.Users.Add(accUser);
            context.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = accUser.Id,
                RoleId = accountantRole.Id
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        // 10. Seed Demo Student (student@p2s-academy.edu)
        bool studentExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == "student@p2s-academy.edu", cancellationToken);

        if (!studentExists && studentRole != null)
        {
            var (stuHash, stuSalt) = passwordHasher.HashPassword("Student@2026!Pak");
            var stuUser = new User
            {
                Id = Guid.NewGuid(),
                OrganizationId = demoOrg.Id,
                BranchId = mainBranch?.Id,
                Username = "student",
                Email = "student@p2s-academy.edu",
                NormalizedEmail = "STUDENT@P2S-ACADEMY.EDU",
                FirstName = "Ali",
                LastName = "Hassan (Grade 10)",
                PasswordHash = stuHash,
                PasswordSalt = stuSalt,
                RoleType = UserRoleType.Student,
                IsActive = true
            };

            context.Users.Add(stuUser);
            context.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = stuUser.Id,
                RoleId = studentRole.Id
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        // 11. Seed Demo Parent (parent@p2s-academy.edu)
        bool parentExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == "parent@p2s-academy.edu", cancellationToken);

        if (!parentExists && parentRole != null)
        {
            var (parentHash, parentSalt) = passwordHasher.HashPassword("Parent@2026!Pak");
            var parentUser = new User
            {
                Id = Guid.NewGuid(),
                OrganizationId = demoOrg.Id,
                BranchId = mainBranch?.Id,
                Username = "parent",
                Email = "parent@p2s-academy.edu",
                NormalizedEmail = "PARENT@P2S-ACADEMY.EDU",
                FirstName = "Dr. Tariq",
                LastName = "Mehmood (Parent)",
                PasswordHash = parentHash,
                PasswordSalt = parentSalt,
                RoleType = UserRoleType.Parent,
                IsActive = true
            };

            context.Users.Add(parentUser);
            context.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = parentUser.Id,
                RoleId = parentRole.Id
            });

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
