# 🎯 ESAPI LATTICE Radiotherapy Generator

## 📖 Overview
The **ESAPI LATTICE Generator** is an open-source automation tool designed for the Varian Eclipse Treatment Planning System (TPS). It automates the complex geometric creation of the 3D hot-spot/cold-spot checkerboard pattern required for Spatially Fractionated Radiation Therapy (LATTICE).

By leveraging ESAPI Boolean operations, this script eliminates manual contouring times, prevents geometric overlaps with Organs at Risk (OARs), and automatically calculates dynamic internal margins based on clinical dose-falloff constraints. An automatic phase/parity search and a Halton-sequence overlap sampler pick and validate the grid placement, and a confirmation dialog lets you review the result before anything is written to the Structure Set.

## ✨ Key Features
* **Hot + Cold Checkerboard Generation:** Places both high-dose "hot" spheres and low-dose "cold" spheres in an alternating 3D checkerboard (parity) pattern — the full LATTICE technique, not just isolated peaks.
* **Automatic Phase/Parity Selection:** Tries 16 grid phase/parity configurations and picks the one whose analytical hot-spot dose-volume ratio best fits the clinical reference band (2–4%).
* **Halton-Sequence Overlap Sampling:** Validates each candidate sphere against the real (possibly concave) target/OAR geometry using a deterministic 513-point low-discrepancy sample, instead of a single center-point check — far more reliable for irregular GTVs.
* **Dynamic Skin & OAR Sparing:** Automatically subtracts user-defined OARs (expanded by sphere radius + safety margin) from the hot region so no hot sphere can overlap a critical structure; cold spheres are deliberately allowed to approach OARs.
* **Cold Spheres Cropped to the GTV:** Cold candidates are accepted with as little as ~50% overlap with the GTV (so their placement can hug the target border), but the drawn geometry is intersected against the GTV surface so no cold structure visually extends past the target contour.
* **Confirmation Dialog:** Before any structure is written, a summary dialog shows hot/cold counts, occupied axial planes, omission counts, and the final dose-volume ratio (with a warning if it falls outside the 2–4% reference band) — you choose whether to proceed.
* **Eclipse 99-Structure Limit Handling:** Detects when individual per-sphere output would exceed Eclipse's structure-set limit and automatically falls back to combined `LRT_Hot`/`LRT_Cold` structures, noting this in the summary.
* **Individual or Grouped Output:** Generate combined `LRT_Hot` / `LRT_Cold` structures, or check "Generate individual structures" to create separate `zH_01`, `zC_01`, ... structures per sphere for manual repositioning.
* **Manual Boost Mode (SRS/SBRT):** Optionally bypass the dose-gradient margin calculation for hot spheres and set a fixed distance from the GTV border instead — useful for small lesions where the automatic dosimetric method leaves no room for vertices.
* **Tilted Vertex Grid (2 axes):** Two independent grid tilt angles — vs. axial (keeps vertices from clustering on a single CT slice for thin/flattened lesions) and vs. sagittal (keeps vertices from clustering on a single side-view plane for lesions narrow in the Left-Right direction). Both are rigid rotations (they don't change sphere-to-sphere spacing) and can be tuned independently per case (try 10–30°).
* **Volume Ratio Control:** Calculates and limits the total hot-spot volume to a maximum of 10% of the target volume, trimming excess spheres automatically as a clinical safety backstop.

## 💻 System Requirements
* **Eclipse TPS:** Version 15.5 or higher.
* **.NET Framework:** 4.8 (matches the project's `TargetFrameworkVersion`; adjust if your clinic's ESAPI version requires a different one).
* **Dependencies:** No NuGet packages. The project references `VMS.TPS.Common.Model.API.dll` and `VMS.TPS.Common.Model.Types.dll` directly from your local Eclipse/ESAPI install (by default `C:\Program Files\Varian\RTM\18.0\esapi\API\`).

## 🛠️ Installation & Compilation (Important)
Because this project relies on external libraries and custom UI frameworks, **it cannot be run directly as a single `.cs` file in the Eclipse Script Runner.** It must be compiled into a `.dll` library.

1. Clone or download this repository to your local machine.
2. Open the solution file (`.sln`) using **Visual Studio**.
3. In Solution Explorer, check the `VMS.TPS.Common.Model.API` and `VMS.TPS.Common.Model.Types` references — if your ESAPI install lives at a different path/version than `C:\Program Files\Varian\RTM\18.0\esapi\API\`, update each reference's **Path** property (or edit the `HintPath` in `LatticeGeneratorTool.csproj`) to point to your local ESAPI API DLLs.
4. Build the solution (`Ctrl + Shift + B` or `Build > Build Solution`).
5. Locate the compiled `.dll` file inside the `bin\Debug` or `bin\Release` folder.
6. In Eclipse, open the Script Runner, navigate to the folder containing your new `.dll`, and execute it.

## 🚀 How to Use
1. Open a Patient and a Structure Set in Eclipse.
2. Ensure you have a target structure contoured (e.g., `GTV`).
3. Run the compiled LATTICE Generator `.dll`.
4. In the UI window, select your Target Structure, OARs to avoid, and define your geometric parameters (Diameter, Separation, Grid Tilt vs. Axial, Grid Tilt vs. Sagittal, Cold Envelope Expansion).
5. Choose your dose-margin mode for hot spheres: use the dose-falloff fields (Peak Dose, Peripheral Dose, Gradient) for standard LATTICE, or check **Manual Boost Mode (SRS/SBRT)** to set a fixed distance from the GTV border instead.
6. Check **Generate individual structures** if you need each sphere as its own structure (`zH_01`, `zC_01`, ...) for manual adjustment; leave it unchecked to get combined `LRT_Hot` / `LRT_Cold` structures.
7. Click **Generate**. The script computes the best grid phase/parity automatically and shows a **confirmation dialog** with hot/cold counts, occupied planes, omissions, and the dose-volume ratio.
8. Review the summary and click **Yes** to write the structures, or **No** to cancel without changing the Structure Set. A persistent `LRT_HotRegion` structure is kept for visual QA of the allowed hot-sphere placement region.

## 📄 License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## ⚠️ Clinical Disclaimer
**For Research and Educational Purposes Only.** This software is provided "as is", without warranty of any kind. It is the sole responsibility of the clinical user (Medical Physicist or Dosimetrist) to strictly verify and validate all generated contours, geometries, and treatment plans before using them for clinical patient treatment. The developers assume no liability for clinical decisions made based on the output of this script.
