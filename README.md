# ESAPI LATTICE Generator Tool (LRT)

## Description
[cite_start]This ESAPI script automates the creation of 3D geometric structures required for LATTICE Radiation Therapy (LRT)[cite: 31]. [cite_start]Based on the clinical implementation guidelines for bulky tumors [cite: 15, 16][cite_start], the script generates a robust grid of high-dose spherical vertices within a defined Gross Tumor Volume (GTV), ensuring strict adherence to dosimetric fall-off constraints and Organ at Risk (OAR) avoidance[cite: 140].

## Clinical Rationale & Mathematical Logic
[cite_start]To safely deliver ablative doses to partial volumes of advanced large tumors [cite: 44][cite_start], the script calculates a safe inner boundary called the **LATTICE Volume ($V_L$)**[cite: 116, 133]. 

The margin to contract the GTV and create the $V_L$ is calculated dynamically using the user's inputs:
`Margin (mm) = (Peak Dose - Peripheral Dose) / (Peak Dose * (Gradient / 100))` + `Vertex Radius`

[cite_start]This ensures that the steep dose gradient from the vertex strictly respects the maximum allowable dose at the GTV periphery (typically < 3 Gy)[cite: 127].

## Features
* **Dynamic Boundary Calculation:** Automatically computes the required clearance from the GTV edge based on the desired dose fall-off (%/mm).
* [cite_start]**OAR Avoidance Boolean Logic:** Users can select multiple OARs (e.g., neural structures, large vessels, bones)[cite: 140]. The script will subtract these structures (plus a user-defined safety margin) from the viable LATTICE Volume space.
* [cite_start]**Volume Ratio Auto-Correction:** The script ensures that the total volume of the generated vertices remains strictly between **1.0% and 10.0%** of the GTV[cite: 119]. If the initial grid exceeds 10%, outer vertices are progressively trimmed.
* **Clean Structure Set:** All generated spheres are merged into a single `HighDose_Vertices` structure to prevent exceeding the Eclipse structure limit.

## Prerequisites
* Varian Eclipse TPS (ESAPI v15.5 or higher).
* An approved Structure Set.
* [cite_start]A target structure defined as DICOM Type `GTV` with a volume $\ge$ 50 cc[cite: 119].

## How to Use
1. Open a patient and navigate to the **External Beam Planning** workspace.
2. Run the `LatticeGenerator.cs` script.
3. In the UI window:
   * **Target Selection:** Choose your GTV (Only GTVs $\ge$ 50 cc will be available).
   * [cite_start]**Geometric & Dosimetric Parameters:** * *Vertex Diameter (cm)*: Typically 0.5 - 1.5 cm[cite: 119].
     * [cite_start]*Vertices Separation (cm)*: Center-to-center distance (typically 2.0 - 5.0 cm)[cite: 119].
     * *Peak Dose & Peripheral Dose Limit*: Used to calculate the safety margin.
     * *Gradient Fall-off (%/mm)*: The expected dose drop-off of your linac.
   * **Avoidance Structures:** Select any OARs crossing the GTV and define a safety margin.
4. Click **Generate LATTICE Geometry**.
5. The script will generate two structures: `LATTICE_Volume` and `HighDose_Vertices`.

## Disclaimer
This script generates optimization structures only; it does not optimize or calculate the dose. It is intended for research and educational purposes. Always review the generated geometry clinically before proceeding with inverse planning.
