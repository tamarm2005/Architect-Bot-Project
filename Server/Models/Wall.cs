
namespace Models
{
    public class Wall
    {
        public int Id { get; set; }
        public double Length { get; set; }
        public string WallType { get; set; } = string.Empty;
        public double Thickness { get; set; }
        public Point3D StartPoint { get; set; } = new Point3D(0, 0, 0, 0);
        public Point3D EndPoint { get; set; } = new Point3D(0, 0, 0, 0);
        public List<Opening> Openings { get; set; } = new List<Opening>();
        public ColorCategory Colors { get; set; } = ColorCategory.White;
        public FinishesCategory Finish { get; set; } = FinishesCategory.Plaster;
        public Material Material { get; set; } = new Material();

        public Wall(int id, Point3D start, Point3D end, string type, Material mat, double thickness = 10)
        {
            Id = id;
            StartPoint = start;
            EndPoint = end;
            WallType = type ?? "Standard";
            Material = mat ?? new Material();

            if (thickness <= 0)
            {
                Thickness = 10;
            }
            else
            {
                Thickness = thickness;
            }

            Length = CalculateLength();

            Openings = new List<Opening>();
        }

        public Wall()
        {

        }

        private double CalculateLength()
        {
            return Math.Sqrt(Math.Pow(EndPoint.X - StartPoint.X, 2) +
                         Math.Pow(EndPoint.Y - StartPoint.Y, 2));
        }

        /*מתודה שמחשבת את הקיר ללא הפתחים*/
        public double NetWall(double wallHeight)
        {
            double neto = 0;
            double OpeningArea = 0;
            foreach (var o in Openings)
            {
                OpeningArea += o.Width * o.Height;
            }
            neto = (CalculateLength() * wallHeight) - OpeningArea;
            return neto < 0 ? 0 : neto;
        }
    }
}