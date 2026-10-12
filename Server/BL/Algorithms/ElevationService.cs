using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

namespace BL.Algorithms
{
    // ===================== מודל נתונים משותף (הכול במטרים, כמו תוכנית הריהוט) =====================
    public class OpeningInfo
    {
        public double X;               // מרכז הפתח (מוחלט)
        public double Y;
        public double Width = 0.9;     // רוחב
        public bool IsDoor;
        public string RoomName = "";
    }

    public class FurnInfo
    {
        public string Name = "";
        public double X0, Y0, X1, Y1;  // מלבן מוחלט
    }

    public class RoomInfo
    {
        public string Name = "";
        public double X, Y, W, L;
        public List<FurnInfo> Furniture = new List<FurnInfo>();
    }

    public class BuildingGeometry
    {
        public List<Vector2> Outline = new List<Vector2>();   // מתאר הקיר החיצוני
        public List<RoomInfo> Rooms = new List<RoomInfo>();
        public List<OpeningInfo> Openings = new List<OpeningInfo>();

        public double MinX => Outline.Min(p => p.X);
        public double MaxX => Outline.Max(p => p.X);
        public double MinY => Outline.Min(p => p.Y);
        public double MaxY => Outline.Max(p => p.Y);
    }

    internal class FacadeEdge
    {
        public Vector2 A;
        public Vector2 B;
        public char Dir;   // כיוון החזית שהקיר פונה אליו: S N E W
    }

    // ===================== כלי עזר משותפים =====================
    internal static class DxfUtil
    {
        public const double K = 100.0;  // מטר -> ס"מ

        public static Layer Lay(DxfDocument doc, string name, short aci, Lineweight lw = Lineweight.Default)
        {
            if (doc.Layers.Contains(name)) return doc.Layers[name];
            var l = new Layer(name) { Color = new AciColor(aci), Lineweight = lw };
            doc.Layers.Add(l);
            return l;
        }

        public static TextStyle Style(DxfDocument d)
        {
            try
            {
                if (d.TextStyles.Contains("HebArial")) return d.TextStyles["HebArial"];
                var s = new TextStyle("HebArial", "arial.ttf");
                d.TextStyles.Add(s);
                return s;
            }
            catch { return null; }
        }

