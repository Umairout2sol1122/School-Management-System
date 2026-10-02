# AI DEVELOPMENT RULES

## Universal Enterprise Project Rules

**Purpose:**
This document defines the mandatory rules for AI-assisted development in this repository.

These rules are designed to work across different technology stacks, framework versions, legacy applications, modern applications, APIs, web applications, desktop applications, frontend applications, databases, and enterprise systems.

---

# 1. MOST IMPORTANT RULE

## UNDERSTAND THE EXISTING PROJECT BEFORE CHANGING IT

The AI is a contributor to an existing project.

The AI is NOT the owner of the architecture.

The AI must understand and follow the existing project's:

* architecture
* folder structure
* coding style
* naming conventions
* business logic
* database design
* API design
* UI patterns
* security model
* configuration
* integrations
* testing approach
* deployment approach

before making changes.

### Never assume that a preferred architecture is better than the existing architecture.

Existing project conventions take priority over AI preferences.

---

# 2. TECHNOLOGY MUST BE DETECTED, NOT ASSUMED

This repository may use any combination of:

* .NET Framework
* ASP.NET MVC
* ASP.NET Web API
* ASP.NET Core
* .NET 6/7/8/9/10+
* C#
* VB.NET
* AngularJS
* Angular
* React
* Vue
* JavaScript
* TypeScript
* jQuery
* Bootstrap
* HTML/CSS
* SQL Server
* PostgreSQL
* MySQL
* Oracle
* Dapper
* ADO.NET
* Entity Framework
* Entity Framework Core
* stored procedures
* REST APIs
* third-party APIs
* legacy technologies
* other technologies already present in the project

## NEVER assume which technology is being used.

Inspect the actual repository.

Determine:

* framework
* language
* framework version
* frontend technology
* database technology
* ORM/data-access technology
* authentication
* authorization
* build system
* package manager
* testing framework
* configuration mechanism

Then follow the existing implementation.

---

# 3. EXISTING PROJECT > AI PREFERENCE

If the AI prefers:

* another architecture
* another framework
* another coding style
* another database approach
* another UI framework
* another naming convention
* another dependency
* another design pattern

that preference MUST NOT override the existing project.

The AI must follow the project unless the user explicitly requests a redesign, migration, modernization, or refactoring.

---

# 4. FIRST ACTION: INSPECT

Before implementing a non-trivial task:

1. Read this file.
2. Inspect the project structure.
3. Identify the relevant project/module.
4. Search for similar functionality.
5. Search for existing implementations.
6. Identify related models/classes.
7. Identify related services/repositories/helpers.
8. Identify related database objects.
9. Identify related APIs.
10. Identify related frontend code.
11. Identify related configuration.
12. Identify security/authorization requirements.

Do not start coding immediately.

---

# 5. DO NOT MODIFY DURING INITIAL ANALYSIS

When asked to analyze a project or task:

DO NOT:

* create files
* delete files
* rename files
* modify source code
* refactor
* redesign
* change database
* change configuration
* install dependencies

unless the user explicitly asks for implementation.

Analysis means analysis.

---

# 6. SEARCH BEFORE CREATING

Before creating anything new, search the repository.

Search for:

* similar classes
* similar methods
* similar controllers
* similar services
* similar repositories
* similar APIs
* similar database queries
* similar UI
* similar JavaScript
* similar validation
* similar business logic
* similar configuration

If an existing implementation can reasonably be reused or extended, prefer it.

---

# 7. NO DUPLICATE FUNCTIONALITY

Do not create duplicate:

* services
* repositories
* controllers
* DTOs
* models
* helpers
* APIs
* JavaScript functions
* CSS classes
* database queries
* validation logic

without first checking whether equivalent functionality already exists.

---

# 8. MINIMUM CHANGE PRINCIPLE

Make the smallest change required to satisfy the task.

Do not:

* rewrite unrelated code
* refactor unrelated code
* rename unrelated variables
* reorganize folders
* redesign architecture
* modernize unrelated code
* upgrade packages unnecessarily
* replace working libraries
* change unrelated UI

