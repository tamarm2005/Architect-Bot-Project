using CadApplication.Algorithms;
using Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BL.Algorithms
{
    //פתחים
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

                if (!IsCorridor(room))
                {
                    var target = ChooseDoorTarget(room, rooms, livingRoom);
                    if (target != null)
                    {
                        var (wallSide, mx, my, size) = GetSharedWallMidpoint(room, target);
                        if (wallSide != null)
                        {
                            double standardWidth = IsWet(room) ? 0.7 : 0.8;
                            double finalSize = Math.Min(standardWidth, Math.Max(0.6, SharedLength(room, target) - 0.3));                            // דלת הזזה רחבה בין הסלון למרפסת
                            if (IsBalcony(room) && target == livingRoom)
                                finalSize = Math.Min(0.9, SharedLength(room, target) - 0.3);
                            openings.Add(new OpeningPlan { RoomName = room.Name, Type = "Door", X = mx, Y = my, Wall = wallSide, Size = finalSize });
                        }
                    }
                }

                // למסדרון ולמרפסת אין חלון רגיל
                if (IsCorridor(room) || IsBalcony(room)) continue;

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

            // דלת כניסה חיצונית - לסלון, בקיר שנוגע במעטפת ואינו פונה למרפסת
            if (livingRoom != null)
            {
                double entranceSize = 0.9; // דלת כניסה תמיד 90 ס"מ
                bool BalconyOnWall(string wall) => rooms.Any(b => IsBalcony(b) && GetSharedWallMidpoint(livingRoom, b).Item1 == wall);

                if (Math.Abs(livingRoom.PositionX - plotMinX) < Tolerance && !BalconyOnWall("Left"))
                    openings.Add(new OpeningPlan { RoomName = livingRoom.Name, Type = "Door", X = livingRoom.PositionX, Y = livingRoom.PositionY + livingRoom.Length * 0.3, Wall = "Left", Size = Math.Min(entranceSize, livingRoom.Length * 0.4), IsEntrance = true });
                else if (Math.Abs(livingRoom.PositionY - plotMinY) < Tolerance && !BalconyOnWall("Bottom"))
                    openings.Add(new OpeningPlan { RoomName = livingRoom.Name, Type = "Door", X = livingRoom.PositionX + livingRoom.Width * 0.3, Y = livingRoom.PositionY, Wall = "Bottom", Size = Math.Min(entranceSize, livingRoom.Width * 0.4), IsEntrance = true });
                else if (Math.Abs((livingRoom.PositionX + livingRoom.Width) - plotMaxX) < Tolerance && !BalconyOnWall("Right"))
                    openings.Add(new OpeningPlan { RoomName = livingRoom.Name, Type = "Door", X = livingRoom.PositionX + livingRoom.Width, Y = livingRoom.PositionY + livingRoom.Length * 0.3, Wall = "Right", Size = Math.Min(entranceSize, livingRoom.Length * 0.4), IsEntrance = true });
                else if (Math.Abs((livingRoom.PositionY + livingRoom.Length) - plotMaxY) < Tolerance && !BalconyOnWall("Top"))
                    openings.Add(new OpeningPlan { RoomName = livingRoom.Name, Type = "Door", X = livingRoom.PositionX + livingRoom.Width * 0.3, Y = livingRoom.PositionY + livingRoom.Length, Wall = "Top", Size = Math.Min(entranceSize, livingRoom.Width * 0.4), IsEntrance = true });
            }

            ResolveOverlaps(openings, rooms);
            return openings;
        }

        // ===================== סיווג חדרים וכללי דלתות =====================
        private static bool HasName(ArchitecturalRoom r, params string[] keys)
        {
            string n = r.Name ?? "";
            foreach (var k in keys) if (n.Contains(k)) return true;
            return false;
        }
        private static bool IsCorridor(ArchitecturalRoom r) { return HasName(r, "מסדרון"); }
        private static bool IsBalcony(ArchitecturalRoom r) { return HasName(r, "מרפסת"); }
        private static bool IsKitchen(ArchitecturalRoom r) { return HasName(r, "מטבח"); }
        private static bool IsWet(ArchitecturalRoom r) { return HasName(r, "שירותים", "אמבט", "רחצה", "מקלח"); }
        private static bool IsMaster(ArchitecturalRoom r) { return HasName(r, "הורים") && !IsWet(r); }
        private static bool IsPublic(ArchitecturalRoom r) { return HasName(r, "סלון", "מגורים", "מטבח", "אוכל", "שירותים"); }
        private static bool IsPrivate(ArchitecturalRoom r) { return !(IsPublic(r) || IsWet(r) || IsBalcony(r) || IsCorridor(r)); }

        // אמבטיה צמודה לחדר הורים: נפתחת רק ממנו
        private static bool IsEnsuite(ArchitecturalRoom room, List<ArchitecturalRoom> rooms)
        {
            if (!IsWet(room) || HasName(room, "שירותים")) return false;
            var baths = rooms.Where(r => IsWet(r) && !HasName(r, "שירותים")).ToList();
            var ens = baths.FirstOrDefault(r => HasName(r, "הורים")) ?? (baths.Count >= 2 ? baths[0] : null);
            return ens == room;
        }

        private ArchitecturalRoom ChooseDoorTarget(ArchitecturalRoom room, List<ArchitecturalRoom> rooms, ArchitecturalRoom living)
        {
            var neighbors = rooms.Where(r => r != room && SharesWall(room, r)).ToList();
            if (neighbors.Count == 0) return null;
            var corridor = neighbors.FirstOrDefault(r => IsCorridor(r));
            ArchitecturalRoom Pick(Func<ArchitecturalRoom, bool> f) { return neighbors.FirstOrDefault(f); }

            if (IsBalcony(room))                                   // מרפסת: רק מהסלון (או מהמטבח)
                return Pick(r => r == living) ?? Pick(r => IsKitchen(r)) ?? Pick(r => IsPublic(r)) ?? neighbors[0];

            if (IsEnsuite(room, rooms))                            // אמבטיה צמודה: רק מחדר הורים
            {
                var m = Pick(r => IsMaster(r));
                if (m != null) return m;
            }

            if (room == living)                                    // הסלון הוא המרכז: נפתח למסדרון
                return corridor ?? Pick(r => IsPublic(r)) ?? neighbors[0];

            if (IsKitchen(room))                                   // מטבח: לסלון
                return (living != null && neighbors.Contains(living)) ? living : (corridor ?? Pick(r => IsPublic(r)) ?? neighbors[0]);

            if (corridor != null) return corridor;                 // כל שאר החדרים: למסדרון
            if (living != null && neighbors.Contains(living)) return living;

            // בלי מסדרון: חדר פרטי לא נפתח לחדר פרטי אחר, ושירותים לא נפתחים למטבח
            bool wc = HasName(room, "שירותים");
            return Pick(r => !IsPrivate(r) && !IsBalcony(r) && !(wc && IsKitchen(r))) ?? neighbors[0];
        }

        private double SharedLength(ArchitecturalRoom a, ArchitecturalRoom b)
        {
            bool vertical = Math.Abs((a.PositionX + a.Width) - b.PositionX) < Tolerance || Math.Abs((b.PositionX + b.Width) - a.PositionX) < Tolerance;
            if (vertical)
                return Math.Min(a.PositionY + a.Length, b.PositionY + b.Length) - Math.Max(a.PositionY, b.PositionY);
            return Math.Min(a.PositionX + a.Width, b.PositionX + b.Width) - Math.Max(a.PositionX, b.PositionX);
        }

                // ===================== מניעת חפיפה בין חלונות לדלתות =====================
        private static bool IsHorizontalWall(OpeningPlan o) { return o.Wall == "Top" || o.Wall == "Bottom"; }

        private static bool OpeningConflict(OpeningPlan o, double center, OpeningPlan other)
        {
            if (other == o) return false;
            bool h1 = IsHorizontalWall(o), h2 = IsHorizontalWall(other);
            if (h1 != h2) return false;
            double line1 = h1 ? o.Y : o.X, line2 = h2 ? other.Y : other.X;
            if (Math.Abs(line1 - line2) > 0.35) return false;           // לא על אותו קיר
            double c2 = h2 ? other.X : other.Y;
            return Math.Abs(center - c2) < (o.Size + other.Size) / 2 + 0.25;
        }

        private void ResolveOverlaps(List<OpeningPlan> openings, List<ArchitecturalRoom> rooms)
        {
            foreach (var w in openings.Where(o => o.Type == "Window").ToList())
            {
                var room = rooms.FirstOrDefault(r => r.Name == w.RoomName);
                if (room == null) continue;

                bool h = IsHorizontalWall(w);
                double lo = (h ? room.PositionX : room.PositionY) + 0.3;
                double hi = (h ? room.PositionX + room.Width : room.PositionY + room.Length) - 0.3;
                double cur = h ? w.X : w.Y;
                if (!openings.Any(o => OpeningConflict(w, cur, o))) continue;

                bool fixedIt = false;
                for (int attempt = 0; attempt < 3 && !fixedIt; attempt++)
                {
                    foreach (double f in new[] { 0.5, 0.35, 0.65, 0.25, 0.75, 0.15, 0.85 })
                    {
                        double c = lo + (hi - lo) * f;
                        if (c - w.Size / 2 < lo || c + w.Size / 2 > hi) continue;
                        if (openings.Any(o => OpeningConflict(w, c, o))) continue;
                        if (h) w.X = c; else w.Y = c;
                        fixedIt = true;
                        break;
                    }
                    if (!fixedIt) w.Size *= 0.7;      // מקטינים את החלון ומנסים שוב
                }
                if (!fixedIt) openings.Remove(w);
            }
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