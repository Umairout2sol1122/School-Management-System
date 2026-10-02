# Exam Theme Design Rule

## Purpose

This document defines the mandatory UI/UX redesign standard for
**ExamPortal** pages.

Whenever an existing ExamPortal page is redesigned, this document must
be treated as the baseline implementation and acceptance rule unless the
task explicitly overrides a specific requirement.

The objective of every redesign is:

> **Preserve the complete existing functionality, business behavior,
> data, and backend output while significantly improving the UI/UX.**

A visually attractive page that loses, changes, or breaks existing
functionality is considered a **failed redesign**.

------------------------------------------------------------------------

# 1. Core Design Principle

For every redesign, the following must remain true:

**Original Functionality = Redesigned Functionality**

**Original Business Result = Redesigned Business Result**

**Original Backend Output = Redesigned Backend Output**

while:

**Redesigned UI/UX \> Original UI/UX**

The redesign must improve only the presentation and user experience
unless functional changes are explicitly requested separately.

There is **zero compromise on functional parity**.

------------------------------------------------------------------------

# 2. Original Implementation Is the Functional Source of Truth

When redesigning an existing page, always treat the current
working/original page as the authoritative source for:

-   Business behavior
-   Razor logic
-   Model binding
-   Form submission
-   Validation
-   JavaScript behavior
-   jQuery behavior
-   AJAX/API calls
-   Filters
-   Search
-   Tables
-   Modals
-   Accordions
-   Dynamic sections
-   Conditional rendering
-   Buttons/actions
-   Statuses
-   Remarks
-   Hidden values
-   Output/results

If an older or experimental redesigned page already exists, it may be
used as a **visual reference only**.

If there is any conflict between an original working page and an
existing redesign:

> **The original working page always wins for functionality, data,
> workflow, validation, and output.**

Never assume a previous redesign contains all original functionality.

------------------------------------------------------------------------

# 3. Mandatory Pre-Redesign Analysis

Do not begin by immediately rewriting HTML or CSS.

Before changing the page, inspect the complete implementation and
understand how it works end-to-end.

Review:

-   `.cshtml` / Razor markup
-   Models/ViewModels
-   Forms
-   Inputs
-   Select/dropdowns
-   Checkboxes
-   Radio buttons
-   Textareas
-   Hidden fields
-   Tables
-   Modals
-   Accordions/collapse components
-   Partial views
-   Razor conditions
-   Razor loops
-   JavaScript
-   jQuery
-   AJAX
-   API calls
-   Event handlers
-   Dynamic DOM manipulation
-   Validation
-   IDs
-   `name` attributes
-   `data-*` attributes
-   CSS classes used by JavaScript
-   Controller/action dependencies
-   Form actions/methods
-   Request payloads
-   Result/output sections
-   Existing responsive behavior

Build an internal inventory of the functionality before redesigning.

Do not assume an element is unnecessary simply because its purpose is
not visually obvious.

------------------------------------------------------------------------

# 4. Functional Parity Is Mandatory

Do **not** change or remove existing functionality unless explicitly
requested.

Do not unintentionally change:

-   Business logic
-   Controller behavior
-   Service behavior
-   API contracts
-   AJAX request payloads
-   Model binding
-   Database interaction
-   Existing processing rules
-   Validation rules
-   Authorization behavior
-   Conditional logic
-   Submission workflow
-   Result calculations
-   Output
-   Default values
-   Enable/disable rules
-   Show/hide rules

The redesigned UI must adapt around the existing functionality.

Never change business behavior simply because doing so makes the
redesign easier.

------------------------------------------------------------------------

# 5. Preserve Functional HTML Contracts

Be extremely careful with existing functional markup.

Preserve wherever required:

-   `id`
-   `name`
-   `value`
-   `data-*`
-   Razor helpers
-   Model expressions
-   Hidden inputs
-   Form action
-   Form method
-   Checkbox values
-   Radio values
-   Select values
-   Button types
-   JavaScript selectors
-   CSS classes referenced by scripts
-   AJAX parameters

Do not casually rename or remove these attributes.

Markup restructuring is allowed only after verifying that it does not
break JavaScript, model binding, validation, submission, or backend
processing.

------------------------------------------------------------------------

# 6. Overall Visual Standard

Every redesigned ExamPortal page must look:

-   Modern
-   Professional
-   Enterprise-grade
-   Clean
-   Consistent
-   Operationally efficient
-   Easy to scan
-   Easy to understand
-   Responsive
-   Accessible

Avoid making pages visually complex just to appear modern.

Prioritize usability, clarity, consistency, and operational efficiency
over decorative effects.

------------------------------------------------------------------------

# 7. Visual Consistency

Maintain a consistent design language across ExamPortal.

Apply consistent:

