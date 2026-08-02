# Changelog

All notable changes to the ESAPI LATTICE Generator are documented here.
Versions correspond to the `AssemblyVersion`/`AssemblyFileVersion` embedded in
`LatticeGeneratorTool.cs`, shown by Eclipse in the "Modified Objects Found"
dialog after running the script — use it to confirm which build was used for
a given approval/QA record.

## [2.0.6.0] - 2026-08-02
### Added
- **Second grid tilt axis ("Grid Tilt vs. Sagittal")**: the existing axial
  tilt only rotates around the Left-Right axis (spreading vertices across
  axial slices); targets narrow in the Left-Right direction still collapsed
  onto a single sagittal plane. Adds an independent second rotation around
  the Superior-Inferior axis, composed with the first, so both axial and
  sagittal views get proper vertex spread. Both rotations are rigid
  (sphere-to-sphere spacing is unchanged); verified with an automated test
  confirming a narrow-in-X object gets more distinct X positions with the
  new tilt than without it.

## [2.0.5.0] - 2026-08-02
### Fixed
- Unselected rows in the OAR list, and combo-box dropdown items, still
  rendered with Eclipse's inherited dark style even after the container
  control's own style was reset — `ListBoxItem`/`ComboBoxItem` are generated
  per-row by WPF itself. Registers empty implicit styles for both, scoped to
  the generator window.

## [2.0.4.0] - 2026-08-02
### Fixed
- The OAR list's scrollbar still rendered with Eclipse's inherited dark
  style even after PR #4's field colors — it's generated internally by
  `ListBox`'s own template, not a control this script constructs directly.
  Registers an empty implicit `Style` for `ScrollBar` scoped to the
  generator window, which takes precedence over Eclipse's Application-level
  implicit style.

## [2.0.3.0] - 2026-08-02
### Fixed
- Setting `Background`/`Foreground` locally on each field wasn't enough to
  escape Eclipse's own inherited WPF theme (the generator window runs
  inside Eclipse's process/Application, which likely registers implicit
  styles for common control types). Sets an explicit, empty local `Style`
  on every field/checkbox/list/combo/button so each falls back to its
  normal default template, on top of which the local colors render
  correctly. Field backgrounds switched to a light blue tint.

## [2.0.2.0] - 2026-08-01
### Fixed
- The "Generate" button parsed all numeric fields (`double.Parse`) before
  entering the method's `try/catch`, so an empty or malformed field (e.g. a
  comma instead of a decimal point) threw an unhandled exception instead of
  a controlled message. Parsing is now wrapped in its own `try/catch` with a
  clear "Entrada Inválida" message.

## [2.0.1.0] - 2026-07-31
### Added
- **Hot + Cold checkerboard engine**: rewrote the vertex-placement logic to
  generate both high-dose "hot" and low-dose "cold" spheres in an alternating
  3D checkerboard/parity pattern, matching the full clinical LATTICE
  technique instead of hot-only vertices.
- **Automatic phase/parity selection**: evaluates 16 grid phase/parity
  configurations and picks the one whose analytical hot-spot dose-volume
  ratio best fits the clinical 2–4% reference band.
- **Halton-sequence overlap sampling**: replaces the single center-point
  overlap check with a deterministic 513-point sampler (plus a cheap 6-point
  extremal pre-check) for more reliable results on concave/irregular GTVs.
- **Pre-generation confirmation dialog**: shows hot/cold counts, occupied
  axial planes, omission counts, and the dose-volume ratio (with an
  out-of-band warning) before any structure is written.
- **Eclipse 99-structure limit handling**: automatically falls back to
  combined `LRT_Hot`/`LRT_Cold` structures if individual per-sphere output
  would exceed the Structure Set limit.
- **Cold Envelope Expansion** field controlling how far cold spheres may
  extend beyond the GTV surface.
- **Distinct structure colors**: hot spheres, cold spheres, and the
  persistent `LRT_HotRegion` QA structure now render in magenta/blue/amber
  respectively, instead of all defaulting to the same magenta.
- **High-contrast clinical UI theme**: replaced inherited system-theme
  styling (which rendered as low-contrast dark-on-dark) with an explicit
  light color scheme across all fields, labels, and checkboxes.
- Moved the pure grid/sampling/phase-selection math into a new,
  ESAPI-independent `LatticeGeometryMath.cs` file.
### Changed
- Output structure naming: `LRT_Vertices`/`zV_*` → `LRT_Hot`/`LRT_Cold` and
  `zH_*`/`zC_*` (legacy names from prior versions are still cleaned up
  automatically on re-run).

## [1.0.0.1] - 2026-03-21 to 2026-07-24
Baseline version number covering the original hot-only vertex generator and
its first round of incremental improvements (not individually version-tagged
at the time):
- Initial release: automated hot-sphere generation within a contracted GTV
  volume, with dose-gradient-based margin calculation and OAR avoidance.
- 10%-of-target Volume Ratio cap as a clinical safety backstop.
- Individual (`zV_01`, `zV_02`, ...) vs. grouped (`LRT_Vertices`) structure
  output toggle.
- **Manual Boost Mode (SRS/SBRT)**: fixed sphere-to-border distance instead
  of the dose-gradient margin, for small lesions.
- **Tilted vertex grid**: adjustable grid tilt vs. the axial plane to avoid
  vertices clustering on a single CT slice for thin/flat lesions.
