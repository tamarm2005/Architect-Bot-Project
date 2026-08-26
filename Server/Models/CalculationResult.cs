namespace Models
{
    public class CalculationResult
    {
        public int Id { get; set; }
        public double Area { get; set; }
        public double Circumference { get; set; }
        public int FurnitureCount { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsValid { get; set; }

        public CalculationResult(int id, double area, double circumference, int count, string msg, bool isValid)
        {
            this.Id = id;
            this.Area = area;
            this.Circumference = circumference;
            this.FurnitureCount = count;
            this.Message = msg;
            this.IsValid = isValid;
        }
        public CalculationResult()
        {
            
        }
    }
}