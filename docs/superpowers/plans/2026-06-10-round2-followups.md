# Round-2 Follow-ups (v1.6.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Martin's five round-2 requests: remember the previous job's measurements, split Eyelet into TG7/TG9 with no fixings default, move Options above Walls, auto zoom-to-fit after generate, and a customer-name dropdown (with initials autofill) fed from an editable CSV.

**Architecture:** All five changes are UI/workflow level — the calculator and generator geometry are untouched except for the `FixingType` enum split (label-only; allowances unchanged). New headless pieces (`CustomerDirectory`, `JobCache`, cache DTOs) follow the established pattern: WPF-free files under `Models`/`IO`, linked into `CanvasCovers.Tests` and unit-tested with MSTest. The job cache stores the operator's RAW typed strings (not parsed values) so a restore reproduces the form exactly; project information (order/network/company) is deliberately never cached per Sebastian's decision. The customer list ships as a read-only seed in `Resources\customers.csv` and is copied on first use to `%AppData%\BesiaCAD\CanvasCovers\customers.csv`, which Martin edits in Notepad.

**Tech Stack:** C# net48, WPF, MSTest, `System.Web.Script.Serialization.JavaScriptSerializer` (built-in `System.Web.Extensions` assembly — no NuGet), DraftSight SDK interop (`Application.Zoom`, verified against `APISDK\samples\C#\3D\SliceEntities`).

**Decisions already made (do not re-litigate):**
- Cache restores walls + options only. Order number, network number, company, project name, date are NEVER cached or restored (operator types those fresh per job).
- Eyelet labels are "Eyelet TG7" / "Eyelet TG9" (matches the FIXINGS table stamped on client drawings). Both keep the -30 allowance.
- Fixings combo has NO default; Generate blocks with a validation error until one is chosen.
- Section order becomes: Previous Job → Project Information → Options → Walls → Layers.
- Customer file is per-user (`%AppData%`), seeded at first run from the install dir. Documented in the quick-start guide.
- Zoom-to-fit runs after generate, BEFORE the DXF save dialog, so the saved view state includes the fit (kills the "save changes?" nag on close).

**CLAUDE.md constraints that apply throughout:**
- §9: no dispatcher exception handler in-host — every new code path reachable from operator input must be defended with try/catch; never let file IO or SDK calls throw on the UI thread.
- §9: verify SDK signatures against samples (done: `Application.Zoom(dsZoomRange_e.dsZoomRange_Fit, null, null)` from `samples\C#\3D\SliceEntities\Commands\CommandSliceEntities.cs:286`).
- §12: one concern per change, test compile after every task.

---

### Task 1: Branch + change-request log

**Files:**
- Create: `docs/CHANGE_REQUESTS_ROUND2.md`

- [ ] **Step 1: Create the working branch**

```bash
git checkout -b round2-followups
```

- [ ] **Step 2: Write the change-request log**

Create `docs/CHANGE_REQUESTS_ROUND2.md`:

```markdown
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
```

- [ ] **Step 3: Commit**

```bash
git add docs/CHANGE_REQUESTS_ROUND2.md
git commit -m "docs: log round-2 change requests from Martin"
```

---

### Task 2: FixingType split — None / Eyelet7 / Eyelet9 (TDD)

**Files:**
- Modify: `CanvasCovers.Tests/FixingAllowanceTests.cs`
- Modify: `CanvasCovers/Models/Products/LiftBlanket/LiftBlanketOptions.cs`
- Modify: `CanvasCovers/Geometry/Products/LiftBlanket/FixingAllowance.cs`
- Modify: `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketGenerator.cs:312-323` (FixingLabel)

- [ ] **Step 1: Update the tests to the new enum members (failing first)**

Replace the `Eyelet_Default_Is_30` test in `CanvasCovers.Tests/FixingAllowanceTests.cs` and add a `None` test:

```csharp
        [TestMethod]
        public void Eyelet_TG7_And_TG9_Default_Is_30()
        {
            Assert.AreEqual(30.0, FixingAllowance.DefaultFor(FixingType.Eyelet7));
            Assert.AreEqual(30.0, FixingAllowance.DefaultFor(FixingType.Eyelet9));
        }

        [TestMethod]
        public void None_Default_Is_0()
        {
            Assert.AreEqual(0.0, FixingAllowance.DefaultFor(FixingType.None));
        }
```

- [ ] **Step 2: Run tests to verify they fail (compile error is the expected failure)**

Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: build FAILS with `'FixingType' does not contain a definition for 'Eyelet7'` (and `None`).

- [ ] **Step 3: Update the enum**

In `CanvasCovers/Models/Products/LiftBlanket/LiftBlanketOptions.cs` replace the enum and the `Fixings` property default:

```csharp
    public enum FixingType
    {
        // "Not chosen yet". There is deliberately NO default fixing (round 2,
        // item 3) — Martin forgot to set it twice and had to edit drawings by
        // hand. The UI blocks Generate until a real fixing is selected, so
        // the generator never sees None.
        None,
        Velcro,
        HooksFacingIn,
        HooksFacingOut,
        PressStuds,
        // TG7 vs TG9 eyelets calculate identically (-30) but the label prints
        // on the COP, so the ordered size must be distinguishable (item 3).
        Eyelet7,
        Eyelet9,
        SelfAdhesiveVelcro,
    }
```

and:

```csharp
        public FixingType Fixings { get; set; } = FixingType.None;
```

- [ ] **Step 4: Update FixingAllowance**

In `CanvasCovers/Geometry/Products/LiftBlanket/FixingAllowance.cs` replace the `Eyelet` case and add `None`:

