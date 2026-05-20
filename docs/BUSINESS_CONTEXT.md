# Business Context

The commercial side of this project. Who it's for, what value it
delivers, pricing thinking, and the productisation question. Kept
separate from the technical docs so the engineering surface doesn't
get tangled with commercial strategy.

---

## The client

**Adelaide Annexes & Canvas** ([adelaideannexe.com.au](https://adelaideannexe.com.au/))
— canvas / awning manufacturer based in South Australia. Produces a wide
range of fabric products: caravan annexes, awnings, bag awnings, mesh
walls, lift blankets, ute canopies, trailer canopies, plus various home
and commercial canvas products. Contact info baked into the dialog's
branded header: 08 8357 4444, sales@adelaideannexe.com.au.

**Why they need this:** the owner has confirmed he personally spends
**5+ hours per week** manually drafting canvas products in DraftSight
from measurement forms. Skilled, repetitive, error-prone work that
should be operator-keyed-in once and machine-produced.

**Why we're well-positioned:** found through tutoring work, existing
relationship, technical credibility already established. They're not
shopping around — they're looking at us specifically.

---

## ROI math

Conservative single-tenant value calculation:

- Owner time saved: **5 h/week × $120/h** (typical Australian
  small-business-owner marginal value) = **$600/week**
- Annualised (50 working weeks): **~$30,000/year**

That's just the time saved. Doesn't count:

- Drawing-error reduction (re-cuts cost fabric + labour)
- Faster quoting → faster sales cycle
- Owner capacity freed for higher-leverage work (sales, ops, growth)
- New-staff training time eliminated (operator doesn't need to learn
  DraftSight, just the form)

Realistic total annual value to a single mid-sized canvas company:
**$30k–60k**. The lift blanket flow alone covers part of this; adding
caravan annexe (their biggest product line) covers more.

---

## Pricing thinking

Two viable structures for Adelaide directly. Both pay back in well
under 6 months at the conservative ROI:

### Option A — one-time licence + annual support

- **Build fee: AUD $18–25k.** Covers all work through caravan flow,
  save/load, DXF auto-export, branded icons, installer, code signing.
- **Annual support: AUD $3–5k.** Updates, bug fixes, ad-hoc support
  during the support window.

### Option B — annual subscription, no upfront

- **AUD $8–12k/year, all-in.** Includes ongoing updates and support.

Both anchor under $30k/year value, so the maths is undeniable to him.
Above $30k/year and he starts pushing back on principle.

### The pitch frame

Don't lead with the price. Lead with the value.

> *"It returns you 5 hours every week — about $30,000 of your time a
> year. Building this and supporting it for a year is $X."*

Owners frequently anchor higher than you'd dare ask. If he names a
number first, he might say something you wouldn't have asked for.

Useful question to put in front of him next time:

> *"If a tool existed that gave you those 5 hours back, what would it
> be worth to you?"*

His answer is your ceiling.

---

## The productisation question (SeboticAI subscription)

The 5h/week data point materially strengthens the case for a wider
product. If **one** owner of a small canvas company is in this pain,
there are dozens to hundreds globally in the same pain. Per-customer
willingness-to-pay just moved from "$50/month subscriber" to "$5–10k/year
customer." That's a meaningfully different unit economics story.

### What's required before productising

- **Generalise the branding.** Adelaide Annexes & Canvas is hardcoded
  in `BrandedHeader`, in the title block strings inside
  `LiftBlanketGenerator`, in `CanvasCovers.xml`. All needs to become
  per-tenant config.
- **Licensing infrastructure.** Per `/CLAUDE.md` §13, the BesiaBIM
  Ed25519 token integration is the planned mechanism. Has to actually
  exist before paid customers can be onboarded.
- **Marketing surface.** Landing page, demo video, pricing page,
  trial-request flow on the SeboticAI site.
- **Sales motion.** The market is canvas / awning / industrial-fabric
  manufacturers using DraftSight. Likely a few hundred globally.
  Cold outreach + LinkedIn + trade publications. A 6–12 month
  commitment to make pay.

### Recommended sequence

1. **Close Adelaide.** Build through caravan annexe + save/load + DXF
   export. Charge properly. Deploy. Give them 3–6 months of happy use.
2. **Use them as a reference customer.** With a real install and a
   real ROI number, the story for customer #2 becomes "we save
   Adelaide Annexes $X/month — we licence it to canvas
   manufacturers." That sells.
3. **Then productise.** By that point you have:
   - A battle-tested codebase
   - A real ROI figure with a real client name attached
   - Time to build licensing and marketing infrastructure

Doing 1 and 3 in parallel from a standing start is how both ship
poorly. Sequence them.

### Honest caveats

- TAM is genuinely small. AutoCAD dominates industrial drafting;
  the DraftSight + canvas-industry intersection is a niche of a niche.
  This is a "few dozen happy customers paying real money" business,
  not a "thousand SaaS subscribers" business.
- Per-customer churn should be low — they don't switch CAD tools
  casually, and a tool saving 5 hours of owner time weekly is sticky.
- The marginal cost of supporting one more customer is low if the
  multi-tenant config is done right.

---

## Things to ask Adelaide at the next meeting

Sequencing matters — get cash and specs in one conversation if you can.

1. **Caravan annexe measurement form / sample DXF.** Without this we
   can't model caravan annexes accurately, and caravan is their
   primary product. Blocks half the demo value.
2. **Confirmation of his time-saved figure.** "5+ hours/week" is what
   you mentioned — get him to confirm out loud. Use it to anchor
   pricing.
3. **What CAM machine and software** they use downstream of the DXF.
   Affects which layer names / colours the machine expects. Currently
   we use sensible defaults (`CC-Outline` red etc.) but they may need
   specific names (e.g. `CUT`, `SCORE`, `DRAW`).
4. **Maximum sheet width** of their cutting bed. Tells us the upper
   validation bound on wall dimensions.
5. **Are there *other* products** beyond lift blanket and caravan
   annexe that they want first? Bag awnings? Ute canopies? Some may
   be cheaper to build than others.
6. **Commercial structure preference** — upfront vs subscription. Let
   him anchor.

---

## Numbers to not say out loud

Internal-only context — what the build is worth from our side.

- Roughly 30–50 hours of development to current state. At
  $120–150/h consulting rate, that's $3.6k–7.5k of work done.
- Remaining work to ship (caravan flow + save/load + DXF + icons +
  installer + signing): another 30–50 hours. Same hourly value.
- So a fair internal cost-plus floor is ~$15k for the build.
- The $18–25k upfront recommendation is **value-based**, not
  cost-based. He's paying for outcomes, not hours.

Never bill hourly on this kind of work. The owner's pain is worth
more than the hours.
