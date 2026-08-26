
namespace Models
{
    public class Opening
    {
        public int Id { get; set; }
        public string OpenName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double Width { get; set; }
        public double Height { get; set; }
        public Point3D StartPoint { get; set; } = new Point3D(0, 0, 0, 0);
        public Point3D EndPoint { get; set; } = new Point3D(0, 0, 0, 0);
        public MaterialCategory Material { get; set; } = MaterialCategory.Wood;
        public double BottomHeight { get; set; }
        public List<Material> Materials { get; set; }

        public Opening(int id, string OpenName,string Type, double w, double h, Point3D start, Point3D end)
        {
            this.Id = id;
            this.OpenName = OpenName;
            this.Type = Type;
            this.Width = w;
            this.Height = h;
            this.StartPoint = start;
            this.EndPoint = end;
        }

        public Opening()
        {

        }
    }
}