The goal is:

> **Change what is necessary. Preserve what already works.**

---

# 9. DO NOT "IMPROVE" UNRELATED CODE

If you see code that you believe could be improved:

DO NOT automatically change it.

If it is outside the requested scope, leave it unchanged.

Report it separately if it is important.

Example:

```text
Unrelated issue found:
[description]

I did not modify it because it is outside the requested scope.
```

---

# 10. NO ASSUMPTIONS

Never invent:

* table names
* column names
* API endpoints
* routes
* class names
* method names
* business rules
* status values
* permissions
* roles
* configuration keys
* database relationships
* response structures
* UI behavior
* external API behavior

Verify them from the project.

---

# 11. AMBIGUITY RULE

If the requirement is unclear and different interpretations could produce different behavior:

STOP.

Ask the user.

Do not silently choose a business interpretation.

---

# 12. BUSINESS LOGIC RULE

Business rules are more important than coding preferences.

If the requirement specifies a rule, implement that rule exactly.

Do not expand it because the AI believes another behavior would be better.

Example:

If the requirement says:

```text
20' containers only
```

do not automatically implement:

```text
20' and 40' containers
```

---

# 13. EXISTING BUSINESS BEHAVIOR

Existing behavior may be intentional.

Do not change behavior simply because it appears:

* old
* unusual
* inefficient
* inconsistent
* unconventional

First determine whether other modules depend on it.

---

# 14. LEGACY CODE

Legacy code is not automatically wrong.

If the project is old:

DO NOT automatically modernize it.

Do not automatically introduce:

* a new framework
* a new architecture
* dependency injection
* async/await everywhere
* a new ORM
* a new frontend framework
* a new CSS framework
* new project structure

unless explicitly requested.

---

# 15. FRAMEWORK VERSION

Always inspect the actual framework version.

Do not assume the latest version.

Examples:

```text
.NET Framework 4.8
ASP.NET MVC 5
ASP.NET Core
.NET 8
.NET 9
.NET 10
```

Use APIs and language features compatible with the actual project.

---

# 16. LANGUAGE VERSION

Inspect the actual language version.

Do not use newer language features that the project does not support.

Do not change the language/framework version unless explicitly requested.

---

# 17. ARCHITECTURE

Identify the project's actual architecture.

It may be:

* MVC
* layered architecture
* monolith
* modular monolith
* Web API
* service-based
* repository/service pattern
* clean architecture
* desktop application
* frontend/backend separation
* legacy architecture
* another pattern

Follow whatever actually exists.

Do not force a preferred architecture onto the project.

---

# 18. FOLDER STRUCTURE

Follow the existing folder structure.

Do not reorganize the project simply because another structure looks cleaner.

Before creating a folder:

1. Search for an appropriate existing folder.
2. Inspect similar functionality.
3. Follow the established pattern.

---

# 19. NAMING CONVENTIONS

Follow existing naming conventions.

Do not rename existing:

* variables
* methods
* classes
* properties
* database objects
* API routes

unless required by the task.

Consistency is more important than personal preference.

---

# 20. EXISTING VARIABLES

Do not unnecessarily rename existing variables.

If existing code uses:

```text
tbdViewitem
```

do not change it to:

```text
viewItem
```

simply because you prefer the second name.

---

# 21. EXISTING METHODS

Do not rename or change signatures of existing methods unnecessarily.

Before changing a method:

1. Search all references.
2. Identify callers.
3. Identify API consumers.
4. Identify frontend consumers.
5. Determine impact.

---

# 22. DATABASE RULES

If the project uses a database:

Inspect the actual database-related implementation before changing it.

Check:

* tables
* columns
* relationships
* constraints
* indexes
* views
* stored procedures
* functions
* triggers
* queries
* transactions

Never invent database structure.

---

# 23. DATABASE TECHNOLOGY

Detect whether the project uses:

* SQL Server
* PostgreSQL
* MySQL
* Oracle
* SQLite
* another database

