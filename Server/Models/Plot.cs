using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public class Plot
    {
        public int PlotId { get; set; }
        public string Name { get; set; } = string.Empty;
        public double TotalArea { get; set; }
        public List<Point3D> BoundaryPoints { get; set; } = new List<Point3D>();
        public List<Point3D> ContourPoints { get; set; } = new List<Point3D>();
        public double AbsoluteZeroLevel { get; set; }
        public double BuildPercentage { get; set; }
        public double FrontSetback { get; set; }
        public double SideSetback { get; set; }
        public double RearSetback { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
