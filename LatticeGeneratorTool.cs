using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]

// TODO: Uncomment the following line if the script requires write access.
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
            // TODO : Add here the code that is called when the script is launched from Eclipse.
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
                Height = 600,
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
                Height = 100,
                Margin = new Thickness(0, 0, 0, 5)
            };
            mainPanel.Children.Add(lstOARs);

            StackPanel pnlOARMargin = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            pnlOARMargin.Children.Add(new TextBlock { Text = "OAR Safety Margin (cm): ", VerticalAlignment = VerticalAlignment.Center });
            TextBox txtOARMargin = new TextBox { Text = "0.5", Width = 50 };
            pnlOARMargin.Children.Add(txtOARMargin);
            mainPanel.Children.Add(pnlOARMargin);

            // -- Sección D: Botón Ejecutar --
            Button btnGenerate = new Button
            {
                Content = "Generate LATTICE Geometry",
                Height = 40,
                FontWeight = FontWeights.Bold,
                Background = System.Windows.Media.Brushes.LightBlue
            };

            // Evento al hacer clic
            btnGenerate.Click += (sender, e) =>
            {
                // Leer valores de la UI
                Structure selectedGTV = cmbGTV.SelectedItem as Structure;
                List<Structure> selectedOARs = lstOARs.SelectedItems.Cast<Structure>().ToList();

                double diameter = double.Parse(txtDiameter.Text);
                double separation = double.Parse(txtSeparation.Text);
                double peakDose = double.Parse(txtPeakDose.Text);
                double periDose = double.Parse(txtPeriDose.Text);
                double gradient = double.Parse(txtGradient.Text);
                double oarMargin = double.Parse(txtOARMargin.Text);

                // Cerrar ventana e iniciar el motor
                mainWindow.DialogResult = true;
                mainWindow.Close();

                // Llamada al motor geométrico (Fase 2)
                GenerateLatticeGeometry(context, selectedGTV, selectedOARs, diameter, separation, peakDose, periDose, gradient, oarMargin);
            };

            mainPanel.Children.Add(btnGenerate);
            mainWindow.Content = mainPanel;
            mainWindow.ShowDialog();
        }

        // =========================================================================
        // FASE 2: MOTOR GEOMÉTRICO LATTICE Y BOOLEANOS
        // =========================================================================
        private void GenerateLatticeGeometry(ScriptContext context, Structure gtv, List<Structure> oars, double diameterCm, double separationCm, double peakDose, double periDose, double gradient, double oarMarginCm)
        {
            try
            {
                // Habilitar la escritura de datos en el paciente
                context.Patient.BeginModifications();
                StructureSet ss = context.StructureSet;

                // 1. Conversión de unidades (cm a mm)
                double radiusMm = (diameterCm / 2.0) * 10.0;
                double separationMm = separationCm * 10.0;
                double oarMarginMm = oarMarginCm * 10.0;

                // Cálculo matemático de la caída de dosis
                double doseDrop = peakDose - periDose;
                double dropRatePerMm = peakDose * (gradient / 100.0);
                double gradientMarginMm = doseDrop / dropRatePerMm;

                double totalContractionMarginMm = gradientMarginMm + radiusMm;

                // 2. Limpieza previa: Borrar estructuras si ya existían de un intento anterior
                RemoveStructureIfExists(ss, "LRT_Volume");
                RemoveStructureIfExists(ss, "LRT_Vertices");

                // Crear nuevas estructuras (Tipo CONTROL)
                Structure vL = ss.AddStructure("CONTROL", "LRT_Volume");
                Structure verticesStruct = ss.AddStructure("CONTROL", "LRT_Vertices");

                // 3. Creación del LATTICE Volume (VL)
                vL.SegmentVolume = gtv.SegmentVolume.Margin(-totalContractionMarginMm);

                if (vL.IsEmpty)
                {
                    MessageBox.Show($"El GTV es demasiado pequeño para acomodar el margen de seguridad de {totalContractionMarginMm:F1} mm. No se puede generar geometría.", "Límite Clínico Alcanzado", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 4. Evasión de Órganos de Riesgo (OAR Boolean Logic)
                foreach (var oar in oars)
                {
                    // Expandimos el OAR por el margen de seguridad e inmediatamente lo restamos del VL
                    var oarExpanded = oar.SegmentVolume.Margin(oarMarginMm);
                    vL.SegmentVolume = vL.SegmentVolume.Sub(oarExpanded);
                }

                if (vL.IsEmpty)
                {
                    MessageBox.Show("El LATTICE Volume se quedó sin espacio útil tras restar los OARs con su margen de seguridad.", "Geometría Vacía", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 5. Generación de la Cuadrícula Espacial (Grid)
                VVector com = vL.CenterPoint;
                var bounds = vL.MeshGeometry.Bounds;

                List<VVector> gridPoints = new List<VVector>();

                // Alineamos la cuadrícula al Centro de Masa (COM) y barremos la caja delimitadora
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

                            // Si el punto cae DENTRO de la zona segura (VL), es un vértice válido
                            if (vL.IsPointInsideSegment(pt))
                            {
                                gridPoints.Add(pt);
                            }
                        }
                    }
                }

                if (gridPoints.Count == 0)
                {
                    MessageBox.Show("No caben vértices dentro del volumen LATTICE disponible. Intenta reducir la separación o el diámetro.", "Sin Vértices", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Ordenar los puntos desde el centro hacia afuera (para poder borrar los periféricos si superamos el 10%)
                gridPoints = gridPoints.OrderBy(p => Math.Pow(p.x - com.x, 2) + Math.Pow(p.y - com.y, 2) + Math.Pow(p.z - com.z, 2)).ToList();

                // 6. Validación Estricta del Volume Ratio (1% - 10%)
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

                // 7. Dibujar Físicamente las Esferas en Eclipse
                foreach (var pt in finalPoints)
                {
                    DrawSphere(verticesStruct, pt, radiusMm, ss.Image);
                }

                // Cálculo Final para el Resumen
                double finalRatio = (finalSphereCount * sphereVolCc / gtvVolCc) * 100.0;

                string msg = $"Geometría LATTICE generada con éxito.\n\n" +
                             $"- Margen interno de seguridad aplicado: {totalContractionMarginMm:F1} mm\n" +
                             $"- Vértices creados: {finalSphereCount}\n" +
                             $"- Volume Ratio final: {finalRatio:F2}%\n";

                if (wasTrimmed) msg += $"\n(Nota: Se recortaron vértices exteriores a un máximo de {maxAllowedSpheres} para respetar la restricción clínica de Volume Ratio <= 10%).";

                MessageBox.Show(msg, "LATTICE Generado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocurrió un error inesperado durante el cálculo: {ex.Message}\n{ex.StackTrace}", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
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
            int minSlice = (int)Math.Floor((center.z - radiusMm - image.Origin.z) / zRes);
            int maxSlice = (int)Math.Ceiling((center.z + radiusMm - image.Origin.z) / zRes);

            for (int s = minSlice; s <= maxSlice; s++)
            {
                double z = image.Origin.z + s * zRes;
                // Calculamos el radio de la esfera en este corte Z específico
                double rZ = Math.Sqrt(Math.Max(0, radiusMm * radiusMm - Math.Pow(z - center.z, 2)));

                if (rZ > 0.5) // Solo se dibuja si el radio en este corte es mayor a medio milímetro
                {
                    VVector[] contour = GenerateCircle(new VVector(center.x, center.y, z), rZ);
                    structure.AddContourOnImagePlane(contour, s);
                }
            }
        }

        private VVector[] GenerateCircle(VVector center, double radius, int segments = 36)
        {
            // Crea un polígono de 36 puntos para asimilar un círculo perfecto
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