        public static string Lvl(double m)
        {
            if (Math.Abs(m) < 0.005) return "±0.00";
            return (m > 0 ? "+" : "-") + Math.Abs(m).ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static bool IsWet(string n) { n = n ?? ""; return n.Contains("שירותים") || n.Contains("אמבט") || n.Contains("מקלח") || n.Contains("רחצה") || n.Contains("כביסה") || n.Contains("WC"); }
        public static bool IsLiving(string n) { return (n ?? "").Contains("סלון"); }
        public static bool IsBedroom(string n) { n = n ?? ""; return n.Contains("שינה") || n.Contains("הורים") || n.Contains("ילד"); }

        // גובה אדן: חדר רטוב גבוה, חלון רחב (ויטרינה) נמוך, אחרת רגיל. ראש כל הפתחים ב-210.
        public const double HeadCm = 210.0;
        public static double SillCm(OpeningInfo o)
        {
            if (IsWet(o.RoomName)) return 150;
            if (o.Width >= 1.8) return 45;
            return 90;
        }

        public static double FurnHeight(string n)
        {
            n = n ?? "";
            if (n.Contains("מקרר")) return 180;
            if (n.Contains("מטבח") || n.Contains("כיור") || n.Contains("כיריים") || n.Contains("תנור")) return 90;
            if (n.Contains("ארון")) return 220;
            if (n.Contains("מיטה") || n.Contains("מזרן")) return 50;
            if (n.Contains("ספה")) return 85;
            if (n.Contains("שולחן")) return 75;
            if (n.Contains("כיסא")) return 90;
            if (n.Contains("אסלה")) return 80;
            if (n.Contains("אמבט")) return 55;
            if (n.Contains("מקלחון") || n.Contains("מקלחת")) return 200;
            if (n.Contains("מכונת")) return 85;
            if (n.Contains("שידה") || n.Contains("מזנון") || n.Contains("טלוויזיה")) return 55;
            return 75;
        }

        // תאימות לאחור: בניית מודל מקובץ DXF (קירות וחדרים בלבד, בלי פתחים וריהוט)
        public static BuildingGeometry FromDxf(DxfDocument d)
        {
            var g = new BuildingGeometry();
            var ext = d.Entities.Lines.Where(l => l.Layer != null && l.Layer.Name == "Exterior_Walls").ToList();
            if (ext.Count >= 3)
            {
                foreach (var l in ext) g.Outline.Add(new Vector2(l.StartPoint.X, l.StartPoint.Y));
            }
            else
            {
                var all = d.Entities.Lines.ToList();
                if (all.Count == 0) return g;
                double x0 = all.Min(l => Math.Min(l.StartPoint.X, l.EndPoint.X));
                double x1 = all.Max(l => Math.Max(l.StartPoint.X, l.EndPoint.X));
                double y0 = all.Min(l => Math.Min(l.StartPoint.Y, l.EndPoint.Y));
                double y1 = all.Max(l => Math.Max(l.StartPoint.Y, l.EndPoint.Y));
                g.Outline.Add(new Vector2(x0, y0)); g.Outline.Add(new Vector2(x1, y0));
                g.Outline.Add(new Vector2(x1, y1)); g.Outline.Add(new Vector2(x0, y1));
            }

            foreach (var pl in d.Entities.Polylines2D.Where(p => p.Layer != null && p.Layer.Name == "A-WALL-INT"))
            {
                if (pl.Vertexes.Count == 0) continue;
                double rx0 = pl.Vertexes.Min(v => v.Position.X), rx1 = pl.Vertexes.Max(v => v.Position.X);
                double ry0 = pl.Vertexes.Min(v => v.Position.Y), ry1 = pl.Vertexes.Max(v => v.Position.Y);
                var r = new RoomInfo { Name = "חדר", X = rx0, Y = ry0, W = rx1 - rx0, L = ry1 - ry0 };
                var t = d.Entities.Texts.FirstOrDefault(tx => tx.Height > 0.2 &&
                        tx.Position.X >= rx0 && tx.Position.X <= rx1 && tx.Position.Y >= ry0 && tx.Position.Y <= ry1);
                if (t != null && !string.IsNullOrWhiteSpace(t.Value)) r.Name = t.Value;
                g.Rooms.Add(r);
            }
            return g;
        }

        // קירות המתאר וכיוון החזית שכל קיר פונה אליו
        public static List<FacadeEdge> BuildEdges(List<Vector2> poly)
        {
            var res = new List<FacadeEdge>();
            double area = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                var p = poly[i]; var q = poly[(i + 1) % poly.Count];
                area += p.X * q.Y - q.X * p.Y;
            }
            double sign = area >= 0 ? 1 : -1;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                double dx = b.X - a.X, dy = b.Y - a.Y;
                if (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6) continue;
                double nx = dy * sign, ny = -dx * sign;   // נורמל החוצה
                char dir = Math.Abs(nx) > Math.Abs(ny) ? (nx > 0 ? 'E' : 'W') : (ny > 0 ? 'N' : 'S');
                res.Add(new FacadeEdge { A = a, B = b, Dir = dir });
            }
            return res;
        }

        static double PointSeg(double px, double py, Vector2 a, Vector2 b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
            double t = l2 < 1e-12 ? 0 : ((px - a.X) * dx + (py - a.Y) * dy) / l2;
            t = Math.Max(0, Math.Min(1, t));
            double cx = a.X + t * dx, cy = a.Y + t * dy;
            return Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
        }

        public static double NearestEdge(OpeningInfo o, List<FacadeEdge> edges, out FacadeEdge best)
        {
            best = null; double bd = double.MaxValue;
            foreach (var e in edges)
            {
                double d = PointSeg(o.X, o.Y, e.A, e.B);
                if (d < bd) { bd = d; best = e; }
            }
            return bd;
        }

        // מיקום לאורך החזית (משמאל לימין כשמסתכלים מבחוץ) ועומק (0 = הכי קרוב)
        public static double U(char d, double x, double y, BuildingGeometry g)
        {
            switch (d)
            {
                case 'S': return x - g.MinX;
                case 'N': return g.MaxX - x;
                case 'E': return y - g.MinY;
                default: return g.MaxY - y;
            }
        }
        public static double Depth(char d, double x, double y, BuildingGeometry g)
        {
            switch (d)
            {
                case 'S': return y - g.MinY;
                case 'N': return g.MaxY - y;
                case 'E': return g.MaxX - x;
                default: return x - g.MinX;
            }
        }