Then follow the existing database implementation.

Do not introduce a different database technology without explicit approval.

---

# 24. DATABASE ACCESS

Detect whether the project uses:

* Dapper
* ADO.NET
* Entity Framework
* EF Core
* stored procedures
* raw SQL
* another data-access method

Follow the existing pattern.

Do not introduce another ORM/data-access technology simply because you prefer it.

---

# 25. SQL SAFETY

When writing SQL:

* use parameters
* avoid SQL injection
* verify table names
* verify column names
* verify joins
* verify filters
* verify update/delete conditions

Never construct unsafe SQL from user input.

---

# 26. DESTRUCTIVE DATABASE OPERATIONS

Never automatically execute destructive operations such as:

```sql
DELETE
TRUNCATE
DROP
ALTER
```

against important environments without explicit authorization.

Always carefully verify destructive operations.

---

# 27. DATABASE DATA INTEGRITY

Consider:

* foreign keys
* duplicates
* NULL values
* existing records
* orphan records
* transactions
* concurrency
* indexes
* backward compatibility

Do not assume the database is empty.

---

# 28. IDENTIFIERS

Be extremely careful with:

* IDs
* GKEYs
* document numbers
* appointment numbers
* invoice numbers
* booking numbers
* container numbers
* transaction numbers
* reference numbers

Do not change identifier generation without understanding concurrency and existing consumers.

---

# 29. CONCURRENCY

Be especially careful with patterns such as:

```sql
MAX(Id) + 1
```

or any manually generated identifier.

If the task touches such logic, analyze concurrency implications before changing it.

---

# 30. TRANSACTIONS

If a feature modifies multiple related records:

Determine whether the operation requires a transaction.

Follow the project's existing transaction pattern.

Do not introduce partial updates accidentally.

---

# 31. API RULES

Before creating or modifying an API:

Search for existing:

* endpoints
* controllers
* routes
* request models
* response models
* authentication
* authorization
* error handling
* logging

Follow the existing API style.

---

# 32. API BREAKING CHANGES

Never silently make breaking API changes.

Before changing:

* request structure
* response structure
* route
* HTTP method
* authentication
* required fields

search for all consumers.

---

# 33. FRONTEND RULES

Detect the actual frontend technology first.

It may use:

* AngularJS
* Angular
* React
* Vue
* jQuery
* JavaScript
* TypeScript
* Razor
* Bootstrap
* another technology

Follow what already exists.

Do not introduce another frontend framework for one feature.

---

# 34. ANGULARJS

If AngularJS exists:

Follow existing:

* controller structure
* services
* directives
* `$scope`
* controller-as
* dependency injection
* API calling patterns
* validation
* routing

Do not convert the application to another framework unless explicitly requested.

---

# 35. JAVASCRIPT

Follow existing JavaScript conventions.

Do not introduce a new JavaScript architecture unnecessarily.

Before creating a function:

Search for equivalent functionality.

---

# 36. UI CONSISTENCY

New UI must look and behave like the existing application.

Inspect existing:

* colors
* fonts
* spacing
* buttons
* forms
* tables
* grids
* modals
* alerts
* loaders
* icons
* navigation

Do not redesign the application while implementing a functional change.

---

# 37. CSS

Before adding CSS:

Search existing styles.

Reuse existing classes where appropriate.

Avoid duplicate styles.

Do not introduce another CSS framework unless explicitly requested.

---

# 38. RESPONSIVENESS

If the existing application is responsive, follow its existing responsive patterns.

Do not redesign responsive behavior unless requested.

---

# 39. AUTHENTICATION

Never bypass existing authentication.

Do not remove:

* login checks
* token validation
* session checks
* authentication middleware
* authentication attributes

unless explicitly requested.

---

# 40. AUTHORIZATION

Never bypass authorization.

Before adding protected functionality:

Identify existing:

* roles
* permissions
* claims
* policies
* access checks

Follow the existing security mechanism.

---

# 41. SECURITY

Every change must consider:

* authentication
* authorization
* SQL injection
* XSS
* CSRF
* input validation
* broken access control
* sensitive data exposure
* unsafe file access
* insecure redirects
* secrets

Do not weaken security for convenience.

---

# 42. SECRETS

Never hardcode:

* passwords
* API keys
* tokens
* private keys
* connection credentials
* secrets

Use the project's existing configuration/secret mechanism.

---

# 43. CONFIGURATION

Before adding configuration:

Search existing:

* config files
* environment variables
* settings classes
* appsettings
* web.config
* environment configuration
* constants

Follow the existing mechanism.

---

# 44. ENVIRONMENTS

Do not mix:

* development configuration
* testing configuration
* staging configuration
* production configuration

Do not hardcode environment-specific values.

---

# 45. EXCEPTION HANDLING

Follow the existing exception-handling strategy.

Never silently swallow exceptions.

Avoid:

```csharp
catch
{
}
```

unless there is an explicit and justified reason.

---

# 46. LOGGING

Use the existing logging system.

Do not introduce another logging framework without approval.

Never log:

* passwords
* tokens
* secrets
* sensitive credentials

---

# 47. ERROR MESSAGES

Do not expose internal information to users.

Never expose:

* SQL
* stack traces
* connection strings
* credentials
* internal secrets

unless explicitly intended for a secure development environment.

---

# 48. EXTERNAL INTEGRATIONS

Before changing an external integration, inspect:

* endpoint
* authentication
* request
* response
* timeout
* retry
* logging
* error handling
* configuration

Do not invent API fields.

---

# 49. THIRD-PARTY DEPENDENCIES

Before adding a package/library:

1. Search existing dependencies.
2. Determine whether the functionality already exists.
3. Determine whether the project already has an equivalent.
4. Check compatibility.
5. Explain why it is required.
6. Obtain approval when it changes project dependencies significantly.

---

# 50. DO NOT UPGRADE RANDOMLY

Do not upgrade:

* framework
* NuGet packages
* npm packages
* Bootstrap
* AngularJS
* jQuery
* database drivers

as part of an unrelated feature.

---

# 51. PERFORMANCE

Do not optimize blindly.

Before optimization:

1. Identify the actual bottleneck.
2. Inspect the relevant code.
3. Inspect database queries.
4. Inspect API calls.
5. Inspect data volume.
6. Identify measurable impact.

Do not rewrite working code based solely on theoretical optimization.

---

# 52. N+1 QUERIES

Be careful when performing database operations inside loops.

Check whether the change creates unnecessary repeated database calls.

---

# 53. FRONTEND PERFORMANCE

Avoid unnecessary:

* API calls
* polling
* watchers
* DOM operations
* repeated rendering
* timers

Follow existing behavior unless optimization is specifically requested.

---

# 54. AUTO REFRESH

If implementing or modifying auto-refresh:

Check:

* interval
* duplicate timers
* cleanup
* API load
* page lifecycle
* business requirements

---

# 55. VALIDATION

Important validation must exist server-side.

Frontend validation is not sufficient for security.

Follow existing validation mechanisms.

---

# 56. STATUS VALUES

Never invent status values.

Search for:

* enums
* constants
* database values
* configuration
* existing mappings

Preserve existing meanings.

---

# 57. SOFT DELETE

If the project uses soft deletion such as:

```text
IsDeleted
```

follow the existing pattern.

Do not physically delete records unless explicitly required.

---

# 58. DATE AND TIME

Determine whether the project uses:

* UTC
* local time
* server time
* database time
* business timezone

Do not change date/time behavior without understanding the existing convention.

---

# 59. FILE UPLOADS

For uploads, consider:

* authorization
* file size
* extension
* MIME type
* filename sanitization
* storage
* overwrite behavior
* malicious files

Do not trust uploaded filenames.

---

# 60. PRINTING

If printing is involved:

Inspect the existing printing implementation.

Do not introduce a new printing library unless required and approved.

---

# 61. REPORTS

Before changing reports:

Identify:

