using System;
using System.Collections.Generic;
using System.Linq;

namespace CadApplication.Algorithms
{
    public class ArchitecturalRoom
    {
        public string Name { get; set; }
        public double Width { get; set; }
        public double Length { get; set; }
        public double Area { get; set; } // 🌟 עכשיו שדה רגיל (לא מחושב) - כדי שאפשר יהיה לבקש רק שטח, בלי רוחב/אורך

        public double PositionX { get; set; }
        public double PositionY { get; set; }

        public List<string> PreferredNeighbors { get; set; } = new List<string>();
    }

    //  מחלקת העזר החדשה שהוספנו כדי למנוע את קריסת השרת!
    public class LayoutResult
    {
        public bool Success { get; set; }
        public double FinalCoveragePercent { get; set; }
        public double TotalAreaM2 { get; set; }
        public List<ArchitecturalRoom> PlacedRooms { get; set; }
    }

    public class GreedyLayoutEngine
    {
        private const double WallThickness = 0.1; // 10 ס"מ עובי קיר פנימי במטרים

        public LayoutResult GenerateLayoutGreedy(List<ArchitecturalRoom> requestedRooms, List<Models.Point3D> buildingFootprint, double actualPlotArea)
        {
            // 1. חישוב מלבן חוסם (Bounding Box) מתוך מעטפת המבנה כדי שה-BSP יוכל לעבוד איתו
            double minX = buildingFootprint.Min(p => p.X);
            double maxX = buildingFootprint.Max(p => p.X);
            double minY = buildingFootprint.Min(p => p.Y);
            double maxY = buildingFootprint.Max(p => p.Y);

            var plotRect = new BL.Algorithms.Rectangle2D
            {
                X = minX,
                Y = minY,
                Width = maxX - minX,
                Length = maxY - minY
            };

            // 2. הפעלת אלגוריתם עץ BSP (Binary Space Partitioning) לחלוקת החדרים!
            var bspSplitter = new BL.Algorithms.BSPSplitter();
            var placedRooms = bspSplitter.Run(plotRect, requestedRooms);

            double totalBuiltArea = 0;

            // 3. אופטימיזציה גרידית - טיפול בעובי קירות ואילוצי שכנות בין החדרים
            for (int i = 0; i < placedRooms.Count; i++)
            {
                var currentRoom = placedRooms[i];

                // אילוץ קירות (הורדת שטח הקיר)
                currentRoom.Width -= (WallThickness * 2);
                currentRoom.Length -= (WallThickness * 2);
                currentRoom.PositionX += WallThickness;
                currentRoom.PositionY += WallThickness;

                // בדיקת אילוצי שכנות
                foreach (var neighborName in currentRoom.PreferredNeighbors)
                {
                    var neighbor = placedRooms.FirstOrDefault(r => r.Name.Contains(neighborName) && r != currentRoom);
                    if (neighbor != null)
                    {
                        double distance = CalculateDistance(currentRoom, neighbor);
                        if (distance > 5.0) // רחוק מדי
                        {
                            // מקרבים את החדר לשכן שלו
                            currentRoom.PositionX = (currentRoom.PositionX > neighbor.PositionX)
                                ? neighbor.PositionX + neighbor.Width + WallThickness
                                : neighbor.PositionX - currentRoom.Width - WallThickness;
                        }
                    }
                }
                currentRoom.Area = currentRoom.Width * currentRoom.Length; // 🌟 מעדכנים את השטח לערך האמיתי הסופי אחרי החלוקה, לא לפי מה שהתבקש
                totalBuiltArea += currentRoom.Area;
            }

            double finalCoverage = (totalBuiltArea / actualPlotArea) * 100;

            return new LayoutResult
            {
                Success = true,
                FinalCoveragePercent = Math.Round(finalCoverage, 2),
                TotalAreaM2 = Math.Round(totalBuiltArea, 2),
                PlacedRooms = placedRooms
            };
        }

        private double CalculateDistance(ArchitecturalRoom r1, ArchitecturalRoom r2)
        {
            double r1CenterX = r1.PositionX + (r1.Width / 2);
            double r1CenterY = r1.PositionY + (r1.Length / 2);
            double r2CenterX = r2.PositionX + (r2.Width / 2);
            double r2CenterY = r2.PositionY + (r2.Length / 2);

            double dx = r1CenterX - r2CenterX;
            double dy = r1CenterY - r2CenterY;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}