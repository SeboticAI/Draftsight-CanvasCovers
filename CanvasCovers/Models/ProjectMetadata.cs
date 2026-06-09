using System;

namespace CanvasCovers.Models
{
    public class ProjectMetadata
    {
        public string CompanyName { get; set; }

        // AAC's customer+depot code, e.g. Kone Melbourne = KM. The MIDDLE
        // section of the blanket text (order + initials + network).
        public string CompanyInitials { get; set; }

        public string NetworkNumber { get; set; }

        // AAC order number from MYOB. The FIRST section of the blanket text.
        public string OrderNumber { get; set; }

        public string ProjectName { get; set; }

        public DateTime? Date { get; set; }
    }
}
