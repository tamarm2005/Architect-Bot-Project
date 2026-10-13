using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

namespace BL.Algorithms
{
    // תוכנית מידות: קירות בעובי אמיתי, פתחים, שמות/שטחים/מידות נקיות לכל חדר, ושלוש שרשראות מידות בכל צד
    public class DimensionsService
    {
        private const double Off1 = 0.8;      // שרשרת ראשונה: 0.8 מ' מהקיר
        private const double OffStep = 0.6;   // מרווח בין שרשראות

        // תאימות לאחור: נתיב לקובץ DXF
        public string GenerateDimensionsPlan(string inputDxfPath)
        {
            try
            {
                var d = DxfDocument.Load(inputDxfPath);
                if (d == null) return "Error: לא ניתן לטעון את קובץ ה-DXF.";
                return GenerateDimensionsPlan(DxfUtil.FromDxf(d));
            }
            catch (Exception ex) { return $"Error: קובץ פגום או לא קריא - {ex.Message}"; }
        }

        public string GenerateDimensionsPlan(BuildingGeometry g)
        {
            if (g == null || g.Outline == null || g.Outline.Count < 3)
                return "Error: לא התקבל מתאר בניין.";

            var doc = new DxfDocument();
            var s = new Sheet(doc, 0, 0);
            var edges = DxfUtil.BuildEdges(g.Outline);
            var style = CreateStyle(doc);

            var lExt = DxfUtil.Lay(doc, "A-WALL-EXT", 7, Lineweight.W50);
            var lInt = DxfUtil.Lay(doc, "A-WALL-INT", 8);
            var lDoor = DxfUtil.Lay(doc, "A-DOOR", 1);
            var lWin = DxfUtil.Lay(doc, "A-WINDOW", 4);
            var lDim = DxfUtil.Lay(doc, "A-ANNO-DIMS", 3);
            var lTxt = DxfUtil.Lay(doc, "A-TEXT", 7);

            // 1. מעטפת הבניין (פנים הקיר החיצוני נוצר מקווי החדרים)
            for (int i = 0; i < g.Outline.Count; i++)
            {
                var a = g.Outline[i]; var b = g.Outline[(i + 1) % g.Outline.Count];
                WallLine(s, a.X, a.Y, b.X, b.Y, lExt, g.Openings);
            }

            // 2. חדרים: קירות פנימיים בעובי 10 (שני קווים), וכיתוב: שם, שטח, מידות נקיות
            var xs = new List<double> { g.MinX, g.MaxX };
            var ys = new List<double> { g.MinY, g.MaxY };
            foreach (var r in g.Rooms)
            {
                bool eL = IsExt(r.X, r.Y + r.L / 2, edges), eR = IsExt(r.X + r.W, r.Y + r.L / 2, edges);
                bool eB = IsExt(r.X + r.W / 2, r.Y, edges), eT = IsExt(r.X + r.W / 2, r.Y + r.L, edges);
                double x0 = r.X + (eL ? 0 : 0.05), x1 = r.X + r.W - (eR ? 0 : 0.05);
                double y0 = r.Y + (eB ? 0 : 0.05), y1 = r.Y + r.L - (eT ? 0 : 0.05);

                WallLine(s, x0, y0, x1, y0, lInt, g.Openings);
                WallLine(s, x1, y0, x1, y1, lInt, g.Openings);
                WallLine(s, x1, y1, x0, y1, lInt, g.Openings);
                WallLine(s, x0, y1, x0, y0, lInt, g.Openings);
                xs.Add(x0); xs.Add(x1); ys.Add(y0); ys.Add(y1);

                double cw = x1 - x0, cl = y1 - y0, cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
                s.Txt(r.Name, cx, cy + 0.2, 0.16, lTxt, TextAlignment.MiddleCenter);
                s.Txt(string.Format(CultureInfo.InvariantCulture, "{0:0.0} מ\"ר", cw * cl), cx, cy, 0.125, lTxt, TextAlignment.MiddleCenter);
                s.Txt(string.Format(CultureInfo.InvariantCulture, "{0:0.00}×{1:0.00}", cw, cl), cx, cy - 0.2, 0.125, lTxt, TextAlignment.MiddleCenter);
            }

            // 3. פתחים (דלתות וחלונות) עם תגית גודל
            foreach (var o in g.Openings) DrawOpening(s, g, edges, o, lDoor, lWin, lTxt);

            // 4. שלוש שרשראות מידות בכל צד
            var wallX = Cluster(xs, 0.04);
            var wallY = Cluster(ys, 0.04);
            Side(doc, g, edges, 'N', wallX, style, lDim);
            Side(doc, g, edges, 'S', wallX, style, lDim);
            Side(doc, g, edges, 'E', wallY, style, lDim);
            Side(doc, g, edges, 'W', wallY, style, lDim);

            // 5. כותרת וחץ צפון
            double mx = (g.MinX + g.MaxX) / 2;
            s.Txt("תוכנית מידות   1:50", mx, g.MinY - 3.4, 0.3, lTxt, TextAlignment.BottomCenter);
            s.Txt("מידות בס\"מ", mx, g.MinY - 3.9, 0.15, lTxt, TextAlignment.BottomCenter);
            double ax = g.MaxX + 3.2, ay = g.MaxY - 1.0;
            s.Ln(ax, ay, ax, ay + 1.2, lTxt);
            s.Ln(ax, ay + 1.2, ax - 0.15, ay + 0.9, lTxt);
            s.Ln(ax, ay + 1.2, ax + 0.15, ay + 0.9, lTxt);
            s.Txt("N", ax, ay + 1.35, 0.25, lTxt, TextAlignment.BottomCenter);

            HebrewFontFixer.ApplyToDoc(doc);
            string outputFileName = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Dimensions_Plan_{Guid.NewGuid()}.dxf");
            doc.Save(outputFileName);
            return outputFileName;
        }

