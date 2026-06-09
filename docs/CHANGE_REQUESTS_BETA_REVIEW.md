# Lift Blanket — Beta Review Change Requests

Source: Adelaide Annexes & Canvas (Martin) review meeting following the v1.4.5
beta. This document captures the agreed scope, effort, and status for each of
the **28** requested changes, numbered to match the client's spreadsheet.

**Status: CLIENT CONFIRMED (2026-06-09).** Martin reviewed the scoping summary
and responded item by item; his confirmations and refinements are folded in
below. The build-now batch is unblocked. A handful of **small open questions**
remain (marked "Open Q" inline) — none block starting; they can be confirmed as
each item is built.

**Effort scale:** Trivial / Small / Medium / Large.

**Coordination note:** items **5, 10+11, 13** all touch width/quilting logic in
`LiftBlanketCalculator`. They share a blast radius and should be built in one
coordinated pass (with the test suite as the safety net), not scattered across
separate installers.

**Open questions still to confirm with Martin (all minor):**
- Keep or drop the **Date** field? (not in his confirmed field list — item 1)
- Does the **blanket text** keep the L/R/B wall suffix to distinguish walls?
  (item 3/12)
- Is **self-adhesive Velcro** a distinct dropdown entry, or does the existing
  Velcro (also 0) cover it? (item 7)
