namespace Models
{
    public class Point3D
    {
        public int Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point3D(int id, double x, double y, double z = 0)
        {
            this.Id = id;
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public Point3D(double x)
        {
            X = x;
        }

        public Point3D() { }

        public override bool Equals(object? obj)
        {
            if (obj is Point3D other)
            {
                return Math.Abs(this.X - other.X) < 0.1 &&
                       Math.Abs(this.Y - other.Y) < 0.1;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Math.Round(X, 1), Math.Round(Y, 1));
        }
    }
}
