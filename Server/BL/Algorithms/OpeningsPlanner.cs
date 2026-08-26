using CadApplication.Algorithms;
using Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BL.Algorithms
{
    public class OpeningPlan
    {
        public string RoomName { get; set; } = "";
        public string Type { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public string Wall { get; set; } = "";
        public double Size { get; set; } = 0.9;
        public bool IsEntrance { get; set; } = false; // 🌟 מבדיל בין דלת כניסה לדירה לבין דלת פנימית
    }

    public class OpeningsPlanner
    {
        private const double DoorWidth = 0.9;
        private const double MinDoorWidth = 0.6;
        private const double Tolerance = 0.25;

        public List<OpeningPlan> GenerateOpenings(List<ArchitecturalRoom> rooms, List<Point3D> buildingFootprint)
        {
            var openings = new List<OpeningPlan>();
            if (rooms == null || rooms.Count == 0) return openings;

            double plotMinX = buildingFootprint.Min(p => p.X);
            double plotMaxX = buildingFootprint.Max(p => p.X);
            double plotMinY = buildingFootprint.Min(p => p.Y);
            double plotMaxY = buildingFootprint.Max(p => p.Y);

            var livingRoom = rooms.FirstOrDefault(r => r.Name != null && r.Name.Contains("סלון"));

            foreach (var room in rooms)
            {
                bool isLiving = room == livingRoom;

                if (!isLiving)
                {
                    var neighbor = (livingRoom != null && SharesWall(room, livingRoom)) ? livingRoom
                        : rooms.FirstOrDefault(r => r != room && SharesWall(room, r));
                    if (neighbor != null)
                    {
                        var (wallSide, mx, my, size) = GetSharedWallMidpoint(room, neighbor);
                        if (wallSide != null)
                        {
                            // 🌟 רוחב דלת סטנדרטי לפי סוג החדר - מוגבל תמיד גם לאורך הקיר בפועל
                            bool isWetRoom = room.Name.Contains("שירותים") || room.Name.Contains("רחצה") || room.Name.Contains("אמבטיה") || room.Name.Contains("מקלחת");
                            double standardWidth = isWetRoom ? 0.7 : 0.8;
                            double finalSize = Math.Min(size, standardWidth);
                            openings.Add(new OpeningPlan { RoomName = room.Name, Type = "Door", X = mx, Y = my, Wall = wallSide, Size = finalSize });
                        }
                    }
                }

                // 🌟 חלון: רק אחד - הקיר החיצוני הכי ארוך של החדר (לא אחד לכל קיר)
                var exteriorCandidates = new List<(string wall, double x, double y, double length)>();
                if (Math.Abs(room.PositionX - plotMinX) < Tolerance)
                    exteriorCandidates.Add(("Left", room.PositionX, room.PositionY + room.Length / 2, room.Length));
                if (Math.Abs((room.PositionX + room.Width) - plotMaxX) < Tolerance)
                    exteriorCandidates.Add(("Right", room.PositionX + room.Width, room.PositionY + room.Length / 2, room.Length));
                if (Math.Abs(room.PositionY - plotMinY) < Tolerance)
                    exteriorCandidates.Add(("Bottom", room.PositionX + room.Width / 2, room.PositionY, room.Width));
                if (Math.Abs((room.PositionY + room.Length) - plotMaxY) < Tolerance)
                    exteriorCandidates.Add(("Top", room.PositionX + room.Width / 2, room.PositionY + room.Length, room.Width));

                if (exteriorCandidates.Count > 0)
                {
                    var best = exteriorCandidates.OrderByDescending(c => c.length).First();
                    // 🌟 בסלון - ויטרינה גדולה (עד 70% מאורך הקיר), בשאר חדרים - חלון רגיל
                    double windowSize = isLiving ? Math.Min(best.length * 0.7, 3.5) : Math.Min(DoorWidth + 0.3, best.length * 0.5);
                    openings.Add(new OpeningPlan { RoomName = room.Name, Type = "Window", X = best.x, Y = best.y, Wall = best.wall, Size = windowSize });
                }
            }

            // דלת כניסה חיצונית - לסלון, בקיר שנוגע במעטפת
            if (livingRoom != null)
            {
                double entranceSize = 0.9; // 🌟 דלת כניסה תמיד 90 ס"מ
                if (Math.Abs(livingRoom.PositionY - plotMinY) < Tolerance)
                    openings.Add(new OpeningPlan { RoomName = livingRoom.Name, Type = "Door", X = livingRoom.PositionX + livingRoom.Width / 2, Y = livingRoom.PositionY, Wall = "Bottom", Size = Math.Min(entranceSize, livingRoom.Width * 0.4), IsEntrance = true });
                else if (Math.Abs(livingRoom.PositionX - plotMinX) < Tolerance)
                    openings.Add(new OpeningPlan { RoomName = livingRoom.Name, Type = "Door", X = livingRoom.PositionX, Y = livingRoom.PositionY + livingRoom.Length / 2, Wall = "Left", Size = Math.Min(entranceSize, livingRoom.Length * 0.4), IsEntrance = true });
            }

            return openings;
        }

        private bool SharesWall(ArchitecturalRoom a, ArchitecturalRoom b) => GetSharedWallMidpoint(a, b).Item1 != null;

        private (string?, double, double, double) GetSharedWallMidpoint(ArchitecturalRoom a, ArchitecturalRoom b)
        {
            if (Math.Abs((a.PositionX + a.Width) - b.PositionX) < Tolerance)
            {
                double s = Math.Max(a.PositionY, b.PositionY), e = Math.Min(a.PositionY + a.Length, b.PositionY + b.Length);
                double size = Math.Min(DoorWidth, (e - s) * 0.6);
                if (e - s > MinDoorWidth) return ("Right", a.PositionX + a.Width, (s + e) / 2, size);
            }
            if (Math.Abs((b.PositionX + b.Width) - a.PositionX) < Tolerance)
            {
                double s = Math.Max(a.PositionY, b.PositionY), e = Math.Min(a.PositionY + a.Length, b.PositionY + b.Length);
                double size = Math.Min(DoorWidth, (e - s) * 0.6);
                if (e - s > MinDoorWidth) return ("Left", a.PositionX, (s + e) / 2, size);
            }
            if (Math.Abs((a.PositionY + a.Length) - b.PositionY) < Tolerance)
            {
                double s = Math.Max(a.PositionX, b.PositionX), e = Math.Min(a.PositionX + a.Width, b.PositionX + b.Width);
                double size = Math.Min(DoorWidth, (e - s) * 0.6);
                if (e - s > MinDoorWidth) return ("Top", (s + e) / 2, a.PositionY + a.Length, size);
            }
            if (Math.Abs((b.PositionY + b.Length) - a.PositionY) < Tolerance)
            {
                double s = Math.Max(a.PositionX, b.PositionX), e = Math.Min(a.PositionX + a.Width, b.PositionX + b.Width);
                double size = Math.Min(DoorWidth, (e - s) * 0.6);
                if (e - s > MinDoorWidth) return ("Bottom", (s + e) / 2, a.PositionY, size);
            }
            return (null, 0, 0, 0);
        }
    }
}