```csharp
                case FixingType.PressStuds:
                    return 40.0;
                case FixingType.Eyelet7:
                case FixingType.Eyelet9:
                    return 30.0;
                case FixingType.Velcro:
                case FixingType.SelfAdhesiveVelcro:
                case FixingType.None:
                    return 0.0;
```

- [ ] **Step 5: Update FixingLabel in the generator**

In `CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketGenerator.cs` (`FixingLabel`, ~line 312) replace the `Eyelet` case:

```csharp
                case FixingType.Eyelet7: return "Eyelet TG7";
                case FixingType.Eyelet9: return "Eyelet TG9";
                // Unreachable in practice — the dialog blocks Generate while
                // no fixing is selected — but keep the label printable.
                case FixingType.None: return "None";
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: PASS (all tests green).

- [ ] **Step 7: Build the add-in project too (generator isn't linked into tests)**

Run: `dotnet build CanvasCovers\CanvasCovers.csproj -c Release`
Expected: Build succeeded, 0 errors. (The XAML still says `Tag="Eyelet"` — that's string-only, fixed next task.)

- [ ] **Step 8: Commit**

```bash
git add CanvasCovers.Tests/FixingAllowanceTests.cs CanvasCovers/Models/Products/LiftBlanket/LiftBlanketOptions.cs CanvasCovers/Geometry/Products/LiftBlanket/FixingAllowance.cs CanvasCovers/Geometry/Products/LiftBlanket/LiftBlanketGenerator.cs
git commit -m "feat: split Eyelet into TG7/TG9, add FixingType.None (round 2 item 3)"
```

---

### Task 3: Fixings combo — two eyelets, no default, Generate validation

**Files:**
- Modify: `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml:153-160` (FixingsInput)
- Modify: `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml.cs:187-217` (ReadOptions)

- [ ] **Step 1: Update the combo in XAML**

Replace the `FixingsInput` ComboBox block (remove `SelectedIndex="3"`, split the Eyelet item):

```xml
                            <!-- No SelectedIndex: deliberately starts unselected
                                 (round 2, item 3). ReadOptions blocks Generate
                                 until the operator chooses. -->
                            <ComboBox Grid.Column="1" Name="FixingsInput" SelectionChanged="FixingsInput_SelectionChanged">
                                <ComboBoxItem Content="Velcro (0)" Tag="Velcro" />
                                <ComboBoxItem Content="Self-adhesive Velcro (0)" Tag="SelfAdhesiveVelcro" />
                                <ComboBoxItem Content="Hooks Facing In (-50)" Tag="HooksFacingIn" />
                                <ComboBoxItem Content="Hooks Facing Out (-50)" Tag="HooksFacingOut" />
                                <ComboBoxItem Content="Press Studs (-40)" Tag="PressStuds" />
                                <ComboBoxItem Content="Eyelet TG7 (-30)" Tag="Eyelet7" />
                                <ComboBoxItem Content="Eyelet TG9 (-30)" Tag="Eyelet9" />
                            </ComboBox>
```

- [ ] **Step 2: Validate in ReadOptions**

In `LiftBlanketWindow.xaml.cs`, replace the first three lines of `ReadOptions` (the `HooksFacingOut` fallback) with:

```csharp
            // No default fixing (round 2, item 3): the operator must choose,
            // because TG7 vs TG9 prints on the COP and a silently-defaulted
            // value already cost two hand-edited drawings.
            FixingType fixing = FixingType.None;
            ComboBoxItem selected = FixingsInput.SelectedItem as ComboBoxItem;
            string tag = selected?.Tag as string;
            if (string.IsNullOrEmpty(tag) || !Enum.TryParse(tag, out fixing) || fixing == FixingType.None)
                errors.Add("Select the fixings required (there is no default).");
```

The rest of `ReadOptions` is unchanged — `Fixings = fixing` now carries `None` only when the error list already blocks Generate.

- [ ] **Step 3: Build**

Run: `dotnet build CanvasCovers\CanvasCovers.csproj -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml.cs
git commit -m "feat: fixings combo starts unselected, validates on Generate (round 2 item 3)"
```

---

### Task 4: Move OPTIONS above WALLS

**Files:**
- Modify: `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml:100-210` (section order)

- [ ] **Step 1: Reorder the section Borders**

Inside the `<ScrollViewer><StackPanel>`, move the entire `OPTIONS` `<Border Style="{StaticResource SectionPanel}">…</Border>` block (the one whose first child is `<TextBlock Style="{StaticResource SectionTitle}" Text="OPTIONS" />`) to sit BETWEEN the `PROJECT INFORMATION` Border and the `WALLS` Border. Move the whole Border element verbatim — no internal edits. Final order of the four Borders:

1. `PROJECT INFORMATION` (MetadataPanel)
2. `OPTIONS` (checkboxes, FixingsInput, allowance/quilt inputs, ExportDxfOption)
3. `WALLS` (WidthWarningText + WallTabs)
4. `LAYERS` (LayersControl, keeps `BorderThickness="0"`)

This reorder is safe by design: `_initialized` guards `SharedParam_Changed` order-independently (see the comment on `_initialized` in the code-behind), and `FixingsInput_SelectionChanged` no longer fires during XAML parse because the combo starts unselected (Task 3).

- [ ] **Step 2: Build**

Run: `dotnet build CanvasCovers\CanvasCovers.csproj -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml
git commit -m "feat: move Options section above Walls (round 2 item 4)"
```

---

### Task 5: Auto zoom-to-fit after generate

**Files:**
- Modify: `CanvasCovers/Commands/OpenCanvasCoversCommand.cs:116-170` (LiftBlanketWindow_GenerateRequested)

- [ ] **Step 1: Add the zoom call**

In `LiftBlanketWindow_GenerateRequested`, immediately AFTER the `try { generator.Generate(e.Job); } catch … return;` block (i.e. first statement of the success path, before the `FailedInsertCount` warning), insert:

```csharp
            // Fit the new drawing in the view (round 2, item 5). Runs BEFORE
            // the DXF save below so the saved state already includes the
            // zoom — otherwise the operator zooms out manually after saving
            // and gets nagged to save again on close. Signature verified
            // against APISDK samples (3D\SliceEntities): Zoom(Fit, null, null).
            try { Application.Zoom(dsZoomRange_e.dsZoomRange_Fit, null, null); }
            catch { /* cosmetic — never fail a successful generate over zoom */ }
