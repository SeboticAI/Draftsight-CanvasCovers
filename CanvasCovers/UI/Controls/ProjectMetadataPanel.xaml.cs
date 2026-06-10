using System;
using System.Collections.Generic;
using System.Windows.Controls;
using CanvasCovers.Models;

namespace CanvasCovers.UI.Controls
{
    public partial class ProjectMetadataPanel : UserControl
    {
        private List<CustomerEntry> _customers = new List<CustomerEntry>();

        public ProjectMetadataPanel()
        {
            InitializeComponent();
            DateInput.SelectedDate = DateTime.Today;
        }

        // Fills the Company Name drop-down. Called by the host window once
        // the customer CSV has been read (round 2, item 6).
        public void SetCustomers(List<CustomerEntry> customers)
        {
            _customers = customers ?? new List<CustomerEntry>();
            CompanyNameInput.Items.Clear();
            foreach (CustomerEntry c in _customers)
            {
                CompanyNameInput.Items.Add(c.Name);
            }
        }

        private void CompanyNameInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CompanyInitialsInput == null) return;   // fires during XAML init
            string name = CompanyNameInput.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) return;     // free-typed text: leave initials alone
            CustomerEntry match = _customers.Find(c => c.Name == name);
            if (match != null)
            {
                CompanyInitialsInput.Text = match.Initials ?? string.Empty;
            }
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
