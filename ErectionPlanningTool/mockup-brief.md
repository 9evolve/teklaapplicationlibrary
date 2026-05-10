# ErectionPlanningTool — HTML Mockup Brief

## What this is
A Tekla Structures macro (C# / WinForms desktop dialog) for field erection planning on structural steel projects. It's a companion to an existing fabrication PM tool. Please create a **single-file HTML mockup** that looks and feels like a compact Windows desktop utility — no external CDN dependencies, inline CSS/JS only.

## Style direction
- Compact WinForms-style dialog, ~640×780px centered on the page
- Light gray window chrome, tab strip at top
- Monospace font (Consolas / Courier New) for data tables
- Blue primary action buttons (`#2196F3`), standard gray secondary buttons
- Subtle grid lines on all tables
- Row color coding where noted

---

## Tab 1 — Sequence

**Controls:**
- Phase: `[dropdown: 1 ▾]` `[↻]` `[Load phase]` `[Auto-number]` `[Select in model]`

**Table** (editable Seq# column, rest read-only):

| Seq# | Mark | Profile | Phase | Weight (kg) | Lot |
|------|------|---------|-------|-------------|-----|
| 1 | A1 | W14X90 | 1 | 412.3 | LOT-A |
| 2 | A2 | W14X90 | 1 | 412.3 | LOT-A |
| 3 | B1 | W21X68 | 1 | 318.7 | LOT-A |
| 4 | B2 | W21X68 | 1 | 318.7 | LOT-B |
| 5 | C1 | W18X55 | 1 | 256.1 | LOT-B |
| *(blank)* | C2 | W18X55 | 1 | 256.1 | LOT-B |

**Footer row:** `[Set seq# on selected: ___]` `[Set]` … `[Apply to model]` (blue) / `[Export CSV]`

---

## Tab 2 — Crane Picks *(most complex tab)*

### Crane Setup panel
- Label: `Position: X=15240.0  Y=8200.0  Z=0.0 mm` (green text when set)
- Button: `[Pick crane position…]`

**Lift chart table** (editable, 2 columns):

| Radius (ft) | Max Cap (tons) |
|-------------|----------------|
| 50 | 40.0 |
| 75 | 28.5 |
| 100 | 18.0 |
| 125 | 12.5 |

Buttons: `[+ Row]` `[− Row]` `[Save chart]`

### Pick Assignment panel
`Pick ID: [P-003___]` `[Assign to selection]` (blue) `[Clear pick on selection]`

### Pick Summary table

| Pick ID | Pcs | Weight (t) | Radius (ft) | Cap (tons) | Status |
|---------|-----|-----------|-------------|------------|--------|
| P-001 | 4 | 8.24 | 62.3 | 34.1 | OK (24%) — green row |
| P-002 | 3 | 22.10 | 88.7 | 20.3 | OVER (109%) — red row |
| P-003 | 5 | 15.60 | 79.4 | 25.8 | Near (60%) — yellow row |
| P-004 | 2 | 4.30 | 45.1 | 40.0 | OK (11%) — green row |

Info label: `4 pick(s) calculated.  ⚠ 1 pick(s) OVER capacity!`

Buttons: `[Calculate all picks]` (blue) `[Color model]` `[Clear colors]` `[Select over-cap]`

---

## Tab 3 — Zone Dash

Hint: *Groups assemblies by UDA_FAB_LOT. Progress from UDA_FIELD_STATUS.*

**Table:**

| Zone / Lot | Total | Not started | Set | Bolted | Welded | Inspected | % Done |
|-----------|-------|------------|-----|--------|--------|-----------|--------|
| LOT-A | 24 | 0 | 0 | 2 | 4 | 18 | 75% — green row |
| LOT-B | 18 | 3 | 6 | 5 | 4 | 0 | 0% |
| LOT-C | 12 | 0 | 4 | 4 | 4 | 0 | 0% |
| (no lot) | 3 | 3 | 0 | 0 | 0 | 0 | 0% |

Buttons: `[Refresh]` (blue) `[Export CSV]` `[Select zone in model]`

---

## Tab 4 — Field Status

Info banner (amber background): `▸ Select assemblies in model, then set status below.`

### Set Field Status group
- `Status: [Bolted ▾]`
- `☑ Auto-stamp date UDA with today (2026-05-10)`
- `Inspector: [__________]`
- `[Apply to selection]` (blue, right-aligned)

### Today's Activity (2026-05-10) group
`[Refresh]` `[Export inspection punchlist]`

**Table:**

| Mark | Status | Inspector | Date | Lot |
|------|--------|-----------|------|-----|
| A3 | Bolted | J.Rivera | 2026-05-10 | LOT-A |
| A4 | Bolted | J.Rivera | 2026-05-10 | LOT-A |
| B3 | Set | — | 2026-05-10 | LOT-B |

Info: `3 piece(s) with activity today.`

---

## Tab 5 — Validate

**Scope:** `● All assemblies in model` `○ Current selection`

**Rules checklist** (all checked):
- ☑ Erection sequence assigned — ERECTION_SEQUENCE > 0
- ☑ Crane pick ID assigned — CRANE_PICK_ID not blank
- ☑ Set pieces have set date — if Field Status=Set → FIELD_SET_DATE not blank
- ☑ Inspected pieces have inspector — if Field Status=Inspected → UDA_INSPECTOR not blank
- ☑ Sequenced pieces are shipped or on site — if Seq# > 0 → Fab Status = Shipped or On site

**Results table:**

| Mark | Profile | Phase | Pick ID | Failed Rule |
|------|---------|-------|---------|-------------|
| C2 | W18X55 | 1 | P-003 | Erection sequence assigned |
| B4 | W21X68 | 1 | — | Crane pick ID assigned |
| D1 | HSS6X6X1/2 | 2 | P-004 | Sequenced pieces are shipped or on site |

Summary: `3 failure(s) in 57 object(s)  ·  Erection sequence assigned: 1, Crane pick ID assigned: 1, Sequenced pieces...: 1`

Buttons: `[Select in model]` `[Export CSV]` … `[Run rules]` (blue, right-aligned)