        // ---------- קווי קיר שנחתכים במקום פתחים ----------
        private void WallLine(Sheet s, double x1, double y1, double x2, double y2, Layer l, List<OpeningInfo> ops)
        {
            bool horiz = Math.Abs(y1 - y2) < 1e-6, vert = Math.Abs(x1 - x2) < 1e-6;
            if (!horiz && !vert) { s.Ln(x1, y1, x2, y2, l); return; }

            double a0 = horiz ? Math.Min(x1, x2) : Math.Min(y1, y2);
            double a1 = horiz ? Math.Max(x1, x2) : Math.Max(y1, y2);
            double perp = horiz ? y1 : x1;

            var gaps = new List<double[]>();
            foreach (var o in ops)
            {
                double op = horiz ? o.Y : o.X, oa = horiz ? o.X : o.Y;
                if (Math.Abs(op - perp) > 0.23) continue;   // הפתח לא על קיר זה
                gaps.Add(new[] { oa - o.Width / 2, oa + o.Width / 2 });
            }

            double cur = a0;
            foreach (var gp in gaps.OrderBy(q => q[0]))
            {
                if (gp[1] <= cur || gp[0] >= a1) continue;
                if (gp[0] > cur) Seg(s, horiz, perp, cur, gp[0], l);
                cur = Math.Max(cur, gp[1]);
            }
            if (cur < a1) Seg(s, horiz, perp, cur, a1, l);
        }

        private static void Seg(Sheet s, bool horiz, double perp, double a, double b, Layer l)
        {
            if (horiz) s.Ln(a, perp, b, perp, l); else s.Ln(perp, a, perp, b, l);
        }

        private static bool IsExt(double x, double y, List<FacadeEdge> edges)
        {
            return DxfUtil.NearestEdge(new OpeningInfo { X = x, Y = y }, edges, out FacadeEdge _) < 0.35;
        }

