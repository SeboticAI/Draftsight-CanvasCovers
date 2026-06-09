# Client Questions

Current open questions for Adelaide Annexes & Canvas after the v1.5.0
beta-review build. Use this for the next conversation with Martin/customer
operators, not the older discovery-phase questions.

## Bring

- `Installer\Output\BesiaCAD-CanvasCovers-Setup-1.5.0.exe`
- `docs\help\CanvasCovers-Quick-Start.pdf`
- one generated Lift Blanket DXF from their real measurements
- screenshots/photos of any cutter output issues

## Must Confirm From Real Cutting

1. **Cut outline size**
   - Does outline = entered width match production now that the old +10mm boost
     is gone?
   - Are operators happy manually adding shrinkage to entered dimensions?

2. **COP placement**
   - Does `COP left = DR-L + S1` and `COP width = S2` match the paper sheet on
     real jobs?
   - Any angled-COP cases that need separate future support?

3. **Height/fixing math**
   - Do Hooks 50, Press Studs 40, Eyelet 30, Velcro 0, Self-adhesive Velcro 0
     cover the jobs they use?
   - Is the positive-magnitude UI wording clear enough even though the printed
     drawing shows allowances as negative values?

4. **Quilting**
   - Is the current even-divided target spacing acceptable?
   - If not, get the exact lookup/formula.
   - Is Quilt Inset default 5mm correct?
   - Are vertical return-boundary quilt lines correct?

5. **Blanket text / filename**
   - Is `<AAC order> <company initials> <network>` the correct content?
   - Keep the per-wall `L/B/R` suffix on printed blanket text?
   - Text height: keep 25mm or move closer to 30mm?

6. **COP reminders**
   - Are vertical reminders inside the cutout readable?
   - Is spacing between BAG / fixing / GLASS BEHIND OK?

7. **Dimensions and title/project notes**
   - Are width dimensions below each wall and one height dimension on the
     leftmost wall enough?
   - Should any right-side project text be removed or reformatted?
   - Keep the Date field?

8. **Layers**
   - Confirm six-layer set is accepted by the cutter without Defpoints.
   - Confirm role defaults:
     - Cut -> `1 Rotary Blade`
     - COP/quilt/annotation -> `5 Draw and Text`
     - Dimensions/project text -> `0`

## Deferred / Optional

- Velcro corners tickbox: if still wanted, confirm exactly which return boxes
  should auto-fill with 50mm.
- General sanity warnings: Martin did not understand item 17; only add concrete
  warnings if he asks for them.
- Clear-or-keep previous job data between generations: parked.
- Split panels wider than 3m: future/manual for now.
- 45-degree COP variant: future/manual for now.

## Caravan Annexe

Still gated on a measurement sheet and matching sample DXF. Do not design this
from memory. Get the artefacts and have the measurer narrate one real job.
