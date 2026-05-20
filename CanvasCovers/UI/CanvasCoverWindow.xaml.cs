using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CanvasCovers.Models;

namespace CanvasCovers.UI
{
    public partial class CanvasCoverWindow : Window
    {
        public CanvasCoverWindow()
        {
            InitializeComponent();
            DateInput.SelectedDate = DateTime.Today;
        }

        public LiftBlanketJob Job { get; private set; }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();

            LiftBlanketJob job = new LiftBlanketJob
            {
                Project = ReadProject(),
                Options = ReadOptions(),
            };

            if (!TryReadWall(LeftWallEnabled, LeftMainWidth, LeftMainHeight,
                    LeftDoorReturn1, LeftDoorReturn2, LeftDoorReturn3,
                    LeftCopEnabled, LeftCopTopOffset, LeftCopHeight, LeftCopWidth,
                    "Left wall", out WallDimensions left))
            {
                return;
            }
            job.LeftWall = left;

            if (!TryReadWall(RightWallEnabled, RightMainWidth, RightMainHeight,
                    RightDoorReturn1, RightDoorReturn2, RightDoorReturn3,
                    RightCopEnabled, RightCopTopOffset, RightCopHeight, RightCopWidth,
                    "Right wall", out WallDimensions right))
            {
                return;
            }
            job.RightWall = right;

            if (!TryReadRearWall(RearWallEnabled, RearWidth, RearHeight, out WallDimensions rear))
            {
                return;
            }
            job.RearWall = rear;

            Job = job;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private ProjectMetadata ReadProject()
        {
            return new ProjectMetadata
            {
                CompanyName = Trim(CompanyNameInput.Text),
                NetworkNumber = Trim(NetworkNumberInput.Text),
                ProjectName = Trim(ProjectNameInput.Text),
                SalesContact = Trim(SalesContactInput.Text),
                MeasuredBy = Trim(MeasuredByInput.Text),
                OrderNumber = Trim(OrderNumberInput.Text),
                Mobile = Trim(MobileInput.Text),
                Date = DateInput.SelectedDate,
            };
        }

        private JobOptions ReadOptions()
        {
            FixingType fixing = FixingType.HooksFacingOut;
            ComboBoxItem selected = FixingsInput.SelectedItem as ComboBoxItem;
            if (selected != null)
            {
                switch ((selected.Content as string) ?? string.Empty)
                {
                    case "Velcro": fixing = FixingType.Velcro; break;
                    case "Hooks Facing In": fixing = FixingType.HooksFacingIn; break;
                    case "Hooks Facing Out": fixing = FixingType.HooksFacingOut; break;
                    case "Press Studs": fixing = FixingType.PressStuds; break;
                }
            }

            return new JobOptions
            {
                ThroughCar = ThroughCarOption.IsChecked == true,
                PlasticCoverOnCop = PlasticCoverOption.IsChecked == true,
                Fixings = fixing,
            };
        }

        private bool TryReadWall(
            CheckBox enabledBox,
            TextBox widthBox, TextBox heightBox,
            TextBox dr1, TextBox dr2, TextBox dr3,
            CheckBox copBox, TextBox copTop, TextBox copHeight, TextBox copWidth,
            string wallLabel,
            out WallDimensions wall)
        {
            wall = new WallDimensions { Enabled = enabledBox.IsChecked == true };

            if (!wall.Enabled)
            {
                return true;
            }

            if (!TryPositive(widthBox.Text, wallLabel + " main width", out double w)) { wall = null; return false; }
            if (!TryPositive(heightBox.Text, wallLabel + " main height", out double h)) { wall = null; return false; }
            if (!TryNonNegative(dr1.Text, wallLabel + " door return 1", out double d1)) { wall = null; return false; }
            if (!TryNonNegative(dr2.Text, wallLabel + " door return 2", out double d2)) { wall = null; return false; }
            if (!TryNonNegative(dr3.Text, wallLabel + " door return 3", out double d3)) { wall = null; return false; }

            wall.MainWidth = w;
            wall.MainHeight = h;
            wall.DoorReturn1 = d1;
            wall.DoorReturn2 = d2;
            wall.DoorReturn3 = d3;
            wall.CopEnabled = copBox.IsChecked == true;

            if (wall.CopEnabled)
            {
                if (!TryNonNegative(copTop.Text, wallLabel + " COP top offset", out double top)) { wall = null; return false; }
                if (!TryPositive(copHeight.Text, wallLabel + " COP height", out double ch)) { wall = null; return false; }
                if (!TryPositive(copWidth.Text, wallLabel + " COP width", out double cw)) { wall = null; return false; }

                if (top + ch > h)
                {
                    ShowError(wallLabel + " COP top offset + COP height exceeds main height.");
                    wall = null;
                    return false;
                }
                if (cw > w)
                {
                    ShowError(wallLabel + " COP width exceeds main width.");
                    wall = null;
                    return false;
                }

                wall.CopTopOffset = top;
                wall.CopHeight = ch;
                wall.CopWidth = cw;
            }

            return true;
        }

        private bool TryReadRearWall(CheckBox enabledBox, TextBox widthBox, TextBox heightBox, out WallDimensions wall)
        {
            wall = new WallDimensions { Enabled = enabledBox.IsChecked == true };

            if (!wall.Enabled)
            {
                return true;
            }

            if (!TryPositive(widthBox.Text, "Rear wall width", out double w)) { wall = null; return false; }
            if (!TryPositive(heightBox.Text, "Rear wall height", out double h)) { wall = null; return false; }

            wall.MainWidth = w;
            wall.MainHeight = h;
            wall.CopEnabled = false;
            return true;
        }

        private bool TryPositive(string text, string label, out double value)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value <= 0)
            {
                ShowError(label + " must be a number greater than zero.");
                value = 0;
                return false;
            }
            return true;
        }

        private bool TryNonNegative(string text, string label, out double value)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value < 0)
            {
                ShowError(label + " must be zero or a positive number.");
                value = 0;
                return false;
            }
            return true;
        }

        private static string Trim(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void ClearError()
        {
            ErrorText.Text = string.Empty;
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }
}
