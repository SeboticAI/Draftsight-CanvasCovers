# Change Requests — Round 3 (Martin, 2026-06-19)

Follow-up to round-2 item 1 (previous-job memory). One item, UI/workflow
only; no geometry changes. Target release: v1.7.0.

| # | Request | Decision / Notes | Status |
|---|---------|------------------|--------|
| 1 | "Load Previous Job's Measurements" should bring the notes back too — project name, customer, initials, etc. — not just the measurements. The operator can still edit them after loading. | Project info is now cached alongside walls + options and restored by the same button. This reverses the original v1.6.0 call to keep order/network/company/project/date blank: per Martin, the full set carries over (including the per-job order and network numbers) and the operator edits whatever differs after loading. Date is cached as a flat `"yyyy-MM-dd"` string (same JavaScriptSerializer-DateTime avoidance as `SavedAt`); a pre-round-3 cache simply restores those fields as blank. | Implemented |

Touched: `CachedJobInputs` (new flat project fields), `LiftBlanketWindow`
`SaveJobCache`/`LoadPreviousButton_Click` (capture + restore via the existing
`ProjectMetadataPanel.Read()`/`Apply(...)`), the Load-Previous note text
(XAML + dynamic), and `JobCacheTests` (project-info round-trip +
pre-round-3-cache null-safety).

Flip the row's Status to **Live-tested** after the in-DraftSight pass
(generate → reopen dialog → Load Previous → confirm the notes return and the
date restores).