        // ---------- ציור פתח ----------
        private void DrawOpening(Sheet s, BuildingGeometry g, List<FacadeEdge> edges, OpeningInfo o,
                                 Layer lDoor, Layer lWin, Layer lTxt)
        {
            double dist = DxfUtil.NearestEdge(o, edges, out FacadeEdge e);
            bool ext = e != null && dist < 0.6;
            bool horiz; double nx, ny, thick, sx = o.X, sy = o.Y;

            if (ext)
            {
                horiz = e.Dir == 'S' || e.Dir == 'N';
                nx = e.Dir == 'E' ? 1 : (e.Dir == 'W' ? -1 : 0);
                ny = e.Dir == 'N' ? 1 : (e.Dir == 'S' ? -1 : 0);
                thick = dist;
            }
            else
            {
                horiz = g.Rooms.Any(r => (Math.Abs(o.Y - r.Y) < 0.25 || Math.Abs(o.Y - (r.Y + r.L)) < 0.25) && o.X > r.X && o.X < r.X + r.W);
                nx = horiz ? 0 : 1; ny = horiz ? 1 : 0;
                thick = 0.1;
                sx = o.X - nx * 0.05; sy = o.Y - ny * 0.05;
            }

            double ux = horiz ? 1 : 0, uy = horiz ? 0 : 1, hw = o.Width / 2;
            var layer = o.IsDoor ? lDoor : lWin;

            // כותפות (שני קצוות הפתח)
            for (int k = -1; k <= 1; k += 2)
            {
                double bx = sx + ux * hw * k, by = sy + uy * hw * k;
                s.Ln(bx, by, bx + nx * thick, by + ny * thick, layer);
            }

            if (o.IsDoor)
            {
                // דלת: קו סגור באמצע עובי הקיר
                s.Ln(sx - ux * hw + nx * thick * 0.5, sy - uy * hw + ny * thick * 0.5,
                     sx + ux * hw + nx * thick * 0.5, sy + uy * hw + ny * thick * 0.5, layer);
            }
            else
            {
                // חלון: שלושה קווי זיגוג
                foreach (double f in new[] { 0.25, 0.5, 0.75 })
                    s.Ln(sx - ux * hw + nx * thick * f, sy - uy * hw + ny * thick * f,
                         sx + ux * hw + nx * thick * f, sy + uy * hw + ny * thick * f, layer);
            }

            // תגית: דלת 90/210, חלון רוחב/גובה ואדן
            double tx = ext ? o.X - nx * 0.45 : o.X + nx * 0.45;
            double ty = ext ? o.Y - ny * 0.45 : o.Y + ny * 0.45;
            int w = (int)Math.Round(o.Width * 100);
            if (o.IsDoor)
            {
                s.Txt($"{w}/210", tx, ty, 0.1, lTxt, TextAlignment.MiddleCenter);
            }
            else
            {
                int sill = (int)DxfUtil.SillCm(o);
                s.Txt($"{w}/{210 - sill}", tx, ty, 0.1, lTxt, TextAlignment.MiddleCenter);
                s.Txt($"אדן {sill}", tx, ty - 0.13, 0.1, lTxt, TextAlignment.MiddleCenter);
            }
        }

        // ---------- שרשראות מידות לצד אחד ----------
        private void Side(DxfDocument doc, BuildingGeometry g, List<FacadeEdge> edges, char dir,
                          List<double> walls, DimensionStyle st, Layer l)
        {
            bool horiz = dir == 'N' || dir == 'S';
            double fixedC = dir == 'N' ? g.MaxY : (dir == 'S' ? g.MinY : (dir == 'E' ? g.MaxX : g.MinX));
            double sign = (dir == 'N' || dir == 'W') ? 1 : -1;   // כיוון ההיסט כך שהמידות יוצאות החוצה
            double lo = horiz ? g.MinX : g.MinY, hi = horiz ? g.MaxX : g.MaxY;

            // שרשרת 1: פתחים בקירות הפונים לצד הזה
            var pts = new List<double> { lo, hi };
            bool any = false;
            foreach (var o in g.Openings)
            {
                double d = DxfUtil.NearestEdge(o, edges, out FacadeEdge e);
                if (e == null || d > 0.6 || e.Dir != dir) continue;
                double a = horiz ? o.X : o.Y;
                pts.Add(a - o.Width / 2); pts.Add(a + o.Width / 2);
                any = true;
            }
            if (any) Chain(doc, pts, fixedC, horiz, sign * Off1, st, l);

            // שרשרת 2: עובי קירות ורוחב נקי של החללים
            Chain(doc, walls, fixedC, horiz, sign * (Off1 + OffStep), st, l);

            // שרשרת 3: מידה כוללת
            Chain(doc, new List<double> { lo, hi }, fixedC, horiz, sign * (Off1 + 2 * OffStep), st, l);
        }

