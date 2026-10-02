# SCHOOL MANAGEMENT SYSTEM — PHASE 1 STRATEGIES & ROADMAP

**Document Version:** 1.0.0  
**Phase:** 1 — Discovery & Brain Analysis  
**Location:** `Brain/PHASE_1_ROADMAP_AND_STRATEGY.md`  

---

## Deliverable 1: Architecture Document

### 1.1 Architecture Pattern: Clean Architecture + Offline-First Multi-Platform
The School Management System utilizes a **Clean Architecture** (Ports & Adapters) modular monolith for the backend service and a reactive **MVVM (Model-View-ViewModel)** architecture for the .NET MAUI desktop and mobile client.

```
+-----------------------------------------------------------------------------------------+
|                                      PRESENTATION                                       |
|                                                                                         |
|   .NET MAUI Client (Desktop & Mobile)           ASP.NET Core Web API (Server Host)     |
|   - Views (XAML + Responsive Code-behind)       - REST Controllers & Minimal Endpoints  |
|   - ViewModels (CommunityToolkit.Mvvm)          - JWT / Bearer Auth & Policy Handlers   |
|   - Reusable UI Controls (Pak2Source Theme)     - Swagger / OpenAPI Documentation       |
|   - Converters, Behaviors & Navigators          - Global Exception & Audit Middleware   |
+-----------------------------------------------------------------------------------------+
                                           |
                                           v
+-----------------------------------------------------------------------------------------+
|                                    APPLICATION CORE                                     |
|                                                                                         |
|   - Use Cases / Command & Query Handlers (CQRS / Service Layer)                         |
|   - Application Interfaces (IRepository, ISyncService, ITenantContext, IEmailService)   |
|   - Validation Rules (FluentValidation)                                                 |
|   - Data Transfer Objects (DTOs), ViewModels & Mapping Profiles                         |
|   - Event Handlers & Domain Notification Dispatchers                                    |
+-----------------------------------------------------------------------------------------+
                                           |
                                           v
+-----------------------------------------------------------------------------------------+
|                                      DOMAIN LAYER                                       |
|                                                                                         |
|   - Pure Enterprise Entities (Organization, Branch, User, Student, Fee, Exam, etc.)     |
|   - Value Objects (Money, Address, ContactNumber, AcademicTerm)                         |
|   - Domain Logic & Aggregate Roots                                                      |
|   - Domain Enums (AttendanceStatus, InvoiceStatus, UserRoleType)                        |
|   - Zero Third-Party Framework Dependencies                                             |
+-----------------------------------------------------------------------------------------+
                                           ^
                                           |
+-----------------------------------------------------------------------------------------+
|                                     INFRASTRUCTURE                                      |
|                                                                                         |
|   Central SQL Server Provider (EF Core + Dapper)    Client Local Provider (SQLite)      |
|   - ApplicationDbContext with Global Tenant Filter   - LocalDbContext / SQLiteStore     |
|   - SQL Server Database Migrations                   - Local SyncQueue & Delta Tracking |
|   - Auditing & Shadow Properties                     - Encrypted Local SQLite DB        |
|   - Central Outbox & Event Persistence               - Device Hardware & File Access    |
|   - Serilog Structured File Logging                  - Network Connectivity Monitor     |
+-----------------------------------------------------------------------------------------+
```

### 1.2 Solution & Project Structure (.NET 10)
```text
SchoolManagement/
├── SchoolManagement.slnx                       # Solution File (.NET 10)
├── Directory.Build.props                      # Central build & package versioning
├── Brain/                                     # Master Architectural Source of Truth
│   ├── AI_DEVELOPMENT_RULES.md
│   ├── EXAM_THEME_DESIGN_RULE.md
│   ├── DEVELOPMENT_UNDERSTANDING.md
│   └── PHASE_1_ROADMAP_AND_STRATEGY.md
├── src/
│   ├── SchoolManagement.Domain/               # Pure domain models, entities, enums
│   ├── SchoolManagement.Application/          # Services, DTOs, interfaces, business logic
│   ├── SchoolManagement.Infrastructure/       # SQL Server, EF Core, Dapper, Serilog
│   ├── SchoolManagement.Api/                  # ASP.NET Core 10 Web API (Central Host)
│   ├── SchoolManagement.Client.Core/          # Shared client logic, SQLite, Sync Engine
│   └── SchoolManagement.Client.Maui/          # .NET MAUI desktop & mobile app
└── tests/
    ├── SchoolManagement.UnitTests/            # Domain & Application logic unit tests
    ├── SchoolManagement.IntegrationTests/     # Database, EF Core & repository tests
    ├── SchoolManagement.SecurityTests/        # Multi-tenant isolation & RBAC authorization tests
    └── SchoolManagement.SyncTests/            # Offline-online synchronization & conflict tests
```