```

`dsZoomRange_e` is in `DraftSight.Interop.dsAutomation`, already imported in this file.

- [ ] **Step 2: Build**

Run: `dotnet build CanvasCovers\CanvasCovers.csproj -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add CanvasCovers/Commands/OpenCanvasCoversCommand.cs
git commit -m "feat: zoom-to-fit after generate, before DXF save (round 2 item 5)"
```

---

### Task 6: Customer dropdown with initials autofill (TDD)

**Files:**
- Create: `CanvasCovers/Models/CustomerDirectory.cs`
- Create: `CanvasCovers/Resources/customers.csv`
- Create: `CanvasCovers.Tests/CustomerDirectoryTests.cs`
- Modify: `CanvasCovers.Tests/CanvasCovers.Tests.csproj` (link the new file)
- Modify: `CanvasCovers/UI/Controls/ProjectMetadataPanel.xaml:31-32` (TextBox → editable ComboBox)
- Modify: `CanvasCovers/UI/Controls/ProjectMetadataPanel.xaml.cs` (SetCustomers + autofill)
- Modify: `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml.cs` (load + wire on Loaded)

- [ ] **Step 1: Write the failing parser tests**

Create `CanvasCovers.Tests/CustomerDirectoryTests.cs`:

```csharp
using CanvasCovers.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class CustomerDirectoryTests
    {
        [TestMethod]
        public void Parse_Reads_Name_And_Initials()
        {
            var list = CustomerDirectory.Parse(new[] { "Kone Melbourne,KM", "LiftCorp,L" });
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("Kone Melbourne", list[0].Name);
            Assert.AreEqual("KM", list[0].Initials);
            Assert.AreEqual("LiftCorp", list[1].Name);
            Assert.AreEqual("L", list[1].Initials);
        }

        [TestMethod]
        public void Parse_Trims_Whitespace()
        {
            var list = CustomerDirectory.Parse(new[] { "  Kone Perth ,  KP  " });
            Assert.AreEqual("Kone Perth", list[0].Name);
            Assert.AreEqual("KP", list[0].Initials);
        }

        [TestMethod]
        public void Parse_Skips_Blanks_Comments_And_Malformed_Lines()
        {
            var list = CustomerDirectory.Parse(new[]
            {
                "", "   ", "# comment line", "NoCommaHere", ",InitialsOnly",
                "Schindler Sydney,SS",
            });
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("Schindler Sydney", list[0].Name);
        }

        [TestMethod]
        public void Parse_Null_Returns_Empty()
        {
            Assert.AreEqual(0, CustomerDirectory.Parse(null).Count);
        }

        [TestMethod]
        public void Parse_Keeps_Entry_With_Empty_Initials()
        {
            // A trailing comma means "no initials yet" — still list the name.
            var list = CustomerDirectory.Parse(new[] { "New Customer," });
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("", list[0].Initials);
        }
    }
}
```

- [ ] **Step 2: Link the (not yet created) source and run to verify failure**

In `CanvasCovers.Tests/CanvasCovers.Tests.csproj`, add to the linked-files ItemGroup:

```xml
    <Compile Include="..\CanvasCovers\Models\CustomerDirectory.cs" Link="Linked\CustomerDirectory.cs" />
```

Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: build FAILS — `CustomerDirectory.cs` does not exist yet.

- [ ] **Step 3: Implement CustomerDirectory**

Create `CanvasCovers/Models/CustomerDirectory.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;

namespace CanvasCovers.Models
{
    public class CustomerEntry
    {
        public CustomerEntry(string name, string initials)
        {
            Name = name;
            Initials = initials;
        }

        public string Name { get; }
        public string Initials { get; }
    }

    // Customer name -> AAC initials list feeding the Company Name drop-down
    // (round 2, item 6). Read from a per-user CSV the operator edits in
    // Notepad; seeded on first use from the read-only copy shipped in the
    // install dir's Resources folder. One "Name,Initials" pair per line;
    // blank lines and #-comments are ignored. WPF-free so the parser links
    // into the headless test project.
    public static class CustomerDirectory
    {
        public static string DefaultUserPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BesiaCAD", "CanvasCovers", "customers.csv");

        public static List<CustomerEntry> Parse(IEnumerable<string> lines)
        {
            var result = new List<CustomerEntry>();
            if (lines == null) return result;
            foreach (string raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string line = raw.Trim();
                if (line.StartsWith("#")) continue;
                int comma = line.IndexOf(',');
                if (comma <= 0) continue;   // no comma, or empty name part
                string name = line.Substring(0, comma).Trim();
                string initials = line.Substring(comma + 1).Trim();
                if (name.Length == 0) continue;
                result.Add(new CustomerEntry(name, initials));
            }
            return result;
        }