-   Typography
-   Font sizing
-   Visual hierarchy
-   Colors
-   Spacing
-   Padding
-   Border radius
-   Cards
-   Section headers
-   Forms
-   Inputs
-   Buttons
-   Tables
-   Modals
-   Accordions
-   Badges
-   Status indicators
-   Icons
-   Hover states
-   Focus states
-   Responsive behavior

Pages within the same module should clearly look like parts of the same
application.

------------------------------------------------------------------------

# 8. Section Design

Organize information into logical visual sections.

Depending on the page, use:

-   Cards
-   Panels
-   Sections
-   Accordions
-   Expandable areas
-   Tabs

Only use these components where they genuinely improve usability.

Do not hide frequently required operational information behind
unnecessary clicks.

## Section Header Rules

Section headers must:

-   Have professional typography.
-   Be consistently aligned.
-   Have appropriate padding.
-   Maintain clear hierarchy.
-   Have adequate spacing before the content.

Content must never appear visually stuck directly against the section
header.

Avoid unnecessary decorative borders.

In particular:

> **Do not use unnecessary green bottom borders under section headers.**

------------------------------------------------------------------------

# 9. Form Design

For form-based pages:

-   Use a consistent responsive grid.
-   Similar inputs should use consistent column widths.
-   Maintain consistent control heights.
-   Align labels correctly.
-   Maintain proper horizontal and vertical spacing.
-   Clearly identify required fields.
-   Keep validation messages visible.
-   Preserve existing validation rules.
-   Preserve default values.
-   Preserve read-only/disabled behavior.
-   Preserve conditional fields.
-   Preserve all hidden fields required for submission.

The form must remain efficient for operational users.

Avoid unnecessarily large controls or excessive whitespace.

------------------------------------------------------------------------

# 10. Progressive / Step-by-Step Forms

For long forms, a progressive step experience may be used where it
genuinely improves usability.

Possible patterns:

-   Stepper
-   Wizard
-   Progressive sections
-   Accordion workflow

If a step-by-step design is implemented:

-   Next must work.
-   Previous must work.
-   Entered values must not be lost.
-   Validation must remain correct.
-   The active step must remain synchronized with displayed content.
-   Users must be able to complete the entire original workflow.
-   Final submission must contain all required original values.

Never introduce a stepper that breaks or limits the original workflow.

------------------------------------------------------------------------

# 11. Tables

Tables are operationally important.

Never remove required information simply to make a table look cleaner.

Preserve all required:

-   Columns
-   Values
-   Statuses
-   Indicators
-   Selection controls
-   Actions
-   Remarks
-   Links

Improve presentation using:

-   Professional table headers
-   Consistent typography
-   Appropriate cell padding
-   Proper vertical alignment
-   Sensible row height
-   Subtle hover states
-   Clear status indicators
-   Organized action areas
-   Responsive containers

## Large Tables

For tables containing many records, consider:

-   Fixed/max-height containers
-   Internal vertical scrolling
-   Sticky headers
-   Controlled horizontal scrolling
-   Pagination if already supported by the functionality

Do not allow very large tables to unnecessarily extend the entire page
when a scrollable container provides a better experience.

Do not remove columns just to avoid horizontal scrolling.

> **Data completeness has higher priority than visual compactness.**

------------------------------------------------------------------------

# 12. Long Text / Long Values

Long values must not destroy the page layout.

Where appropriate use:

-   Ellipsis/truncation
-   Tooltip
-   Popover
-   View More
-   Expandable content
-   Details modal

The complete underlying value must remain accessible to the user.

Never permanently hide or discard information.

------------------------------------------------------------------------

# 13. Filters and Search

If the page contains search/filter controls:

-   Keep them compact.
-   Use professional spacing.
-   Use consistent typography.
-   Align controls properly.
-   Make them responsive.
-   Clearly show active selections.
-   Preserve all original filter options and combinations.

Verify:

-   Individual filters
-   Combined filters
-   Search
-   Reset
-   Default state
-   Multi-selection
-   Date filtering
-   Status/stage filtering
-   Empty results
-   Re-filtering after data changes

A redesigned filter may look different, but it must return the same
functional dataset as the original implementation.

------------------------------------------------------------------------

# 14. Buttons and Actions

Every original action must remain available.

Use clear visual hierarchy for:

-   Primary actions
-   Secondary actions
-   Navigation actions
-   Destructive actions

Maintain consistent:

-   Height
-   Padding
-   Typography
-   Icons
-   Spacing
-   Hover states
-   Focus states
-   Disabled states
-   Loading states where appropriate

Do not change what an action does merely because its appearance changes.

------------------------------------------------------------------------

# 15. Modals

Modals must be professionally designed and operationally usable.

Ensure:

-   Modal title is aligned correctly.
-   Close button is vertically centered with the header.
-   Modal dimensions suit the information.
-   Content has proper spacing.
-   Footer actions are clear.
-   Modal remains responsive.

## Large Modals

If a modal contains significant information:

-   Use a sensible fixed/max height.
-   Make the modal body internally scrollable.
-   Keep header/footer stable where useful.
-   Break information into logical sections.

Where appropriate use:

-   Accordions
-   Cards
-   Tabs
-   Section navigation

Only use these if they improve readability.

Do not remove information to reduce modal size.

------------------------------------------------------------------------

# 16. Accordions / Collapse Components

Accordion sections should have:

-   Clear headers
-   Professional typography
-   Proper spacing
-   Appropriate icons
-   Clear expanded/collapsed state
-   Correct alignment
-   Smooth interaction
-   Consistent styling

Do not use accordion sections merely as decoration.

Frequently required information should not require unnecessary expansion
steps.

------------------------------------------------------------------------

# 17. Statuses, SLA and Important Records

Where records require attention, use subtle but clear visual indicators.

Examples:

-   Status badges
-   Warning icons
-   Accent borders
-   Light highlighting
-   Attention indicators

For SLA-exceeded or exception records, subtle red highlighting may be
used where appropriate.

Do not overwhelm the page with aggressive colors.

Important operational conditions must remain immediately identifiable.

------------------------------------------------------------------------

# 18. Remarks UX

Where remarks are required, the UI should make the requirement obvious.

Where appropriate distinguish:

-   Remarks Required
-   Remarks Provided
-   No Remarks Required

For table-based remarks, consider compact patterns such as:

-   Add Remark button
-   Remark icon
-   Inline editor
-   Popover
-   Small modal
-   Truncated preview with View/Edit

If a remark is mandatory by existing business rules, the redesigned UI
must not allow that requirement to be bypassed.

------------------------------------------------------------------------

# 19. Mobile Responsiveness --- Mandatory

**Mobile responsiveness is a mandatory and non-negotiable Exam Theme
requirement.**

Every redesigned page must work correctly on:

-   Large desktop
-   Standard desktop
-   Laptop
-   Tablet
-   Mobile phone

A redesign is **not complete** until the mobile layout has been
explicitly verified.

## Mobile Verification Must Include

Verify:

-   No overlapping elements.
-   No clipped content.
-   No broken grids.
-   No inaccessible controls.
-   No hidden required actions.
-   No unusable buttons.
-   No broken forms.
-   No broken tables.
-   No broken modals.
-   No broken accordions.
-   No broken filters.
-   No unreadable typography.
-   No unnecessary page-level horizontal scrolling.

Forms must stack appropriately.

Buttons and controls must remain touch-friendly.

Modals must fit and remain usable on small screens.

## Mobile Tables

Wide operational tables may use controlled horizontal scrolling **inside
the table container**.

Do not hide required columns/data simply to make the table fit on
mobile.

Functional completeness remains mandatory on mobile.

------------------------------------------------------------------------

# 20. Accessibility

Follow practical accessibility standards.

Ensure:

-   Proper labels
-   Semantic markup
-   Keyboard navigation
-   Visible focus states
-   Sufficient color contrast
-   Appropriate ARIA attributes where needed
-   Accessible modal behavior
-   Accessible accordion behavior

Do not rely exclusively on color to communicate critical information.

------------------------------------------------------------------------

# 21. Performance and Dependencies

Keep redesigns lightweight.

Prefer reusing the application's existing:

-   Bootstrap
-   CSS
-   JavaScript
-   jQuery
-   Icon libraries
-   Shared UI components

Avoid introducing a heavy UI framework solely for visual improvements.

Add custom styling only where necessary.

Avoid unnecessary JavaScript.

Do not duplicate existing functionality with another library.

------------------------------------------------------------------------

# 22. Scope Control

Only modify the requested page/component unless broader changes are
explicitly required.

Do not use a UI redesign task as an opportunity to:

-   Rewrite controllers
-   Change business services
-   Change database logic
-   Change APIs
-   Rename models
-   Re-architect the application
-   Refactor unrelated JavaScript
-   Redesign unrelated pages

If shared CSS must be modified, verify that it does not unintentionally
change other pages.

Prefer page-scoped styling where practical.

------------------------------------------------------------------------

# 23. Existing Redesigns Must Not Be Trusted Blindly

If both an original page and a previously redesigned page exist, perform
an explicit comparison.

Audit:

**Original → Previous Redesign → New Final Redesign**

Look for functionality accidentally omitted from the previous redesign.

Examples:

-   Missing fields
-   Missing controls
-   Missing columns
-   Missing actions
-   Missing filters
-   Missing validation
-   Missing statuses
-   Missing remarks
-   Missing modal information
-   Missing events
-   Broken AJAX
-   Incorrect request payloads
-   Incorrect model binding
-   Missing hidden values
-   Different output/results