---

## Deliverable 2: Module Roadmap

| Phase | Module Name | Scope & Key Capabilities |
| :---: | :--- | :--- |
| **Phase 2** | **Core Foundation** | Solution setup, DI containers, configuration, Serilog file logging, base entities, result monads, error handlers, Pak2Source theme resources. |
| **Phase 3** | **Database Foundation** | Central SQL Server schemas, SQLite local schema, EF Core migrations, global tenant query filters, indexes, constraints, soft deletion. |
| **Phase 4** | **Auth & RBAC** | Password hashing (PBKDF2/Argon2), JWT token issuing, refresh tokens, session management, permissions evaluator, user activation/locking. |
| **Phase 5** | **SuperAdmin Portal** | Platform overview, Organization CRUD, Branch setup, Org Admin assignments, global feature flags, system-wide audit explorer. |
| **Phase 6** | **Org Admin Portal** | School dashboard, branch switching, role management, custom permission assignment, school academic profile, custom branding setup. |
| **Phase 7** | **Student & Parent** | Admission workflow, enrollment ID generation, student profiles, parent/guardian linking, sibling connections, emergency contacts. |
| **Phase 8** | **Academics** | Academic years/terms, classes, sections, subjects, teacher-to-subject assignments, syllabus tracking, student promotion workflows. |
| **Phase 9** | **Attendance** | Student, teacher, and staff attendance; status (Present, Absent, Late, Leave, HalfDay); offline barcode/quick mark, sync reconciliation. |
| **Phase 10**| **Fee Management** | Fee heads, structure templates, student fee ledger, invoice generation, discount/scholarship rules, partial payments, receipts. |
| **Phase 11**| **Examinations** | Exam scheduling, mark sheets, grading scales (GPA/letter), automated position calculation, report card generation, result publishing. |
| **Phase 12**| **Timetable** | Class schedules, teacher schedules, room allocations, automated conflict detection (prevent teacher/room double-booking). |
| **Phase 13**| **Homework** | Assignments creation, attachments, student submission tracking, teacher grading and remarks UX. |
| **Phase 14**| **Communication** | Multi-channel notice board, event announcements, targeted alerts (parents/teachers), push notification stubs. |
| **Phase 15**| **Sync Engine** | Bi-directional synchronization engine, outbox sync queue, idempotency deduplication, conflict resolution policies, retry handling. |
| **Phase 16**| **Reporting** | Printable student transcripts, fee collection summaries, attendance analytics, PDF generation, CSV/Excel data exports. |
| **Phase 17**| **Security Hardening**| Cross-tenant penetration tests, SQL injection validation, token revocation tests, rate limiting verification. |
| **Phase 18**| **Performance** | Database query profiling, indexing verification, large dataset pagination ($10,000+$ records), async UI responsiveness. |
| **Phase 19**| **System QA** | End-to-end multi-tenant validation, complete offline-to-online lifecycle acceptance, disaster recovery tests. |
| **Phase 20**| **Production Readiness**| Final build verification, documentation sign-off, deployment runbooks, health check endpoints. |

---

## Deliverable 3: Database Roadmap

### 3.1 Conventions & Schemas
* **Database Engine:** Microsoft SQL Server 2019/2022 on development instance `.` with user `sa`.
* **Primary Key Convention:** `Guid` (`uniqueidentifier`) for all distributed entity keys to prevent offline ID collision.
* **Audit Shadow Properties (Inherited from `BaseEntity`):**
  * `Id (Guid, PK)`
  * `OrganizationId (Guid, FK -> Organizations.Id, Indexed)`
  * `BranchId (Guid, Nullable FK -> Branches.Id, Indexed)`
  * `CreatedAtUtc (DateTime2(7))`
  * `CreatedBy (NVarChar(100))`
  * `UpdatedAtUtc (DateTime2(7), Nullable)`
  * `UpdatedBy (NVarChar(100), Nullable)`
  * `IsDeleted (Bit, Default 0, Indexed with Filtered Index)`
  * `RowVersion (RowVersion / Timestamp for optimistic concurrency)`
  * `SyncVersion (BigInt, incrementing sequence for sync tracking)`

