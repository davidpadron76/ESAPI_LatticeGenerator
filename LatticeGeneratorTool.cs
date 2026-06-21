using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]
[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        public Script()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context /*, System.Windows.Window window, ScriptEnvironment environment*/)
        {
            // 1. Validaciones iniciales
            StructureSet ss = context.StructureSet;
            if (ss == null)
            {
                MessageBox.Show("Por favor, abre un plan o un Structure Set antes de ejecutar el script.", "Error LATTICE", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 2. Filtrar GTVs viables (Volumen >= 50 cc) y OARs
            var validGTVs = ss.Structures.Where(s => s.DicomType == "GTV" && s.Volume >= 50.0).ToList();
            var allStructures = ss.Structures.Where(s => !s.IsEmpty && s.DicomType != "EXTERNAL").ToList();

            if (!validGTVs.Any())
            {
                MessageBox.Show("No se encontró ningún GTV con un volumen mayor o igual a 50 cc en este Structure Set.", "Restricción Clínica", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3. Construir la Interfaz Gráfica (WPF Programático)
            Window mainWindow = new Window
            {
                Title = "LATTICE Generator Tool (LRT)",
                Width = 400,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            StackPanel mainPanel = new StackPanel { Margin = new Thickness(15) };

            // -- Sección A: Selección de Target --
            mainPanel.Children.Add(new TextBlock { Text = "1. Target Selection (GTV >= 50cc):", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
            ComboBox cmbGTV = new ComboBox { DisplayMemberPath = "Id", ItemsSource = validGTVs, SelectedIndex = 0, Margin = new Thickness(0, 0, 0, 15) };
            mainPanel.Children.Add(cmbGTV);

            // -- Sección B: Parámetros --
            mainPanel.Children.Add(new TextBlock { Text = "2. Geometric & Dosimetric Parameters:", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });

            var pnlParams = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };

            pnlParams.Children.Add(new TextBlock { Text = "Vertex Diameter (cm):", VerticalAlignment = VerticalAlignment.Center });
            TextBox txtDiameter = new TextBox { Text = "1.0", Margin = new Thickness(5) };
            pnlParams.Children.Add(txtDiameter);

            pnlParams.Children.Add(new TextBlock { Text = "Vertices Separation (cm):", VerticalAlignment = VerticalAlignment.Center });
            TextBox txtSeparation = new TextBox { Text = "3.0", Margin = new Thickness(5) };
            pnlParams.Children.Add(txtSeparation);

            pnlParams.Children.Add(new TextBlock { Text = "Peak Dose (Gy):", VerticalAlignment = VerticalAlignment.Center });
            TextBox txtPeakDose = new TextBox { Text = "20.0", Margin = new Thickness(5) };
            pnlParams.Children.Add(txtPeakDose);

            pnlParams.Children.Add(new TextBlock { Text = "Peripheral Dose Limit (Gy):", VerticalAlignment = VerticalAlignment.Center });
            TextBox txtPeriDose = new TextBox { Text = "3.0", Margin = new Thickness(5) };
            pnlParams.Children.Add(txtPeriDose);

            pnlParams.Children.Add(new TextBlock { Text = "Gradient Fall-off (%/mm):", VerticalAlignment = VerticalAlignment.Center });
            TextBox txtGradient = new TextBox { Text = "10.0", Margin = new Thickness(5) };
            pnlParams.Children.Add(txtGradient);

            mainPanel.Children.Add(pnlParams);

            // -- Sección C: OARs a evitar --
            mainPanel.Children.Add(new TextBlock { Text = "3. Avoidance Structures (OARs):", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 15, 0, 5) });
            ListBox lstOARs = new ListBox
            {
                SelectionMode = SelectionMode.Multiple,
                DisplayMemberPath = "Id",
                ItemsSource = allStructures,
                Height = 80,
                Margin = new Thickness(0, 0, 0, 5)
            };
            mainPanel.Children.Add(lstOARs);

            StackPanel pnlOARMargin = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            pnlOARMargin.Children.Add(new TextBlock { Text = "OAR Safety Margin (cm): ", VerticalAlignment = VerticalAlignment.Center });
            TextBox txtOARMargin = new TextBox { Text = "0.5", Width = 50 };
            pnlOARMargin.Children.Add(txtOARMargin);
            mainPanel.Children.Add(pnlOARMargin);

            // -- NUEVA SECCIÓN: Opción de Estructuras Individuales --
            CheckBox cbIndividual = new CheckBox 
            { 
                Content = "Generate individual structures (allows manual moving)",
                Margin = new Thickness(0, 5, 0, 15),
                ToolTip = "If checked, creates zV_01, zV_02, etc. instead of a single LRT_Vertices structure."
            };
            mainPanel.Children.Add(cbIndividual);

            // -- Sección D: Botón Ejecutar --
            Button btnGenerate = new Button
            {
                Content = "Generate LATTICE Geometry",
                Height = 40,
                FontWeight = FontWeights.Bold,
                Background = System.Windows.Media.Brushes.LightBlue
            };

            btnGenerate.Click += (sender, e) =>
            {
                Structure selectedGTV = cmbGTV.SelectedItem as Structure;
                List<Structure> selectedOARs = lstOARs.SelectedItems.Cast<Structure>().ToList();

                double diameter = double.Parse(txtDiameter.Text, CultureInfo.InvariantCulture);
                double separation = double.Parse(txtSeparation.Text, CultureInfo.InvariantCulture);
                double peakDose = double.Parse(txtPeakDose.Text, CultureInfo.InvariantCulture);
                double periDose = double.Parse(txtPeriDose.Text, CultureInfo.InvariantCulture);
                double gradient = double.Parse(txtGradient.Text, CultureInfo.InvariantCulture);
                double oarMargin = double.Parse(txtOARMargin.Text, CultureInfo.InvariantCulture);
                bool makeIndividual = cbIndividual.IsChecked == true;

                mainWindow.DialogResult = true;
                mainWindow.Close();

                GenerateLatticeGeometry(context, selectedGTV, selectedOARs, diameter, separation, peakDose, periDose, gradient, oarMargin, makeIndividual);
            };

            mainPanel.Children.Add(btnGenerate);
            mainWindow.Content = mainPanel;
            mainWindow.ShowDialog();
        }

        // =========================================================================
        // FASE 2: MOTOR GEOMÉTRICO LATTICE Y BOOLEANOS
        // =========================================================================
        private void GenerateLatticeGeometry(ScriptContext context, Structure gtv, List<Structure> oars, double diameterCm, double separationCm, double peakDose, double periDose, double gradient, double oarMarginCm, bool makeIndividual)
        {
            try
            {
                context.Patient.BeginModifications();
                StructureSet ss = context.StructureSet;

                double radiusMm = (diameterCm / 2.0) * 10.0;
                double separationMm = separationCm * 10.0;
                double oarMarginMm = oarMarginCm * 10.0;

                double doseDrop = peakDose - periDose;
                double dropRatePerMm = peakDose * (gradient / 100.0);
                double gradientMarginMm = doseDrop / dropRatePerMm;

                double totalContractionMarginMm = gradientMarginMm + radiusMm;

                // Limpieza previa (Borra tanto el global como los individuales previos)
                RemoveStructureIfExists(ss, "LRT_Volume");
                RemoveStructureIfExists(ss, "LRT_Vertices");
                var oldIndividuals = ss.Structures.Where(s => s.Id.StartsWith("zV_")).ToList();
                foreach (var ind in oldIndividuals) ss.RemoveStructure(ind);

                Structure vL = ss.AddStructure("CONTROL", "LRT_Volume");
                Structure verticesStruct = null;
                
                if (!makeIndividual)
                {
                    verticesStruct = ss.AddStructure("CONTROL", "LRT_Vertices");
                }

                vL.SegmentVolume = gtv.SegmentVolume.Margin(-totalContractionMarginMm);

                if (vL.IsEmpty)
                {
                    MessageBox.Show($"El GTV es demasiado pequeño para acomodar el margen de seguridad de {totalContractionMarginMm:F1} mm.", "Límite Clínico Alcanzado", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                foreach (var oar in oars)
                {
                    var oarExpanded = oar.SegmentVolume.Margin(oarMarginMm);
                    vL.SegmentVolume = vL.SegmentVolume.Sub(oarExpanded);
                }

                if (vL.IsEmpty)
                {
                    MessageBox.Show("El LATTICE Volume se quedó sin espacio útil tras restar los OARs.", "Geometría Vacía", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                VVector com = vL.CenterPoint;
                var bounds = vL.MeshGeometry.Bounds;

                List<VVector> gridPoints = new List<VVector>();

                double xMin = com.x - Math.Ceiling((com.x - bounds.X) / separationMm) * separationMm;
                double xMax = bounds.X + bounds.SizeX;
                double yMin = com.y - Math.Ceiling((com.y - bounds.Y) / separationMm) * separationMm;
                double yMax = bounds.Y + bounds.SizeY;
                double zMin = com.z - Math.Ceiling((com.z - bounds.Z) / separationMm) * separationMm;
                double zMax = bounds.Z + bounds.SizeZ;

                for (double x = xMin; x <= xMax; x += separationMm)
                {
                    for (double y = yMin; y <= yMax; y += separationMm)
                    {
                        for (double z = zMin; z <= zMax; z += separationMm)
                        {
                            VVector pt = new VVector(x, y, z);
                            if (vL.IsPointInsideSegment(pt))
                            {
                                gridPoints.Add(pt);
                            }
                        }
                    }
                }

                if (gridPoints.Count == 0)
                {
                    MessageBox.Show("No caben vértices dentro del volumen LATTICE disponible.", "Sin Vértices", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                gridPoints = gridPoints.OrderBy(p => Math.Pow(p.x - com.x, 2) + Math.Pow(p.y - com.y, 2) + Math.Pow(p.z - com.z, 2)).ToList();

                double sphereVolCc = (4.0 / 3.0) * Math.PI * Math.Pow(radiusMm / 10.0, 3);
                double gtvVolCc = gtv.Volume;
                double maxRatio = 0.10;
                int maxAllowedSpheres = (int)Math.Floor((gtvVolCc * maxRatio) / sphereVolCc);

                int finalSphereCount = gridPoints.Count;
                bool wasTrimmed = false;

                if (finalSphereCount > maxAllowedSpheres)
                {
                    finalSphereCount = maxAllowedSpheres;
                    wasTrimmed = true;
                }

                var finalPoints = gridPoints.Take(finalSphereCount).ToList();

                // Dibujar Físicamente las Esferas en Eclipse
                int counter = 1;
                foreach (var pt in finalPoints)
                {
                    if (makeIndividual)
                    {
                        // Crear estructura individual (zV_01, zV_02...)
                        string vName = $"zV_{counter:00}";
                        Structure indStr = ss.AddStructure("CONTROL", vName);
                        DrawSphere(indStr, pt, radiusMm, ss.Image);
                        counter++;
                    }
                    else
                    {
                        // Dibujar sobre la estructura global
                        DrawSphere(verticesStruct, pt, radiusMm, ss.Image);
                    }
                }

                double finalRatio = (finalSphereCount * sphereVolCc / gtvVolCc) * 100.0;

                string msg = $"Geometría LATTICE generada con éxito.\n\n" +
                             $"- Margen interno de seguridad aplicado: {totalContractionMarginMm:F1} mm\n" +
                             $"- Vértices creados: {finalSphereCount}\n" +
                             $"- Volume Ratio final: {finalRatio:F2}%\n" +
                             $"- Modo: {(makeIndividual ? "Estructuras Individuales" : "Estructura Única")}\n";

                if (wasTrimmed) msg += $"\n(Nota: Se recortaron vértices exteriores a un máximo de {maxAllowedSpheres} para respetar el Volume Ratio <= 10%).";

                MessageBox.Show(msg, "LATTICE Generado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocurrió un error inesperado: {ex.Message}\n{ex.StackTrace}", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =========================================================================
        // MÉTODOS AUXILIARES (Helpers)
        // =========================================================================
        private void RemoveStructureIfExists(StructureSet ss, string id)
        {
            var target = ss.Structures.FirstOrDefault(s => s.Id == id);
            if (target != null)
            {
                ss.RemoveStructure(target);
            }
        }

        private void DrawSphere(Structure structure, VVector center, double radiusMm, VMS.TPS.Common.Model.API.Image image)
        {
            double zRes = image.ZRes;
            int minSlice = Math.Max(0, (int)Math.Floor((center.z - radiusMm - image.Origin.z) / zRes));
            int maxSlice = Math.Min(image.ZSize - 1, (int)Math.Ceiling((center.z + radiusMm - image.Origin.z) / zRes));

            for (int s = minSlice; s <= maxSlice; s++)
            {
                double z = image.Origin.z + s * zRes;
                double rZ = Math.Sqrt(Math.Max(0, radiusMm * radiusMm - Math.Pow(z - center.z, 2)));

                if (rZ > 0.5)
                {
                    VVector[] contour = GenerateCircle(new VVector(center.x, center.y, z), rZ);
                    structure.AddContourOnImagePlane(contour, s);
                }
            }
        }

        private VVector[] GenerateCircle(VVector center, double radius, int segments = 36)
        {
            VVector[] pts = new VVector[segments];
            for (int i = 0; i < segments; i++)
            {
                double angle = i * 2.0 * Math.PI / segments;
                pts[i] = new VVector(center.x + radius * Math.Cos(angle), center.y + radius * Math.Sin(angle), center.z);
            }
            return pts;
        }
    }
}