using System;

namespace CanvasCovers.Models
{
    public class ProjectMetadata
    {
        public string CompanyName { get; set; }

        public string NetworkNumber { get; set; }

        public string ProjectName { get; set; }

        public string SalesContact { get; set; }

        public string MeasuredBy { get; set; }

        public string OrderNumber { get; set; }

        public string Mobile { get; set; }

        public DateTime? Date { get; set; }

        // Free-form notes / delivery instructions — mirrors the NOTES block
        // on the Adelaide Annexe measurement sheet. Rendered as a full-width
        // row at the bottom of the title block when non-empty.
        public string Notes { get; set; }
    }
}