### 3.2 Schema Architecture
```sql
-- Core Platform Schemas
CREATE SCHEMA [Tenant];      -- Organizations, Branches, Subscriptions, Branding
CREATE SCHEMA [Security];    -- Users, Roles, Permissions, UserRoles, RolePermissions, RefreshTokens
CREATE SCHEMA [Academic];    -- Sessions, Classes, Sections, Subjects, ClassSubjects
CREATE SCHEMA [Student];     -- Students, Guardians, Enrollments, StudentDocuments
CREATE SCHEMA [Staff];       -- Teachers, StaffProfiles, Qualifications, TeacherAssignments
CREATE SCHEMA [Attendance];  -- StudentAttendance, StaffAttendance, AttendanceCorrections
CREATE SCHEMA [Finance];     -- FeeHeads, FeeStructures, Invoices, InvoiceItems, Payments
CREATE SCHEMA [Exam];        -- ExamTypes, Exams, ExamSchedules, ExamMarks, GradeScales
CREATE SCHEMA [Schedule];    -- Timetables, RoomAllocations, Periods
CREATE SCHEMA [Audit];       -- AuditLogs, SystemEvents
CREATE SCHEMA [Sync];        -- SyncLog, ChangeTracking, IdempotencyRecords
```

### 3.3 Strict Tenant Isolation Rules
1. Every table in non-platform schemas **must** have an `OrganizationId` column.
2. Filtered unique constraints must enforce tenant scoping:
   * E.g., `RegistrationNumber` unique within `OrganizationId`:
     `CREATE UNIQUE INDEX UQ_Student_RegNo ON [Student].[Students](OrganizationId, RegistrationNumber) WHERE IsDeleted = 0;`
3. EF Core Global Query Filter applied dynamically in `ApplicationDbContext.OnModelCreating`.

---

## Deliverable 4: Testing Strategy

Per Rule 12 and Rule 13, testing is developed **test-first** alongside each module.

### 4.1 Test Hierarchy & Scenario Matrix
For every functional feature, seven distinct test categories are mandatory:
1. **Positive Scenarios (TC-POS):** Valid data yields successful creation, modification, or calculation.
2. **Negative Scenarios (TC-NEG):** Validation failures on missing required fields, illegal values, or corrupt formats.
3. **Authorization & Tenant Isolation Scenarios (TC-SEC):** Organization A cannot read, query, update, or delete Organization B data; unauthenticated calls rejected with 401; unauthorized roles rejected with 403.
4. **Boundary Scenarios (TC-BND):** Minimum values, maximum length strings, zero amounts, empty collections, very large numbers.
5. **Offline & Sync Scenarios (TC-OFF):** Operations performed with network disabled; local persistence verified; queue insertion confirmed; sync upon reconnect verified.
6. **Concurrency & Idempotency Scenarios (TC-CON):** Dual submission with identical idempotency token results in single processing; concurrent updates trigger optimistic concurrency handling.
7. **Database Scenarios (TC-DAT):** Foreign key constraint enforcement, soft-deletion exclusion in queries, transaction rollback upon mid-operation failure.

### 4.2 Standard Test Case Specification Format
```text
Test Case ID: TC-[MODULE]-[NUMBER]
Module:       [Module Name]
Scenario:     [Scenario Description]
Category:     [Positive | Negative | Security | Boundary | Offline | Concurrency | Database]
Preconditions:[Initial State]
Input:        [Specific Test Payload]
Expected:     [Verifiable Assertion]
Actual:       [Recorded Output]
Status:       [PASS | FAIL]
```

---

## Deliverable 5: Offline / Online Synchronization Strategy

