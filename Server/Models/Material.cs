namespace Models
{
    public class Material
    {
        public int Id { get; set; }
        public MaterialCategory Type { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public double PricePerUnit { get; set; }
        public MeasurementUnit Unit { get; set; }
        public double Weight { get; set; }
        public string Texture { get; set; } = string.Empty;

        public Material()
        {

        }

        public Material(int id, MaterialCategory type, string name, double price, MeasurementUnit unit, double weight, string texture)
        {
            this.Id = id;
            this.Type = type;
            this.MaterialName = name;
            this.PricePerUnit = price;
            this.Unit = unit;
            this.Weight = weight;
            this.Texture = texture;
        }
    }
}