        private void Chain(DxfDocument doc, List<double> pts, double fixedC, bool horiz, double offset,
                           DimensionStyle st, Layer l)
        {
            var p = Cluster(pts, 0.04);
            for (int i = 0; i < p.Count - 1; i++)
            {
                double a = p[i], b = p[i + 1];
                if (b - a < 0.08) continue;   // לא כותבים מידה זעירה
                Vector2 v1 = horiz ? new Vector2(a, fixedC) : new Vector2(fixedC, a);
                Vector2 v2 = horiz ? new Vector2(b, fixedC) : new Vector2(fixedC, b);
                var dim = new LinearDimension(v1, v2, offset, horiz ? 0 : 90, st) { Layer = l };
                dim.UserText = ((int)Math.Round((b - a) * 100)).ToString();   // בס"מ
                doc.Entities.Add(dim);
            }
        }

        private static List<double> Cluster(IEnumerable<double> v, double tol)
        {
            var res = new List<double>();
            foreach (var x in v.OrderBy(q => q))
                if (res.Count == 0 || x - res[res.Count - 1] > tol) res.Add(x);
            return res;
        }

        private DimensionStyle CreateStyle(DxfDocument doc)
        {
            const string name = "Arch_Standard_M";
            if (doc.DimensionStyles.Contains(name)) return doc.DimensionStyles[name];
            var st = new DimensionStyle(name)
            {
                DimScaleOverall = 1.0,
                TextHeight = 0.125,    // 2.5 מ"מ על הנייר בקנה מידה 1:50
                ArrowSize = 0.1,
                TextOffset = 0.05,
                TextInsideAlign = false
            };
            doc.DimensionStyles.Add(st);
            return st;
        }