* data source
* query
* filters
* calculations
* authorization
* export
* print behavior

Preserve existing business calculations unless explicitly changed.

---

# 62. TESTING

Every meaningful implementation must be verified.

At minimum:

1. Build the affected project.
2. Check compilation.
3. Review changed files.
4. Check obvious runtime issues.
5. Test the affected workflow where possible.

---

# 63. TEST CASES

For meaningful functionality, consider:

### Positive

Expected valid behavior.

### Negative

Invalid input.

### Boundary

Minimum/maximum values.

### Duplicate

Repeated requests or duplicate data.

### Empty

No data/null values.

### Authorization

Unauthorized access.

### Existing Data

Existing records continue to work.

### Integration Failure

External dependency unavailable.

---

# 64. BUILD VERIFICATION

Never claim:

```text
Build successful
```

unless the build was actually performed and succeeded.

If the environment prevents building, clearly state:

```text
Build not executed because:
[reason]
```

---

# 65. TEST VERIFICATION

Never claim something was tested if it was not actually tested.

Use accurate language:

```text
Verified:
...

Not verified:
...
```

---

# 66. GIT SAFETY

Do not automatically:

* commit
* push
* merge
* rebase
* reset
* force push
* delete branches

unless explicitly instructed.

---

# 67. DESTRUCTIVE GIT COMMANDS

Never execute commands such as:

```bash
git reset --hard
git clean -fd
git push --force
```

without explicit authorization.

---

# 68. USER CHANGES

Before editing a file, determine whether it contains existing user changes.

Never overwrite user work.

Never revert unrelated changes.

Never assume uncommitted changes are mistakes.

---

# 69. DIFF REVIEW

After implementation, inspect the final changes.

Look for:

* unexpected files
* unrelated changes
* accidental deletions
* renamed variables
* formatting-only changes
* configuration changes
* database changes
* API changes

Remove unrelated modifications.

---

# 70. NO MASS FORMATTING

Do not reformat entire files simply because one section was changed.

Keep diffs small and reviewable.

---

# 71. COMMENTS

Add comments only when they explain something non-obvious.

Good comment:

```text
Business rule: appointment cancellation is allowed only before gate-in.
```

Avoid comments that merely repeat the code.

---

# 72. DOCUMENTATION

Document complex or important business behavior when appropriate.

Do not generate unnecessary documentation for trivial changes.

---

# 73. ANALYSIS OUTPUT

Before significant implementation, provide:

```text
REQUIREMENT
------------

What I understand:
...

EXISTING IMPLEMENTATION
-----------------------

Relevant files:
...

Existing pattern:
...

REUSE
-----

Existing code that can be reused:
...

PROPOSED CHANGE
---------------

...

FILES TO MODIFY
---------------

...

FILES TO CREATE
---------------

...

DATABASE IMPACT
---------------

...

API IMPACT
----------

...

UI IMPACT
---------

...

RISKS
-----

...

QUESTIONS
---------

...
```

Then wait for approval when the task is significant.

---

# 74. IMPLEMENTATION RULE

After approval:

Implement ONLY the approved plan.

Do not expand scope.

Do not introduce unrelated improvements.

Do not redesign the architecture.

Do not create unnecessary files.

Do not change unrelated behavior.

---

# 75. IF YOU DISCOVER SOMETHING NEW

If implementation reveals an unexpected issue:

Do not silently expand the task.

Stop and report:

```text
NEW ISSUE DISCOVERED

...

This was not included in the approved scope.

Recommended action:
...
```

Continue only if it is safe and clearly within scope or the user approves.

---

# 76. FINAL REVIEW

Before completion, verify:

* [ ] Requirement implemented
* [ ] Existing architecture followed
* [ ] Existing pattern reused
* [ ] No unnecessary files created
* [ ] No unnecessary refactoring
* [ ] No unrelated changes
* [ ] Existing variables preserved
* [ ] Existing methods preserved where possible
* [ ] Database assumptions verified
* [ ] API consumers considered
* [ ] Authentication preserved
* [ ] Authorization preserved
* [ ] Security considered
* [ ] Configuration preserved
* [ ] User changes preserved
* [ ] Git state preserved
* [ ] Final diff reviewed
* [ ] Build attempted
* [ ] Testing performed where possible
* [ ] Failures reported honestly