        // Reads the operator's editable copy, creating it from the shipped
        // seed on first use. Any IO failure returns an empty list — the
        // drop-down is convenience sugar and must never block the dialog
        // (no dispatcher exception handler in-host, CLAUDE.md §9).
        public static List<CustomerEntry> LoadOrSeed(string userPath, string seedPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(userPath) && !File.Exists(userPath)
                    && !string.IsNullOrEmpty(seedPath) && File.Exists(seedPath))
                {
                    string dir = Path.GetDirectoryName(userPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.Copy(seedPath, userPath);
                }
                if (!string.IsNullOrEmpty(userPath) && File.Exists(userPath))
                    return Parse(File.ReadAllLines(userPath));
            }
            catch { /* fall through */ }
            return new List<CustomerEntry>();
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Create the seed CSV (Martin's 29 entries)**

Create `CanvasCovers/Resources/customers.csv` (the existing `Resources\**\*` glob in the csproj copies it to output; the installer already ships `Resources\*` — no installer change needed):

```
# CanvasCovers customer list - one "Company Name,Initials" per line.
# Edit the copy at %AppData%\BesiaCAD\CanvasCovers\customers.csv
# (this installed copy is only the first-run seed).
Custom Elevators,CE
Elevator Direction,ED
Infinity Lifts,ILS
Kleeman Adelaide,KLA
Kleeman Melbourne,KLM
Kleeman Sydney,KLS
Kone Adelaide,KA
Kone Brisbane,KB
Kone Canberra,KCAN
Kone Darwin,KD
Kone Gold Coast,KGC
Kone Hobart,KH
Kone Melbourne,KM
Kone New Zealand,KNZ
Kone Newcastle,KN
Kone Perth,KP
Kone Sunshine Coast,KSC
Kone Sydney,KS
Kone Townsville,KT
LiftCorp,L
Octagon Lifts,OCT
Otis Perth,OP
Schindler Brisbane,SB
Schindler Perth,SP
Schindler Queensland,SQ
Schindler Sydney,SS
TK Elevators,TKE
Wild Industries,WI
Miscellaneous,MISC
```

- [ ] **Step 6: Swap the Company Name TextBox for an editable ComboBox**

In `CanvasCovers/UI/Controls/ProjectMetadataPanel.xaml`, replace

```xml
        <TextBox Grid.Row="0" Grid.Column="1" Name="CompanyNameInput" />
```

with

```xml
        <!-- Editable: the listed customers fill the dropdown (and auto-fill
             the initials), but a brand-new customer can still be typed
             free-hand (round 2, item 6). -->
        <ComboBox Grid.Row="0" Grid.Column="1" Name="CompanyNameInput"
                  IsEditable="True" Height="24" Margin="0,2,0,2"
                  VerticalContentAlignment="Center"
                  SelectionChanged="CompanyNameInput_SelectionChanged" />
```

- [ ] **Step 7: Wire the autofill in the panel code-behind**

In `CanvasCovers/UI/Controls/ProjectMetadataPanel.xaml.cs`, add `using System.Collections.Generic;` and these members (Read/Apply stay unchanged — an editable ComboBox round-trips through `.Text` exactly like a TextBox):

```csharp
        private List<CustomerEntry> _customers = new List<CustomerEntry>();

        // Fills the Company Name drop-down. Called by the host window once
        // the customer CSV has been read (round 2, item 6).
        public void SetCustomers(List<CustomerEntry> customers)
        {
            _customers = customers ?? new List<CustomerEntry>();
            CompanyNameInput.Items.Clear();
            foreach (CustomerEntry c in _customers)
            {
                CompanyNameInput.Items.Add(c.Name);
            }
        }

        private void CompanyNameInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CompanyInitialsInput == null) return;   // fires during XAML init
            string name = CompanyNameInput.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) return;     // free-typed text: leave initials alone
            CustomerEntry match = _customers.Find(c => c.Name == name);
            if (match != null)
            {
                CompanyInitialsInput.Text = match.Initials ?? string.Empty;
            }
        }
```

Also add `using System.Windows.Controls;` if not already present (it is — the class derives from UserControl in that namespace; just confirm).

- [ ] **Step 8: Load the directory in the window**

In `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml.cs`, add `using System.IO;` and `using System.Reflection;` to the usings, then append to the END of `LiftBlanketWindow_Loaded`:

```csharp
            // Customer drop-down (round 2, item 6). Best-effort: any IO
            // problem just leaves the combo empty-but-typable.
            try
            {
                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string seedPath = string.IsNullOrEmpty(assemblyDir)
                    ? null
                    : Path.Combine(assemblyDir, "Resources", "customers.csv");
                MetadataPanel.SetCustomers(
                    CanvasCovers.Models.CustomerDirectory.LoadOrSeed(
                        CanvasCovers.Models.CustomerDirectory.DefaultUserPath, seedPath));
            }
            catch { /* drop-down is optional — never block the dialog */ }
```

- [ ] **Step 9: Build + test**

Run: `dotnet build CanvasCovers\CanvasCovers.csproj -c Release`
Expected: Build succeeded. Then `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj` — PASS.

- [ ] **Step 10: Commit**

```bash
git add CanvasCovers/Models/CustomerDirectory.cs CanvasCovers/Resources/customers.csv CanvasCovers.Tests/CustomerDirectoryTests.cs CanvasCovers.Tests/CanvasCovers.Tests.csproj CanvasCovers/UI/Controls/ProjectMetadataPanel.xaml CanvasCovers/UI/Controls/ProjectMetadataPanel.xaml.cs CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml.cs
git commit -m "feat: customer dropdown with initials autofill from editable CSV (round 2 item 6)"
```

---

### Task 7: Job cache — save on Generate, Load Previous button (TDD)

**Files:**
- Create: `CanvasCovers/Models/Products/LiftBlanket/CachedWallInputs.cs`
- Create: `CanvasCovers/Models/Products/LiftBlanket/CachedJobInputs.cs`
- Create: `CanvasCovers/IO/JobCache.cs`
- Create: `CanvasCovers.Tests/JobCacheTests.cs`
- Modify: `CanvasCovers/CanvasCovers.csproj` (System.Web.Extensions reference)
- Modify: `CanvasCovers.Tests/CanvasCovers.Tests.csproj` (reference + links)
- Modify: `CanvasCovers/UI/Controls/WallBlanket.xaml.cs` (Read/ApplyCacheInputs)
- Modify: `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml` (Previous Job section)
- Modify: `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml.cs` (save + load + button state)

- [ ] **Step 1: Write the failing round-trip tests**

Create `CanvasCovers.Tests/JobCacheTests.cs`:

```csharp
using System;
using System.IO;
using CanvasCovers.IO;
using CanvasCovers.Models.Products.LiftBlanket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanvasCovers.Tests
{
    [TestClass]
    public class JobCacheTests
    {
        private string _path;

        [TestInitialize]
        public void Setup()
        {
            _path = Path.Combine(Path.GetTempPath(),
                "canvascovers-test-" + Guid.NewGuid().ToString("N"), "last-job.json");
        }

        [TestCleanup]
        public void Cleanup()
        {
            string dir = Path.GetDirectoryName(_path);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        [TestMethod]
        public void Save_Then_TryLoad_RoundTrips()
        {
            var data = new CachedJobInputs
            {
                SavedAt = "2026-06-10 14:30",
                ThroughCar = true,
                FixingsTag = "Eyelet9",
                FixingAllowanceText = "35",
                QuiltInsetText = "5",
                QuiltingSpacingText = "650",
                QuiltingEnabled = false,
                ExportDxf = false,
                Left = new CachedWallInputs
                {
                    IncludeWall = true,
                    IncludeCop = true,
                    Seg1 = "812.5",
                    MeasuredHeight = "2200",
                    CopHeight = "1000",
                    CopGapBottom = "150",
                    TotalWidth = "",
                },
                Rear = new CachedWallInputs { IncludeWall = false, Seg1 = "1400" },
            };

            JobCache.Save(data, _path);
            CachedJobInputs loaded = JobCache.TryLoad(_path);

            Assert.IsNotNull(loaded);
            Assert.AreEqual("2026-06-10 14:30", loaded.SavedAt);
            Assert.IsTrue(loaded.ThroughCar);
            Assert.AreEqual("Eyelet9", loaded.FixingsTag);
            Assert.AreEqual("35", loaded.FixingAllowanceText);
            Assert.IsFalse(loaded.QuiltingEnabled);
            Assert.IsFalse(loaded.ExportDxf);
            Assert.AreEqual("812.5", loaded.Left.Seg1);
            Assert.AreEqual("2200", loaded.Left.MeasuredHeight);
            Assert.IsTrue(loaded.Left.IncludeCop);
            Assert.IsFalse(loaded.Rear.IncludeWall);
            Assert.IsNull(loaded.Right);   // never set — stays null, applied as no-op
        }

        [TestMethod]
        public void TryLoad_Missing_File_Returns_Null()
        {
            Assert.IsNull(JobCache.TryLoad(_path));
        }

        [TestMethod]
        public void TryLoad_Corrupt_File_Returns_Null()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, "{not valid json!!");
            Assert.IsNull(JobCache.TryLoad(_path));
        }
    }
}
```

- [ ] **Step 2: Add links + references, run to verify failure**

In `CanvasCovers.Tests/CanvasCovers.Tests.csproj` add to the linked ItemGroup:

```xml
    <Compile Include="..\CanvasCovers\Models\Products\LiftBlanket\CachedWallInputs.cs" Link="Linked\CachedWallInputs.cs" />
    <Compile Include="..\CanvasCovers\Models\Products\LiftBlanket\CachedJobInputs.cs" Link="Linked\CachedJobInputs.cs" />
    <Compile Include="..\CanvasCovers\IO\JobCache.cs" Link="Linked\JobCache.cs" />
```

and a new ItemGroup (framework assembly, no NuGet):

```xml
  <ItemGroup>
    <Reference Include="System.Web.Extensions" />
  </ItemGroup>
```

Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: build FAILS — the three linked files don't exist yet.

- [ ] **Step 3: Create the cache DTOs**

Create `CanvasCovers/Models/Products/LiftBlanket/CachedWallInputs.cs`:

```csharp
namespace CanvasCovers.Models.Products.LiftBlanket
{
    // One wall's raw dialog state for the job cache (round 2, item 1).
    // Strings are the operator's literal typed text — NOT parsed numbers —
    // so a restore reproduces the form exactly, including blanks.
    // Public parameterless ctor + get/set properties: required by
    // JavaScriptSerializer round-tripping.
    public class CachedWallInputs
    {
        public bool IncludeWall { get; set; } = true;
        public bool IncludeCop { get; set; }
        public string DoorReturnLeft { get; set; }
        public string Seg1 { get; set; }
        public string Seg2 { get; set; }
        public string Seg3 { get; set; }
        public string DoorReturnRight { get; set; }
        public string TotalWidth { get; set; }
        public string MeasuredHeight { get; set; }
        public string CopHeight { get; set; }
        public string CopGapBottom { get; set; }
    }
}
```

Create `CanvasCovers/Models/Products/LiftBlanket/CachedJobInputs.cs`:

```csharp
namespace CanvasCovers.Models.Products.LiftBlanket
{
    // The dialog state captured after each successful Generate (round 2,
    // item 1): walls + options ONLY. Project information (order number,
    // network number, company, project name, date) is deliberately NOT
    // cached — repeat jobs reuse measurements, never job identity, so those
    // fields must start blank and be typed fresh per job.
    public class CachedJobInputs
    {
        // Display-only "when was this saved" stamp, local time. Kept as a
        // pre-formatted string: JavaScriptSerializer's DateTime handling is
        // timezone-hostile and nothing computes with this value.
        public string SavedAt { get; set; }

        public CachedWallInputs Left { get; set; }
        public CachedWallInputs Right { get; set; }
        public CachedWallInputs Rear { get; set; }

        public bool ThroughCar { get; set; }
        public bool PlasticCover { get; set; }
        public bool BagRequired { get; set; }
        public bool GlassBehind { get; set; }

        // The fixings ComboBoxItem Tag (e.g. "Eyelet9"); null/empty = none
        // selected. Restored by tag match; an unknown tag (from an older
        // version) simply leaves the combo unselected.
        public string FixingsTag { get; set; }

        public string FixingAllowanceText { get; set; }
        public string QuiltInsetText { get; set; }
        public string QuiltingSpacingText { get; set; }
        public bool QuiltingEnabled { get; set; } = true;
        public bool ExportDxf { get; set; } = true;
    }
}
```

- [ ] **Step 4: Create JobCache**

Create `CanvasCovers/IO/JobCache.cs`:

```csharp
using System;
using System.IO;
using System.Web.Script.Serialization;
using CanvasCovers.Models.Products.LiftBlanket;

namespace CanvasCovers.IO
{
    // Persists the last successfully generated job's dialog state to a
    // per-user JSON file (round 2, item 1 — Martin's 12-identical-blankets
    // workflow). JavaScriptSerializer (System.Web.Extensions) is the
    // zero-dependency net48 JSON option; the DTOs are flat strings/bools so
    // its limitations don't bite.
    public static class JobCache
    {
        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BesiaCAD", "CanvasCovers", "last-job.json");

        // Throws on IO problems — callers on the UI thread must wrap this
        // (no dispatcher exception handler in-host, CLAUDE.md §9).
        public static void Save(CachedJobInputs data, string path)
        {
            if (data == null || string.IsNullOrEmpty(path)) return;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(data));
        }

        // Null when there is no usable cache (missing, corrupt, unreadable).
        // Never throws: a broken cache file must read as "no previous job",
        // not crash the dialog.
        public static CachedJobInputs TryLoad(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                return new JavaScriptSerializer()
                    .Deserialize<CachedJobInputs>(File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: PASS.

- [ ] **Step 6: Add the framework reference to the add-in project**

In `CanvasCovers/CanvasCovers.csproj`, inside the existing interop `<ItemGroup>` (or its own), add:

```xml
    <Reference Include="System.Web.Extensions" />
```

- [ ] **Step 7: Add cache read/apply to WallBlanket**

In `CanvasCovers/UI/Controls/WallBlanket.xaml.cs`, add `using CanvasCovers.Models.Products.LiftBlanket;` and these two methods (place after `CopGapBottomText`):

```csharp
        // ---- job cache (round 2, item 1) ----

        public CachedWallInputs ReadCacheInputs()
        {
            return new CachedWallInputs
            {
                IncludeWall = IncludeWall.IsChecked == true,
                IncludeCop = IncludeCop.IsChecked == true,
                DoorReturnLeft = _drLeft.Text,
                Seg1 = _seg1.Text,
                Seg2 = _seg2.Text,
                Seg3 = _seg3.Text,
                DoorReturnRight = _drRight.Text,
                TotalWidth = _totalWidth.Text,
                MeasuredHeight = _measuredHeight.Text,
                CopHeight = _copHeight.Text,
                CopGapBottom = _copGapBottom.Text,
            };
        }

        // Restores cached raw text. Setting _measuredHeight.Text fires
        // Input_Changed un-suppressed, so the wall counts as manually
        // edited afterwards — correct: a restored height must not be
        // overwritten by left-wall mirroring.
        public void ApplyCacheInputs(CachedWallInputs c)
        {
            if (c == null) return;
            IncludeWall.IsChecked = c.IncludeWall;
            IncludeCop.IsChecked = c.IncludeCop;
            _drLeft.Text = c.DoorReturnLeft ?? string.Empty;
            _seg1.Text = c.Seg1 ?? string.Empty;
            _seg2.Text = c.Seg2 ?? string.Empty;
            _seg3.Text = c.Seg3 ?? string.Empty;
            _drRight.Text = c.DoorReturnRight ?? string.Empty;
            _totalWidth.Text = c.TotalWidth ?? string.Empty;
            _measuredHeight.Text = c.MeasuredHeight ?? string.Empty;
            _copHeight.Text = c.CopHeight ?? string.Empty;
            _copGapBottom.Text = c.CopGapBottom ?? string.Empty;
        }
```

- [ ] **Step 8: Add the Previous Job section to the window XAML**

In `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml`, insert as the FIRST child of the `<ScrollViewer><StackPanel>` (above PROJECT INFORMATION):

```xml
                <Border Style="{StaticResource SectionPanel}">
                    <StackPanel>
                        <TextBlock Style="{StaticResource SectionTitle}" Text="PREVIOUS JOB" />
                        <DockPanel>
                            <Button Name="LoadPreviousButton" DockPanel.Dock="Left"
                                    Width="230" Height="26"
                                    VerticalAlignment="Top"
                                    Content="Load Previous Job's Measurements"
                                    ToolTipService.ShowOnDisabled="True"
                                    Click="LoadPreviousButton_Click" />
                            <TextBlock Name="LoadPreviousNote"
                                       Style="{StaticResource MutedLabel}"
                                       Margin="12,4,0,0"
                                       Text="Brings back the walls and options from the last generated drawing. Order number, network number and project details stay blank." />
                        </DockPanel>
                    </StackPanel>
                </Border>
```

- [ ] **Step 9: Save on successful Generate + restore on click**

In `CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml.cs`:

(a) add usings: `using CanvasCovers.IO;` (Models.Products.LiftBlanket is already imported).

(b) in `GenerateButton_Click`, change the success tail to:

```csharp
            GenerateRequestedEventArgs args = new GenerateRequestedEventArgs(Job);
            GenerateRequested?.Invoke(this, args);
            if (!args.Cancel)
            {
                SaveJobCache();
                Close();
            }
```

(c) append to the END of `LiftBlanketWindow_Loaded` (after the customer-directory block from Task 6):

```csharp
            // Previous-job button state (round 2, item 1).
            try
            {
                CachedJobInputs cached = JobCache.TryLoad(JobCache.DefaultPath);
                if (cached == null)
                {
                    LoadPreviousButton.IsEnabled = false;
                    LoadPreviousButton.ToolTip =
                        "Becomes available after the first drawing is generated on this machine.";
                }
                else if (!string.IsNullOrEmpty(cached.SavedAt))
                {
                    LoadPreviousNote.Text =
                        "Brings back the walls and options last generated (" + cached.SavedAt
                        + "). Order number, network number and project details stay blank.";
                }
            }
            catch { LoadPreviousButton.IsEnabled = false; }
```

(d) add the new methods:

```csharp
        // Captures the raw dialog state after a successful Generate so the
        // next job can start from it (round 2, item 1). Best-effort: a cache
        // write failure must never block a generate that already succeeded.
        private void SaveJobCache()
        {
            try
            {
                var data = new CachedJobInputs
                {
                    SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    Left = LeftBlanket.ReadCacheInputs(),
                    Right = RightBlanket.ReadCacheInputs(),
                    Rear = RearBlanket.ReadCacheInputs(),
                    ThroughCar = ThroughCarOption.IsChecked == true,
                    PlasticCover = PlasticCoverOption.IsChecked == true,
                    BagRequired = BagRequiredOption.IsChecked == true,
                    GlassBehind = GlassBehindOption.IsChecked == true,
                    FixingsTag = (FixingsInput.SelectedItem as ComboBoxItem)?.Tag as string,
                    FixingAllowanceText = FixingAllowanceInput.Text,
                    QuiltInsetText = QuiltInsetInput.Text,
                    QuiltingSpacingText = QuiltingSpacingInput.Text,
                    QuiltingEnabled = QuiltingOption.IsChecked == true,
                    ExportDxf = ExportDxfOption.IsChecked == true,
                };
                JobCache.Save(data, JobCache.DefaultPath);
            }
            catch { /* never block a successful generate over the cache */ }
        }

        private void LoadPreviousButton_Click(object sender, RoutedEventArgs e)
        {
            CachedJobInputs data = JobCache.TryLoad(JobCache.DefaultPath);
            if (data == null)
            {
                ShowError("No previous job was found to load.");
                return;
            }
            ClearError();

            // Options BEFORE walls: Through Car drives the rear tab's enabled
            // state, which must be settled before the rear wall's cached
            // include/values land.
            ThroughCarOption.IsChecked = data.ThroughCar;
            PlasticCoverOption.IsChecked = data.PlasticCover;
            BagRequiredOption.IsChecked = data.BagRequired;
            GlassBehindOption.IsChecked = data.GlassBehind;
            SelectFixingsByTag(data.FixingsTag);
            // Allowance AFTER fixings: selecting a fixing re-seeds the
            // allowance box with that fixing's default, and the cached
            // (possibly overridden) value must win.
            FixingAllowanceInput.Text = data.FixingAllowanceText ?? string.Empty;
            QuiltInsetInput.Text = data.QuiltInsetText ?? string.Empty;
            QuiltingSpacingInput.Text = data.QuiltingSpacingText ?? string.Empty;
            QuiltingOption.IsChecked = data.QuiltingEnabled;
            ExportDxfOption.IsChecked = data.ExportDxf;

            LeftBlanket.ApplyCacheInputs(data.Left);
            RightBlanket.ApplyCacheInputs(data.Right);
            RearBlanket.ApplyCacheInputs(data.Rear);

            PushSharedParams();
            UpdateWidthWarning();
        }

        // Selects the fixings entry whose Tag matches, or clears the
        // selection when the tag is empty/unknown (e.g. cache written by a
        // version with different fixing names).
        private void SelectFixingsByTag(string tag)
        {
            if (!string.IsNullOrEmpty(tag))
            {
                foreach (object item in FixingsInput.Items)
                {
                    if (item is ComboBoxItem ci && (ci.Tag as string) == tag)
                    {
                        FixingsInput.SelectedItem = ci;
                        return;
                    }
                }
            }
            FixingsInput.SelectedIndex = -1;
        }
```

- [ ] **Step 10: Build + full test run**

Run: `dotnet build CanvasCovers\CanvasCovers.csproj -c Release`
Expected: Build succeeded, 0 errors.
Run: `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: PASS.

- [ ] **Step 11: Commit**

```bash
git add CanvasCovers/Models/Products/LiftBlanket/CachedWallInputs.cs CanvasCovers/Models/Products/LiftBlanket/CachedJobInputs.cs CanvasCovers/IO/JobCache.cs CanvasCovers.Tests/JobCacheTests.cs CanvasCovers.Tests/CanvasCovers.Tests.csproj CanvasCovers/CanvasCovers.csproj CanvasCovers/UI/Controls/WallBlanket.xaml.cs CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml CanvasCovers/UI/Products/LiftBlanket/LiftBlanketWindow.xaml.cs
git commit -m "feat: remember previous job, Load Previous button (round 2 item 1)"
```

---

### Task 8: Version bump + docs

**Files:**
- Modify: `CanvasCovers/Properties/AssemblyInfo.cs:14-15`
- Modify: `Installer/CanvasCovers.iss:13`
- Modify: `docs/help/CanvasCovers-Quick-Start.html`
- Modify: `docs/CHANGE_REQUESTS_ROUND2.md` (statuses)
- Modify: `docs/STATUS.md`

- [ ] **Step 1: Bump versions**

`AssemblyInfo.cs`:

```csharp
[assembly: AssemblyVersion("1.6.0.0")]
[assembly: AssemblyFileVersion("1.6.0.0")]
```

`Installer/CanvasCovers.iss`:

```
#define MyAppVersion       "1.6.0"
```

- [ ] **Step 2: Update the quick-start guide**

In `docs/help/CanvasCovers-Quick-Start.html`:

(a) In the section `<h2>3. Filling in the form</h2>`, add (adapting to the page's existing list/paragraph markup) coverage of the three operator-visible behaviour changes:

```html
<h3>Repeating a job</h3>
<p>After every drawing you generate, CanvasCovers remembers what you typed.
Next time the form opens, click <b>Load Previous Job's Measurements</b> (top
of the form) to bring back all the walls and options. Only the order number,
network number and project details stay blank &mdash; type those fresh, then
click Generate. Perfect for a batch of identical blankets.</p>

<h3>Choosing fixings</h3>
<p>The Fixings box now starts <b>empty on purpose</b> &mdash; the form will
not generate until you choose, so a forgotten fixing can't slip onto a
drawing. The two eyelet sizes (TG7 and TG9) are listed separately because the
chosen size prints on the COP.</p>

<h3>The customer drop-down</h3>
<p>The Company Name box is a drop-down of your regular customers; picking one
fills in the initials automatically. You can still type any new name by hand.
To add or change customers, edit this file in Notepad:</p>
<p><code>%AppData%\BesiaCAD\CanvasCovers\customers.csv</code></p>
<p>(Paste that into the File Explorer address bar.) One customer per line,
as <code>Company Name,INITIALS</code>. Save the file, then close and reopen
the CanvasCovers form &mdash; no restart needed.</p>
```

(b) Mention in section 2 ("Make a drawing") that the view now zooms to fit the finished drawing automatically.

- [ ] **Step 3: Regenerate the quick-start PDF (best-effort)**

```powershell
& "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --headless --disable-gpu --print-to-pdf="docs\help\CanvasCovers-Quick-Start.pdf" "file:///<absolute-repo-path>/docs/help/CanvasCovers-Quick-Start.html"
```

Compare page count/layout against the previous PDF. If Edge headless mangles the layout, leave the old PDF, note "PDF regeneration pending" in the commit body, and flag it to Sebastian.

- [ ] **Step 4: Update statuses**

- `docs/CHANGE_REQUESTS_ROUND2.md`: flip items 1, 3, 4, 5, 6 to **Implemented**.
- `docs/STATUS.md`: add a v1.6.0-dev entry summarising the five round-2 changes and that they await live testing in DraftSight.

- [ ] **Step 5: Commit**

```bash
git add CanvasCovers/Properties/AssemblyInfo.cs Installer/CanvasCovers.iss docs/help/CanvasCovers-Quick-Start.html docs/help/CanvasCovers-Quick-Start.pdf docs/CHANGE_REQUESTS_ROUND2.md docs/STATUS.md
git commit -m "chore: bump to 1.6.0, document round-2 features in quick start"
```

---

### Task 9: Final verification + installer build

- [ ] **Step 1: Clean build + full test suite**

Run: `dotnet build CanvasCovers\CanvasCovers.csproj -c Release` then `dotnet test CanvasCovers.Tests\CanvasCovers.Tests.csproj`
Expected: Build succeeded, all tests PASS.

- [ ] **Step 2: Build the installer (DraftSight must be closed)**

Run: `.\Installer\build.ps1`
Expected: `Installer\Output\BesiaCAD-CanvasCovers-Setup-1.6.0.exe` produced. If DraftSight is running the script refuses — close it first.

- [ ] **Step 3: Live-test checklist (manual, in DraftSight — for Sebastian/Martin)**

1. Install 1.6.0, open the form: section order is Previous Job → Project Info → Options → Walls → Layers.
2. Fixings is blank; Generate without choosing → error "Select the fixings required"; choose Eyelet TG9 → allowance seeds to 30; drawing prints "FIXINGS REQUIRED - EYELET TG9".
3. After Generate, the whole drawing (notes included) is visible without manual zoom; save via the DXF dialog; close the drawing — no second save prompt.
4. Reopen the form: "Load Previous Job's Measurements" is enabled and shows the save time; click it → walls/options return, project info blank; change order + network number; Generate → second blanket. (Martin's 12 kept blankets are the acceptance test.)
5. Company Name drop-down lists the 29 customers; picking "Kone Melbourne" fills "KM"; typing a brand-new name leaves initials alone. Edit `%AppData%\BesiaCAD\CanvasCovers\customers.csv`, add a line, reopen the form → new entry appears.

- [ ] **Step 4: Merge decision**

Use superpowers:finishing-a-development-branch — merge `round2-followups` to `main` after the live test passes, mirroring the beta-review flow.

---

## Self-review notes

- **Spec coverage:** item 1 → Task 7; item 3 → Tasks 2+3; item 4 → Task 4; item 5 → Task 5; item 6 → Task 6; Sebastian's "Layers last" → already last, Task 4 preserves it; quick-start documentation request → Task 8. No gaps.
- **Type consistency:** `CachedWallInputs`/`CachedJobInputs` property names match between DTO definitions (Task 7 Step 3), WallBlanket methods (Step 7), window save/load (Step 9), and tests (Step 1). `FixingType.Eyelet7/Eyelet9/None` consistent across Tasks 2, 3, and 7's tag strings.
- **Known accepted trade-offs:** layer settings are not cached (defaults are correct for the shop; cuts DTO surface); a restored mirrored height becomes "manually edited" (harmless); `JavaScriptSerializer` requires the `System.Web.Extensions` reference in both projects (framework assembly, no NuGet).
