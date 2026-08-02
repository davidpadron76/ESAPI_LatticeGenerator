using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Globalization;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMS.TPS.LatticeMath;

[assembly: AssemblyVersion("2.0.5.0")]
[assembly: AssemblyFileVersion("2.0.5.0")]
[assembly: AssemblyInformationalVersion("2.0.5")]
[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        private const int EclipseStructureLimit = 99;
        private const int StructureLimitSafetyBuffer = 2;
        private const int HaltonSampleCount = 513;
        private const double RatioBandMinPercent = 2.0;
        private const double RatioBandMaxPercent = 4.0;

        // Colores de estructuras: hot y cold deben ser visualmente distinguibles
        // en Eclipse (por defecto, todas las estructuras "CONTROL" salen magenta).
        private static readonly Color HotStructureColor = Color.FromRgb(236, 0, 140);   // magenta: alta dosis
        private static readonly Color ColdStructureColor = Color.FromRgb(0, 112, 192);  // azul: baja dosis
        private static readonly Color HotRegionColor = Color.FromRgb(255, 193, 7);      // ámbar: región QA, no es una esfera

        // Tema claro de alto contraste para la ventana WPF (no depender del tema
        // heredado del sistema/Eclipse, que puede volver ilegibles los controles).
        private static readonly Brush PanelBackgroundBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0xF8, 0xFB));
        private static readonly Brush HeaderTextBrush = new SolidColorBrush(Color.FromRgb(0x1B, 0x3A, 0x57));
        private static readonly Brush LabelTextBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x2E));
        private static readonly Brush FieldBackgroundBrush = new SolidColorBrush(Color.FromRgb(0xE3, 0xF1, 0xFC)); // azul claro tenue
        private static readonly Brush FieldBorderBrush = new SolidColorBrush(Color.FromRgb(0x9F, 0xB8, 0xC9));
        private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x86, 0xC1));

        // Eclipse puede tener sus propios estilos WPF implícitos (TargetType, sin
        // x:Key) a nivel de aplicación, que se aplican automáticamente a CUALQUIER
        // control sin un Style explícito propio -- incluidos los que crea este
        // script, ya que la ventana corre dentro del mismo proceso/Application de
        // Eclipse. Un Style local vacío (sin Setters) hace que WPF ignore ese estilo
        // heredado y use la plantilla por defecto del control, sobre la cual sí se
        // aplican Background/Foreground/BorderBrush seteados localmente.
        private static readonly Style PlainTextBoxStyle = new Style(typeof(TextBox));
        private static readonly Style PlainComboBoxStyle = new Style(typeof(ComboBox));
        private static readonly Style PlainListBoxStyle = new Style(typeof(ListBox));
        private static readonly Style PlainCheckBoxStyle = new Style(typeof(CheckBox));
        private static readonly Style PlainButtonStyle = new Style(typeof(Button));

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
                Width = 420,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = PanelBackgroundBrush
            };

            // Los controles internos que el propio WPF genera por cada fila/elemento
            // (la barra de desplazamiento del ListBox, cada ListBoxItem/ComboBoxItem)
            // no son creados por este script, así que no se les puede asignar un
            // Style local directamente. Se registran aquí estilos implícitos vacíos
            // con alcance a esta ventana, que tienen prioridad sobre cualquier estilo
            // implícito heredado de Eclipse a nivel de Application.
            mainWindow.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), new Style(typeof(System.Windows.Controls.Primitives.ScrollBar)));
            mainWindow.Resources.Add(typeof(ListBoxItem), new Style(typeof(ListBoxItem)));
            mainWindow.Resources.Add(typeof(ComboBoxItem), new Style(typeof(ComboBoxItem)));

            StackPanel mainPanel = new StackPanel { Margin = new Thickness(15), Background = PanelBackgroundBrush };

            // -- Sección A: Selección de Target --
            mainPanel.Children.Add(new TextBlock { Text = "1. Target Selection (GTV >= 50cc):", FontWeight = FontWeights.Bold, Foreground = HeaderTextBrush, Margin = new Thickness(0, 0, 0, 5) });
            ComboBox cmbGTV = new ComboBox { Style = PlainComboBoxStyle, DisplayMemberPath = "Id", ItemsSource = validGTVs, SelectedIndex = 0, Margin = new Thickness(0, 0, 0, 15), Background = FieldBackgroundBrush, Foreground = LabelTextBrush };
            mainPanel.Children.Add(cmbGTV);

            // -- Sección B: Parámetros --
            mainPanel.Children.Add(new TextBlock { Text = "2. Geometric & Dosimetric Parameters:", FontWeight = FontWeights.Bold, Foreground = HeaderTextBrush, Margin = new Thickness(0, 0, 0, 5) });

            var pnlParams = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };

            pnlParams.Children.Add(new TextBlock { Text = "Vertex Diameter (cm):", Foreground = LabelTextBrush, VerticalAlignment = VerticalAlignment.Center });
            TextBox txtDiameter = new TextBox { Style = PlainTextBoxStyle, Text = "1.0", Margin = new Thickness(5), Background = FieldBackgroundBrush, Foreground = LabelTextBrush, BorderBrush = FieldBorderBrush };
            pnlParams.Children.Add(txtDiameter);

            pnlParams.Children.Add(new TextBlock { Text = "Vertices Separation (cm):", Foreground = LabelTextBrush, VerticalAlignment = VerticalAlignment.Center });
            TextBox txtSeparation = new TextBox { Style = PlainTextBoxStyle, Text = "3.0", Margin = new Thickness(5), Background = FieldBackgroundBrush, Foreground = LabelTextBrush, BorderBrush = FieldBorderBrush };
            pnlParams.Children.Add(txtSeparation);

            pnlParams.Children.Add(new TextBlock { Text = "Grid Tilt vs. Axial (°):", Foreground = LabelTextBrush, VerticalAlignment = VerticalAlignment.Center });
            TextBox txtTiltDeg = new TextBox
            {
                Style = PlainTextBoxStyle,
                Text = "15.0",
                Margin = new Thickness(5),
                Background = FieldBackgroundBrush,
                Foreground = LabelTextBrush,
                BorderBrush = FieldBorderBrush,
                ToolTip = "Inclina la malla de vértices respecto al plano axial (eje Izquierda-Derecha) para que no queden todos en el mismo corte. Prueba valores entre 10 y 30°."
            };
            pnlParams.Children.Add(txtTiltDeg);

            pnlParams.Children.Add(new TextBlock { Text = "Peak Dose (Gy):", Foreground = LabelTextBrush, VerticalAlignment = VerticalAlignment.Center });
            TextBox txtPeakDose = new TextBox { Style = PlainTextBoxStyle, Text = "20.0", Margin = new Thickness(5), Background = FieldBackgroundBrush, Foreground = LabelTextBrush, BorderBrush = FieldBorderBrush };
            pnlParams.Children.Add(txtPeakDose);

            pnlParams.Children.Add(new TextBlock { Text = "Peripheral Dose Limit (Gy):", Foreground = LabelTextBrush, VerticalAlignment = VerticalAlignment.Center });
            TextBox txtPeriDose = new TextBox { Style = PlainTextBoxStyle, Text = "3.0", Margin = new Thickness(5), Background = FieldBackgroundBrush, Foreground = LabelTextBrush, BorderBrush = FieldBorderBrush };
            pnlParams.Children.Add(txtPeriDose);

            pnlParams.Children.Add(new TextBlock { Text = "Gradient Fall-off (%/mm):", Foreground = LabelTextBrush, VerticalAlignment = VerticalAlignment.Center });
            TextBox txtGradient = new TextBox { Style = PlainTextBoxStyle, Text = "10.0", Margin = new Thickness(5), Background = FieldBackgroundBrush, Foreground = LabelTextBrush, BorderBrush = FieldBorderBrush };
            pnlParams.Children.Add(txtGradient);

            pnlParams.Children.Add(new TextBlock { Text = "Boost: Distancia al borde GTV (cm):", Foreground = LabelTextBrush, VerticalAlignment = VerticalAlignment.Center });
            TextBox txtManualDist = new TextBox { Style = PlainTextBoxStyle, Text = "0.2", Margin = new Thickness(5), IsEnabled = false, Background = FieldBackgroundBrush, Foreground = LabelTextBrush, BorderBrush = FieldBorderBrush };
            pnlParams.Children.Add(txtManualDist);

            pnlParams.Children.Add(new TextBlock { Text = "Cold Envelope Expansion (cm):", Foreground = LabelTextBrush, VerticalAlignment = VerticalAlignment.Center });
            TextBox txtColdEnvelope = new TextBox
            {
                Style = PlainTextBoxStyle,
                Text = "0.5",
                Margin = new Thickness(5),
                Background = FieldBackgroundBrush,
                Foreground = LabelTextBrush,
                BorderBrush = FieldBorderBrush,
                ToolTip = "Distancia hacia afuera del borde del GTV hasta donde pueden extenderse las esferas 'cold' (valles de baja dosis)."
            };
            pnlParams.Children.Add(txtColdEnvelope);

            mainPanel.Children.Add(pnlParams);

            // -- NUEVA SECCIÓN: Modo Boost Manual (SRS/SBRT) --
            // Reemplaza el cálculo de margen por gradiente de dosis por una distancia fija
            // definida manualmente. Pensado para lesiones pequeñas (SRS/SBRT) donde el
            // método dosimétrico automático no deja espacio útil para generar vértices.
            CheckBox cbManualBoost = new CheckBox
            {
                Style = PlainCheckBoxStyle,
                Content = "Modo Boost Manual (SRS/SBRT): usar distancia fija al borde en vez de gradiente de dosis",
                Margin = new Thickness(0, 5, 0, 10),
                Foreground = LabelTextBrush,
                ToolTip = "Ideal para lesiones pequeñas donde el cálculo dosimétrico automático no deja espacio para vértices. Define directamente qué tan lejos del borde del GTV se ubica la superficie de la esfera hot."
            };
            cbManualBoost.Checked += (sender, e) =>
            {
                txtManualDist.IsEnabled = true;
                txtPeakDose.IsEnabled = false;
                txtPeriDose.IsEnabled = false;
                txtGradient.IsEnabled = false;
            };
            cbManualBoost.Unchecked += (sender, e) =>
            {
                txtManualDist.IsEnabled = false;
                txtPeakDose.IsEnabled = true;
                txtPeriDose.IsEnabled = true;
                txtGradient.IsEnabled = true;
            };
            mainPanel.Children.Add(cbManualBoost);

            // -- Sección C: OARs a evitar --
            mainPanel.Children.Add(new TextBlock { Text = "3. Avoidance Structures (OARs):", FontWeight = FontWeights.Bold, Foreground = HeaderTextBrush, Margin = new Thickness(0, 15, 0, 5) });
            ListBox lstOARs = new ListBox
            {
                Style = PlainListBoxStyle,
                SelectionMode = SelectionMode.Multiple,
                DisplayMemberPath = "Id",
                ItemsSource = allStructures,
                Height = 80,
                Margin = new Thickness(0, 0, 0, 5),
                Background = FieldBackgroundBrush,
                Foreground = LabelTextBrush,
                BorderBrush = FieldBorderBrush
            };
            mainPanel.Children.Add(lstOARs);

            StackPanel pnlOARMargin = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            pnlOARMargin.Children.Add(new TextBlock { Text = "OAR Safety Margin (cm) — Hot region only: ", Foreground = LabelTextBrush, VerticalAlignment = VerticalAlignment.Center });
            TextBox txtOARMargin = new TextBox { Style = PlainTextBoxStyle, Text = "0.5", Width = 50, Background = FieldBackgroundBrush, Foreground = LabelTextBrush, BorderBrush = FieldBorderBrush };
            pnlOARMargin.Children.Add(txtOARMargin);
            mainPanel.Children.Add(pnlOARMargin);

            // -- NUEVA SECCIÓN: Opción de Estructuras Individuales --
            CheckBox cbIndividual = new CheckBox
            {
                Style = PlainCheckBoxStyle,
                Content = "Generate individual structures (allows manual moving)",
                Margin = new Thickness(0, 5, 0, 15),
                Foreground = LabelTextBrush,
                ToolTip = "If checked, creates zH_01/zC_01, etc. instead of single LRT_Hot / LRT_Cold structures."
            };
            mainPanel.Children.Add(cbIndividual);

            // -- Sección D: Botón Ejecutar --
            Button btnGenerate = new Button
            {
                Style = PlainButtonStyle,
                Content = "Generate LATTICE Geometry",
                Height = 40,
                FontWeight = FontWeights.Bold,
                Background = AccentBrush,
                Foreground = Brushes.White
            };

            btnGenerate.Click += (sender, e) =>
            {
                Structure selectedGTV = cmbGTV.SelectedItem as Structure;
                List<Structure> selectedOARs = lstOARs.SelectedItems.Cast<Structure>().ToList();

                double diameter, separation, tiltDeg, peakDose, periDose, gradient, oarMargin, coldEnvelopeCm, manualDistCm;
                bool makeIndividual, manualBoost;

                try
                {
                    diameter = double.Parse(txtDiameter.Text, CultureInfo.InvariantCulture);
                    separation = double.Parse(txtSeparation.Text, CultureInfo.InvariantCulture);
                    tiltDeg = double.Parse(txtTiltDeg.Text, CultureInfo.InvariantCulture);
                    peakDose = double.Parse(txtPeakDose.Text, CultureInfo.InvariantCulture);
                    periDose = double.Parse(txtPeriDose.Text, CultureInfo.InvariantCulture);
                    gradient = double.Parse(txtGradient.Text, CultureInfo.InvariantCulture);
                    oarMargin = double.Parse(txtOARMargin.Text, CultureInfo.InvariantCulture);
                    coldEnvelopeCm = double.Parse(txtColdEnvelope.Text, CultureInfo.InvariantCulture);
                    makeIndividual = cbIndividual.IsChecked == true;
                    manualBoost = cbManualBoost.IsChecked == true;
                    manualDistCm = manualBoost ? double.Parse(txtManualDist.Text, CultureInfo.InvariantCulture) : 0.0;
                }
                catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                {
                    MessageBox.Show("Uno o más campos numéricos están vacíos o contienen un valor inválido. Revisa que todos usen punto decimal (ej. 1.0) y vuelve a intentar.", "Entrada Inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                mainWindow.DialogResult = true;
                mainWindow.Close();

                GenerateLatticeGeometry(context, selectedGTV, selectedOARs, diameter, separation, tiltDeg, peakDose, periDose, gradient, oarMargin, coldEnvelopeCm, makeIndividual, manualBoost, manualDistCm);
            };

            mainPanel.Children.Add(btnGenerate);
            mainWindow.Content = mainPanel;
            mainWindow.ShowDialog();
        }

        // =========================================================================
        // FASE 2: MOTOR GEOMÉTRICO LATTICE (HOT + COLD CHECKERBOARD)
        // =========================================================================
        private void GenerateLatticeGeometry(ScriptContext context, Structure gtv, List<Structure> oars,
            double diameterCm, double separationCm, double tiltDeg,
            double peakDose, double periDose, double gradient,
            double oarMarginCm, double coldEnvelopeCm,
            bool makeIndividual, bool manualBoost, double manualDistCm)
        {
            StructureSet ss = context.StructureSet;

            try
            {
                try
                {
                    context.Patient.BeginModifications();

                    double radiusMm = (diameterCm / 2.0) * 10.0;
                    double separationMm = separationCm * 10.0;
                    double oarMarginMm = oarMarginCm * 10.0;
                    double coldEnvelopeMm = coldEnvelopeCm * 10.0;
                    double tiltRad = tiltDeg * Math.PI / 180.0;

                    double hotBorderClearanceMm = HotBorderClearanceCalculator.ComputeMm(peakDose, periDose, gradient, manualBoost, manualDistCm);

                    CleanupPriorOutputs(ss);

                    Structure hotRegion = BuildHotRegion(ss, gtv, oars, hotBorderClearanceMm, radiusMm, oarMarginMm);
                    if (hotRegion == null)
                    {
                        return;
                    }

                    Structure coldEnvelope = BuildColdEnvelope(ss, gtv, coldEnvelopeMm);

                    Vec3 com;
                    int n;
                    ComputeGridExtent(hotRegion, coldEnvelope, separationMm, out com, out n);

                    double gtvVolCc = gtv.Volume;
                    double sphereVolCc = (4.0 / 3.0) * Math.PI * Math.Pow(radiusMm / 10.0, 3);

                    List<ConfigScore> scores;
                    List<GridBuildResult> builds;
                    EvaluateAllConfigurations(hotRegion, coldEnvelope, com, separationMm, n, tiltRad, sphereVolCc, gtvVolCc, out scores, out builds);

                    int bestIdx = PhaseSelector.SelectBestConfigurationIndex(scores, RatioBandMinPercent, RatioBandMaxPercent);
                    GridBuildResult winner = builds[bestIdx];

                    var unitSamples = HaltonSequence.GenerateUnitSphereSamples(HaltonSampleCount);

                    int haltonRejectedHot, haltonRejectedCold;
                    List<GridCandidate> haltonHot = ApplyHaltonFiltering(winner.AcceptedHot, hotRegion, radiusMm, unitSamples, out haltonRejectedHot);
                    List<GridCandidate> haltonCold = ApplyHaltonFiltering(winner.AcceptedCold, gtv, radiusMm, unitSamples, out haltonRejectedCold);

                    bool wasTrimmed;
                    int maxAllowedHot;
                    List<GridCandidate> finalHot = ApplyHotVolumeCap(haltonHot, com, sphereVolCc, gtvVolCc, out wasTrimmed, out maxAllowedHot);
                    List<GridCandidate> finalCold = haltonCold;

                    if (finalHot.Count == 0 && finalCold.Count == 0)
                    {
                        MessageBox.Show("No fue posible colocar ninguna esfera hot ni cold dentro de la geometría disponible.", "Sin Vértices", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    int omittedClearanceHot = scores[bestIdx].HotConsidered - winner.AcceptedHot.Count;
                    int omittedClearanceCold = scores[bestIdx].ColdConsidered - winner.AcceptedCold.Count;
                    double finalRatioPercent = (finalHot.Count * sphereVolCc / gtvVolCc) * 100.0;
                    int occupiedPlanes = ComputeOccupiedPlaneCount(finalHot, finalCold, radiusMm, ss.Image);

                    string confirmMsg = BuildConfirmationMessage(gtv.Id, finalHot.Count, finalCold.Count, occupiedPlanes,
                        omittedClearanceHot, omittedClearanceCold, haltonRejectedHot, haltonRejectedCold, finalRatioPercent);

                    MessageBoxResult confirmResult = MessageBox.Show(confirmMsg, "Confirmar Generación LATTICE", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (confirmResult != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    // El cold envelope es solo un andamio temporal: nunca se persiste.
                    ss.RemoveStructure(coldEnvelope);

                    bool actuallyIndividual;
                    string fallbackNote;
                    bool canProceed = DecideOutputMode(ss, makeIndividual, finalHot.Count, finalCold.Count, out actuallyIndividual, out fallbackNote);
                    if (!canProceed)
                    {
                        return;
                    }

                    // La región hot se conserva como estructura de control persistente para QA visual.
                    hotRegion.Id = "LRT_HotRegion";

                    WriteOutputs(ss, ss.Image, finalHot, finalCold, radiusMm, actuallyIndividual);

                    double effectiveHotMarginMm = hotBorderClearanceMm + radiusMm;
                    string marginModeMsg = manualBoost
                        ? $"Boost Manual (distancia al borde: {manualDistCm:F2} cm)"
                        : $"Gradiente de Dosis ({gradient:F1} %/mm)";

                    string msg = BuildFinalSummaryMessage(marginModeMsg, effectiveHotMarginMm, tiltDeg, finalHot.Count, finalCold.Count,
                        occupiedPlanes, finalRatioPercent, actuallyIndividual, wasTrimmed, maxAllowedHot, fallbackNote);

                    MessageBox.Show(msg, "LATTICE Generado", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                finally
                {
                    // Cualquier estructura de andamiaje que no haya sido renombrada/eliminada
                    // (éxito parcial, cancelación del usuario, o excepción) se limpia aquí.
                    RemoveStructuresByPrefix(ss, "zTMP_");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocurrió un error inesperado: {ex.Message}\n{ex.StackTrace}", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Structure BuildHotRegion(StructureSet ss, Structure gtv, List<Structure> oars, double hotBorderClearanceMm, double radiusMm, double oarMarginMm)
        {
            Structure hotRegion = ss.AddStructure("CONTROL", "zTMP_HotRegion");
            hotRegion.Color = HotRegionColor;

            // Erosión secuencial: primero el margen clínico de borde, luego el radio
            // de la esfera (para que la esfera completa quepa dentro de la región).
            hotRegion.SegmentVolume = gtv.SegmentVolume.Margin(-hotBorderClearanceMm).Margin(-radiusMm);

            if (hotRegion.IsEmpty)
            {
                MessageBox.Show($"El GTV es demasiado pequeño para acomodar el margen hot de {hotBorderClearanceMm + radiusMm:F1} mm.", "Límite Clínico Alcanzado", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            foreach (var oar in oars)
            {
                var oarExpanded = oar.SegmentVolume.Margin(radiusMm + oarMarginMm);
                hotRegion.SegmentVolume = hotRegion.SegmentVolume.Sub(oarExpanded);
            }

            if (hotRegion.IsEmpty)
            {
                MessageBox.Show("La región hot se quedó sin espacio útil tras restar los OARs.", "Geometría Vacía", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            return hotRegion;
        }

        private Structure BuildColdEnvelope(StructureSet ss, Structure gtv, double coldEnvelopeMm)
        {
            Structure coldEnvelope = ss.AddStructure("CONTROL", "zTMP_ColdEnvelope");
            coldEnvelope.SegmentVolume = gtv.SegmentVolume.Margin(coldEnvelopeMm);
            return coldEnvelope;
        }

        private void ComputeGridExtent(Structure hotRegion, Structure coldEnvelope, double separationMm, out Vec3 com, out int n)
        {
            com = ToVec3(hotRegion.CenterPoint);

            var bounds = coldEnvelope.MeshGeometry.Bounds;
            double halfDiagonalMm = Math.Sqrt(
                Math.Pow(bounds.SizeX / 2.0, 2) +
                Math.Pow(bounds.SizeY / 2.0, 2) +
                Math.Pow(bounds.SizeZ / 2.0, 2));
            n = (int)Math.Ceiling(halfDiagonalMm / separationMm) + 1;
        }

        private void EvaluateAllConfigurations(Structure hotRegion, Structure coldEnvelope, Vec3 com, double separationMm, int n, double tiltRad,
            double sphereVolCc, double gtvVolCc, out List<ConfigScore> scores, out List<GridBuildResult> builds)
        {
            scores = new List<ConfigScore>();
            builds = new List<GridBuildResult>();

            Func<Vec3, bool> isInsideHot = p => hotRegion.IsPointInsideSegment(ToVVector(p));
            Func<Vec3, bool> isInsideCold = p => coldEnvelope.IsPointInsideSegment(ToVVector(p));
            Func<Vec3, bool> bboxFilter = BuildBoundingBoxFilter(coldEnvelope);

            foreach (var phase in PhaseSelector.EnumerateConfigurations(separationMm))
            {
                GridBuildResult build = GridBuilder.BuildCandidateGrid(com, separationMm, n, tiltRad, phase, isInsideHot, isInsideCold, bboxFilter);
                double ratio = (build.AcceptedHot.Count * sphereVolCc / gtvVolCc) * 100.0;
                scores.Add(new ConfigScore(build.HotConsidered, build.AcceptedHot.Count, build.ColdConsidered, build.AcceptedCold.Count, ratio));
                builds.Add(build);
            }
        }

        private Func<Vec3, bool> BuildBoundingBoxFilter(Structure region)
        {
            var bounds = region.MeshGeometry.Bounds;
            double x0 = bounds.X, x1 = bounds.X + bounds.SizeX;
            double y0 = bounds.Y, y1 = bounds.Y + bounds.SizeY;
            double z0 = bounds.Z, z1 = bounds.Z + bounds.SizeZ;
            return p => p.X >= x0 && p.X <= x1 && p.Y >= y0 && p.Y <= y1 && p.Z >= z0 && p.Z <= z1;
        }

        private List<GridCandidate> ApplyHaltonFiltering(List<GridCandidate> candidates, Structure region, double radiusMm, IReadOnlyList<Vec3> unitSamples, out int rejectedCount)
        {
            var accepted = new List<GridCandidate>();
            rejectedCount = 0;
            Func<Vec3, bool> isInside = p => region.IsPointInsideSegment(ToVVector(p));

            foreach (var candidate in candidates)
            {
                bool? preCheck = SphereOverlapSampler.ExtremalPreCheck(candidate.Position, radiusMm, isInside);
                bool passes;

                if (preCheck.HasValue)
                {
                    passes = preCheck.Value;
                }
                else
                {
                    int insideCount = SphereOverlapSampler.CountInside(candidate.Position, radiusMm, unitSamples, isInside);
                    passes = SphereOverlapSampler.PassesThreshold(insideCount, unitSamples.Count);
                }

                if (passes)
                {
                    accepted.Add(candidate);
                }
                else
                {
                    rejectedCount++;
                }
            }

            return accepted;
        }

        private List<GridCandidate> ApplyHotVolumeCap(List<GridCandidate> hotCandidates, Vec3 com, double sphereVolCc, double gtvVolCc, out bool wasTrimmed, out int maxAllowed)
        {
            const double maxRatio = 0.10;
            maxAllowed = (int)Math.Floor((gtvVolCc * maxRatio) / sphereVolCc);
            wasTrimmed = false;

            if (hotCandidates.Count <= maxAllowed)
            {
                return hotCandidates;
            }

            wasTrimmed = true;
            return hotCandidates
                .OrderBy(c => (c.Position - com).LengthSquared)
                .Take(Math.Max(maxAllowed, 0))
                .ToList();
        }

        private int ComputeOccupiedPlaneCount(List<GridCandidate> hot, List<GridCandidate> cold, double radiusMm, VMS.TPS.Common.Model.API.Image image)
        {
            var planes = new HashSet<int>();
            foreach (var c in hot.Concat(cold))
            {
                int minSlice, maxSlice;
                GetSliceRange(ToVVector(c.Position), radiusMm, image, out minSlice, out maxSlice);
                for (int s = minSlice; s <= maxSlice; s++)
                {
                    planes.Add(s);
                }
            }
            return planes.Count;
        }

        private string BuildConfirmationMessage(string gtvId, int hotCount, int coldCount, int occupiedPlanes,
            int omittedClearanceHot, int omittedClearanceCold, int omittedHaltonHot, int omittedHaltonCold, double ratioPercent)
        {
            string msg = $"Target: {gtvId}\n\n" +
                         $"- Esferas Hot: {hotCount}\n" +
                         $"- Esferas Cold: {coldCount}\n" +
                         $"- Cortes axiales ocupados: {occupiedPlanes}\n" +
                         $"- Omitidas por margen/OAR (hot): {omittedClearanceHot}\n" +
                         $"- Omitidas por margen (cold): {omittedClearanceCold}\n" +
                         $"- Omitidas por solape insuficiente (hot): {omittedHaltonHot}\n" +
                         $"- Omitidas por solape insuficiente (cold): {omittedHaltonCold}\n" +
                         $"- Volume Ratio (hot) analítico: {ratioPercent:F2}%\n";

            if (ratioPercent < RatioBandMinPercent || ratioPercent > RatioBandMaxPercent)
            {
                msg += $"\nADVERTENCIA: el ratio está fuera de la banda de referencia clínica {RatioBandMinPercent:F0}-{RatioBandMaxPercent:F0}%.\n";
            }

            msg += "\n¿Deseas generar las estructuras con estos parámetros?";
            return msg;
        }

        private bool DecideOutputMode(StructureSet ss, bool makeIndividual, int hotCount, int coldCount, out bool actuallyIndividual, out string fallbackNote)
        {
            fallbackNote = null;
            actuallyIndividual = makeIndividual;

            int existingCount = ss.Structures.Count();
            int neededIndividual = hotCount + coldCount;
            int neededCombined = (hotCount > 0 ? 1 : 0) + (coldCount > 0 ? 1 : 0);

            if (actuallyIndividual && existingCount + neededIndividual > EclipseStructureLimit - StructureLimitSafetyBuffer)
            {
                actuallyIndividual = false;
                fallbackNote = "\n(Nota: se generaron estructuras agrupadas en vez de individuales porque se alcanzaría el límite de 99 estructuras de Eclipse.)";
            }

            int neededFinal = actuallyIndividual ? neededIndividual : neededCombined;
            if (existingCount + neededFinal > EclipseStructureLimit - StructureLimitSafetyBuffer)
            {
                MessageBox.Show($"No hay espacio suficiente en el Structure Set para generar el LATTICE (límite de {EclipseStructureLimit} estructuras de Eclipse). Elimina estructuras no utilizadas e intenta de nuevo.", "Límite de Estructuras Alcanzado", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private void WriteOutputs(StructureSet ss, VMS.TPS.Common.Model.API.Image image, List<GridCandidate> finalHot, List<GridCandidate> finalCold, double radiusMm, bool actuallyIndividual)
        {
            if (actuallyIndividual)
            {
                int counter = 1;
                foreach (var c in finalHot)
                {
                    Structure s = ss.AddStructure("CONTROL", $"zH_{counter:00}");
                    s.Color = HotStructureColor;
                    DrawSphere(s, ToVVector(c.Position), radiusMm, image);
                    counter++;
                }

                counter = 1;
                foreach (var c in finalCold)
                {
                    Structure s = ss.AddStructure("CONTROL", $"zC_{counter:00}");
                    s.Color = ColdStructureColor;
                    DrawSphere(s, ToVVector(c.Position), radiusMm, image);
                    counter++;
                }
            }
            else
            {
                if (finalHot.Count > 0)
                {
                    Structure hotStruct = ss.AddStructure("CONTROL", "LRT_Hot");
                    hotStruct.Color = HotStructureColor;
                    foreach (var c in finalHot)
                    {
                        DrawSphere(hotStruct, ToVVector(c.Position), radiusMm, image);
                    }
                }

                if (finalCold.Count > 0)
                {
                    Structure coldStruct = ss.AddStructure("CONTROL", "LRT_Cold");
                    coldStruct.Color = ColdStructureColor;
                    foreach (var c in finalCold)
                    {
                        DrawSphere(coldStruct, ToVVector(c.Position), radiusMm, image);
                    }
                }
            }
        }

        private string BuildFinalSummaryMessage(string marginModeMsg, double effectiveHotMarginMm, double tiltDeg,
            int hotCount, int coldCount, int occupiedPlanes, double finalRatioPercent, bool actuallyIndividual,
            bool wasTrimmed, int maxAllowedHot, string fallbackNote)
        {
            string msg = $"Geometría LATTICE generada con éxito.\n\n" +
                         $"- Modo de margen (hot): {marginModeMsg}\n" +
                         $"- Margen hot aplicado (borde a centro): {effectiveHotMarginMm:F1} mm\n" +
                         $"- Inclinación de malla vs. axial: {tiltDeg:F1}°\n" +
                         $"- Esferas Hot creadas: {hotCount}\n" +
                         $"- Esferas Cold creadas: {coldCount}\n" +
                         $"- Cortes axiales ocupados: {occupiedPlanes}\n" +
                         $"- Volume Ratio final (hot): {finalRatioPercent:F2}%\n" +
                         $"- Modo de salida: {(actuallyIndividual ? "Estructuras Individuales" : "Estructura Única")}\n";

            if (wasTrimmed)
            {
                msg += $"\n(Nota: Se recortaron esferas hot exteriores a un máximo de {maxAllowedHot} para respetar el Volume Ratio <= 10%).";
            }

            if (fallbackNote != null)
            {
                msg += fallbackNote;
            }

            return msg;
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

        private void RemoveStructuresByPrefix(StructureSet ss, string prefix)
        {
            var toRemove = ss.Structures.Where(s => s.Id.StartsWith(prefix)).ToList();
            foreach (var s in toRemove)
            {
                ss.RemoveStructure(s);
            }
        }

        private void CleanupPriorOutputs(StructureSet ss)
        {
            // Migración de nombres heredados (versiones previas del script)
            RemoveStructureIfExists(ss, "LRT_Volume");
            RemoveStructureIfExists(ss, "LRT_Vertices");
            RemoveStructuresByPrefix(ss, "zV_");

            // Salidas actuales
            RemoveStructureIfExists(ss, "LRT_Hot");
            RemoveStructureIfExists(ss, "LRT_Cold");
            RemoveStructureIfExists(ss, "LRT_HotRegion");
            RemoveStructuresByPrefix(ss, "zH_");
            RemoveStructuresByPrefix(ss, "zC_");
            RemoveStructuresByPrefix(ss, "zTMP_");
        }

        private static void GetSliceRange(VVector center, double radiusMm, VMS.TPS.Common.Model.API.Image image, out int minSlice, out int maxSlice)
        {
            double zRes = image.ZRes;
            minSlice = Math.Max(0, (int)Math.Floor((center.z - radiusMm - image.Origin.z) / zRes));
            maxSlice = Math.Min(image.ZSize - 1, (int)Math.Ceiling((center.z + radiusMm - image.Origin.z) / zRes));
        }

        private void DrawSphere(Structure structure, VVector center, double radiusMm, VMS.TPS.Common.Model.API.Image image)
        {
            double zRes = image.ZRes;
            int minSlice, maxSlice;
            GetSliceRange(center, radiusMm, image, out minSlice, out maxSlice);

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

        private static VVector[] GenerateCircle(VVector center, double radius, int segments = 36)
        {
            VVector[] pts = new VVector[segments];
            for (int i = 0; i < segments; i++)
            {
                double angle = i * 2.0 * Math.PI / segments;
                pts[i] = new VVector(center.x + radius * Math.Cos(angle), center.y + radius * Math.Sin(angle), center.z);
            }
            return pts;
        }

        private static Vec3 ToVec3(VVector v)
        {
            return new Vec3(v.x, v.y, v.z);
        }

        private static VVector ToVVector(Vec3 v)
        {
            return new VVector(v.X, v.Y, v.Z);
        }
    }
}