### 5.1 Architecture: The Outbox Sync Engine
```
Local Client (SQLite)                              Server (Central SQL Server)
====================                              ===========================
[User Action in App]
        |
        v
Insert/Update Local Entity
  + SyncStatus = 'Pending'
  + ClientSyncId = GUID
        |
        v
Insert into Local [SyncQueue]
  (Payload, EntityName, Action, Timestamp)
        |
    [Offline?] ----> Wait in Queue (App remains 100% operational)
        |
     [Online]
        |
        v
SyncEngine Background Worker
        |
  HTTP POST /api/sync/push
  { Batch: [SyncQueueItem] } ------------------------> Ingestion Endpoint
                                                            |
                                                       Verify JWT & Tenant
                                                            |
                                                       Check Idempotency Cache
                                                       (ClientSyncId)
                                                            |
                                                       Begin Transaction
                                                       Apply Entity Mutation
                                                       Check RowVersion / Conflict
                                                            |
                                                       Commit & Return Acks
                                                            |
  HTTP 200 OK { Acks: [Result] } <--------------------------+
        |
Mark Local Entity SyncStatus = 'Synced'
Remove from / Mark Completed in [SyncQueue]
        |
Pull Remote Changes:
  HTTP GET /api/sync/pull?since={timestamp} ---------> Query Central Changes
                                                       WHERE OrganizationId = @OrgId
                                                       AND SyncVersion > @LastVersion
                                                            |
Update Local SQLite Database <----------------------- Return Change Delta
```

### 5.2 Conflict Resolution Matrix
* **Last-Write-Wins (LWW) with Audit:** Updates to non-critical fields (e.g., student contact information) use client timestamp with server validation.
* **Server Authority:** Security roles, permission revocations, and invoice numbers are strictly generated/validated by central server.
* **Additive Operational Aggregation:** Attendance entries and fee transaction receipts are immutable event logs, preventing destructive overwrite.

---

## 6. Deliverable 6: Security Strategy

### 6.1 Authentication Architecture
* **Algorithm:** Password hashing using PBKDF2 (SHA-512, 100,000 iterations) or Argon2id with unique cryptographic salt per user.
* **Token Standard:** Stateless JWT access tokens (short-lived, 15-minute expiry) paired with securely stored, revokable refresh tokens in SQL Server.
* **Tenant Binding:** JWT payload strictly includes claims:
  * `sub`: User ID
  * `tenant_id`: Organization ID
  * `branch_id`: Default Branch ID (if applicable)
  * `role`: User Role Type
  * `permissions`: Array of granted permission strings

### 6.2 Defense-in-Depth Tenant Isolation
1. **Client Isolation:** Client SQLite storage is scoped only to the currently logged-in user and organization.
2. **Transport Security:** All communication strictly over HTTPS with payload integrity validation.
3. **Application Layer:** Custom authorization middleware `TenantAuthorizationMiddleware` extracts `tenant_id` from validated claims and sets `ITenantContext`.
4. **Data Access Layer:** Global query filters on EF Core guarantee that every query automatically appends `WHERE OrganizationId = @CurrentTenantId`.

### 6.3 Audit & Anti-Tampering
* Zero logging of sensitive data (no passwords, tokens, or payment card details).
* All security-critical events (login, failed login, lockouts, permission modifications, cross-tenant attempts) recorded to immutable structured audit logs.

---

## Deliverable 7: UI / Theme Strategy (Pak2Source Design System)

### 7.1 Visual Foundation & Pak2Source Tokens
In compliance with `pak2source.com` branding and `Brain/EXAM_THEME_DESIGN_RULE.md`:
* **Colors:**
  * Primary Accent: `#6C5CE7`
  * Primary Deep: `#5840BA`
  * Surface Dark: `#121214` (Background), `#1A1A1E` (Card Container), `#24242A` (Elevated)
  * Surface Light: `#F8F9FD` (Background), `#FFFFFF` (Card Container), `#E2E8F0` (Border)
* **Design Principles:**
  * Clean, 2026 SaaS aesthetic.
  * No arbitrary decorative borders (specifically no unwarranted green section header lines).
  * High-contrast typography for readability.
  * Mobile-responsive layout matrices: forms automatically stack from 3-4 columns on desktop to 1 column on mobile.
  * Operationally complete tables: internal horizontal and vertical scrolling, sticky headers, zero column deletion.
  * Responsive feedback: loading skeletons, progress bars during sync, non-intrusive toast notifications, accessible dialogs.

---

## 8. Phase 1 Sign-Off & Verification

Phase 1 discovery, Brain analysis, and architecture specifications are complete and committed to the project source of truth. All requirements from the Master Development Prompt are mapped to concrete, executable phases. Proceeding to **Phase 2 — Core Foundation**.
