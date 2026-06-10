# Change Requests — Round 2 (Martin, 2026-06-10)

Second feedback round after the v1.5.0 beta-review release. Five items,
all UI/workflow; no geometry changes. Target release: v1.6.0.

| # | Request | Decision / Notes | Status |
|---|---------|------------------|--------|
| 1 | Remember the previous job: 12 identical blankets differ only by order + network number. Tick/click to bring back the previous data. | Cache raw form text to `%AppData%\BesiaCAD\CanvasCovers\last-job.json` on every successful Generate. "Load Previous Job's Measurements" button restores walls + options ONLY — order number, network number, company, project name, date stay blank (Sebastian's call: avoids confusing stale job identity). | Planned |
| 2 | (no item 2 — Martin's numbering skips it) | — | — |
| 3 | Both eyelet choices (7 and 9) in fixings — same calculation, different label printed on the COP. No default for fixings; warn if not chosen. | `FixingType.Eyelet` split into `Eyelet7`/`Eyelet9` ("Eyelet TG7"/"Eyelet TG9", both -30). Combo starts unselected; Generate validation blocks until chosen. `FixingType.None` added so "not chosen" is representable. | Planned |
| 4 | Move "Options" above "Walls" so those questions are answered first. | Done in XAML; final order: Previous Job, Project Info, Options, Walls, Layers (Sebastian: Layers last — rarely touched). | Planned |
| 5 | Generated drawing is way too big to see; manual zoom-out then triggers the save-again prompt on close. | `Application.Zoom(dsZoomRange_Fit, null, null)` (SDK-sample-verified) immediately after generate, BEFORE the DXF save, so the saved state includes the fitted view. | Planned |
| 6 | Drop-down for company names (15–20 choices) with initials auto-populated. | Editable ComboBox fed from `customers.csv` (Name,Initials per line). Seed ships in install dir `Resources\`; copied on first use to `%AppData%\BesiaCAD\CanvasCovers\customers.csv` which Martin edits in Notepad (documented in quick-start guide). Martin supplied 29 entries. | Planned |

Flip each row's Status to **Implemented** as its task lands, and to
**Live-tested** after the in-DraftSight pass.
