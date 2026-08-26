using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BL.Algorithms
{
    // מודל עזר פנימי לייצוג מלבן חיתוך (רצפת חדר)
    public class Rectangle2D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Length { get; set; }
        public double Area => Width * Length;
    }

    public class LayoutGeneratorService
    {
        private Random _random = new Random();

        // 1. יצירת מעטפת הבניין (Offset פנימה)
        public List<Models.Point3D> GenerateBuildingFootprint(List<Models.Point3D> plotVertices, double offsetDistance = 3.0)
        {
            // 🎯 הגנה קריטית: אם לא התקבלו נקודות מגרש מהקליינט, נייצר מגרש Fallback יציב במטרים כדי למנוע קריסה!
            if (plotVertices == null || plotVertices.Count < 3)
            {
                plotVertices = new List<Models.Point3D>
                {
                    new Models.Point3D(0, 0, 0, 0 ),
                    new Models.Point3D(0, 25, 0, 0 ),
                    new Models.Point3D(0, 25, 20, 0 ),
                    new Models.Point3D(0, 0, 20, 0 )
                };
            }

            Models.Point3D centroid = CalculateCentroid(plotVertices);
            List<Models.Point3D> buildingFootprint = new List<Models.Point3D>();

            foreach (var vertex in plotVertices)
            {
                double dx = centroid.X - vertex.X;
                double dy = centroid.Y - vertex.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance == 0) continue;

                double dirX = dx / distance;
                double dirY = dy / distance;

                // הגבלת הקיזוז ל-40% כדי לשמור על פרופורציות המגרש
                double shiftAmount = Math.Min(offsetDistance, distance * 0.4);
                buildingFootprint.Add(new Models.Point3D(0, vertex.X + (dirX * shiftAmount), vertex.Y + (dirY * shiftAmount), 0));
            }

            // הגנת חירום אחרונה למניעת רשימה ריקה
            if (buildingFootprint.Count == 0)
            {
                return plotVertices;
            }

            return buildingFootprint;
        }

        // 2. חלוקת השטח הפנימי לחדרים (2D Slice / BSP)
        public List<CadApplication.Algorithms.ArchitecturalRoom> SliceFootprintIntoRooms(
            List<Models.Point3D> footprint,
            List<CadApplication.Algorithms.ArchitecturalRoom> requestedRooms)
        {
            // הגנה קריטית למקרה שהמעטפת התקבלה ריקה (מונע Sequence contains no elements)
            if (footprint == null || footprint.Count == 0)
            {
                footprint = new List<Models.Point3D>
                {
                    new Models.Point3D(0, 2, 2, 0 ),
                    new Models.Point3D(0, 23, 2, 0 ),
                    new Models.Point3D(0, 23, 18, 0 ),
                    new Models.Point3D(0, 2, 18, 0 )
                };
            }

            // אנו מוצאים את גבולות המלבן החוסם (Bounding Box) של המעטפת
            double minX = footprint.Min(v => v.X);
            double maxX = footprint.Max(v => v.X);
            double minY = footprint.Min(v => v.Y);
            double maxY = footprint.Max(v => v.Y);

            Rectangle2D initialContainer = new Rectangle2D
            {
                X = minX,
                Y = minY,
                Width = maxX - minX,
                Length = maxY - minY
            };

            // מיון החדרים מהגדול לקטן
            var sortedRooms = requestedRooms.OrderByDescending(r => r.Width * r.Length).ToList();

            List<Rectangle2D> allocatedSpaces = new List<Rectangle2D> { initialContainer };

            // ריצה על כל החדרים וביצוע חיתוך (Slicing) של השטח הפנוי הגדול ביותר
            foreach (var room in sortedRooms)
            {
                var targetSpace = allocatedSpaces.OrderByDescending(s => s.Area).FirstOrDefault();
                if (targetSpace == null) break;

                allocatedSpaces.Remove(targetSpace);

                bool splitVertically = targetSpace.Width > targetSpace.Length;

                Rectangle2D roomSpace = new Rectangle2D();
                Rectangle2D remainingSpace = new Rectangle2D();

                double requiredArea = room.Width * room.Length;

                // הגנה מפני חלוקה באפס (אם השטח שהוקצה הוא אפס)
                double targetArea = targetSpace.Area > 0 ? targetSpace.Area : 1.0;
                double splitRatio = requiredArea / targetArea;
                if (splitRatio > 0.9) splitRatio = 0.7;

                if (splitVertically)
                {
                    double cutWidth = targetSpace.Width * splitRatio;

                    roomSpace = new Rectangle2D { X = targetSpace.X, Y = targetSpace.Y, Width = cutWidth, Length = targetSpace.Length };
                    remainingSpace = new Rectangle2D { X = targetSpace.X + cutWidth, Y = targetSpace.Y, Width = targetSpace.Width - cutWidth, Length = targetSpace.Length };
                }
                else
                {
                    double cutLength = targetSpace.Length * splitRatio;

                    roomSpace = new Rectangle2D { X = targetSpace.X, Y = targetSpace.Y, Width = targetSpace.Width, Length = cutLength };
                    remainingSpace = new Rectangle2D { X = targetSpace.X, Y = targetSpace.Y + cutLength, Width = targetSpace.Width, Length = targetSpace.Length - cutLength };
                }

                room.PositionX = roomSpace.X;
                room.PositionY = roomSpace.Y;
                room.Width = roomSpace.Width;
                room.Length = roomSpace.Length;

                if (remainingSpace.Area > 0)
                {
                    allocatedSpaces.Add(remainingSpace);
                }
            }

            return sortedRooms;
        }

        private Models.Point3D CalculateCentroid(List<Models.Point3D> vertices)
        {
            double xSum = 0;
            double ySum = 0;
            int count = vertices.Count;

            // מניעת חלוקה באפס
            if (count == 0) return new Models.Point3D(0, 12.5, 10.0, 0 );

            foreach (var v in vertices)
            {
                xSum += v.X;
                ySum += v.Y;
            }

            return new Models.Point3D(0, xSum / count, ySum / count, 0 );
        }
    }
}
