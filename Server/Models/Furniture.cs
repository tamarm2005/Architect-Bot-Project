namespace Models
{
    public class Furniture
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public double Width { get; set; }
        public double Height { get; set; }
        public double Length { get; set; }
        public Point3D Location { get; set; }
        public FurnitureCategory Category { get; set; }
        public Material Materials { get; set; }
        public ColorCategory Colors { get; set; }
        public DesignStyle Style { get; set; }
        public double Rotation { get; set; } = 0;
        public bool IsFixed { get; set; } = false; //  רהיט שממוקם ידנית לפי כלל קבוע - האלגוריתם לא נוגע בו

        public Furniture(int id, string name, double w, double h, double l, FurnitureCategory cat, Material mat)
        {
            this.Id = id;
            this.ItemName = name;
            this.Width = w > 0 ? w : 1;
            this.Height = h > 0 ? h : 1;
            this.Length = l > 0 ? l : 1;
            this.Category = cat;
            this.Materials = mat;
            this.Location = new Point3D(0, 0, 0, 0);
        }
        public Furniture()
        {
            ItemName = "New Item";
            Width = 1;
            Height = 1;
            Length = 1;
            Location = new Point3D(0, 0, 0, 0);
            Materials = new Material();
        }
    }
}