Never propagate a regression from an older redesign into the new
implementation.

------------------------------------------------------------------------

# 24. Mandatory Functional Regression Verification

After redesigning, perform a strict **before-vs-after functional
comparison**.

Do not consider visual inspection sufficient.

Test every:

-   Input
-   Dropdown
-   Checkbox
-   Radio button
-   Hidden field
-   Button
-   Link/action
-   Table
-   Filter
-   Search operation
-   Modal
-   Accordion
-   Validation
-   JavaScript interaction
-   jQuery interaction
-   AJAX/API call
-   Conditional section
-   Submission workflow
-   Output/result

Ask:

1.  Did any field disappear?
2.  Did any table column disappear?
3.  Did any action disappear?
4.  Did any ID/selector change and break JavaScript?
5.  Did markup restructuring affect model binding?
6.  Are all required `name` attributes preserved?
7.  Are hidden fields still submitted?
8.  Are validation rules still triggered correctly?
9.  Does dynamic data still render correctly?
10. Do AJAX requests send the same required payload?
11. Do all original actions still execute?
12. Does the backend receive the same required information?
13. Does equivalent input produce the same output?
14. Are large datasets still usable?
15. Does mobile work correctly?
16. Did CSS changes unintentionally affect other pages?
17. Are there new JavaScript console errors?

Any functional difference is a **regression** and must be fixed.

------------------------------------------------------------------------

# 25. Original-vs-Redesign Parity Matrix

For substantial redesigns, internally verify a parity matrix similar to:

  Functional Area      Original          Redesigned   Result
  -------------------- ----------------- ------------ --------
  Fields               Available         Available    PASS
  Hidden Values        Available         Available    PASS
  Validation           Working           Working      PASS
  Search               Working           Working      PASS
  Filters              Working           Working      PASS
  Table Data           Complete          Complete     PASS
  Actions              Working           Working      PASS
  Modals               Complete          Complete     PASS
  AJAX/API             Working           Working      PASS
  Model Binding        Correct           Correct      PASS
  Backend Output       Expected          Same         PASS
  Desktop Responsive   Working           Working      PASS
  Tablet Responsive    Working           Working      PASS
  Mobile Responsive    Working           Working      PASS
  Console Errors       None/New Issues   None         PASS

Expand this matrix based on the actual page.

Every applicable item must pass.

------------------------------------------------------------------------

# 26. Before-vs-After Output Verification

For equivalent test data:

1.  Execute the workflow on the original page.
2.  Record the visible result and backend-relevant behavior.
3.  Execute the same workflow on the redesigned page.
4.  Compare the results.

Compare:

-   Displayed data
-   Table records
-   Statuses
-   Remarks
-   Validation messages
-   Actions
-   Modal content
-   Success/error messages
-   Request/submission behavior
-   Final output

If:

**Original Output != Redesigned Output**

the redesign is **not complete**.

------------------------------------------------------------------------

# 27. Console and Runtime Verification

Before completion:

-   Check the browser console.
-   Verify no new JavaScript errors.
-   Verify no failed UI-related AJAX calls.
-   Verify no missing resources caused by the redesign.
-   Verify all interactive components initialize correctly.

A page that visually renders but produces runtime errors is not
complete.

------------------------------------------------------------------------

# 28. Definition of Done

A redesign is complete only when:

-   The page follows the Exam Theme Design Rule.
-   UI/UX is professionally improved.
-   Existing functionality is completely preserved.
-   Existing business logic is unchanged.
-   Existing data is preserved.
-   Existing validation works.
-   Existing Razor behavior works.
-   Existing JavaScript/jQuery works.
-   Existing AJAX/API behavior works.
-   Existing model binding works.
-   Existing submission works.
-   Existing backend output remains the same.
-   Every required action remains available.
-   Tables retain all required information.
-   Modals retain all required information/functionality.
-   Desktop layout passes.
-   Laptop layout passes.
-   Tablet layout passes.
-   **Mobile layout passes.**
-   No new console errors exist.
-   No unrelated functionality has been modified.
-   Before-vs-after functional verification passes.

------------------------------------------------------------------------

# Final Acceptance Formula

The following must be true before declaring the redesign complete:

> **Original Functionality = Redesigned Functionality**

> **Original Business Result = Redesigned Business Result**

> **Original Backend Output = Redesigned Backend Output**

> **Desktop + Tablet + Mobile = Fully Usable**

> **Redesigned UI/UX \> Original UI/UX**

## Final Rule

**Do not change what the page does unless explicitly requested. Improve
how professionally, clearly, efficiently, and responsively the user can
do it.**

Functional parity and mobile responsiveness are mandatory acceptance
criteria, not optional enhancements.
