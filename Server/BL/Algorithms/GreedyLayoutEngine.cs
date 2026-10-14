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
        private const double WallThickness = 0.05; // 5 ס"מ עובי קיר פנימי במטרים

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
            var placedRooms = ZonedLayout(plotRect, requestedRooms);

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

        // ===================== פריסה לפי אזורים =====================
        private static bool Has(string n, params string[] keys)
        {
            n = n ?? "";
            foreach (var k in keys) if (n.Contains(k)) return true;
            return false;
        }

        private static bool IsWetName(string n) { return Has(n, "שירותים", "אמבט", "רחצה", "מקלח"); }

        private static double AreaOf(ArchitecturalRoom r)
        {
            if (r.Area > 0) return r.Area;
            return r.Width * r.Length > 0 ? r.Width * r.Length : 8.0;
        }

        private List<ArchitecturalRoom> ZonedLayout(BL.Algorithms.Rectangle2D plot, List<ArchitecturalRoom> rooms)
        {
            const double CorrW = 1.2;   // רוחב מסדרון
            const double BalD = 1.5;    // עומק מרפסת

            bool horiz = plot.Width >= plot.Length;
            double U = horiz ? plot.Width : plot.Length;   // הציר הארוך
            double V = horiz ? plot.Length : plot.Width;   // הציר הקצר (v=0 הוא החזית)

            void Put(ArchitecturalRoom r, double u, double v, double du, double dv)
            {
                if (horiz) { r.PositionX = plot.X + u; r.PositionY = plot.Y + v; r.Width = du; r.Length = dv; }
                else { r.PositionX = plot.X + v; r.PositionY = plot.Y + u; r.Width = dv; r.Length = du; }
                r.Area = du * dv;
            }

            var balconies = rooms.Where(r => Has(r.Name, "מרפסת")).ToList();
            var rest = rooms.Where(r => !balconies.Contains(r)).ToList();

            // אזור ציבורי: סלון (ראשון), אוכל, מטבח, שירותי אורחים
            var living = rest.Where(r => Has(r.Name, "סלון", "מגורים")).ToList();
            var dining = rest.Where(r => Has(r.Name, "אוכל") && !living.Contains(r)).ToList();
            var kitchen = rest.Where(r => Has(r.Name, "מטבח")).ToList();
            var guestWc = rest.Where(r => Has(r.Name, "שירותים")).ToList();
            var pubRow = living.Concat(dining).Concat(kitchen).Concat(guestWc).Distinct().ToList();

            // אזור פרטי: הורים, אמבטיה צמודה, שאר החדרים, אמבטיה משותפת
            var priv = rest.Where(r => !pubRow.Contains(r)).ToList();
            var master = priv.FirstOrDefault(r => Has(r.Name, "הורים") && !IsWetName(r.Name));
            var baths = priv.Where(r => IsWetName(r.Name)).ToList();
            var ensuite = baths.FirstOrDefault(r => Has(r.Name, "הורים")) ?? (baths.Count >= 2 ? baths[0] : null);
            var bedrooms = priv.Where(r => !IsWetName(r.Name) && r != master).ToList();

            var privRow = new List<ArchitecturalRoom>();
            if (master != null) privRow.Add(master);
            if (ensuite != null) privRow.Add(ensuite);
            privRow.AddRange(bedrooms);
            privRow.AddRange(baths.Where(b => b != ensuite));

            bool hasCorr = pubRow.Count > 0 && privRow.Count > 0;
            double db = balconies.Count > 0 ? BalD : 0;
            double vAvail = V - db - (hasCorr ? CorrW : 0);
            if (vAvail < 2.0 && balconies.Count > 0)      // אין מקום לרצועת מרפסת - המרפסת הופכת לחדר רגיל
            {
                pubRow.AddRange(balconies); balconies.Clear(); db = 0;
                vAvail = V - (hasCorr ? CorrW : 0);
            }

            double pubA = pubRow.Sum(r => AreaOf(r)), privA = privRow.Sum(r => AreaOf(r));
            double share = (pubA + privA) > 0 ? pubA / (pubA + privA) : 1;
            double vPub = privRow.Count == 0 ? vAvail : (pubRow.Count == 0 ? 0 : vAvail * share);
            if (hasCorr && vAvail >= 4.8) vPub = Math.Max(2.4, Math.Min(vAvail - 2.4, vPub));
            double vPriv = vAvail - vPub;

            void PlaceRow(List<ArchitecturalRoom> row, double v0, double depth)
            {
                if (row.Count == 0) return;
                depth = Math.Max(depth, 0.5);
                var w = row.Select(r => Math.Max(IsWetName(r.Name) ? 1.4 : 2.2, AreaOf(r) / depth)).ToList();
                double sum = w.Sum(), u = 0;
                for (int i = 0; i < row.Count; i++)
                {
                    double wi = w[i] * U / sum;
                    Put(row[i], u, v0, wi, depth);
                    u += wi;
                }
            }

            // מרפסת: רצועה בחזית, מתחילה מצד הסלון
            double ub = 0;
            foreach (var b in balconies)
            {
                double wb = Math.Min(AreaOf(b) / db, U - ub);
                if (wb < 0.8) wb = Math.Max(0.5, Math.Min(0.8, U - ub));
                Put(b, ub, 0, wb, db);
                ub += wb;
            }

            PlaceRow(pubRow, db, vPub);

            var result = new List<ArchitecturalRoom>();
            result.AddRange(pubRow);
            result.AddRange(balconies);

            double vNext = db + vPub;
            if (hasCorr)
            {
                var corr = new ArchitecturalRoom { Name = "מסדרון" };
                Put(corr, 0, vNext, U, CorrW);
                result.Add(corr);
                vNext += CorrW;
            }

            PlaceRow(privRow, vNext, vPriv);
            result.AddRange(privRow);
            return result;
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