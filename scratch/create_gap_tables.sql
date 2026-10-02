IF NOT EXISTS (SELECT * FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'Student' AND t.name = 'AdmissionApplications')
BEGIN
    CREATE TABLE [Student].[AdmissionApplications] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [BranchId] UNIQUEIDENTIFIER NULL,
        [ApplicationNumber] NVARCHAR(50) NOT NULL,
        [FirstName] NVARCHAR(100) NOT NULL,
        [LastName] NVARCHAR(100) NOT NULL,
        [DateOfBirth] DATETIME2 NOT NULL,
        [Gender] INT NOT NULL,
        [DesiredGrade] NVARCHAR(100) NOT NULL,
        [GuardianName] NVARCHAR(150) NULL,
        [GuardianPhone] NVARCHAR(50) NULL,
        [GuardianEmail] NVARCHAR(150) NULL,
        [GuardianRelationship] NVARCHAR(50) NULL,
        [Address] NVARCHAR(500) NULL,
        [Status] INT NOT NULL DEFAULT 1,
        [ReviewNotes] NVARCHAR(1000) NULL,
        [RejectionReason] NVARCHAR(1000) NULL,
        [EnrolledStudentId] UNIQUEIDENTIFIER NULL,
        [DecisionDateUtc] DATETIME2 NULL,
        [ReviewedByUserId] UNIQUEIDENTIFIER NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAtUtc] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0
    );
    CREATE INDEX [IX_AdmissionApplications_Org_Status] ON [Student].[AdmissionApplications] ([OrganizationId], [Status]);
    PRINT 'Table Student.AdmissionApplications created.';
END

IF NOT EXISTS (SELECT * FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'Tenant' AND t.name = 'CommunicationLogs')
BEGIN
    CREATE TABLE [Tenant].[CommunicationLogs] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [BranchId] UNIQUEIDENTIFIER NULL,
        [Title] NVARCHAR(250) NOT NULL,
        [Message] NVARCHAR(MAX) NOT NULL,
        [Channel] NVARCHAR(50) NOT NULL DEFAULT 'InApp',
        [TargetAudience] NVARCHAR(50) NOT NULL DEFAULT 'All',
        [RecipientCount] INT NOT NULL DEFAULT 0,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Delivered',
        [FailureReason] NVARCHAR(500) NULL,
        [SentByUserId] UNIQUEIDENTIFIER NULL,
        [SentByUserEmail] NVARCHAR(150) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAtUtc] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0
    );
    CREATE INDEX [IX_CommunicationLogs_Org] ON [Tenant].[CommunicationLogs] ([OrganizationId], [CreatedAtUtc]);
    PRINT 'Table Tenant.CommunicationLogs created.';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Tenant.TimetableSlots') AND name = 'SubstituteTeacherStaffId')
BEGIN
    ALTER TABLE [Tenant].[TimetableSlots] ADD [SubstituteTeacherStaffId] UNIQUEIDENTIFIER NULL;
    PRINT 'Column SubstituteTeacherStaffId added to Tenant.TimetableSlots.';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Tenant.TimetableSlots') AND name = 'SubstitutionNotes')
BEGIN
    ALTER TABLE [Tenant].[TimetableSlots] ADD [SubstitutionNotes] NVARCHAR(500) NULL;
    PRINT 'Column SubstitutionNotes added to Tenant.TimetableSlots.';
END
