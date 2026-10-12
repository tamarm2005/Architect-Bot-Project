using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

namespace BL.Algorithms
{
    public class SectionGenerator
    {
        private const double K = DxfUtil.K;
        private const double FloorT = 20;      // רצפה
        private const double ClearH = 260;     // גובה נקי
        private const double SlabT = 20;       // תקרה
        private const double ParapetH = 60;
        private const double FootDepth = 80;   // עומק יסוד
        private const double FloorAbove = 15;  // רצפה מעל פני קרקע

        private class Cut { public bool H; public double C; public double Score; }
        private class Wall { public double P, Q, A, B; public bool Ext; public OpeningInfo Op; }

        // תאימות לאחור: קבלת DxfDocument
        public Dictionary<string, DxfDocument> GenerateAutoSections(DxfDocument planDxf)
        {
            return GenerateAutoSections(DxfUtil.FromDxf(planDxf));
        }

        // מחזיר את שני החתכים: AA (אופקי, מבט צפונה) ו-BB (אנכי, מבט מזרחה)
        public Dictionary<string, DxfDocument> GenerateAutoSections(BuildingGeometry g)
        {
            var res = new Dictionary<string, DxfDocument>();
            if (g == null || g.Rooms.Count == 0 || g.Outline.Count < 3) return res;

            var a = ChooseCut(g, true);
            var b = ChooseCut(g, false);
            if (a != null) res["Section_AA"] = Build(g, a, "A-A");
            if (b != null) res["Section_BB"] = Build(g, b, "B-B");
            return res;
        }

        // ---------- בחירת קו החתך החכם ----------
        private Cut ChooseCut(BuildingGeometry g, bool h)
        {
            double lo = h ? g.MinY : g.MinX, hi = h ? g.MaxY : g.MaxX, mid = (lo + hi) / 2;
            Cut best = null;
            for (double c = lo + 0.5; c < hi - 0.5; c += 0.1)
            {
                double s = Score(g, h, c);
                if (s < -1e8) continue;
                s -= Math.Abs(c - mid) * 0.15;
                if (best == null || s > best.Score) best = new Cut { H = h, C = c, Score = s };
            }
            return best;
        }

        private double Score(BuildingGeometry g, bool h, double c)
        {
            // לא עוברים על קיר מקביל (זה ייתן "קיר שלם" ולא חתך)
            foreach (var r in g.Rooms)
            {
                double e0 = h ? r.Y : r.X, e1 = h ? r.Y + r.L : r.X + r.W;
                if (Math.Abs(c - e0) < 0.3 || Math.Abs(c - e1) < 0.3) return -1e9;
            }
            var crossed = g.Rooms.Where(r => h ? (r.Y < c && c < r.Y + r.L) : (r.X < c && c < r.X + r.W)).ToList();
            if (crossed.Count == 0) return -1e9;

            double s = crossed.Count;
            if (crossed.Any(r => DxfUtil.IsLiving(r.Name))) s += 4;
            if (crossed.Any(r => DxfUtil.IsWet(r.Name))) s += 3;
            if (crossed.Any(r => DxfUtil.IsBedroom(r.Name))) s += 1.5;

            double open = 0;
            foreach (var o in g.Openings)
            {
                double along = h ? o.X : o.Y, perp = h ? o.Y : o.X;
                if (Math.Abs(perp - c) > o.Width / 2 - 0.05) continue;
                bool onWall = g.Rooms.Any(r => Math.Abs(along - (h ? r.X : r.Y)) < 0.35 ||
                                               Math.Abs(along - (h ? r.X + r.W : r.Y + r.L)) < 0.35);
                if (onWall) open += o.IsDoor ? 3 : 2.5;
            }
            s += Math.Min(open, 10);

            double furn = 0;
            foreach (var r in crossed)
                foreach (var f in r.Furniture)
                    if (h ? (f.Y0 < c && c < f.Y1) : (f.X0 < c && c < f.X1)) furn += 0.4;
            s += Math.Min(furn, 4);
            return s;
        }