---

# 77. FINAL REPORT

After implementation, report:

```text
IMPLEMENTATION SUMMARY
======================

Requirement:
...

Implemented:
...

Files Modified:
...

Files Created:
...

Database Changes:
...

API Changes:
...

Frontend Changes:
...

Security Considerations:
...

Testing:
...

Build:
PASS / FAIL / NOT RUN

Unverified Items:
...

Potential Risks:
...

Unrelated Issues Found:
...
```

---

# 78. ABSOLUTE NO-GO RULES

The AI must NOT:

* invent business rules
* invent database objects
* invent APIs
* bypass authentication
* bypass authorization
* expose secrets
* overwrite user changes
* execute destructive Git commands without permission
* execute destructive production database operations without authorization
* introduce a new architecture unnecessarily
* introduce duplicate functionality
* refactor unrelated code
* upgrade frameworks unnecessarily
* install dependencies unnecessarily
* claim tests were performed when they were not
* claim deployment occurred when it did not
* claim a build succeeded when it was not verified

---

# 79. WHEN REDESIGN IS EXPLICITLY REQUESTED

If the user explicitly requests:

* redesign
* refactoring
* modernization
* migration
* architecture change
* framework upgrade

then analyze the existing implementation first.

Provide:

1. Current architecture
2. Problems/limitations
3. Proposed architecture
4. Affected modules
5. Migration impact
6. Risks
7. Rollback considerations
8. Implementation plan

Do not immediately rewrite everything.

---

# 80. PRIORITY ORDER

When multiple considerations conflict, use this priority:

```text
1. Explicit user requirement
2. Security
3. Data integrity
4. Existing business rules
5. Existing project behavior
6. Existing project architecture
7. Existing project conventions
8. Backward compatibility
9. Performance
10. Code cleanliness
11. AI preference
```

AI preference is the lowest priority.

---

# 81. UNIVERSAL DEVELOPMENT PRINCIPLE

The AI must think:

```text
UNDERSTAND
    ↓
SEARCH
    ↓
VERIFY
    ↓
REUSE
    ↓
PLAN
    ↓
GET APPROVAL
    ↓
IMPLEMENT
    ↓
BUILD
    ↓
TEST
    ↓
REVIEW
    ↓
REPORT
```

Never:

```text
ASSUME
    ↓
CREATE
    ↓
REFACTOR EVERYTHING
    ↓
CLAIM DONE
```

---

# 82. FINAL PRINCIPLE

## DO NOT BE CREATIVE WITH EXISTING ENTERPRISE CODE UNLESS ASKED.

When the user asks for a new feature:

**FOLLOW THE EXISTING PROJECT.**

When the user asks for a redesign:

**PROPOSE A NEW DESIGN.**

When the user asks for a refactor:

**ANALYZE BEFORE REFACTORING.**

When the requirement is unclear:

**ASK.**

When existing code already solves the problem:

**REUSE IT.**

When something is outside scope:

**LEAVE IT ALONE.**

When uncertain:

**STOP AND VERIFY.**

---

# 83. FINAL COMMAND

Before every significant change, ask:

```text
Have I read the project rules?

Have I identified the actual technology?

Have I inspected the relevant existing code?

Have I searched for an existing implementation?

Am I following the existing architecture?

Am I reusing existing functionality?

Am I making the minimum required change?

Am I assuming anything that I have not verified?

Could this affect another module?

Could this affect the database?

Could this affect an API consumer?

Could this affect security?

Could this affect production?

Have I preserved existing user changes?

Have I reviewed my final diff?

Have I actually verified the result?
```

If the answer to any important question is uncertain:

## STOP → INVESTIGATE → VERIFY → ASK IF NECESSARY

# END OF AI DEVELOPMENT RULES
