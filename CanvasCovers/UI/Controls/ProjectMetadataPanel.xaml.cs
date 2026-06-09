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
                CompanyInitials = Trim(CompanyInitialsInput.Text),
                NetworkNumber = Trim(NetworkNumberInput.Text),
                OrderNumber = Trim(OrderNumberInput.Text),
                ProjectName = Trim(ProjectNameInput.Text),
                Date = DateInput.SelectedDate,
            };
        }

        public void Apply(ProjectMetadata data)
        {
            if (data == null) return;
            CompanyNameInput.Text = data.CompanyName ?? string.Empty;
            CompanyInitialsInput.Text = data.CompanyInitials ?? string.Empty;
            NetworkNumberInput.Text = data.NetworkNumber ?? string.Empty;
            OrderNumberInput.Text = data.OrderNumber ?? string.Empty;
            ProjectNameInput.Text = data.ProjectName ?? string.Empty;
            DateInput.SelectedDate = data.Date;
        }

        private static string Trim(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
    }
}