        // דלת הכניסה: דלת על קיר חיצוני, עדיפות לסלון / חדר כניסה
        public static OpeningInfo FindEntrance(BuildingGeometry g, List<FacadeEdge> edges)
        {
            var ext = g.Openings.Where(o => o.IsDoor && NearestEdge(o, edges, out _) < 0.6).ToList();
            return ext.FirstOrDefault(o => IsLiving(o.RoomName) || (o.RoomName ?? "").Contains("כניסה")) ?? ext.FirstOrDefault();
        }
    }

    // "דף שרטוט" עם נקודת התחלה: כל הציור יחסי אליה
    internal class Sheet
    {
        public readonly DxfDocument Doc;
        public readonly double OX, OY;
        public Sheet(DxfDocument doc, double ox, double oy) { Doc = doc; OX = ox; OY = oy; }

        public void Ln(double x1, double y1, double x2, double y2, Layer l)
        {
            Doc.Entities.Add(new Line(new Vector2(OX + x1, OY + y1), new Vector2(OX + x2, OY + y2)) { Layer = l });
        }

        public void Rect(double x0, double y0, double x1, double y1, Layer l)
        {
            Ln(x0, y0, x1, y0, l); Ln(x1, y0, x1, y1, l); Ln(x1, y1, x0, y1, l); Ln(x0, y1, x0, y0, l);
        }

        // קיווי אלכסוני (פוצ'ה) בתוך מלבן
        public void Poche(double x0, double y0, double x1, double y1, double step, Layer l)
        {
            for (double c = x0 + y0 + step; c < x1 + y1; c += step)
            {
                double yLo = Math.Max(y0, c - x1), yHi = Math.Min(y1, c - x0);
                if (yHi - yLo > 0.5) Ln(c - yLo, yLo, c - yHi, yHi, l);
            }
        }

        public void Txt(string str, double x, double y, double h, Layer l, TextAlignment al = TextAlignment.BaselineLeft)
        {
            var t = new Text(HebrewFontFixer.Visual(str), new Vector2(OX + x, OY + y), h) { Layer = l, Alignment = al };
            var st = DxfUtil.Style(Doc);
            if (st != null) t.Style = st;
            Doc.Entities.Add(t);
        }

        public void LevelMark(double xl, double y, string label, Layer line, Layer txt)
        {
            Ln(xl, y, xl + 110, y, line);
            Ln(xl + 20, y, xl + 12, y + 12, line);
            Ln(xl + 20, y, xl + 28, y + 12, line);
            Ln(xl + 12, y + 12, xl + 28, y + 12, line);
            Txt(label, xl + 34, y + 3, 10, txt);
        }

        public void DimH(double x0, double x1, double y, string text, Layer l, Layer txt, double h = 9)
        {
            Ln(x0, y, x1, y, l);
            Ln(x0 - 4, y - 4, x0 + 4, y + 4, l);
            Ln(x1 - 4, y - 4, x1 + 4, y + 4, l);
            Txt(text, (x0 + x1) / 2, y + 3, h, txt, TextAlignment.BottomCenter);
        }

        public void DimV(double x, double y0, double y1, string text, Layer l, Layer txt, double h = 9)
        {
            Ln(x, y0, x, y1, l);
            Ln(x - 4, y0 - 4, x + 4, y0 + 4, l);
            Ln(x - 4, y1 - 4, x + 4, y1 + 4, l);
            Txt(text, x - 6, (y0 + y1) / 2, h, txt, TextAlignment.MiddleRight);
        }
    }

    // מחליף את סגנון כל הטקסטים בקובץ ל-Arial כדי שעברית תוצג
    public static class HebrewFontFixer
    {
        private static bool IsHeb(char c) { return c >= '\u0590' && c <= '\u05FF'; }

        // AutoCAD (בגרסה עברית) מסדר כל תו מימין לשמאל, כולל ספרות ולטינית.
        // לכן הופכים כל קטע שאינו עברית או רווח, ומשאירים את העברית ואת סדר המילים.
        public static string Visual(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < s.Length)
            {
                char ch = s[i];
                if (IsHeb(ch) || char.IsWhiteSpace(ch)) { sb.Append(ch); i++; continue; }
                int j = i;
                while (j < s.Length && !IsHeb(s[j]) && !char.IsWhiteSpace(s[j])) j++;
                var run = s.Substring(i, j - i).ToCharArray();
                Array.Reverse(run);
                sb.Append(run);
                i = j;
            }
            return sb.ToString();
        }

        // לא מסמן פעמיים: טקסט שכבר בסגנון HebArial מדולג
        public static void ApplyToDoc(DxfDocument doc)
        {
            TextStyle style;
            if (doc.TextStyles.Contains("HebArial")) style = doc.TextStyles["HebArial"];
            else { style = new TextStyle("HebArial", "arial.ttf"); doc.TextStyles.Add(style); }

            foreach (var t in doc.Entities.Texts)
            {
                if (t.Style != null && t.Style.Name == "HebArial") continue;
                t.Value = Visual(t.Value);
                t.Style = style;
            }
            foreach (var m in doc.Entities.MTexts)
            {
                if (m.Style != null && m.Style.Name == "HebArial") continue;
                m.Style = style;
            }
        }

        public static void Apply(string path)
        {
            var doc = DxfDocument.Load(path);
            if (doc == null) return;
            ApplyToDoc(doc);
            doc.Save(path);
        }
    }

    // ===================== חזיתות =====================
    public class ElevationService
    {
        private const double K = DxfUtil.K;
        private const double FloorAbove = 15;   // רצפה גמורה מעל פני הקרקע
        private const double ClearH = 260;      // גובה נקי עד תקרה
        private const double SlabT = 20;        // עובי תקרה
        private const double ParapetH = 60;     // גובה מעקה גג

        // תאימות לאחור: קבלת נתיב לקובץ DXF
        public string GenerateElevationsPlan(string inputDxfPath)
        {
            return GenerateElevationsPlan(DxfUtil.FromDxf(DxfDocument.Load(inputDxfPath)));
        }

        public string GenerateElevationsPlan(BuildingGeometry g)
        {
            if (g == null || g.Outline == null || g.Outline.Count < 3)
                return "Error: לא התקבל מתאר בניין.";

            var doc = new DxfDocument();
            var edges = DxfUtil.BuildEdges(g.Outline);
            var entrance = DxfUtil.FindEntrance(g, edges);

            double wNS = (g.MaxX - g.MinX) * K, wEW = (g.MaxY - g.MinY) * K;
            double col1X = Math.Max(wNS, wEW) + 700;
            double rowY = -(FloorAbove + ClearH + SlabT + ParapetH + 900);

            Facade(doc, g, edges, entrance, 'S', 0, 0, "חזית דרומית");
            Facade(doc, g, edges, entrance, 'N', col1X, 0, "חזית צפונית");
            Facade(doc, g, edges, entrance, 'E', 0, rowY, "חזית מזרחית");
            Facade(doc, g, edges, entrance, 'W', col1X, rowY, "חזית מערבית");

            string outputPath = Path.Combine(Path.GetTempPath(), $"Arch_Elevations_{Guid.NewGuid()}.dxf");
            doc.Save(outputPath);
            return outputPath;
        }

        private void Facade(DxfDocument doc, BuildingGeometry g, List<FacadeEdge> edges, OpeningInfo entrance,
                            char dir, double ox, double oy, string title)
        {
            var segs = edges.Where(e => e.Dir == dir).ToList();
            if (segs.Count == 0) return;

            var s = new Sheet(doc, ox, oy);
            double fl = FloorAbove;
            double wallTop = fl + ClearH + SlabT;
            double top = wallTop + ParapetH;
            double W = (dir == 'S' || dir == 'N') ? (g.MaxX - g.MinX) * K : (g.MaxY - g.MinY) * K;

            var lWall = DxfUtil.Lay(doc, "A-ELEV-WALL", 7, Lineweight.W50);
            var lFar = DxfUtil.Lay(doc, "A-ELEV-FAR", 8);
            var lGround = DxfUtil.Lay(doc, "A-ELEV-GROUND", 3, Lineweight.W50);
            var lWin = DxfUtil.Lay(doc, "A-ELEV-WIND", 4);
            var lDoor = DxfUtil.Lay(doc, "A-ELEV-DOOR", 1);
            var lDim = DxfUtil.Lay(doc, "A-ELEV-DIM", 2);
            var lTxt = DxfUtil.Lay(doc, "A-TEXT", 7);

            // 1. קירות החזית (הקרובים עבים, הרחוקים דקים - לבניין שאינו מלבן)
            var items = segs.Select(e =>
            {
                double u0 = DxfUtil.U(dir, e.A.X, e.A.Y, g) * K, u1 = DxfUtil.U(dir, e.B.X, e.B.Y, g) * K;
                double d = DxfUtil.Depth(dir, (e.A.X + e.B.X) / 2, (e.A.Y + e.B.Y) / 2, g) * K;
                return new { U0 = Math.Min(u0, u1), U1 = Math.Max(u0, u1), D = d };
            }).ToList();
            double front = items.Min(i => i.D);
            foreach (var it in items.OrderByDescending(i => i.D))
            {
                var l = it.D > front + 5 ? lFar : lWall;
                s.Ln(it.U0, 0, it.U0, top, l);
                s.Ln(it.U1, 0, it.U1, top, l);
                s.Ln(it.U0, top, it.U1, top, l);        // ראש המעקה
                s.Ln(it.U0, wallTop, it.U1, wallTop, l); // קו תקרה / בסיס מעקה
            }

            // 2. קרקע
            s.Ln(-150, 0, W + 150, 0, lGround);
            s.Poche(-150, -35, 0, 0, 25, lGround);
            s.Poche(W, -35, W + 150, 0, 25, lGround);

            // 3. פתחים אמיתיים מהרשימה (רק אלה שעל קיר חיצוני הפונה לחזית הזו)
            var cuts = new List<double> { 0, W };
            foreach (var o in g.Openings)
            {
                double dist = DxfUtil.NearestEdge(o, edges, out FacadeEdge e);
                if (e == null || dist > 0.6 || e.Dir != dir) continue;
                double u = DxfUtil.U(dir, o.X, o.Y, g) * K, w = o.Width * K;
                if (u - w / 2 < 0 || u + w / 2 > W) continue;
                if (o.IsDoor) DrawDoor(s, u, w, fl, ReferenceEquals(o, entrance), lDoor);
                else DrawWindow(s, u, w, fl, DxfUtil.SillCm(o), lWin);
                cuts.Add(u - w / 2); cuts.Add(u + w / 2);
            }

            // 4. מפלסים
            double xl = -140;
            s.LevelMark(xl, 0, DxfUtil.Lvl(-fl / K), lDim, lTxt);
            s.LevelMark(xl, fl, DxfUtil.Lvl(0), lDim, lTxt);
            s.LevelMark(xl, wallTop, DxfUtil.Lvl((wallTop - fl) / K), lDim, lTxt);
            s.LevelMark(xl, top, DxfUtil.Lvl((top - fl) / K), lDim, lTxt);

            // 5. שרשרת מידות אופקית + מידה כוללת + כותרת
            cuts = cuts.OrderBy(c => c).ToList();
            for (int i = 0; i < cuts.Count - 1; i++)
                if (cuts[i + 1] - cuts[i] >= 8)
                    s.DimH(cuts[i], cuts[i + 1], -70, ((int)Math.Round(cuts[i + 1] - cuts[i])).ToString(), lDim, lTxt);
            s.DimH(0, W, -120, (W / K).ToString("0.00", CultureInfo.InvariantCulture), lDim, lTxt, 12);
            s.Txt(title + "   1:100", W / 2, -190, 28, lTxt, TextAlignment.BottomCenter);
        }

        private void DrawWindow(Sheet s, double u, double w, double fl, double sill, Layer l)
        {
            double x0 = u - w / 2, x1 = u + w / 2, y0 = fl + sill, y1 = fl + DxfUtil.HeadCm;
            s.Rect(x0, y0, x1, y1, l);
            s.Rect(x0 + 5, y0 + 5, x1 - 5, y1 - 5, l);
            int n = Math.Max(1, (int)Math.Round(w / 80.0));
            double sw = (w - 10) / n, my = (y0 + y1) / 2;
            for (int i = 0; i < n; i++)
            {
                double sx0 = x0 + 5 + i * sw, sx1 = sx0 + sw;
                if (i > 0) s.Ln(sx0, y0 + 5, sx0, y1 - 5, l);
                bool hingeLeft = i % 2 == 0;
                double hx = hingeLeft ? sx0 : sx1, tx = hingeLeft ? sx1 : sx0;
                s.Ln(hx, y0 + 5, tx, my, l);   // חיווי כיוון פתיחה
                s.Ln(hx, y1 - 5, tx, my, l);
            }
            s.Ln(x0 - 5, y0 - 3, x1 + 5, y0 - 3, l);   // אדן
        }

        private void DrawDoor(Sheet s, double u, double w, double fl, bool entrance, Layer l)
        {
            double x0 = u - w / 2, x1 = u + w / 2, y0 = fl, y1 = fl + DxfUtil.HeadCm;
            s.Rect(x0, y0, x1, y1, l);
            s.Rect(x0 + 6, y0, x1 - 6, y1 - 6, l);
            s.Ln(x1 - 22, y0 + 105, x1 - 10, y0 + 105, l);   // ידית
            if (entrance)
            {
                s.Rect(x0 + 14, y0 + 12, x1 - 14, y1 - 18, l); // פאנל לדלת כניסה
                s.Rect(x0 - 20, 0, x1 + 20, fl, l);            // מפתן
            }
        }
    }
}