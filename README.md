# 🎯 ESAPI LATTICE Radiotherapy Generator

## 📖 Overview
The **ESAPI LATTICE Generator** is an open-source automation tool designed for the Varian Eclipse Treatment Planning System (TPS). It automates the complex geometric creation of 3D spherical arrays (vertices) required for Spatially Fractionated Radiation Therapy (LATTICE). 

By leveraging ESAPI Boolean operations, this script eliminates manual contouring times, prevents geometric overlaps with Organs at Risk (OARs), and automatically calculates dynamic internal margins based on clinical dose-falloff constraints.

## ✨ Key Features
* **Automated Sphere Generation:** Instantly creates a 3D lattice of optimization spheres within a designated target volume (e.g., GTV).
* **Dynamic Skin & OAR Sparing:** Automatically subtracts user-defined OARs and external body margins to prevent vertices from overlapping with critical structures.
* **Parametric UI (WPF):** Intuitive user interface to define sphere radius, center-to-center spacing, and required dose-falloff margins.
* **Volume Ratio Control:** Calculates and limits the total LATTICE volume to a maximum of 10% of the target volume, trimming excess spheres automatically.

## 💻 System Requirements
* **Eclipse TPS:** Version 15.5 or higher.
* **.NET Framework:** Compatible with 4.5 or higher (Ensure your Visual Studio project target framework matches your clinic's ESAPI version).
* **Dependencies:** `EsapiEssentials` (via NuGet).

## 🛠️ Installation & Compilation (Important)
Because this project relies on external libraries and custom UI frameworks, **it cannot be run directly as a single `.cs` file in the Eclipse Script Runner.** It must be compiled into a `.dll` library.

1. Clone or download this repository to your local machine.
2. Open the solution file (`.sln`) using **Visual Studio**.
3. In the Solution Explorer, right-click the solution and select **Restore NuGet Packages** (This will download the required `EsapiEssentials` library).
4. Build the solution (`Ctrl + Shift + B` or `Build > Build Solution`).
5. Locate the compiled `.dll` file inside the `bin\Debug` or `bin\Release` folder.
6. In Eclipse, open the Script Runner, navigate to the folder containing your new `.dll`, and execute it.

## 🚀 How to Use
1. Open a Patient and a Structure Set in Eclipse.
2. Ensure you have a target structure contoured (e.g., `GTV`).
3. Run the compiled LATTICE Generator `.dll`.
4. In the UI window, select your Target Structure, OARs to avoid, and define your geometric parameters (Radius, Spacing).
5. Click **Generate** and review the created structures in the TPS.

## 📄 License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## ⚠️ Clinical Disclaimer
**For Research and Educational Purposes Only.** This software is provided "as is", without warranty of any kind. It is the sole responsibility of the clinical user (Medical Physicist or Dosimetrist) to strictly verify and validate all generated contours, geometries, and treatment plans before using them for clinical patient treatment. The developers assume no liability for clinical decisions made based on the output of this script.