- Which exact **return boxes** does the velcro-corners tickbox fill? (item 9)
- **Item 17** needs explaining to Martin (he didn't understand it) — or drop it.

---

## Form & input fields

### 1 — Field set (confirmed) · Small · Build
Martin's confirmed field list — **NOTE the reversal: Order number is KEPT**, not
removed as originally planned. Final fields:

- **Company name** — the customer's company name.
- **Company Initials** — NEW free-text field. AAC's code identifying the
  customer + depot (e.g. Kone Melbourne = KM, Otis Perth = OP). Becomes the
  MIDDLE section of the blanket text.
- **Network number** — provided by the customer. THIRD section of the blanket
  text.
- **AAC Order number** — from AAC's MYOB program. FIRST section of the blanket
  text. (This is the existing OrderNumber field, kept and relabelled — it was
  originally slated for removal.)
- **Project name** — job description; not used in the blanket text, just sits in
  the diagram so the job can be double-checked later (easier than re-reading a
  long network number).

REMOVE: Sales contact, Measured by, Mobile, Notes. Strip their textboxes, the
matching properties in `ProjectMetadata.cs`, and the lines that print them in
`LiftBlanketGenerator.cs`.
*Open Q:* the **Date** field isn't in Martin's list — keep (harmless, on the
template) or drop?

### 2 — Canvas/Production number · Superseded by item 1
Martin: "covered in item one." The original "Canvas/Production number" idea is
replaced by the new **Company Initials** + **AAC Order number** fields. No
separate Canvas/Production field.

### 3 — Blanket text + file naming (confirmed, fully specified) · Small · Build
The text printed on the blanket is the **"blanket text"**, a concatenation with
a space between three sections:

```
<AAC order number> <company initials> <network number>
```

- The **same string** is used for the printed blanket text AND the saved
  filename.
- Printed on **all** blankets, **upside down** (180°), **~25mm down from the
  top** (becomes the bottom when folded — see item 12 for placement).
- Text height: 20 is fine if everything is otherwise unchanged; target is
  ~25–30mm high. (Martin finds the 20 number confusing across drawings but
  accepts it if behaviour is unchanged.)

This unblocks the previously-parked file-naming work — the customer code is the
new Company Initials field. Logic is isolated and unit-tested in
`DxfExporter.Filename.cs`. The blanket-text content also changes the per-wall
identifier label currently built by `BuildProjectTag` in the generator.
*Open Q:* does the blanket text keep the L/R/B wall suffix to distinguish walls?

---

## Calculation & input behaviour

### 4 — Auto-populate height only (confirmed) · Small · Build
Pre-populate the **height** of the right wall and rear wall from the left wall
height, as a placeholder. **Nothing else** is pre-populated — Martin wants all
other boxes entered manually because "weird things" occasionally occur and
forcing manual entry is safer. One-way seed; fields independent thereafter.

### 5 — Optional total-width input (confirmed) · Small · Build
Confirmed mutually exclusive: when "Include COP" is **unticked**, the segment
boxes collapse so the operator enters a **single total width**. The five-segment
system is otherwise unchanged. Total width feeds the cut width directly; the
COP/segment maths is never involved (no COP when COP is off).

### 6 — Fixing allowance auto-populates from fastener · Small · Build (with 7)
The auto-populate-with-override mechanism already exists. Real work is adding the
missing **Eyelet** fixing type — see item 7.

### 7 — Fixing types & values (confirmed) · Small · Build (with 6)
Confirmed allowances, auto-populated from the fixing selection, user-overridable:

- Hooks (facing in / out): **−50**
- Press studs: **−40**
- Eyelet (TG9 / TG7): **−30** (TG9/TG7 are display labels only; same value)
- Velcro: **0**
- Self-adhesive Velcro: **0** — Martin noted this type (stick-on Velcro the
  blanket adheres to, no added height).

Work: add an **Eyelet** member to `FixingType` (value 30 in `FixingAllowance.cs`)
— this also closes a latent bug where a fixing with no explicit case silently
returns 0.
*Open Q:* is self-adhesive Velcro a SEPARATE dropdown entry, or does the existing
Velcro (also 0) already cover it? Both are 0, so functionally identical — only a
label difference.

### 8 — Show allowance in dropdown text · Trivial · Build (with 6/7)
e.g. "Hooks facing in (−50)". Source the label text and value from the same
place as items 6/7 so they can't drift.

### 9 — Velcro corners tickbox (reframed — much simpler) · Small · Build (low priority)
Martin reframed this: it does **NOT** alter width / COP / quilting as new
geometry. It's simply a **tickbox that auto-populates the return boxes** on the
left and right walls with the 50mm return (back wall unchanged). It reuses the
existing door-return mechanism — the operator can already enter these manually.
Effort drops from Medium to Small; Martin is happy to enter manually for now, so
low priority.
*Open Q:* which return boxes exactly — the rear return on each side wall (two
total)?

---

## Edge & quilting allowances

### 10 + 11 — Outline = true width; quilt inset its own value · Small · Build (one unit)
No input required from Martin (confirmed earlier). A single value
(`EdgeAllowanceMm`) currently does two jobs. **Delete the width-boost** (outline
= exactly the entered size — overall total or sum of segments; the operator adds
their own 10mm shrinkage manually) and **promote the inset to a new user field**
"Quilt Inset (mm)", default **5mm**, can go lower. Remove the old "Edge
Allowance" field. *Note:* generated outlines become ~10mm narrower than v1.4.5
(intended — release-note it). COP horizontal-offset math simplifies; tests cover
the knock-on.

---

## Drawing output

### 12 — Move blanket text to bottom-centre, inverted, fixed (confirmed) · Small · Build
Move the blanket text (item 3) to the bottom-centre of each panel, inverted at
the very top of the boxes (becomes the bottom when folded), ~25mm down from the
top to clear the binding. Fixed position so it doesn't shift with width.
Martin confirmed the `InsertSimpleNote` flow: set text height first, then the
angle — **180 gives upside-down** (he does the equivalent manually by drawing the
line). The on-host rotation risk is resolved.

### 13 — Refine quilting line spacing · Small · Build
No input required. Internal spacing distribution only — the **first line at the
returns and the return behaviour stay exactly as today**. A tweak to the
even-spacing logic, not a re-spec. Existing quilting tests protect the kept
behaviour.

### 14 — Reminders inside the COP cutout — VERTICAL (confirmed) · Small · Build
Print reminders onto the draw layer inside the COP cutout (the panel that gets
cut out and binned) so the operator can't miss them — e.g. "BAG" when a storage
bag is required, plus the fixing type. Martin: make the reminder text
**vertical** so it fits the cutout more easily. Checkbox toggles the reminder;
fixing-type text is automatic.

### 15 — Glass-behind label checkbox · Trivial · Build
No input required. New checkbox + one conditional text insert — same shape as the
existing plastic-cover option.

---

## Validation & user feedback

### 16 — Warn on L/R width mismatch — WARNING ONLY (confirmed) · Trivial · Build
Live, **non-blocking** warning. Martin stressed it must be **just a
warning/reminder**, because lifts with one angled COP will legitimately have a
left/right mismatch — it must never block.

### 17 — General sanity-check warnings · Unclear · Needs clarification / candidate to drop
Martin didn't understand this item ("I'm not sure what this one means"). It
originated as our suggested category. **Action:** explain it to Martin with
concrete examples (e.g. "COP wider than the wall", "height below a sane minimum",
"spacing larger than the wall") so he can decide which rules he wants — or drop
the item. Not estimable until then.

### 18 — Auto-dimension — template dimensions only (confirmed) · Small · Mostly built
Martin: don't add lots of dimensions — they're only for peace of mind until the
add-in is fully trusted. **Use only the dimensions that already appear on the
template drawings.** The generator already emits width + height dims, so this is
near-complete; verify it matches the template and stop there.

---

## Layers

### 19 — Remove the Defpoints layer · Trivial · Build
No input required. Delete the Defpoints entry from `DefaultLayers()` in
`LayerSettings.cs`.

### 20 — Keep other cutting-tool layers + tickboxes + colours · No-op · Already built
No input required. Already works in `LayersPanel`.

---

## Duplicate / reuse

### 21 — Clear-or-keep between blankets · Medium · Parked
Martin: "over my head" — deferred. Net-new window lifecycle (the window currently
closes on Generate). Parked.

---

## Settings & environment — handled outside the add-in

### 22 — Standardise model background to RGB 254, 250, 220 · Done
Martin: "already done." DraftSight profile setting.

### 23 — Fix dimension text size · Not add-in work
No input required. Handled via the standardised profile.

### 24 — Research/standardise the DraftSight settings profile (XML) · Not add-in work
No input required. The proper fix for 22/23 across home/work/dev machines.

### 25 — Ribbon vs menu display · Resolved — fix Martin's setup, not code
No input required. Switch Martin to the ribbon workspace via the profile setup.
Menu/toolbar support was rejected (SDK crash zone, CLAUDE.md §9). The
typed-command fallback `CANVASCOVERSOPEN` exists as a safety net.

### 26 — Tab name · Resolved
Martin: "name is okay." Keep the current window title
("Adelaide Annexes & Canvas — Lift Blanket Generator"). No change.

---

## Future / likely scope increase — quote separately

### 27 — Split panels wider than 3m into two walls · Large · Future (manual for now)
Martin: "we will do this manually for now." Deferred.

### 28 — 45-degree COP wall variant · Large · Future (manual for now)
Martin: "we will do this manually for now." Deferred.

---

## Summary

**Build now (next-installer batch):** 1, 3, 4, 5, 6, 7, 8, 10+11, 12, 13, 14,
15, 16, 18, 19, 20 — all confirmed. (2 is folded into 1; 9 is an optional
low-priority Small; 26 is resolved.) The items with real substance: 5 (alternate
input mode), 10+11 (the allowance refactor), 12 (text placement, rotation
confirmed), 3 (blanket-text builder shared by print + filename + per-wall label).

**Reuse the calculator pass:** build 5, 10+11, 13 together (shared blast radius).

**Needs a quick word with Martin before/while building (minor):** Date field
keep/drop (1), wall-suffix in blanket text (3), self-adhesive Velcro label (7),
which return boxes (9), and explaining item 17 (or dropping it).

**Parked / deferred:** 9 (optional, manual for now), 17 (needs explanation), 21
(over Martin's head).

**Not add-in work (DraftSight profile/settings):** 22 (done), 23, 24, 25.

**Future / manual for now:** 27, 28.
