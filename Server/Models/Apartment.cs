using Models;

namespace Models
{
    public class Apartment
    {
        private object r;

        public int Id { get; set; }
        public List<Room> Rooms { get; set; } = new List<Room>();
        public string Name { get; set; } = string.Empty;
        public double Area { get; set; }
        public int floor { get; set; }
        public DesignStyle MainTheme { get; set; }

        public Apartment(int id, string Name, double Area, int floor, DesignStyle MainTheme)
        {
            this.Id = id;
            this.Name = Name;
            this.Area = Area;
            this.floor = floor;
            this.MainTheme = MainTheme;
        }

        public Apartment()
        {

        }

        /*מחזירה את שטח החדרים שבדירה*/
        public double GetTotalArea()
        {
            double total = 0;
            foreach (var r in Rooms)
            {
                total += r.CalculateArea();
            }
            return total;
        }

        /*בודקת אם שטח החדרים לא עולה על שטח הדירה*/
        public bool IsAreaValid()
        {
            double roomArea = GetTotalArea();
            if (roomArea > this.Area)
                return false;
            return true;
        }

        public string DataForDownload()
        {
            string DimensionsFile = "פירוט מידות חדרים לדירת: " + this.Name + Environment.NewLine;
            DimensionsFile += "תאריך הפקה: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + Environment.NewLine;
            DimensionsFile += "------------------------------------------" + Environment.NewLine;

            foreach (var r in Rooms)
            {
                DimensionsFile += "חדר: " + r.Name + Environment.NewLine;
                DimensionsFile += "- שטח: " + r.Area.ToString("F2") + " מ\"ר" + Environment.NewLine;
                DimensionsFile += "- היקף: " + r.Perimeter.ToString("F2") + " מטר" + Environment.NewLine;
                DimensionsFile += "- כמות רהיטים: " + r.furniture.Count + Environment.NewLine;
                DimensionsFile += "----------------" + Environment.NewLine;
            }
            DimensionsFile += "שטח כולל מחושב של הדירה: " + GetTotalArea().ToString("F2") + " מ\"ר" + Environment.NewLine;
            return DimensionsFile;
        }
    }
}
