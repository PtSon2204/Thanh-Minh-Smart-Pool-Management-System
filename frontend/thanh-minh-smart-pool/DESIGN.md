# Thanh Minh Smart Pool Admin Design System

## 1. Atmosphere & Identity

The admin interface is an aquatic operations console. It uses a cool blue navigation frame, calm neutral work surfaces, and concise data-first controls. Keep existing Ant Design patterns and the current pool brand colors. The signature is a blue-to-cyan sidebar paired with bright, readable tables and restrained status colors.

## 2. Color

### Palette

| Role | Token | Value | Usage |
|---|---|---|---|
| Brand primary | `--primary` | `#0077b6` | Main actions and selected controls |
| Brand deep | `--primary-dark` | `#005f8e` | Sidebar header and strong emphasis |
| Brand secondary | `--secondary` | `#00b4d8` | Sidebar gradient and supporting brand accents |
| Brand accent | `--accent` | `#f77f00` | Existing public-site accent; do not add to admin actions by default |
| Admin canvas | `--admin-canvas` | `#e2e8f0` | Outer admin work area |
| Surface | `--admin-surface` | `#ffffff` | Drawers, tables, and controls |
| List canvas | `--list-canvas` | `#f4f6f8` | Search areas and table backgrounds |
| Border | `--admin-border` | `#cbd5e1` | Shared borders |
| List divider | `--list-divider` | `#e4e7ec` | Table separators |
| Muted text | `--list-muted` | `#667085` | Labels and secondary information |
| Success | `--status-success` | `#52c41a` | Active and successful states |
| Error | `--status-error` | `#ff4d4f` | Error states |
| Success action text | `--action-success-text` | `#237804` | Text on pale green action surfaces |
| Warning action text | `--action-warning-text` | `#ad6800` | Text on pale amber action surfaces |
| Danger action text | `--action-danger-text` | `#cf1322` | Text on pale red action surfaces |

### Rules

- Use the primary blue for actions. Use green for activation and returns.
- Use amber only for pause or caution actions. Use red only for destructive actions.
- Keep text readable against tinted action backgrounds. Preserve Ant Design focus indicators.

## 3. Typography

### Scale

| Level | Size | Weight | Usage |
|---|---:|---:|---|
| Page title | 20px | 700 | Admin list titles |
| Body | 16px | 400 | Main content and form controls |
| Secondary | 14px | 400 | Table supporting text |
| Label | 12px | 600 | Filters and table headings |

### Font Stack

- Primary: `Open Sans`, system UI, `-apple-system`, Roboto, sans-serif.
- Numeric data uses tabular figures.

## 4. Spacing & Layout

### Base Unit

Spacing uses the existing 4px scale: `--space-1` 4px, `--space-2` 8px, `--space-3` 12px, `--space-4` 16px, `--space-5` 20px, `--space-6` 24px, `--space-8` 32px.

### Grid

- Admin pages use the existing collapsible sidebar and fluid content area.
- Tables scroll within their content region. Rental tables switch to cards when their container is narrow.
- Breakpoints: 640px for mobile layout, 768px tablet, 1024px desktop navigation, and 1280px wide layouts.
- Drawers use up to 680px width on desktop and the full viewport on mobile.

## 5. Components

### Admin action group

- **Structure:** One wrapping group of Ant Design buttons in a table action cell.
- **Variants:** View (neutral blue), edit (primary blue), positive (green), warning (amber), destructive (red).
- **Spacing:** 8px gap; controls stay at least 32px high.
- **States:** Default, hover, pressed, keyboard focus, disabled, and loading.
- **Accessibility:** Use button elements, visible focus, and labels that name the action and record.
- **Motion:** Existing short color and transform feedback only. Respect reduced-motion preferences.
- **Layout:** Wrapping cluster on narrow widths; right-aligned on wide tables.

### Rental stock preview

- **Structure:** Two label/value rows in a two-column grid.
- **Variants:** Selected tracked product and no product selected.
- **Spacing:** 16px padding; 8px row gap.
- **States:** Empty, selected, and insufficient stock validation.
- **Accessibility:** Labels stay intact; values use tabular numerals.
- **Motion:** None.
- **Layout:** Labels have a readable desktop column; each row stacks on narrow screens.

### Staff role filter

- **Structure:** Labeled select beside the employment-status filter.
- **Variants:** All roles, Admin, and Staff.
- **Spacing:** Uses the shared filter gap and input dimensions.
- **States:** Default, selected, loading, disabled, and clear.
- **Accessibility:** Native Ant Design select semantics and a visible label.
- **Motion:** None beyond the library's standard menu behavior.
- **Layout:** Inline on desktop; full width on mobile.

### Staff profile mobile card

- **Structure:** Employee identity and role, a two-column detail list, then the shared action cluster.
- **Variants:** Working and inactive employee.
- **Spacing:** 16px card padding and 12px internal gaps.
- **States:** Default, loading, empty, and error are owned by the surrounding list.
- **Accessibility:** Each value has a visible description; actions keep text labels.
- **Motion:** None.
- **Layout:** One card per row below 640px; the desktop table remains visible above that breakpoint.

### Service and inventory mobile cards

- **Structure:** Product identity and key values, followed by the shared action cluster.
- **Variants:** Tracked stock and untracked stock; active and paused service.
- **Spacing:** 16px card padding and 12px internal gaps.
- **States:** Disabled stock adjustment for untracked products; list loading, empty, and error states remain outside the card.
- **Accessibility:** Every action retains its visible label and product-specific accessible name.
- **Motion:** None.
- **Layout:** One card per row up to 1024px; tables remain for wider screens.

### Service status action

- **Structure:** A separately confirmed action in the service row, outside the edit form.
- **Variants:** Pause active service; activate inactive service.
- **Spacing:** Shares the admin action group.
- **States:** Default, confirmation, loading, success, and error.
- **Accessibility:** Confirmation names the service and the new state.
- **Motion:** Existing Ant Design transition only.
- **Layout:** Inline with edit action; wraps on mobile.

## 6. Motion & Interaction

- Keep existing Ant Design transition behavior.
- Use short, clear feedback for hover, press, loading, and confirmation.
- Do not animate layout dimensions.
- Respect reduced-motion preferences.

## 7. Depth & Surface

Use a mixed strategy already present in admin pages: subtle borders for separation and a soft one-pixel list shadow. Drawers and table rows remain white over the cool gray list canvas. Avoid nested cards and heavy shadows.

## 8. Accessibility Constraints & Accepted Debt

### Constraints

- Target WCAG 2.2 AA contrast: 4.5:1 for body text and 3:1 for large text.
- Every action remains keyboard reachable and has a visible focus state.
- Controls retain text labels; color does not carry status meaning alone.
- At 375px, drawers and filters must fit without horizontal page overflow.
- Rental customer name and phone remain optional pending pool-owner confirmation.

### Accepted Debt

| Item | Location | Why accepted | Owner / Exit |
|---|---|---|---|
| Existing admin palette has legacy values outside a single semantic token layer | `src/index.css`, `src/features/services/styles/serviceManagement.css` | This task preserves the current visual identity and avoids a broad theme migration | Revisit during a separate admin-wide design-system migration |
