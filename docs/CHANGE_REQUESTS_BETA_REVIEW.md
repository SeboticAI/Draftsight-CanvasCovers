# Lift Blanket — Beta Review Change Requests

Source: Adelaide Annexes & Canvas (Martin) review meeting following the v1.4.5
beta. This document captures the agreed scope, effort, and status for each of
the **28** requested changes, numbered to match the client's spreadsheet.

**Status of this document:** the summary below was sent to Martin to confirm we
understood each item correctly. **Awaiting his confirmation before any build
starts.** No production code has been changed yet — this pass was scoping only.

**Effort scale:** Trivial / Small / Medium / Large.

**Two small assumptions flagged to Martin** (noted inline): item 5 (total-width
and "Include COP" are mutually exclusive) and item 14 (BAG via a checkbox;
fixing-type text automatic). Plus the final tab name (item 26).

**Coordination note:** items **5, 10+11, 13** all touch width/quilting logic in
`LiftBlanketCalculator`. They share a blast radius and should be built in one
coordinated pass (with the test suite as the safety net), not scattered across
separate installers.

---

## Form & input fields

### 1 — Remove unused fields · Trivial · Build
Remove the textboxes for Sales contact, Measured by, Order number, Mobile, and
Notes (all on the client's paperwork already). Also strip the matching
properties in `ProjectMetadata.cs` and the lines that print them in
`LiftBlanketGenerator.cs`, so the form and the generated drawing stay
consistent.

### 2 — Keep Company / Network / Project; add Canvas/Production number · Trivial · Build
Keep the three existing fields. Add one new "Canvas/Production number" property,
one XAML row, and a Read/Apply line — mirroring the existing fields.

### 3 — File naming (print string) · Small · Parked
Save using the concatenated print string = production note no. + customer code
(KS/SP/OT/OCT) + network no. The same string is used for both the print label
and the saved filename. Logic is isolated and unit-tested in
`DxfExporter.Filename.cs`, so it's safe to change.
**Parked** pending the meeting detail: where the **customer code** comes from
(a new field the user selects/types, vs derived from existing data). That is the
swing factor on effort.

---

## Calculation & input behaviour

### 4 — Auto-populate right + rear height from left · Small · Build
When the left-wall height is entered, copy it into the right and rear walls **as
a placeholder only**. The fields are independent thereafter — editing the right
or rear height never changes the left. One-way seed, no two-way binding.
(Note: the right wall is **not** gaining three separate height inputs — it
receives the single copied value.)

### 5 — Optional total-width input · Small–Medium · Build
The five-segment system stays unchanged. Add a **separate total-width box** that
is only usable when "Include COP" is **off**; it feeds the cut width directly,
bypassing the segment sum. Because COP placement only exists when COP is on, the
COP/segment maths is never involved, so there's no risk to the working COP
logic.
*Assumption (confirm):* total-width and "Include COP" are mutually exclusive in
the UI.

### 6 — Fixing allowance auto-populates from fastener · Small · Build (with 7)
The auto-populate-with-override mechanism already exists. The real work is adding
the missing **Eyelet** fixing type: a new `FixingType.Eyelet` enum member, a
dropdown entry, and a `30` case in `FixingAllowance.cs`. This also closes a
latent bug where a fixing type with no explicit case silently returns 0.

### 7 — Confirm fixing values (Hooks 50 / Press studs 40 / Eyelets 30) · Trivial · Build (with 6)
Confirmed values, auto-populated from the fixing selection, user-overridable for
special cases. TG9/TG7 are **display text only** and share the same value (30) —
so one allowance, optionally two dropdown labels pointing at it. Built together
with item 6.

### 8 — Show allowance in dropdown text · Trivial · Build (with 6/7)
Show the allowance in the fastener dropdown, e.g. "Hooks facing in (−50)". Edit
the dropdown item labels. Source the label text and the value from the same
place as items 6/7 so they cannot drift apart.

### 9 — Velcro corners (optional 50mm return on L & R) · Medium · Parked (future)
Optionally auto-add a 50mm return on the left and right side walls (back wall
stays standard). This is real new geometry — it changes cut width / COP offset /
quilting bounds. Flagged by the client as a **future** feature; parked.

---

## Edge & quilting allowances

### 10 + 11 — Outline = true width; quilt inset its own value · Small · Build (one unit)
Currently a single value (`EdgeAllowanceMm`) does two jobs: it boosts the
outline width **and** sets the quilting inset. Per the client:

- **Outline is always the true width** — the overall total or the sum of the
  five segments. No automatic boost. The operator adds the 10mm shrinkage
  themselves when entering sizes.
- **Quilt inset becomes its own user-inputable field** ("Quilt Inset (mm)"),
  default **5mm**, may be set lower. It only pulls the quilting lines inward from
  the outline by that amount.

So: remove the width-boost job entirely, remove the old "Edge Allowance" field,
and add the new "Quilt Inset" field feeding the quilt-line clearance.
*Note:* generated outlines become ~10mm narrower than v1.4.5 (intended) — worth
a release-note line. The COP horizontal-offset math simplifies accordingly; the
test suite covers the knock-on.

---

## Drawing output

### 12 — Move printed blanket text to bottom-centre, inverted, fixed · Small–Medium · Build
Move the printed blanket text to the bottom-centre of each panel, inverted at the
very top of the boxes (which becomes the bottom when folded), ~25mm up from the
edge to clear the binding. Fixed position so it does not shift as the blanket
width changes. Coordinate change in the calculator (testable). "Inverted" = 180°
rotation; one on-host check needed to confirm `InsertSimpleNote`'s angle
parameter renders rotated text.

### 13 — Refine quilting line spacing · Small · Build
Refine the internal spacing distribution only. The **first line at the returns
and the return behaviour stay exactly as they are today** — this is a tweak to
the even-spacing logic, not a re-specification. Existing quilting tests continue
to protect the kept behaviour. (Precise "what's off about current spacing" to be
provided at build time.)

### 14 — Reminders inside the COP cutout · Small · Build
Print reminders onto the draw layer **inside the COP cutout** (the panel that
gets cut out and binned) so the operator can't miss them — e.g. "BAG" when a
storage bag is required, plus the fixing type. A checkbox toggles the reminder;
the text is placed inside the COP rectangle and sized to fit.
*Assumption (confirm):* BAG is driven by a checkbox; the fixing-type text is
automatic (already on the job).

### 15 — Glass-behind label checkbox · Trivial · Build
Add an optional checkbox for a glass-behind label (from the rectangle template).
New checkbox + one conditional text insert — same shape as the existing plastic-
cover option.

---

## Validation & user feedback

### 16 — Warn when L & R wall total widths don't match · Trivial · Build
Cars are square, so a left/right mismatch is a data error (often leftover from a
previous order). Live, **non-blocking** warning shown as the operator enters
values — a caution, not a hard block.

### 17 — General sanity-check warnings · Small (per rule) · Parked
General warnings for values that don't make sense. The validation framework
already exists, so each defined rule is cheap. **Parked** until the client
provides the actual list of checks — "values that don't make sense" is not
estimable as stated.

### 18 — Auto-dimension key measurements · Small–Medium (lean Small) · Build
Auto-dimension the **key** measurements (width / height / COP — not exhaustive)
so drawings can be checked visually rather than re-measured. Partly built already
(width + height dimensions exist). Known SDK gotchas are already handled (aligned
dimensions; `DIMSCALE` for visible text).

---

## Layers

### 19 — Remove the Defpoints layer · Trivial · Build
Delete the Defpoints entry from `DefaultLayers()` in `LayerSettings.cs`. Rows
rebuild automatically and no role points at it.

### 20 — Keep other cutting-tool layers + tickboxes + colours · No-op · Already built
Keep the other cutting-tool layers (rotary blade, drag blade, crease, draw/text)
for future products; keep the tickbox to assign elements to layers and change
colours. This already works in `LayersPanel` — the item is "don't remove it."

---

## Duplicate / reuse

### 21 — Clear-or-keep between blankets on the same job · Medium · Parked
A clear-or-keep option between blankets on the same job, ideally a forced prompt
so old data can't carry over accidentally. Net-new window lifecycle: the window
currently closes on Generate, so this means keeping it open and selectively
resetting fields (on the COM UI thread). **Parked** for now.

---

## Settings & environment — handled outside the add-in

These were resolved in the meeting as **DraftSight profile/settings** work, not
add-in code.

### 22 — Standardise model background to RGB 254, 250, 220 · Not add-in work
DraftSight application setting, applied via the profile.

### 23 — Fix dimension text size · Not add-in work
Symptom of multi-machine settings drift; handled via the standardised profile.

### 24 — Research/standardise the DraftSight settings profile (XML) · Not add-in work
The proper fix for 22/23 across home, work, and dev machines. A settings/profile
task outside the codebase.

### 25 — Ribbon vs menu display · Resolved — fix Martin's setup, not code
The add-in registers a ribbon tab correctly. Martin's instance was in classic-
menu mode (File, Edit, …) with no full ribbon, so the add-in rendered as a
separate ribbon-style menu. **Decision:** switch Martin to the ribbon workspace
as part of the profile setup (items 22–24). Building menu/toolbar support was
explicitly rejected — it is the SDK's documented crash zone (CLAUDE.md §9). The
typed-command fallback `CANVASCOVERSOPEN` already exists as a safety net for any
misconfigured machine.

### 26 — Confirm the tab name · Trivial · Confirm
One-string change wherever the name lands. Current window title:
"Adelaide Annexes & Canvas — Lift Blanket Generator". Awaiting the final name
from the client.

---

## Future / likely scope increase — quote separately

### 27 — Split panels wider than 3m into two walls · Large · Future / separate quote
Fundamentally changes the segment-driven calculator: one wall can become two cut
pieces, which ripples through the calculator, the generator's wall layout, COP
placement, quilting, dimensioning, and the per-wall-tab UI. Design-first;
separate quote.

### 28 — 45-degree COP wall variant · Large · Future / separate quote
An angled wall with the COP in the 45° section, effectively moving the COP into
the return. The geometry engine is axis-aligned (`RectSpec` has no rotation), so
an angled wall breaks that assumption throughout. Effectively a new product
variant. Design-first; separate quote.

---

## Summary

**Build now (next-installer batch):** 1, 2, 4, 5, 6, 7, 8, 10+11, 12, 13, 14,
15, 16, 18, 19, 20, 26 — mostly Trivial/Small, on the tested calculator or
isolated UI. The items with real substance: 5 (alternate input mode), 10+11 (the
allowance refactor), 12 (text placement + rotation check), 18 (dimensions).

**Parked (need input or deferred):** 3 (customer-code source), 9 (future), 17
(client's rule list), 21 (deferred lifecycle work).

**Not add-in work (DraftSight profile/settings):** 22, 23, 24, 25.

**Future / separate quote:** 27, 28.
