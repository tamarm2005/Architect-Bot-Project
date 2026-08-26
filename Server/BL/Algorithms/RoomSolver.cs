using System;
using System.Linq;
using System.Collections.Generic;
using Models;

namespace BL.Algorithms
{
    internal class RoomSolver
    {
        private const double ClearanceBuffer = 0.30;

        public Point3D GetFurnitureCenter(Furniture f)
        {
            bool isRotated = (Math.Abs(f.Rotation - 90) < 0.1 || Math.Abs(f.Rotation - 270) < 0.1);
            double effectiveWidth = isRotated ? f.Length : f.Width;
            double effectiveLength = isRotated ? f.Width : f.Length;

            double centerX = f.Location.X + (effectiveWidth / 2);
            double centerY = f.Location.Y + (effectiveLength / 2);
            return new Point3D(0, centerX, centerY, 0 );
        }

        public bool IsInsideRoom(Furniture f, Room r)
        {
            bool isRotated = (Math.Abs(f.Rotation - 90) < 0.1 || Math.Abs(f.Rotation - 270) < 0.1);
            double effectiveWidth = isRotated ? f.Length : f.Width;
            double effectiveLength = isRotated ? f.Width : f.Length;

            return (f.Location.X >= 0) &&
                   (f.Location.X + effectiveWidth <= r.Width) &&
                   (f.Location.Y >= 0) &&
                   (f.Location.Y + effectiveLength <= r.Length);
        }

        // עכשיו מקבל את רשימת הרהיטים שכבר הוצבו בפועל + את הרהיט המועמד בנפרד
        public double CalculateScore(Room room, List<Furniture> placedFurniture, Furniture candidate)
        {
            var activeFurniture = new List<Furniture>(placedFurniture) { candidate };

            double score = 0;

            foreach (var f in activeFurniture)
            {
                if (!IsInsideRoom(f, room)) score -= 5000;
            }

            score -= CheckOpeningsObstruction(room, activeFurniture);
            score += CalculateWallAdjacencyBonus(room, activeFurniture);
            score += CalculateSemanticBonus(activeFurniture); // הבונוס שעושה את הקסם
            score += CalculateSpreadBonus(placedFurniture, candidate); // חדש: דוחף לפזר ריהוט בחדר, לא לדחוס הכל בצד אחד

            return score;
        }

        // בדיקה מפורשת: האם המיקום שנבחר בפועל חופף למשהו שכבר הוצב?
        public bool HasCollisionWithPlaced(List<Furniture> placed, Furniture candidate)
        {
            var combined = new List<Furniture>(placed) { candidate };
            return CheckRoomCollisionsWithClearance(combined);
        }

        // בונוס קטן שמעדיף מיקום רחוק מריהוט שכבר הוצב - זה מה ששובר את ה"הכל נדבק לפינה אחת"
        private double CalculateSpreadBonus(List<Furniture> placed, Furniture candidate)
        {
            if (placed.Count == 0) return 0;
            var candidateCenter = GetFurnitureCenter(candidate);
            double minDist = placed.Min(p => CalculateDistance(GetFurnitureCenter(p), candidateCenter));
            return minDist * 5; // משקל קטן בכוונה - רק שובר שוויון, לא דורס את שאר ההיגיון
        }

        private double CheckOpeningsObstruction(Room room, List<Furniture> furniture)
        {
            double totalPenalty = 0;
            foreach (var f in furniture)
            {
                if (Math.Abs(f.Location.X - room.DoorX) < 0.8 && Math.Abs(f.Location.Y - room.DoorY) < 0.8)
                {
                    totalPenalty += 1500;
                }
                if (Math.Abs(f.Location.X - room.WindowX) < 0.6 && Math.Abs(f.Location.Y - room.WindowY) < 0.6)
                {
                    if (f.Height > 1.2) totalPenalty += 1000;
                }
            }
            return totalPenalty;
        }

        private bool CheckRoomCollisionsWithClearance(List<Furniture> furniture)
        {
            for (int i = 0; i < furniture.Count; i++)
            {
                for (int j = i + 1; j < furniture.Count; j++)
                {
                    var f1 = furniture[i];
                    var f2 = furniture[j];

                    bool f1Rotated = (Math.Abs(f1.Rotation - 90) < 0.1 || Math.Abs(f1.Rotation - 270) < 0.1);
                    double f1W = f1Rotated ? f1.Length : f1.Width;
                    double f1L = f1Rotated ? f1.Width : f1.Length;

                    bool f2Rotated = (Math.Abs(f2.Rotation - 90) < 0.1 || Math.Abs(f2.Rotation - 270) < 0.1);
                    double f2W = f2Rotated ? f2.Length : f2.Width;
                    double f2L = f2Rotated ? f2.Width : f2.Length;

                    Point3D center1 = GetFurnitureCenter(f1);
                    Point3D center2 = GetFurnitureCenter(f2);

                    bool collisionX = Math.Abs(center1.X - center2.X) < ((f1W + ClearanceBuffer) + (f2W + ClearanceBuffer)) / 2;
                    bool collisionY = Math.Abs(center1.Y - center2.Y) < ((f1L + ClearanceBuffer) + (f2L + ClearanceBuffer)) / 2;

                    if (collisionX && collisionY) return true;
                }
            }
            return false;
        }

        private double CalculateWallAdjacencyBonus(Room room, List<Furniture> furniture)
        {
            double bonus = 0;
            foreach (var f in furniture)
            {
                bool isRotated = (Math.Abs(f.Rotation - 90) < 0.1 || Math.Abs(f.Rotation - 270) < 0.1);
                double fW = isRotated ? f.Length : f.Width;
                double fL = isRotated ? f.Width : f.Length;

                if (f.Location.X <= 0.05 || f.Location.Y <= 0.05 || Math.Abs((f.Location.X + fW) - room.Width) <= 0.05 || Math.Abs((f.Location.Y + fL) - room.Length) <= 0.05)
                {
                    bonus += 300;
                }
            }
            return bonus;
        }

        private double CalculateSemanticBonus(List<Furniture> furniture)
        {
            double bonus = 0;

            var chairs = furniture.Where(f => f.Name != null && f.Name.Contains("כיסא")).ToList();
            var desks = furniture.Where(f => f.Name != null && (f.Name.Contains("שולחן") || f.Name.Contains("כתיבה"))).ToList();
            var beds = furniture.Where(f => f.Name != null && f.Name.Contains("מיטה")).ToList();
            var nightstands = furniture.Where(f => f.Name != null && f.Name.Contains("שידה")).ToList();

            foreach (var chair in chairs)
            {
                foreach (var desk in desks)
                {
                    if (CalculateDistance(GetFurnitureCenter(chair), GetFurnitureCenter(desk)) < 0.8) bonus += 800;
                }
            }

            foreach (var nightstand in nightstands)
            {
                foreach (var bed in beds)
                {
                    if (CalculateDistance(GetFurnitureCenter(nightstand), GetFurnitureCenter(bed)) < 0.6) bonus += 500;
                }
            }

            return bonus;
        }

        private double CalculateDistance(Point3D p1, Point3D p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }
    }
}