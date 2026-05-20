using System;
using System.Windows.Controls;
using CanvasCovers.Models;

namespace CanvasCovers.UI.Controls
{
    public partial class ProjectMetadataPanel : UserControl
    {
        public ProjectMetadataPanel()
        {
            InitializeComponent();
            DateInput.SelectedDate = DateTime.Today;
        }

        public ProjectMetadata Read()
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

        public void Apply(ProjectMetadata data)
        {
            if (data == null) return;
            CompanyNameInput.Text = data.CompanyName ?? string.Empty;
            NetworkNumberInput.Text = data.NetworkNumber ?? string.Empty;
            ProjectNameInput.Text = data.ProjectName ?? string.Empty;
            SalesContactInput.Text = data.SalesContact ?? string.Empty;
            MeasuredByInput.Text = data.MeasuredBy ?? string.Empty;
            OrderNumberInput.Text = data.OrderNumber ?? string.Empty;
            MobileInput.Text = data.Mobile ?? string.Empty;
            DateInput.SelectedDate = data.Date;
        }

        private static string Trim(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
    }
}
