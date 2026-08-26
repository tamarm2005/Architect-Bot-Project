using System;
using System.Collections.Generic;
using System.Linq;

namespace Models
{
    public class Room
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;
        public double FloorLevel { get; set; }
        public double Area { get; set; }
        public double Perimeter { get; set; }
        public List<Wall> walls { get; set; } = new List<Wall>();
        public List<Opening> openings { get; set; } = new List<Opening>();
        public List<Furniture> furniture { get; set; } = new List<Furniture>();
        public List<Point3D> Vertices { get; set; } = new List<Point3D>();
        public RoomType Type { get; set; }
        public DesignStyle Style { get; set; }
        public double CeilingHeight { get; set; }
        public double Width { get; set; }
        public double Length { get; set; }
        public double DoorX { get; set; }
        public double DoorY { get; set; }
        public double WindowX { get; set; }
        public double WindowY { get; set; }

        // 🎯 השדות החדשים שהוספנו כדי להחזיר את מיקום החדר לקליינט (ריאקט)
        public double PositionX { get; set; }
        public double PositionY { get; set; }

        public Room(int ID, string Name, double FloorLevel, RoomType Type, DesignStyle Style, double CeilingHeight, double Width, double Length)
        {
            this.ID = ID;
            this.Name = Name;
            this.FloorLevel = FloorLevel;
            this.Type = Type;
            this.Style = Style;
            this.CeilingHeight = CeilingHeight;
            this.Width = Width;
            this.Length = Length;
        }

        public Room()
        {
            walls = new List<Wall>();
            openings = new List<Opening>();
            furniture = new List<Furniture>();
            Vertices = new List<Point3D>();
        }

        /*חישוב השטח של החדר*/
        public double CalculateArea()
        {
            if (Area > 0) return Area;

            if (Vertices == null || Vertices.Count < 3)
                return 0;

            double area = 0;
            return area;
        }

        /*חישוב ההיקף של החדר*/
        public double CalculatePerimeter()
        {
            if (Perimeter > 0)
                return Perimeter;

            if (Vertices == null || Vertices.Count < 2)
                return 0;

            double scope = 0;

            for (int i = 0; i < Vertices.Count; i++)
            {
                var next = Vertices[(i + 1) % Vertices.Count];
                scope += Math.Sqrt(Math.Pow(Vertices[i].X - next.X, 2) + Math.Pow(Vertices[i].Y - next.Y, 2));
            }
            return scope > 500 ? scope / 1000.0 : scope;
        }

        /*חישוב שטח החדר ללא הרהיטים*/
        public double GetAvailableSpace()
        {
            double Area = CalculateArea();
            double FurnitureArea = 0;
            double FreeSpace = 0;
            foreach (var f in furniture)
            {
                FurnitureArea += f.Width * f.Height;
            }
            FreeSpace = Area - FurnitureArea;
            if (FreeSpace < 0)
                FreeSpace = 0;
            return FreeSpace;
        }

        /*בדיקה ששום רהיט לא עולה אחד על השני בחדר*/
        public bool CheckFurnitureCollision()
        {
            foreach (var f1 in furniture)
            {
                foreach (var f2 in furniture)
                {
                    if (f1 == f2)
                    {
                        continue;
                    }
                    if (IsOverlapping(f1, f2))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /*בודק שאין שתי רהיטים מתנגשים*/
        public bool IsOverlapping(Furniture f1, Furniture f2)
        {
            if (f1.Location.X + f1.Width <= f2.Location.X || f2.Location.X + f2.Width <= f1.Location.X)
                return false;
            if (f1.Location.Y + f1.Length <= f2.Location.Y || f2.Location.Y + f1.Length <= f1.Location.Y)
                return false;
            return true;
        }

        /*בודק אם יש מספיק שטח פנוי בחדר כדי להוסיף את הרהיט*/
        public bool CanFitFurniture(Furniture f)
        {
            double furnitureArea = f.Width * f.Length;
            return GetAvailableSpace() >= furnitureArea;
        }

        /*סקירה והוספת רהיט*/
        public bool AddFurniture(Furniture item)
        {
            if (!CanFitFurniture(item))
                return false;
            if (item.Height > CeilingHeight)
                return false;
            if (item.Style != Style)
                return false;
            furniture.Add(item);
            if (CheckFurnitureCollision())
            {
                furniture.Remove(item);
                return false;
            }
            return true;
        }

        /*מחיקת רהיט מהרשימה */
        public bool RemoveFurniture(string furnitureName)
        {
            Furniture? itemToRemove = null;
            foreach (var f in furniture)
            {
                if (f.ItemName == furnitureName)
                {
                    itemToRemove = f;
                    break;
                }
            }
            if (itemToRemove != null)
            {
                furniture.Remove(itemToRemove);
                return true;
            }
            return false;
        }

        public CalculationResult GetRoomSummary()
        {
            double area = CalculateArea();
            double circumference = CalculatePerimeter();
            int count = furniture.Count;
            double usedSpace = 0;
            foreach (var f in furniture)
            {
                usedSpace += (f.Width * f.Length);
            }
            double percentUsed = area > 0 ? (usedSpace / area) * 100 : 0;
            string msg = $"החדר עוצב עם {count} רהיטים. {percentUsed:F1}% מהשטח תפוס.";

            return new CalculationResult(ID, area, circumference, count, msg, true);
        }
    }
}