        // ---------- בניית החתך ----------
        private DxfDocument Build(BuildingGeometry g, Cut cut, string label)
        {
            var doc = new DxfDocument();
            var s = new Sheet(doc, 0, 0);
            var lCut = DxfUtil.Lay(doc, "A-SECT-CUT", 7, Lineweight.W50);
            var lHatch = DxfUtil.Lay(doc, "A-SECT-HATCH", 8);
            var lBeyond = DxfUtil.Lay(doc, "A-SECT-BEYOND", 8);
            var lFurn = DxfUtil.Lay(doc, "A-SECT-FURN", 6);
            var lFurnCut = DxfUtil.Lay(doc, "A-SECT-FURN-CUT", 1);
            var lGround = DxfUtil.Lay(doc, "A-SECT-GROUND", 3, Lineweight.W50);
            var lWin = DxfUtil.Lay(doc, "A-SECT-GLASS", 4);
            var lDim = DxfUtil.Lay(doc, "A-SECT-DIM", 2);
            var lTxt = DxfUtil.Lay(doc, "A-TEXT", 7);

            bool h = cut.H; double c = cut.C;
            double origin = h ? g.MinX : g.MinY, end = h ? g.MaxX : g.MaxY;
            double Ltot = (end - origin) * K;
            double slabTop = ClearH + SlabT, top = slabTop + ParapetH;

            // אופקי: משמאל (מערב) לימין (מזרח). אנכי: מבט מזרחה, אז הצפון משמאל.
            double T(double v) => h ? (v - origin) * K : (end - v) * K;
            double A0(RoomInfo r) => h ? r.X : r.Y;
            double A1(RoomInfo r) => h ? r.X + r.W : r.Y + r.L;
            double Along(OpeningInfo o) => h ? o.X : o.Y;
            double Perp(OpeningInfo o) => h ? o.Y : o.X;

            var crossed = g.Rooms.Where(r => h ? (r.Y < c && c < r.Y + r.L) : (r.X < c && c < r.X + r.W))
                                 .OrderBy(r => A0(r)).ToList();

            // 1. קירות נחתכים: מאחדים קצוות חדרים סמוכים לקיר אחד
            var raw = new List<double>();
            foreach (var r in crossed) { raw.Add(A0(r)); raw.Add(A1(r)); }
            raw.Sort();
            var pos = new List<double>();
            foreach (var v in raw)
            {
                if (pos.Count > 0 && v - pos[pos.Count - 1] < 0.12) pos[pos.Count - 1] = (pos[pos.Count - 1] + v) / 2;
                else pos.Add(v);
            }

            var walls = new List<Wall>();
            foreach (var p in pos)
            {
                double q = T(p);
                var w = new Wall { P = p, Q = q };
                if (q < 45) { w.Ext = true; double th = Math.Clamp(q, 10, 35); w.A = q - th; w.B = q; }
                else if (Ltot - q < 45) { w.Ext = true; double th = Math.Clamp(Ltot - q, 10, 35); w.A = q; w.B = q + th; }
                else { w.A = q - 5; w.B = q + 5; }
                double pp = p;
                w.Op = g.Openings.Where(o => Math.Abs(Along(o) - pp) < 0.35 && Math.Abs(Perp(o) - c) < o.Width / 2)
                                 .OrderBy(o => Math.Abs(Perp(o) - c)).FirstOrDefault();
                walls.Add(w);
            }
            walls = walls.OrderBy(w => w.Q).ToList();

            // 2. רצפה ותקרה
            s.Rect(0, -FloorT, Ltot, 0, lCut);
            s.Poche(0, -FloorT, Ltot, 0, 20, lHatch);
            s.Rect(0, ClearH, Ltot, slabTop, lCut);
            s.Poche(0, ClearH, Ltot, slabTop, 20, lHatch);

            // 3. יסודות, קירות (עם פתחים), מעקה
            foreach (var w in walls)
            {
                double mid = (w.A + w.B) / 2;
                s.Rect(mid - 30, -FootDepth, mid + 30, -FloorT, lCut);
                s.Poche(mid - 30, -FootDepth, mid + 30, -FloorT, 30, lHatch);

                if (w.Op == null)
                {
                    s.Rect(w.A, 0, w.B, ClearH, lCut);
                    s.Poche(w.A, 0, w.B, ClearH, 15, lHatch);
                }
                else if (w.Op.IsDoor)
                {
                    s.Rect(w.A, DxfUtil.HeadCm, w.B, ClearH, lCut);       // משקוף בלבד מעל הדלת
                    s.Poche(w.A, DxfUtil.HeadCm, w.B, ClearH, 15, lHatch);
                }
                else
                {
                    double sill = DxfUtil.SillCm(w.Op);
                    s.Rect(w.A, 0, w.B, sill, lCut);
                    s.Poche(w.A, 0, w.B, sill, 15, lHatch);
                    s.Rect(w.A, DxfUtil.HeadCm, w.B, ClearH, lCut);
                    s.Poche(w.A, DxfUtil.HeadCm, w.B, ClearH, 15, lHatch);
                    s.Ln(mid - 1.5, sill, mid - 1.5, DxfUtil.HeadCm, lWin);   // זיגוג כפול
                    s.Ln(mid + 1.5, sill, mid + 1.5, DxfUtil.HeadCm, lWin);
                }

                if (w.Ext)
                {
                    s.Rect(w.A, slabTop, w.B, top, lCut);
                    s.Poche(w.A, slabTop, w.B, top, 15, lHatch);
                }
            }
            var ext = walls.Where(w => w.Ext).ToList();
            if (ext.Count >= 2) s.Ln(ext.First().B, slabTop + 4, ext.Last().A, slabTop + 4, lBeyond);   // גימור גג

            // 4. קרקע משני הצדדים
            s.Ln(-250, -FloorAbove, 0, -FloorAbove, lGround);
            s.Ln(Ltot, -FloorAbove, Ltot + 250, -FloorAbove, lGround);
            s.Poche(-250, -60, 0, -FloorAbove, 25, lGround);
            s.Poche(Ltot, -60, Ltot + 250, -FloorAbove, 25, lGround);

            // 5. ריהוט: חתוך (אדום) ומה שנראה מאחור (דק)
            foreach (var r in crossed)
            {
                foreach (var f in r.Furniture)
                {
                    double f0 = h ? f.X0 : f.Y0, f1 = h ? f.X1 : f.Y1;
                    double p0 = h ? f.Y0 : f.X0, p1 = h ? f.Y1 : f.X1;
                    double xa = T(f0), xb = T(f1);
                    if (xa > xb) { var t = xa; xa = xb; xb = t; }
                    double hh = DxfUtil.FurnHeight(f.Name);
                    if (p0 < c && c < p1) s.Rect(xa, 0, xb, hh, lFurnCut);
                    else if (p0 > c) s.Rect(xa, 0, xb, hh, lFurn);
                }

                // פתחים בקיר הרחוק של החדר (נראים מאחורי החתך)
                double far = h ? r.Y + r.L : r.X + r.W;
                foreach (var o in g.Openings)
                {
                    if (Math.Abs(Perp(o) - far) > 0.35) continue;
                    if (Along(o) < A0(r) || Along(o) > A1(r)) continue;
                    double xa = T(Along(o) - o.Width / 2), xb = T(Along(o) + o.Width / 2);
                    if (xa > xb) { var t = xa; xa = xb; xb = t; }
                    double y0 = o.IsDoor ? 0 : DxfUtil.SillCm(o);
                    s.Rect(xa, y0, xb, DxfUtil.HeadCm, lBeyond);
                }
            }

            // 6. שמות חדרים בין הקירות
            for (int i = 0; i < walls.Count - 1; i++)
            {
                double mp = (walls[i].P + walls[i + 1].P) / 2;
                var room = crossed.FirstOrDefault(r => A0(r) <= mp && mp <= A1(r));
                if (room == null) continue;
                s.Txt(room.Name, (walls[i].B + walls[i + 1].A) / 2, ClearH - 28, 12, lTxt, TextAlignment.BottomCenter);
            }

            // 7. מידות אופקיות: עובי קירות + רוחב נקי של כל חלל, ומידה כוללת
            var br = new List<double> { 0, Ltot };
            foreach (var w in walls) { br.Add(w.A); br.Add(w.B); }
            br = br.Distinct().OrderBy(v => v).ToList();
            for (int i = 0; i < br.Count - 1; i++)
                if (br[i + 1] - br[i] >= 8)
                    s.DimH(br[i], br[i + 1], -120, ((int)Math.Round(br[i + 1] - br[i])).ToString(), lDim, lTxt);
            s.DimH(0, Ltot, -175, (Ltot / K).ToString("0.00", CultureInfo.InvariantCulture), lDim, lTxt, 12);

            // 8. מידות אנכיות ומפלסים
            s.DimV(-330, 0, ClearH, "260", lDim, lTxt);
            s.DimV(-330, ClearH, slabTop, "20", lDim, lTxt);
            s.DimV(-330, slabTop, top, "60", lDim, lTxt);
            double xl = -470;
            s.LevelMark(xl, -FootDepth, DxfUtil.Lvl(-FootDepth / K), lDim, lTxt);
            s.LevelMark(xl, -FloorAbove, DxfUtil.Lvl(-FloorAbove / K), lDim, lTxt);
            s.LevelMark(xl, 0, DxfUtil.Lvl(0), lDim, lTxt);
            s.LevelMark(xl, ClearH, DxfUtil.Lvl(ClearH / K), lDim, lTxt);
            s.LevelMark(xl, slabTop, DxfUtil.Lvl(slabTop / K), lDim, lTxt);
            s.LevelMark(xl, top, DxfUtil.Lvl(top / K), lDim, lTxt);

            // 9. כותרת
            s.Txt($"חתך {label}   1:50", Ltot / 2, -250, 28, lTxt, TextAlignment.BottomCenter);
            s.Txt(h ? "מבט לכיוון צפון" : "מבט לכיוון מזרח", Ltot / 2, -290, 14, lTxt, TextAlignment.BottomCenter);
            return doc;
        }
    }
}