        // =====================================================================
        //  תוכנית מידות פנימיות - נוספת מעל התוכנית הקיימת באותו קובץ (הכול במטרים)
        // =====================================================================
        public void AddInteriorDimensions(DxfDocument doc, BuildingGeometry g)
        {
            if (doc == null || g == null || g.Outline == null || g.Outline.Count < 3 || g.Rooms.Count == 0) return;

            double dy = (g.MaxY - g.MinY) + 7.0;          // הסטה כלפי מעלה מעל התוכנית הקיימת
            const double TH = 0.125;                      // גובה טקסט מידות (כמו בתוכנית הקיימת)
            const double TK = 0.04;                       // חצי אורך קו אלכסוני בקצה מידה
            const double MIN_DIM = 0.08;                  // מידה קטנה מזה לא מצוירת

            var lWall = DxfUtil.Lay(doc, "A-IDIM-WALL", 7, Lineweight.W35);
            var lOpen = DxfUtil.Lay(doc, "A-IDIM-OPEN", 4);
            var lDim = DxfUtil.Lay(doc, "A-IDIM-DIM", 2);
            var lTxt = DxfUtil.Lay(doc, "A-IDIM-TEXT", 7);
            var style = DxfUtil.Style(doc);

            void Ln(double x1, double y1, double x2, double y2, Layer l)
            {
                doc.Entities.Add(new Line(new Vector2(x1, y1 + dy), new Vector2(x2, y2 + dy)) { Layer = l });
            }

            void Txt(string str, double x, double y, double h, Layer l, TextAlignment al, double rot = 0)
            {
                var t = new Text(HebrewFontFixer.Visual(str), new Vector2(x, y + dy), h) { Layer = l, Alignment = al, Rotation = rot };
                if (style != null) t.Style = style;
                doc.Entities.Add(t);
            }

            string Cm(double m) { return ((int)Math.Round(m * 100)).ToString(); }

            void Hdim(double x0, double x1, double y, double val)
            {
                if (val < MIN_DIM) return;
                Ln(x0, y, x1, y, lDim);
                Ln(x0 - TK, y - TK, x0 + TK, y + TK, lDim);
                Ln(x1 - TK, y - TK, x1 + TK, y + TK, lDim);
                Txt(Cm(val), (x0 + x1) / 2, y + 0.03, TH, lTxt, TextAlignment.BottomCenter);
            }

            void Vdim(double x, double y0, double y1, double val)
            {
                if (val < MIN_DIM) return;
                Ln(x, y0, x, y1, lDim);
                Ln(x - TK, y0 - TK, x + TK, y0 + TK, lDim);
                Ln(x - TK, y1 - TK, x + TK, y1 + TK, lDim);
                Txt(Cm(val), x - 0.03, (y0 + y1) / 2, TH, lTxt, TextAlignment.BottomCenter, 90);
            }

            // ---------- מתאר חיצוני ----------
            for (int i = 0; i < g.Outline.Count; i++)
            {
                var a = g.Outline[i]; var b = g.Outline[(i + 1) % g.Outline.Count];
                Ln(a.X, a.Y, b.X, b.Y, lWall);
            }

            // גבולות כל החדרים (כדי לדעת איזה צד חדר הוא קיר חיצוני)
            double rMinX = double.MaxValue, rMaxX = double.MinValue, rMinY = double.MaxValue, rMaxY = double.MinValue;
            foreach (var r in g.Rooms)
            {
                rMinX = Math.Min(rMinX, r.X); rMaxX = Math.Max(rMaxX, r.X + r.W);
                rMinY = Math.Min(rMinY, r.Y); rMaxY = Math.Max(rMaxY, r.Y + r.L);
            }
            const double HALF = 0.05;      // חצי עובי מחיצה פנימית (5 ס"מ מכל צד)
            const double TOL = 0.03;

            // מלבן נקי (פנים קירות) לכל חדר
            var clear = new System.Collections.Generic.Dictionary<RoomInfo, double[]>();
            foreach (var r in g.Rooms)
            {
                double x0 = r.X + (Math.Abs(r.X - rMinX) < TOL ? 0 : HALF);
                double x1 = r.X + r.W - (Math.Abs(r.X + r.W - rMaxX) < TOL ? 0 : HALF);
                double y0 = r.Y + (Math.Abs(r.Y - rMinY) < TOL ? 0 : HALF);
                double y1 = r.Y + r.L - (Math.Abs(r.Y + r.L - rMaxY) < TOL ? 0 : HALF);
                clear[r] = new[] { x0, y0, x1, y1 };

                Ln(x0, y0, x1, y0, lWall); Ln(x1, y0, x1, y1, lWall);
                Ln(x1, y1, x0, y1, lWall); Ln(x0, y1, x0, y0, lWall);

                double cw = x1 - x0, ch = y1 - y0;
                Txt(r.Name ?? "", (x0 + x1) / 2, y0 + ch * 0.72, 0.16, lTxt, TextAlignment.MiddleCenter);
                Hdim(x0, x1, y0 + ch * 0.45, cw);                  // רוחב נקי
                Vdim(x0 + cw * 0.3, y0, y1, ch);                   // אורך נקי
            }

            // ---------- פתחים: סימון + שרשרת מרחקים מהפינות ----------
            foreach (var o in g.Openings)
            {
                // החדר של הפתח (לפי שם, ואם אין - הקרוב ביותר)
                RoomInfo best = null; double bd = double.MaxValue; int bside = 0;
                foreach (var r in g.Rooms)
                {
                    if (!string.IsNullOrEmpty(o.RoomName) && r.Name != o.RoomName) continue;
                    double[] ds = { Math.Abs(o.Y - r.Y), Math.Abs(o.Y - (r.Y + r.L)), Math.Abs(o.X - r.X), Math.Abs(o.X - (r.X + r.W)) };
                    for (int k = 0; k < 4; k++) if (ds[k] < bd) { bd = ds[k]; best = r; bside = k; }
                }
                if (best == null || bd > 0.6) continue;

                var c = clear[best];
                double cx0 = c[0], cy0 = c[1], cx1 = c[2], cy1 = c[3];
                double w = o.Width;

                if (bside == 0 || bside == 1)    // קיר אופקי (תחתון/עליון)
                {
                    bool bottom = bside == 0;
                    double wallY = bottom ? best.Y : best.Y + best.L;
                    double clearY = bottom ? cy0 : cy1;
                    double xa = Math.Max(cx0, o.X - w / 2), xb = Math.Min(cx1, o.X + w / 2);
                    if (xb - xa < MIN_DIM) continue;

                    Ln(xa, wallY - 0.1, xa, wallY + 0.1, lOpen);
                    Ln(xb, wallY - 0.1, xb, wallY + 0.1, lOpen);
                    if (o.IsDoor) Ln(xa, clearY, xb, clearY, lOpen);
                    else { Ln(xa, wallY - 0.04, xb, wallY - 0.04, lOpen); Ln(xa, wallY, xb, wallY, lOpen); Ln(xa, wallY + 0.04, xb, wallY + 0.04, lOpen); }

                    double yd = clearY + (bottom ? 0.3 : -0.3);
                    Hdim(cx0, xa, yd, xa - cx0);
                    Hdim(xa, xb, yd, xb - xa);
                    Hdim(xb, cx1, yd, cx1 - xb);
                }
                else                              // קיר אנכי (שמאלי/ימני)
                {
                    bool left = bside == 2;
                    double wallX = left ? best.X : best.X + best.W;
                    double clearX = left ? cx0 : cx1;
                    double ya = Math.Max(cy0, o.Y - w / 2), yb = Math.Min(cy1, o.Y + w / 2);
                    if (yb - ya < MIN_DIM) continue;

                    Ln(wallX - 0.1, ya, wallX + 0.1, ya, lOpen);
                    Ln(wallX - 0.1, yb, wallX + 0.1, yb, lOpen);
                    if (o.IsDoor) Ln(clearX, ya, clearX, yb, lOpen);
                    else { Ln(wallX - 0.04, ya, wallX - 0.04, yb, lOpen); Ln(wallX, ya, wallX, yb, lOpen); Ln(wallX + 0.04, ya, wallX + 0.04, yb, lOpen); }

                    double xd = clearX + (left ? 0.3 : -0.3);
                    Vdim(xd, cy0, ya, ya - cy0);
                    Vdim(xd, ya, yb, yb - ya);
                    Vdim(xd, yb, cy1, cy1 - yb);
                }
            }

            // ---------- כותרת, חץ צפון, הערה ----------
            double midX = (g.MinX + g.MaxX) / 2;
            Txt("תוכנית מידות פנימיות 1:50", midX, g.MaxY + 1.2, 0.35, lTxt, TextAlignment.BottomCenter);
            Txt("מידות בס\"מ, מידות נקיות בין פני קירות", midX, g.MaxY + 0.8, 0.16, lTxt, TextAlignment.BottomCenter);

            double nx = g.MaxX + 1.2, ny = g.MaxY + 0.8;
            doc.Entities.Add(new Circle(new Vector2(nx, ny + dy), 0.3) { Layer = lWall });
            Ln(nx, ny - 0.3, nx, ny + 0.3, lWall);
            Ln(nx, ny + 0.3, nx - 0.1, ny + 0.05, lWall);
            Ln(nx, ny + 0.3, nx + 0.1, ny + 0.05, lWall);
            Txt("N", nx, ny + 0.38, 0.18, lTxt, TextAlignment.BottomCenter);
        }
    }
}