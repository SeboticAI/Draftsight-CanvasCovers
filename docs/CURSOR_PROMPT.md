# Cursor / Claude Code Kickoff Prompt

The prompt below is what to paste into Claude Code (or Cursor's chat) when
starting work on this project. It primes the assistant with the right
context, the right caution, and the right working method.

Reuse it whenever a new session starts and the assistant has no memory of
prior context.

---

## The prompt — copy from below

```
You are working on a C# DraftSight add-in. The host CAD application is
DraftSight 2026 Professional. The end goal is a free demo to win a paid
engagement with a canvas cover manufacturing client.

Before doing anything else, read these files in order:

1. `CLAUDE.md` — the canonical playbook for DraftSight add-in development.
   It is the source of truth for project structure, the COM/CLSID loading
   mechanics, the dev loop, and gotchas. Cite section numbers when justifying
   architectural decisions.
2. `docs/PROJECT_BRIEF.md` — context on the client, the problem, and the
   strategic shape of the project.
3. `docs/DEMO_SCOPE.md` — the exact contract for what the demo will and
   will not do. Treat this as a hard scope boundary. Anything outside is
   deferred.
4. `docs/HELLO_DRAFTSIGHT.md` — the first-time setup walkthrough. If the
   user has not yet completed this (no proof the SDK sample loads), do not
   write any project code yet — push back and confirm the hello-world step
   succeeded first.

## How to work

- **Read before writing.** Always check the existing files before producing
  new code. The playbook contains a csproj template, an App.cs skeleton, and
  the XML config format — use them, do not reinvent.
- **DraftSight API knowledge is thin in your training data.** When you need
  to call a DraftSight API, do not invent method names from memory. Either:
  (a) reference a known-working pattern in `APISDK\samples\` and ask the
  user to share the relevant snippet, or
  (b) state clearly that the exact API signature needs to be verified and
  propose a placeholder with a `// TODO: verify against APISDK docs` comment.
  Do not bluff.
- **One concern per change.** No mass refactors. Each edit should be small,
  reversible, and tested before the next.
- **Test compiles after each change.** Don't pile up unverified edits.
  `dotnet build` is cheap.
- **Surface load errors loudly.** Every COM entry point should be wrapped in
  a try/catch with `MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace)`.
  DraftSight silently swallows add-in errors otherwise.
- **Generate fresh GUIDs.** Use `[guid]::NewGuid()` in PowerShell. Never
  copy a GUID from documentation.
- **Resist scope creep.** If you find yourself wanting to add something not
  in `DEMO_SCOPE.md`, log it in `docs/POST_DEMO_BACKLOG.md` and move on.
- **Use the project structure from `CLAUDE.md` §3.** Don't invent a new
  layout.

## What to do first

After reading the four files above, before writing any code, do these in
order:

1. **Confirm with the user that hello-world succeeded.** Ask whether
   `docs/HELLO_DRAFTSIGHT.md` Step 9 outcome #1 has been achieved. If not,
   guide them through whichever step failed. Do not proceed to scaffolding
   until this is confirmed.
2. **Scaffold the `CanvasCovers/` project per `CLAUDE.md` §3.** Create:
   - `CanvasCovers/CanvasCovers.csproj` from the template in §4 (generate a
     fresh GUID for the `[Guid]` attribute and the matching XML)
   - `CanvasCovers/App.cs` from the skeleton in §6
   - `CanvasCovers/CanvasCovers.xml` (the DraftSight add-in config)
   - Empty folders: `Common\`, `Commands\`, `Models\`, `Geometry\`, `UI\`,
     `Resources\`, `Properties\`
   - A minimal `Properties\AssemblyInfo.cs` if needed
3. **Stop and confirm the scaffold builds and loads** before adding any
   feature code. The button does not need to do anything yet — it just
   needs to appear on the ribbon and respond to a click with a "Hello"
   MessageBox. This is the project-level equivalent of hello-world: it
   isolates scaffolding bugs from feature bugs.
4. **Only after step 3 succeeds**, start implementing `DEMO_SCOPE.md`,
   working in this order:
   1. `Models/CoverParameters.cs` — POCO with the form fields
   2. `Geometry/RectangularCover.cs` — generates the entities from params
      (start with just the `CUT` layer outline; verify it draws; then add
      `STITCH`, `FIX`, `REF` one at a time)
   3. `UI/CoverForm.xaml` + `.xaml.cs` — the WPF window
   4. `Commands/GenerateCoverCommand.cs` — wires the ribbon click to the
      form, and the form's Generate button to the geometry
   5. Polish, validation, error handling
5. **Test against an empty DraftSight drawing after each geometry step.**
   The locked-DLL dev loop (close DraftSight, build, copy DLL, reopen
   DraftSight) is the slowest part of this work — make every cycle count.

## When you're unsure

- Unsure whether a DraftSight API exists or has the signature you think:
  state the uncertainty, ask the user to check the SDK sample or `.chm`
  docs, and write a placeholder. Do not guess.
- Unsure whether a change is in scope: cite `DEMO_SCOPE.md` and ask.
- Unsure about the right place to put a file: cite `CLAUDE.md` §3 and ask.

## What success looks like

The user can open DraftSight, click a ribbon button labelled "Generate
Cover", fill in 7 numeric fields, click Generate, and see a rounded
rectangle with stitch line and eyelet circles appear on four correctly
named layers. That's the whole demo.

Now read the four files and report back what's already established, what's
missing, and what you propose to do first.
```

---

## Notes for Seb (not part of the prompt)

- This prompt is deliberately long. It front-loads the discipline so the
  assistant doesn't have to be re-corrected later. Long prompts beat short
  prompts for novel domains where the AI's training data is thin.
- The "do not bluff" instruction is the most important line in the prompt.
  Without it, the AI will confidently hallucinate DraftSight API method
  names that don't exist, and you'll spend hours chasing imaginary methods.
- The "hello-world first" gate is also critical. If you let the AI dive
  into writing project code before the SDK sample loads, you've created
  two unknowns (environment + new code) that interact in painful ways.
- If you find yourself fighting the AI on something, paste the relevant
  `CLAUDE.md` section and tell it to re-read. Anchoring back to the
  playbook resolves 80% of disagreements.

## When to refresh this prompt

- After hello-world succeeds → remove the "do not proceed until
  hello-world is confirmed" gate.
- After the scaffold loads → remove the "scaffold first, then features"
  gate.
- After each major milestone → trim outdated parts so the prompt stays
  short and current.
