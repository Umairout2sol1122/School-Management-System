IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Books' AND schema_id = SCHEMA_ID('Tenant'))
BEGIN
    CREATE TABLE [Tenant].[Books] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [OrganizationId] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NULL,
        [Title] nvarchar(250) NOT NULL,
        [Author] nvarchar(200) NOT NULL,
        [Isbn] nvarchar(50) NULL,
        [Publisher] nvarchar(200) NULL,
        [Category] nvarchar(100) NOT NULL DEFAULT 'General',
        [TotalCopies] int NOT NULL DEFAULT 1,
        [AvailableCopies] int NOT NULL DEFAULT 1,
        [RackNumber] nvarchar(50) NULL,
        [IsActive] bit NOT NULL DEFAULT 1,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy] nvarchar(max) NOT NULL DEFAULT 'System',
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL DEFAULT 0,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedByUserId] nvarchar(max) NULL,
        [SyncStatus] int NOT NULL DEFAULT 0,
        [SyncVersion] bigint NOT NULL DEFAULT 1,
        [RowVersion] rowversion NULL
    );
    CREATE INDEX [IX_Books_Org_Branch] ON [Tenant].[Books] ([OrganizationId], [BranchId]);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BookIssues' AND schema_id = SCHEMA_ID('Tenant'))
BEGIN
    CREATE TABLE [Tenant].[BookIssues] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [OrganizationId] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NULL,
        [BookId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NULL,
        [StaffProfileId] uniqueidentifier NULL,
        [IssueDate] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [DueDate] datetime2 NOT NULL,
        [ReturnDate] datetime2 NULL,
        [FineAmount] decimal(18,2) NOT NULL DEFAULT 0.00,
        [Status] int NOT NULL DEFAULT 1,
        [Remarks] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy] nvarchar(max) NOT NULL DEFAULT 'System',
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL DEFAULT 0,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedByUserId] nvarchar(max) NULL,
        [SyncStatus] int NOT NULL DEFAULT 0,
        [SyncVersion] bigint NOT NULL DEFAULT 1,
        [RowVersion] rowversion NULL
    );
    CREATE INDEX [IX_BookIssues_Org_Book] ON [Tenant].[BookIssues] ([OrganizationId], [BookId]);
    CREATE INDEX [IX_BookIssues_Student] ON [Tenant].[BookIssues] ([StudentId]);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Vehicles' AND schema_id = SCHEMA_ID('Tenant'))
BEGIN
    CREATE TABLE [Tenant].[Vehicles] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [OrganizationId] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NULL,
        [VehicleNumber] nvarchar(50) NOT NULL,
        [Model] nvarchar(100) NOT NULL,
        [Capacity] int NOT NULL DEFAULT 30,
        [DriverName] nvarchar(150) NOT NULL,
        [DriverPhone] nvarchar(50) NOT NULL,
        [DriverLicenseNumber] nvarchar(100) NULL,
        [Status] int NOT NULL DEFAULT 1,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy] nvarchar(max) NOT NULL DEFAULT 'System',
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL DEFAULT 0,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedByUserId] nvarchar(max) NULL,
        [SyncStatus] int NOT NULL DEFAULT 0,
        [SyncVersion] bigint NOT NULL DEFAULT 1,
        [RowVersion] rowversion NULL
    );
    CREATE INDEX [IX_Vehicles_Org_Branch] ON [Tenant].[Vehicles] ([OrganizationId], [BranchId]);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TransportRoutes' AND schema_id = SCHEMA_ID('Tenant'))
BEGIN
    CREATE TABLE [Tenant].[TransportRoutes] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [OrganizationId] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NULL,
        [VehicleId] uniqueidentifier NULL,
        [RouteName] nvarchar(150) NOT NULL,
        [StartLocation] nvarchar(200) NOT NULL,
        [EndLocation] nvarchar(200) NOT NULL,
        [StopsJson] nvarchar(max) NULL,
        [MonthlyFare] decimal(18,2) NOT NULL DEFAULT 0.00,
        [IsActive] bit NOT NULL DEFAULT 1,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy] nvarchar(max) NOT NULL DEFAULT 'System',
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL DEFAULT 0,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedByUserId] nvarchar(max) NULL,
        [SyncStatus] int NOT NULL DEFAULT 0,
        [SyncVersion] bigint NOT NULL DEFAULT 1,
        [RowVersion] rowversion NULL
    );
    CREATE INDEX [IX_TransportRoutes_Org_Vehicle] ON [Tenant].[TransportRoutes] ([OrganizationId], [VehicleId]);
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TransportAllocations' AND schema_id = SCHEMA_ID('Tenant'))
BEGIN
    CREATE TABLE [Tenant].[TransportAllocations] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [OrganizationId] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [TransportRouteId] uniqueidentifier NOT NULL,
        [StopName] nvarchar(150) NULL,
        [StartDate] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [EndDate] datetime2 NULL,
        [IsActive] bit NOT NULL DEFAULT 1,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy] nvarchar(max) NOT NULL DEFAULT 'System',
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL DEFAULT 0,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedByUserId] nvarchar(max) NULL,
        [SyncStatus] int NOT NULL DEFAULT 0,
        [SyncVersion] bigint NOT NULL DEFAULT 1,
        [RowVersion] rowversion NULL
    );
    CREATE INDEX [IX_TransportAllocations_Student] ON [Tenant].[TransportAllocations] ([StudentId]);
    CREATE INDEX [IX_TransportAllocations_Route] ON [Tenant].[TransportAllocations] ([TransportRouteId]);
END
