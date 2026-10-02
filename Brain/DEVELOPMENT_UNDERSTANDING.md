# SCHOOL MANAGEMENT SYSTEM — DEVELOPMENT UNDERSTANDING DOCUMENT

**Document Version:** 1.0.0  
**Date:** October 2026  
**Status:** Approved Baseline / Phase 1 Discovery & Brain Analysis  
**Repository Source of Truth:** `Brain/`  

---

## 1. Executive Summary & Objective

This document establishes the official development baseline and architectural understanding for the **School Management System (SMS)** — a modern, commercial-grade, multi-tenant enterprise platform built for both **Online** and **Offline** operations across desktop and mobile devices.

The system is designed to serve multiple educational organizations, each with independent branches, custom branding, granular RBAC permissions, and comprehensive academic workflows (student admissions, parent portals, teacher workloads, attendance tracking, fee invoicing and payment reconciliation, examination grading, class scheduling, homework tracking, communication notices, and administrative reporting).

---

## 2. Brain Analysis & Source of Truth

Per **Rule 0 (Absolute Rule — Read Brain First)** and **AI Development Rules 1–83**, the `Brain/` folder is the permanent authoritative source of truth. All conventions, constraints, and instructions inside `Brain/` take precedence over AI preferences.

### 2.1 Analysis of `Brain/AI_DEVELOPMENT_RULES.md`
The document establishes 83 universal enterprise development principles:
1. **Understand Before Modifying (Rules 1, 4, 5, 81):** No changes without prior inspection. Analysis never modifies source files.
2. **Framework & Language Alignment (Rules 2, 15, 16):** Detect and adhere to project technology (.NET 10, C# 13/14). No random upgrades.
3. **Minimum Change & Scope Control (Rules 8, 9, 74):** Smallest possible footprint for each task; never refactor or "improve" unrelated modules.
4. **No Assumptions (Rule 10):** Never invent table names, columns, APIs, routes, status codes, or permissions. Verify from project codebase or schemas.
5. **Database Safety & Integrity (Rules 22–30):** Parameterized SQL, zero SQL injection risk, strict handling of foreign keys, nullability, transactions, soft-deletes (`IsDeleted`), and concurrency controls.
6. **Security & Secrets (Rules 39–42):** Multi-tenant boundary enforcement at data access layer; never expose secrets, passwords, connection strings, or tokens in code or logs.
7. **Comprehensive Text-Based Logging & Error Handling (Rules 45–47):** Structured logging without sensitive data; user-friendly error messages paired with detailed server-side error logging.
8. **Automated Testing & Build Verification (Rules 62–65):** Test-first development. Tests must be created alongside business code and actually run. Never claim a build or test passed without physical execution.

### 2.2 Analysis of `Brain/EXAM_THEME_DESIGN_RULE.md`
The document sets mandatory standards for UI/UX quality and functional preservation:
1. **Golden Parity Formula:**
   $$\text{Original Functionality} = \text{Redesigned Functionality}$$
   $$\text{Original Business Result} = \text{Redesigned Business Result}$$
   $$\text{Original Backend Output} = \text{Redesigned Backend Output}$$
   $$\text{Redesigned UI/UX} > \text{Original UI/UX}$$
2. **Mobile Responsiveness:** Mandatory on large desktop, laptop, tablet, and mobile phone. No horizontal page overflow, touch-friendly hit targets ($\ge 44\times 44\text{ px}$), clean stacking form grids.
3. **Data Completeness in Tables:** Wide tables must feature internal horizontal/vertical scrolling with sticky headers. Columns and critical operational fields must never be removed for aesthetic simplicity.
4. **Section Design & Visual Hierarchy:** Clear card separation, consistent padding, professional typography, and removal of arbitrary styling artifacts (e.g., no unwarranted green header borders).
5. **Operational State Feedback:** Loading indicators, skeleton loaders, subtle status badges (success, warning, danger, SLA breached), and confirmation modals for destructive actions.

---

## 3. Technology Stack & Environment Discovery

| Layer | Selected & Verified Technology | Configuration & Details |
| :--- | :--- | :--- |
| **SDK / Runtime** | **.NET 10 (SDK 10.0.401)** | .NET 10.0.12 Runtime installed (`win-x64`) |
| **Client UI** | **.NET MAUI (.NET 10)** | Multi-platform app UI targeting Windows Desktop & Mobile, MVVM pattern |
| **Backend API** | **ASP.NET Core 10 Web API** | RESTful services, JWT Bearer Auth, Swagger / OpenAPI, Rate Limiting |
| **Central Database** | **Microsoft SQL Server 2019 / 2022** | Default instance `.` / `localhost`, developer auth (`sa` / `out2sol`) |
| **Data Access** | **EF Core 10 + Dapper (Hybrid)** | EF Core for transactional entities/migrations; Dapper for high-throughput reporting queries |
| **Local Offline DB**| **SQLite (via SQLite-net-pcl / EF Core SQLite)** | Encrypted local database on client device for offline resilience |
| **Sync Engine** | **Bi-directional Sync Service** | Outbox pattern, Sync Queue, Idempotency keys, Conflict Resolution |
| **Testing** | **xUnit + FluentAssertions + Moq** | Unit, integration, security, boundary, and offline synchronization suites |
| **Logging** | **Serilog (Structured Text / File)** | Rolling file sinks, JSON format, strict PII/secret exclusion |

---

## 4. Branding & Visual Design System (Pak2Source)

The system visual language strictly incorporates **Pak2Source** branding (`pak2source.com`):

### 4.1 Color Palette Tokens
* **Brand Primary (Pak2Source Purple):** `#6C5CE7` (Accent), `#5840BA` (Deep Purple)
* **Brand Secondary / Gradient:** Linear gradient from `#6C5CE7` to `#A29BFE`
* **Dark Surface / Background (Dark Theme):**
  * Base Background: `#121214`
  * Card / Surface Container: `#1A1A1E`
  * Elevated Card / Dropdown: `#24242A`
  * Border Stroke: `#2E2E38`
* **Light Surface / Background (Light Theme):**
  * Base Background: `#F8F9FD`
  * Card / Surface Container: `#FFFFFF`
  * Border Stroke: `#E2E8F0`
* **Text Tokens:**
  * Dark Mode: Text Primary `#F8FAFC`, Secondary `#94A3B8`, Muted `#64748B`
  * Light Mode: Text Primary `#0F172A`, Secondary `#475569`, Muted `#94A3B8`
* **Status Badges:**
  * Success: `#10B981` (Green)
  * Warning: `#F59E0B` (Amber)
  * Danger / Overdue: `#EF4444` (Red)
  * Info: `#3B82F6` (Blue)

### 4.2 Typography & Controls
* **Font Family:** `Inter`, `Segoe UI`, or system Sans-Serif.
* **Control Geometry:** Rounded corners ($8\text{ px}$ inputs/buttons, $12\text{ px}$ cards), subtle elevation shadows, clear hover/focus outline in Pak2Source purple (`#6C5CE7`).
* **Design Parity:** Centralized Resource Dictionaries in MAUI (`Styles/Colors.xaml`, `Styles/Styles.xaml`) ensuring zero styling duplication.

---

## 5. Multi-Tenant Architecture & Data Isolation

```
                                  +------------------------------------+
                                  |         SuperAdmin Portal          |
                                  | (Platform Config, Organizations)   |
                                  +-----------------+------------------+
                                                    |
                         +--------------------------+--------------------------+
                         |                                                     |
            +------------v------------+                           +------------v------------+
            |     Organization A      |                           |     Organization B      |
            |   (School / Customer)   |                           |   (School / Customer)   |
            +------------+------------+                           +------------+------------+
                         |                                                     |
         +---------------+---------------+                     +---------------+---------------+
         |                               |                     |                               |
+--------v--------+             +--------v--------+   +--------v--------+             +--------v--------+
| Branch 1 (Main) |             | Branch 2 (City) |   | Branch 1 (East) |             | Branch 2 (West) |
+--------+--------+             +--------+--------+   +--------+--------+             +--------+--------+
         |                               |                     |                               |
         +-- Students, Teachers, Fees, --+                     +-- Students, Teachers, Fees, --+
             Attendance, Exams, Reports                            Attendance, Exams, Reports
```

### 5.1 Tenant Isolation Invariants
1. **Never Rely on Client-Side Tenant Parameters:** Every authenticated user payload carries `OrganizationId` and accessible `BranchId`s.
2. **Data Layer Enforcement:** Central EF Core queries implement Global Query Filters:
   ```csharp
   modelBuilder.Entity<Student>()
       .HasQueryFilter(e => e.OrganizationId == _currentTenantService.OrganizationId && !e.IsDeleted);
   ```
3. **Cross-Tenant Prevention:** SuperAdmin can manage cross-organization settings; Organization Admins, Teachers, Parents, and Students are strictly confined to their own `OrganizationId`.

---

## 6. Offline + Online Architecture & Synchronization

```
+-------------------------------------------------------------------------------+
|                             CLIENT (.NET MAUI App)                            |
|                                                                               |
|  +------------------------+                    +---------------------------+  |
|  |   UI / ViewModels      |                    |   Local SQLite Database   |  |
|  | (Offline-first actions)| <================> | (Encrypted tenant store)  |  |
|  +-----------+------------+                    +-------------+-------------+  |
|              |                                               |                |
|              v                                               v                |
|  +------------------------+                    +---------------------------+  |
|  |       Sync Engine      | <----------------> |    Local Sync Outbox      |  |
|  |  (Periodic / Push-Pull)|                    | (Pending, InFlight, Done) |  |
|  +-----------+------------+                    +---------------------------+  |
+--------------|----------------------------------------------------------------+
               | HTTPS / WebSockets
               v
+-------------------------------------------------------------------------------+
|                            SERVER (Central API & DB)                          |
|                                                                               |
|  +------------------------+                    +---------------------------+  |
|  |    Sync Ingest API     |                    |   Central SQL Server DB   |  |
|  | (Conflict detection &  | <================> | (Multi-tenant master db,  |  |
|  |  idempotency handler)  |                    |  row versioning, audit)   |  |
|  +------------------------+                    +---------------------------+  |
+-------------------------------------------------------------------------------+
```

### 6.1 Synchronization Lifecycle & States
1. **Local Mutation:** When a user creates/updates an entity offline, it is written to the local SQLite database with `SyncStatus = Pending` and an entry is inserted into `SyncQueue` with a unique `ClientSyncId` (GUID).
2. **Network Detection:** `ConnectivityManager` continuously monitors internet availability.
3. **Upload Batching:** When online, the Sync Engine packages batches of pending queue items and sends them to the `/api/sync/push` endpoint.
4. **Server Ingestion:** The server verifies authorization, validates row version (`RowVersion` / timestamp), and applies transactions.
5. **Conflict Resolution Strategy:**
   * Default: *Server Wins* with local backup notification.
   * Last-Write-Wins (LWW) with client-submitted timestamp comparison.
   * Business-specific reconciliation for Attendance & Fees (additive / non-destructive).
6. **Download Delta:** Client queries `/api/sync/pull?since={lastSyncTimestamp}&organizationId={orgId}` to receive updates made on other clients/central portal.

---

## 7. Role-Based Access Control (RBAC) Architecture

* **Entities:** `User`, `Role`, `Permission`, `UserRole`, `RolePermission`.
* **Granular Permission Keys:**
  * `Organizations.Manage`, `Branches.Manage`
  * `Users.View`, `Users.Create`, `Users.Edit`, `Users.Deactivate`
  * `Students.View`, `Students.Create`, `Students.Edit`, `Students.Delete`, `Students.Export`
  * `Teachers.View`, `Teachers.Create`, `Teachers.Edit`, `Teachers.Assign`
  * `Attendance.View`, `Attendance.Mark`, `Attendance.Edit`, `Attendance.Approve`
  * `Fees.View`, `Fees.CreateStructure`, `Fees.GenerateInvoices`, `Fees.CollectPayment`, `Fees.Refund`
  * `Exams.View`, `Exams.Schedule`, `Exams.EnterMarks`, `Exams.PublishResults`
  * `Timetable.View`, `Timetable.Manage`
  * `Reports.View`, `Reports.Export`
* **Policy Authorization:** Attributes `[RequirePermission(Permissions.Students.Create)]` on endpoints and XAML permission-visibility converters on the client UI.

---

## 8. Audit Logging & Diagnostic System

Every sensitive, administrative, financial, and student-lifecycle operation generates a structured text log record stored both in file sink and an indexed `AuditLogs` table:

```json
{
  "Timestamp": "2026-10-01T16:20:00.1234567Z",
  "CorrelationId": "f3ecdd9b-5741-4d12-a3a4-17901035908c",
  "UserId": "usr_99812",
  "UserEmail": "admin@schoolalpha.edu",
  "OrganizationId": "org_001",
  "BranchId": "br_001",
  "Module": "FeeManagement",
  "Action": "CollectPayment",
  "EntityName": "StudentFeePayment",
  "EntityId": "pay_55412",
  "Result": "Success",
  "Details": "Collected fee PKR 15,000 for Invoice INV-2026-0042",
  "IpAddress": "192.168.1.50",
  "IsOfflineSync": false
}
```

* **Zero Credential Exposure Rule:** No passwords, hashes, credit card details, API secrets, or bearer tokens are ever written to logs.

---

## 9. Comprehensive Testing Framework

Per Rule 12 & 13: **Testing is not optional and must be developed test-first.**

### 9.1 Test Pyramid
1. **Unit Tests:** Business logic, fee calculations, grade point averages, conflict detection in timetables, password hashing, and sync queue serialization.
2. **Integration Tests:** Database transactions, EF Core repository operations, SQL Server constraints, soft deletion filters, and migration integrity.
3. **Security & Tenant Isolation Tests:** Explicit tests asserting that Organization A users cannot read/modify Organization B data under any HTTP header or payload spoofing.
4. **Offline Sync Scenario Tests:**
   * Offline create $\to$ reconnect $\to$ sync success.
   * Concurrency: duplicate submissions $\to$ idempotency key prevents double invoicing.
   * Conflict resolution: concurrent edits $\to$ conflict policy execution.
5. **UI & ViewModel Tests:** Validation feedback, command execution states, reactive properties, and navigation guards.

---

## 10. Identified Risks & Mitigation Strategies

| Risk | Impact | Mitigation Strategy |
| :--- | :--- | :--- |
| **Data Collision in Offline Sync** | High | UUIDs (Guid) generated on client for all primary keys; Server-side row versioning (`byte[] RowVersion`) and idempotency checks. |
| **Cross-Tenant Data Leakage** | Critical | Strict global query filters in EF Core, token-derived tenant context, and integration security tests for all endpoints. |
| **Financial / Fee Inconsistencies** | High | Strict database transactions across invoice generation and receipt collection; audit log recording of every rupee transacted. |
| **MAUI Multi-Platform UI Glitches**| Medium | Standardized XAML styles, clean MVVM, headless testing of ViewModels, responsive Grid/Flex layouts avoiding fixed pixel widths. |
| **Performance with Large Datasets** | Medium | Server-side pagination, indexed foreign keys, compiled queries, Dapper for heavy aggregate reports. |

---

## 11. Recommended Implementation Sequence

```
Phase 1: Discovery, Brain Analysis & Roadmap (COMPLETED - Documented here)
   ↓
Phase 2: Core Foundation & Solution Architecture (.NET 10 Solution, Domain, Infrastructure, Shared DTOs, MAUI Client, Serilog)
   ↓
Phase 3: Database Foundation (Central SQL Server + Local SQLite schemas, EF Core migrations, Tenant Isolation)
   ↓
Phase 4: Authentication, Authorization & RBAC (PBKDF2/Argon2 hashing, JWT/Session handling, Permission middleware)
   ↓
Phase 5: SuperAdmin Portal (Platform config, Organization & Branch CRUD, Organization Admins, Branding settings)
   ↓
Phase 6: Organization Admin Portal (Dashboard, Branch management, School configuration, Staff roles)
   ↓
Phase 7: Student, Parent & Teacher Modules (Admissions, profiles, enrollments, relations)
   ↓
Phase 8: Academic Management (Sessions, classes, sections, subjects, teacher assignments, promotions)
   ↓
Phase 9: Attendance Module (Student, teacher & staff daily tracking, offline marking, sync reconciliation)
   ↓
Phase 10: Fee Management (Fee heads, structures, student allocation, invoicing, payments, receipt generation)
   ↓
Phase 11: Examination & Grading (Exam scheduling, marks entry, automated grading, report cards)
   ↓
Phase 12: Timetable & Scheduling (Class/teacher schedules, room allocations, conflict detection)
   ↓
Phase 13: Homework & Assignments (Assignment creation, submissions, teacher reviews)
   ↓
Phase 14: Communication & Notifications (Notice boards, alerts, parent-teacher messaging)
   ↓
Phase 15: Offline / Online Sync Engine Hardening (Conflict resolution, background queues, network retry)
   ↓
Phase 16: Reporting & Analytics (PDF report generation, student transcripts, fee collections, attendance stats)
   ↓
Phase 17: Security Hardening & Penetration Verification
   ↓
Phase 18: Performance & Load Optimization
   ↓
Phase 19: Full End-to-End System QA & Acceptance Scenarios
   ↓
Phase 20: Production Readiness & Deployment Finalization
```
