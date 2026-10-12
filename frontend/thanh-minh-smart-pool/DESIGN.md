# Thanh Minh Smart Pool Admin Design System

## 1. Atmosphere & Identity

An aquatic operations console for pool staff and managers. The interface uses a calm, cool-blue navigation frame with light surfaces and compact, readable operational content. This staff page adapts the supplied employee-card reference while keeping Thanh Minh colors and real staff data.

## 2. Color

### Palette

| Role | Token | Value | Usage |
|---|---|---|---|
| Brand primary | `--primary` | `#0077b6` | Main actions and selected controls |
| Brand deep | `--primary-dark` | `#005f8e` | Strong emphasis and links |
| Brand secondary | `--secondary` | `#00b4d8` | Supporting brand accents |
| Page background | `--bg` | `#FEF1E6` | Existing application canvas |
| Admin canvas | `--admin-canvas` | `#e2e8f0` | Admin workspace |
| Surface | `--admin-surface` | `#ffffff` | Employee cards and controls |
| Card details surface | `--list-canvas` | `#f4f6f8` | Lower employee-card details band |
| Border | `--admin-border` | `#cbd5e1` | Shared borders |
| List divider | `--list-divider` | `#e4e7ec` | Card separation |
| Muted text | `--list-muted` | `#667085` | Supporting labels |
| Success | `--status-success` | `#52c41a` | Working employee status |
| Error | `--status-error` | `#ff4d4f` | Errors and destructive actions |
| Primary light | `--primary-light` | `#90e0ef` | Focus outlines |

### Rules
- Use existing CSS variables. Do not add raw colors in staff page styles.
- Use blue for primary actions. Status must include text as well as color.
- Do not use the reference image's orange action button.

## 3. Typography

### Scale

| Level | Size | Weight | Usage |
|---|---:|---:|---|
| Page title | 20px | 700 | Staff page heading |
| Employee name | 16px | 700 | Card identity |
| Body | 14px | 400 | Contact values and card content |
| Supporting text | 14px | 400 | Username and employment status |
| Label | 12px | 600 | Detail labels |

### Font Stack
- Primary: `Open Sans`, system UI, `-apple-system`, Roboto, sans-serif.
- Use tabular numerals for salary values.
- Keep all body text at least 14px where space permits; compact detail labels are 12px.

## 4. Spacing & Layout

### Base Unit
Use existing 4px tokens: `--space-1` 4px, `--space-2` 8px, `--space-3` 12px, `--space-4` 16px, `--space-5` 20px, `--space-6` 24px, `--space-8` 32px.

### Grid
- Keep the existing collapsible admin sidebar and fluid content area.
- Staff cards use a responsive CSS grid based on the available staff-list width: three columns at 900px or wider, two from 600px, one below 600px. This accounts for the admin sidebar reducing available width at tablet viewports.
- Use content-driven `minmax(0, 1fr)` tracks so long contacts cannot widen the page.
- Preserve the existing staff filters and pagination.
- At 375px, filters, cards, actions, and pagination fit without horizontal page overflow.

## 5. Components

### Staff profile card
- **Structure:** White identity band with circular avatar, employee name, role, username, employment status, and an accessible overflow action menu. Light-gray details band contains join date, email, phone, and base salary.
- **Variants:** Working, inactive, unknown status; real avatar or generic icon fallback; complete or missing optional identity/contact fields.
- **Spacing:** Existing 4px scale; 16px card padding; 12px detail grid gaps.
- **States:** Default, keyboard focus, menu open, missing data, broken avatar, loading/error/empty supplied by the list.
- **Accessibility:** Avatar has appropriate alternative text or is decorative beside the visible name. Overflow trigger has a name containing the employee name. Menu actions use text labels: View and Edit. Visible focus is required.
- **Motion:** Existing short Ant Design transitions only. Honor reduced-motion settings.
- **Layout:** One card per grid cell. Details use a two-column grid where there is room and collapse to one column on narrow cards.
- **Data boundary:** Use only `GetStaffsResponse`. The current API has no department field. Show account details read-only. The drawer continues to edit employment data only.

### Staff list header and filters
- **Structure:** Existing page title, server result count, blue Add employee action, then search, status, and role filters.
- **States:** Default, filtering, loading, empty, error/retry, permission denied.
- **Accessibility:** Keep visible labels, native Ant Design control semantics, and keyboard focus.
- **Layout:** Existing desktop filters may be inline; they stack on narrow widths.

### Staff pagination
- **Structure:** Existing Ant Design pagination below the card grid.
- **States:** Hidden when there are no results; page and page-size changes request the matching server page.
- **Layout:** Scroll or wrap within the content area on narrow screens.

## 6. Motion & Interaction

- Keep existing Ant Design transition behavior.
- Hover and active feedback apply only to interactive elements.
- Do not animate card layout dimensions.
- Respect `prefers-reduced-motion`.

## 7. Depth & Surface

Use a mixed strategy already present in admin pages: white cards over the light-gray list canvas, a subtle divider, and the existing soft list shadow. The lower details band uses a tonal light-gray surface. Avoid nested cards and heavy shadows.

## 8. Accessibility Constraints & Accepted Debt

### Constraints
- Target WCAG 2.2 AA contrast: 4.5:1 for body text and 3:1 for large text.
- Every action remains keyboard reachable with visible focus.
- Do not rely on color alone for status.
- Long names and contact strings wrap safely.
- At 375px, the page has no horizontal overflow.
- Maintain read-only account fields and existing employment-only edit flow.

### Accepted Debt

| Item | Location | Why accepted | Owner / Exit |
|---|---|---|---|
| The staff API does not provide a department field or cross-status totals | `StaffProfilesPage.jsx` and `GetStaffsResponse.cs` | Do not invent employee attributes or derive global counts from a filtered page | Backend/API owner; revisit when the contract provides these fields |
