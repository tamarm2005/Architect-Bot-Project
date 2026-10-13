using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

// תוכנית גגות
namespace BL.Algorithms
{
    public class RoofService
    {
        public class RoofSpecification
        {
            public string RoofType { get; set; }
            public double SlopePercent { get; set; }
            public double WallThickness { get; set; }
        }

        public class RainDrain
        {
            public Vector2 Position { get; set; }
            public string LayerName { get; set; }
        }

        private Layer CreateRoofLayer(DxfDocument doc)
        {
            Layer roofLayer = new Layer("A-ROOF-PLAN")
            {
                Color = new AciColor(62),
                Lineweight = Lineweight.W20
            };

            if (!doc.Layers.Contains(roofLayer.Name))
                doc.Layers.Add(roofLayer);

            return roofLayer;
        }

        public string GenerateRoofPlan(double length, double width, RoofSpecification spec)
        {
            string fileName = $"Roof_Plan_{Guid.NewGuid()}.dxf";
            DxfDocument doc = new DxfDocument();
            doc.Entities.Add(new Line(new Vector2(0, 0), new Vector2(length, 0)));
            doc.Save(fileName);
            return fileName;
        }

        public string GenerateRoofPlanFromWalls(List<Line> inputWalls, RoofSpecification spec)
        {
            DxfDocument doc = new DxfDocument();

            if (inputWalls == null || !inputWalls.Any())
            {
                return "Error: לא התקבלו קירות מהשרטוט המקורי.";
            }

            // המערכת עובדת במטרים, והשירות הזה בס"מ - ממירים אם צריך
            double maxCoord = inputWalls.Max(w => Math.Max(
                Math.Max(Math.Abs(w.StartPoint.X), Math.Abs(w.EndPoint.X)),
                Math.Max(Math.Abs(w.StartPoint.Y), Math.Abs(w.EndPoint.Y))));
            if (maxCoord < 200.0)
            {
                inputWalls = inputWalls.Select(w => new Line(
                    new Vector2(w.StartPoint.X * 100, w.StartPoint.Y * 100),
                    new Vector2(w.EndPoint.X * 100, w.EndPoint.Y * 100))
                { Layer = w.Layer }).ToList();
            }
            if (spec.WallThickness < 5.0) spec.WallThickness *= 100;

            var roofLayer = CreateRoofLayer(doc);

            List<Line> cleanWalls = DrawOriginalWallsAndFilter(doc, inputWalls, roofLayer);

            if (!cleanWalls.Any())
                return "Error: כל הקירות קצרים מדי אחרי סינון.";

            double minX = cleanWalls.Min(w => Math.Min(w.StartPoint.X, w.EndPoint.X));
            double maxX = cleanWalls.Max(w => Math.Max(w.StartPoint.X, w.EndPoint.X));
            double minY = cleanWalls.Min(w => Math.Min(w.StartPoint.Y, w.EndPoint.Y));
            double maxY = cleanWalls.Max(w => Math.Max(w.StartPoint.Y, w.EndPoint.Y));
            Vector2 center = new Vector2((minX + maxX) / 2, (minY + maxY) / 2);

            DrawInternalParapetOffsetWithFillet(doc, cleanWalls, spec.WallThickness, roofLayer);

            double width = maxX - minX;
            double height = maxY - minY;
            double roofArea = width * height / 10000.0;

            List<Line> parapetLines = doc.Entities.Lines.Where(l => l.Layer.Name == roofLayer.Name).ToList();

            List<RainDrain> drains = CalculateAndPlaceDrains(doc, parapetLines, inputWalls, roofArea, center, roofLayer);

            DrawParapetAnnotations(doc, center, maxY, roofLayer);

            string outputPath = Path.Combine(Path.GetTempPath(), $"Roof_Parapet_{Guid.NewGuid()}.dxf");
            HebrewFontFixer.ApplyToDoc(doc);
            doc.Save(outputPath);
            return outputPath;
        }

        public List<RainDrain> CalculateAndPlaceDrains(DxfDocument doc, List<Line> parapetLines, List<Line> allDxfLines, double roofArea, Vector2 center, Layer roofLayer)
        {
            List<RainDrain> placedDrains = new List<RainDrain>();

            int drainsRequired = (int)Math.Ceiling(roofArea / 100.0);
            if (drainsRequired > 4 || drainsRequired < 2) drainsRequired = 2;

            var forbiddenLines = allDxfLines.Where(l =>
              l.Layer.Name.ToUpper().Contains("WINDOW") || l.Layer.Name.Contains("חלון") ||
              l.Layer.Name.ToUpper().Contains("DOOR") || l.Layer.Name.Contains("דלת") ||
              l.Layer.Name.ToUpper().Contains("MAMAD") || l.Layer.Name.Contains("ממד")
            ).ToList();

            Layer drainLayer = new Layer("A-ROOF-DRAIN") { Color = new AciColor(3) };
            if (!doc.Layers.Contains(drainLayer.Name)) doc.Layers.Add(drainLayer);

            List<Vector2> validPoints = new List<Vector2>();
            foreach (var line in parapetLines)
            {
                Vector2 start = new Vector2(line.StartPoint.X, line.StartPoint.Y);
                Vector2 end = new Vector2(line.EndPoint.X, line.EndPoint.Y);

                if (Vector2.Distance(start, end) < 150.0) continue;

                Vector2 direction = end - start;
                direction.Normalize();

                Vector2 pointA = start + direction * 60.0;
                Vector2 pointB = end - direction * 60.0;

                if (!IsPointNearForbiddenLines(pointA, forbiddenLines)) validPoints.Add(pointA);
                if (!IsPointNearForbiddenLines(pointB, forbiddenLines)) validPoints.Add(pointB);
            }

            if (!validPoints.Any())
            {
                foreach (var line in parapetLines)
                {
                    validPoints.Add(new Vector2((line.StartPoint.X + line.EndPoint.X) / 2, (line.StartPoint.Y + line.EndPoint.Y) / 2));
                }
            }

            if (validPoints.Any())
            {
                Vector2 firstDrain = validPoints[0];
                placedDrains.Add(new RainDrain { Position = firstDrain, LayerName = drainLayer.Name });

                if (drainsRequired > 1)
                {
                    var remainingPoints = validPoints.OrderByDescending(p => Vector2.Distance(firstDrain, p)).ToList();
                    for (int i = 0; i < drainsRequired - 1 && i < remainingPoints.Count; i++)
                    {
                        placedDrains.Add(new RainDrain { Position = remainingPoints[i], LayerName = drainLayer.Name });
                    }
                }
            }

            foreach (var drain in placedDrains)
            {
                DrawSingleDrainGeometry(doc, drain.Position, parapetLines, drainLayer);
            }

            return placedDrains;
        }

        private void DrawSingleDrainGeometry(DxfDocument doc, Vector2 position, List<Line> parapetLines, Layer drainLayer)
        {
            // 1. מציאת קו המעקה הקרוב ביותר לצמ"ג
            var nearestWall = parapetLines.OrderBy(w => Vector2.Distance(position, new Vector2((w.StartPoint.X + w.EndPoint.X) / 2, (w.StartPoint.Y + w.EndPoint.Y) / 2))).First();

            double dx = nearestWall.EndPoint.X - nearestWall.StartPoint.X;
            double dy = nearestWall.EndPoint.Y - nearestWall.StartPoint.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);

            if (len < 0.001) return;

            Vector2 wallDir = new Vector2(dx / len, dy / len);

            // נורמל בסיסי (90 מעלות שמאלה)
            Vector2 normalA = new Vector2(-wallDir.Y, wallDir.X);

            // נקודת בדיקה קטנה כדי לראות אם כיוון normalA הוא לתוך הגג
            Vector2 testPoint = position + normalA * 5.0;

            // 2. בדיקה אבסולוטית בתוך פוליגון המעקה הפנימי
            bool isInside = false;
            for (int j = 0, k = parapetLines.Count - 1; j < parapetLines.Count; k = j++)
            {
                double w1x = parapetLines[j].StartPoint.X;
                double w1y = parapetLines[j].StartPoint.Y;
                double w2x = parapetLines[k].StartPoint.X;
                double w2y = parapetLines[k].StartPoint.Y;

                if (w1y > testPoint.Y != w2y > testPoint.Y &&
                  testPoint.X < (w2x - w1x) * (testPoint.Y - w1y) / (w2y - w1y + 0.0001) + w1x)
                {
                    isInside = !isInside;
                }
            }

            // קביעת כיוונים אבסולוטיים
            Vector2 inboundNormal = isInside ? normalA : -normalA;   // 100% פנימה לגג
            Vector2 outboundNormal = -inboundNormal;                // 100% החוצה לקיר

            double boxSize = 20.0;

            // 3. הגדרת 4 פינות הקופסה (הריבוע הירוק) - פונה פנימה לגג
            Vector2 pA = position;
            Vector2 pB = position + wallDir * boxSize;
            Vector2 pC = pB + inboundNormal * boxSize;
            Vector2 pD = pA + inboundNormal * boxSize;

            doc.Entities.Add(new Line(pA, pB) { Layer = drainLayer });
            doc.Entities.Add(new Line(pB, pC) { Layer = drainLayer });
            doc.Entities.Add(new Line(pC, pD) { Layer = drainLayer });
            doc.Entities.Add(new Line(pD, pA) { Layer = drainLayer });

            // א. עיגול הצינור במרכז הקופסה (בתוך הגג)
            Vector2 boxCenter = position + wallDir * (boxSize / 2) + inboundNormal * (boxSize / 2);
            Circle innerCircle = new Circle(boxCenter, 5.0) { Layer = drainLayer };
            doc.Entities.Add(innerCircle);

            // ב. תיקון הסימן: העיגול השני בתוך עובי הקיר (בכיוון ה-outbound המוחלט)
            Vector2 insideWallCenter = position + wallDir * (boxSize / 2) + outboundNormal * 10.0;
            Circle wallCircle = new Circle(insideWallCenter, 5.0) { Layer = drainLayer };
            doc.Entities.Add(wallCircle);

            // ג. קווי חיבור צידיים
            Vector2 radiusOffset = wallDir * 5.0;
            Vector2 dashStart1 = boxCenter - radiusOffset;
            Vector2 dashEnd1 = insideWallCenter - radiusOffset;
            Vector2 dashStart2 = boxCenter + radiusOffset;
            Vector2 dashEnd2 = insideWallCenter + radiusOffset;

            doc.Entities.Add(new Line(dashStart1, dashEnd1) { Layer = drainLayer });
            doc.Entities.Add(new Line(dashStart2, dashEnd2) { Layer = drainLayer });

            // ד. שרטוט חץ וכיתוב טקסט (ממוקם בתוך הגג)
            Vector2 textPosition = boxCenter + inboundNormal * 40.0 + wallDir * 30.0;
            Vector2 arrowHead = boxCenter;

            doc.Entities.Add(new Line(textPosition, arrowHead) { Layer = drainLayer });

            Vector2 arrowDir = arrowHead - textPosition;
            arrowDir.Normalize();
            Vector2 arrowRight = new Vector2(-arrowDir.Y, arrowDir.X) * 3.0;
            doc.Entities.Add(new Line(arrowHead, arrowHead - arrowDir * 7.0 + arrowRight) { Layer = drainLayer });
            doc.Entities.Add(new Line(arrowHead, arrowHead - arrowDir * 7.0 - arrowRight) { Layer = drainLayer });

            Text txt = new Text("4\" GMZ", new Vector2(textPosition.X, textPosition.Y + 5), 12.0)
            {
                Layer = drainLayer,
                Color = new AciColor(7)
            };
            doc.Entities.Add(txt);
        }
        private bool IsPointNearForbiddenLines(Vector2 pt, List<Line> forbiddenLines)
        {
            foreach (var forbLine in forbiddenLines)
            {
                Vector2 forbStart = new Vector2(forbLine.StartPoint.X, forbLine.StartPoint.Y);
                Vector2 forbEnd = new Vector2(forbLine.EndPoint.X, forbLine.EndPoint.Y);

                if (Vector2.Distance(pt, forbStart) < 60.0 || Vector2.Distance(pt, forbEnd) < 60.0)
                    return true;
            }
            return false;
        }

        private (Layer extWall, Layer intPara, Layer lText) CreateParapetLayers(DxfDocument doc)
        {
            Layer lExtWall = new Layer("A-WALL-EXTR") { Color = new AciColor(5) };
            Layer lIntParapet = new Layer("A-ROOF-PARA") { Color = new AciColor(1) };
            Layer lText = new Layer("A-TEXT") { Color = new AciColor(7) };

            doc.Layers.Add(lExtWall);
            doc.Layers.Add(lIntParapet);
            doc.Layers.Add(lText);

            return (lExtWall, lIntParapet, lText);
        }

        private List<Line> DrawOriginalWallsAndFilter(DxfDocument doc, List<Line> inputWalls, Layer wallLayer)
        {
            List<Line> validWalls = new List<Line>();

            foreach (var wall in inputWalls)
            {
                double dx = wall.EndPoint.X - wall.StartPoint.X;
                double dy = wall.EndPoint.Y - wall.StartPoint.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);

                if (length < 20.0) continue;

                Line extLine = new Line(new Vector2(wall.StartPoint.X, wall.StartPoint.Y), new Vector2(wall.EndPoint.X, wall.EndPoint.Y))
                {
                    Layer = wallLayer
                };
                doc.Entities.Add(extLine);

                validWalls.Add(wall);
            }

            return validWalls;
        }

        private void DrawInternalParapetOffsetWithFillet(DxfDocument doc, List<Line> filteredWalls, double thickness, Layer parapetLayer)
        {
            if (filteredWalls == null || filteredWalls.Count < 2) return;

            List<(Vector2 Start, Vector2 End)> offsetLines = new List<(Vector2 Start, Vector2 End)>();

            double minX = filteredWalls.Min(w => Math.Min(w.StartPoint.X, w.EndPoint.X));
            double maxX = filteredWalls.Max(w => Math.Max(w.StartPoint.X, w.EndPoint.X));
            double minY = filteredWalls.Min(w => Math.Min(w.StartPoint.Y, w.EndPoint.Y));
            double maxY = filteredWalls.Max(w => Math.Max(w.StartPoint.Y, w.EndPoint.Y));
            Vector2 center = new Vector2((minX + maxX) / 2, (minY + maxY) / 2);

            foreach (var wall in filteredWalls)
            {
                double p1x = wall.StartPoint.X;
                double p1y = wall.StartPoint.Y;
                double p2x = wall.EndPoint.X;
                double p2y = wall.EndPoint.Y;

                double dx = p2x - p1x;
                double dy = p2y - p1y;
                double len = Math.Sqrt(dx * dx + dy * dy);

                if (len < 0.001) continue;

                double nx = -dy / len;
                double ny = dx / len;

                double midX = (p1x + p2x) / 2;
                double midY = (p1y + p2y) / 2;

                double testX = midX + nx * thickness;
                double testY = midY + ny * thickness;

                bool isInside = false;
                for (int j = 0, k = filteredWalls.Count - 1; j < filteredWalls.Count; k = j++)
                {
                    double w1x = filteredWalls[j].StartPoint.X;
                    double w1y = filteredWalls[j].StartPoint.Y;
                    double w2x = filteredWalls[k].StartPoint.X;
                    double w2y = filteredWalls[k].StartPoint.Y;

                    if (w1y > testY != w2y > testY &&
                      testX < (w2x - w1x) * (testY - w1y) / (w2y - w1y + 0.0001) + w1x)
                    {
                        isInside = !isInside;
                    }
                }

                if (!isInside)
                {
                    nx = -nx;
                    ny = -ny;
                }

                Vector2 oStart = new Vector2(p1x + nx * thickness, p1y + ny * thickness);
                Vector2 oEnd = new Vector2(p2x + nx * thickness, p2y + ny * thickness);

                offsetLines.Add((oStart, oEnd));
            }

            for (int i = 0; i < offsetLines.Count; i++)
            {
                var current = offsetLines[i];
                Vector2 finalStart = current.Start;
                Vector2 finalEnd = current.End;

                var next = offsetLines.Where((l, idx) => idx != i)
                           .OrderBy(l => Vector2.Distance(current.End, l.Start))
                           .FirstOrDefault();

                var prev = offsetLines.Where((l, idx) => idx != i)
                           .OrderBy(l => Vector2.Distance(current.Start, l.End))
                           .FirstOrDefault();

                if (next.Start != Vector2.Zero && Vector2.Distance(current.End, next.Start) < 1000)
                {
                    Vector2 nextIntersection = FindIntersection(current.Start, current.End, next.Start, next.End);
                    if (!double.IsNaN(nextIntersection.X) && !double.IsNaN(nextIntersection.Y))
                    {
                        finalEnd = nextIntersection;
                    }
                }

                if (prev.End != Vector2.Zero && Vector2.Distance(current.Start, prev.End) < 1000)
                {
                    Vector2 prevIntersection = FindIntersection(prev.Start, prev.End, current.Start, current.End);
                    if (!double.IsNaN(prevIntersection.X) && !double.IsNaN(prevIntersection.Y))
                    {
                        finalStart = prevIntersection;
                    }
                }

                if (Vector2.Distance(finalStart, finalEnd) < 10.0) continue;

                Line parapetLine = new Line(finalStart, finalEnd) { Layer = parapetLayer };
                doc.Entities.Add(parapetLine);
            }
        }

        private Vector2 FindIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            double tNum = (p1.X - p3.X) * (p3.Y - p4.Y) - (p1.Y - p3.Y) * (p3.X - p4.X);
            double uNum = (p1.X - p2.X) * (p1.Y - p3.Y) - (p1.Y - p2.Y) * (p1.X - p3.X);
            double denom = (p1.X - p2.X) * (p3.Y - p4.Y) - (p1.Y - p2.Y) * (p3.X - p4.X);

            if (Math.Abs(denom) < 0.0001)
            {
                return p2;
            }

            double t = tNum / denom;
            return new Vector2(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
        }

        private void DrawParapetAnnotations(DxfDocument doc, Vector2 center, double maxY, Layer textLayer)
        {
            Text titleText = new Text("תוכנית גג שטוח עם מעקה", new Vector2(center.X - 200, maxY + 100), 40)
            {
                Layer = textLayer
            };

            Text scaleText = new Text("קנה מידה 1:100", new Vector2(center.X - 100, maxY + 40), 25)
            {
                Layer = textLayer
            };

            doc.Entities.Add(titleText);
            doc.Entities.Add(scaleText);
        }
        // =====================================================================
        //  תוכנית גגות מלאה - לפי BuildingGeometry (מטרים), מצויר בס"מ בקנה מידה 1:100
        // =====================================================================
        public string GenerateRoofPlanFromGeometry(BuildingGeometry g, RoofSpecification spec)
        {
            if (g == null || g.Outline == null || g.Outline.Count < 3)
                return "Error: לא התקבל מתאר בניין.";

            const double K = DxfUtil.K;
            double wallT = (spec != null && spec.WallThickness > 0) ? spec.WallThickness : 20.0;
            if (wallT < 5.0) wallT *= 100;                       // אם הגיע במטרים
            double slope = (spec != null && spec.SlopePercent > 0) ? spec.SlopePercent : 1.5;
            string slopeTxt = slope.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

            var doc = new DxfDocument();
            var s = new Sheet(doc, 0, 0);

            // ---------- שכבות ----------
            var lOut = DxfUtil.Lay(doc, "A-ROOF-OUT", 7, Lineweight.W50);
            var lPara = DxfUtil.Lay(doc, "A-ROOF-PARA", 1, Lineweight.W35);
            var lHatch = DxfUtil.Lay(doc, "A-ROOF-HATCH", 8);
            var lBelow = DxfUtil.Lay(doc, "A-ROOF-BELOW", 8);
            lBelow.Linetype = Linetype.Dashed;
            var lRidge = DxfUtil.Lay(doc, "A-ROOF-RIDGE", 6);
            lRidge.Linetype = Linetype.Dashed;
            var lDrain = DxfUtil.Lay(doc, "A-ROOF-DRAIN", 3, Lineweight.W35);
            var lSlope = DxfUtil.Lay(doc, "A-ROOF-SLOPE", 4);
            var lEq = DxfUtil.Lay(doc, "A-ROOF-EQUIP", 5);
            var lDim = DxfUtil.Lay(doc, "A-ROOF-DIM", 2);
            var lTxt = DxfUtil.Lay(doc, "A-TEXT", 7);

            // ---------- מתאר בס"מ (יחסית לפינה השמאלית-תחתונה) ----------
            double ox = g.MinX, oy = g.MinY;
            var P = g.Outline.Select(p => new Vector2((p.X - ox) * K, (p.Y - oy) * K)).ToList();
            double W = (g.MaxX - g.MinX) * K, H = (g.MaxY - g.MinY) * K;

            var inner = RoofInset(P, wallT);
            if (inner == null || inner.Count < 3) return "Error: לא ניתן לחשב פרפט פנימי.";

            // הגג נקי מחדרים: לא מציירים קירות פנימיים ולא שמות חדרים

            // ---------- 2. פרפט: קו חיצוני + קו פנימי + קווי פוצ'ה ----------
            for (int i = 0; i < P.Count; i++)
            {
                var a = P[i]; var b = P[(i + 1) % P.Count];
                s.Ln(a.X, a.Y, b.X, b.Y, lOut);
                var c = inner[i]; var d = inner[(i + 1) % inner.Count];
                s.Ln(c.X, c.Y, d.X, d.Y, lPara);

                // פוצ'ה רק לקירות אנכיים/אופקיים (בין הקו החיצוני לפנימי)
                if (Math.Abs(a.Y - b.Y) < 0.01)
                    RoofHatch(s, Math.Min(a.X, b.X), Math.Min(a.Y, c.Y), Math.Max(a.X, b.X), Math.Max(a.Y, c.Y), 12, lHatch);
                else if (Math.Abs(a.X - b.X) < 0.01)
                    RoofHatch(s, Math.Min(a.X, c.X), Math.Min(a.Y, b.Y), Math.Max(a.X, c.X), Math.Max(a.Y, b.Y), 12, lHatch);
            }

            // ---------- 3. ניקוזים ----------
            double ix0 = inner.Min(p => p.X), ix1 = inner.Max(p => p.X);
            double iy0 = inner.Min(p => p.Y), iy1 = inner.Max(p => p.Y);
            double areaM2 = Math.Abs(RoofArea(inner)) / 10000.0;
            int n = (int)Math.Ceiling(areaM2 / 100.0);
            if (n < 2) n = 2; if (n > 4) n = 4;
            var centroid = new Vector2(inner.Average(p => p.X), inner.Average(p => p.Y));

            bool longX = (ix1 - ix0) >= (iy1 - iy0);
            double[] fracs = n == 2 ? new[] { 0.25, 0.75 } : n == 3 ? new[] { 0.2, 0.5, 0.8 } : new[] { 0.25, 0.75, 0.25, 0.75 };
            var drains = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                bool far = (n == 4 && i >= 2);     // 4 ניקוזים: שניים בכל צד ארוך
                double along = fracs[i];
                double px, py;
                if (longX)
                {
                    px = ix0 + (ix1 - ix0) * along;
                    py = far ? iy1 - 55 : iy0 + 55;
                    if (n == 3 && i == 1) py = iy1 - 55;
                }
                else
                {
                    py = iy0 + (iy1 - iy0) * along;
                    px = far ? ix1 - 55 : ix0 + 55;
                    if (n == 3 && i == 1) px = ix1 - 55;
                }
                var pt = new Vector2(px, py);
                // לא בפינות/מחוץ לגג: מזיזים לכיוון מרכז הגג עד שנכנסים
                for (int k = 0; k < 40 && !RoofIn(inner, pt.X, pt.Y); k++)
                {
                    var v = centroid - pt; double l = Math.Sqrt(v.X * v.X + v.Y * v.Y); if (l < 1) break;
                    pt = new Vector2(pt.X + v.X / l * 10, pt.Y + v.Y / l * 10);
                }
                // לא מעל פתח: אם יש פתח במרחק פחות מ-70 ס"מ מזיזים 100 ס"מ לאורך הקיר
                foreach (var o in g.Openings)
                {
                    double dx = (o.X - ox) * K - pt.X, dy = (o.Y - oy) * K - pt.Y;
                    if (Math.Sqrt(dx * dx + dy * dy) < 70)
                    {
                        if (longX) pt = new Vector2(pt.X + 100, pt.Y); else pt = new Vector2(pt.X, pt.Y + 100);
                        break;
                    }
                }
                if (RoofIn(inner, pt.X, pt.Y)) drains.Add(pt);
            }

            foreach (var d in drains)
            {
                RoofCircle(s, d.X, d.Y, 10, lDrain);
                RoofCircle(s, d.X, d.Y, 5, lDrain);
                s.Ln(d.X - 14, d.Y, d.X + 14, d.Y, lDrain);
                s.Ln(d.X, d.Y - 14, d.X, d.Y + 14, lDrain);
                var dir = Unit(centroid.X - d.X, centroid.Y - d.Y);
                s.Txt("ניקוז 4\"", d.X + dir.X * 45 + 12, d.Y + dir.Y * 45 + 12, 12, lTxt);

                // פתח גלישה בפרפט הקרוב: מלבן קטן בעובי הקיר, 5 ס"מ מעל פני הגג
                RoofOverflow(s, P, inner, d, wallT, lDrain);

                // חצי שיפוע לכיוון הניקוז
                var side = new Vector2(-dir.Y, dir.X);
                foreach (var lat in new[] { 0.0, -180.0, 180.0 })
                {
                    double sx = d.X + dir.X * 230 + side.X * lat, sy = d.Y + dir.Y * 230 + side.Y * lat;
                    if (!RoofIn(inner, sx, sy)) continue;
                    RoofArrow(s, sx, sy, d.X + side.X * lat * 0.25 + dir.X * 22, d.Y + side.Y * lat * 0.25 + dir.Y * 22, lSlope);
                    if (lat == 0.0) s.Txt("שיפוע " + slopeTxt + "%", sx + 8, sy + 8, 11, lTxt);
                }
            }

            // קו רכס בין ניקוזים סמוכים באותה שורה/עמודה
            for (int i = 0; i + 1 < drains.Count; i++)
            {
                var a = drains[i]; var b = drains[i + 1];
                if (longX && Math.Abs(a.Y - b.Y) < 1)
                {
                    double mx = (a.X + b.X) / 2;
                    s.Ln(mx, iy0 + 20, mx, iy1 - 20, lRidge);
                    s.Txt("קו רכס", mx + 6, (iy0 + iy1) / 2, 11, lTxt);
                }
                else if (!longX && Math.Abs(a.X - b.X) < 1)
                {
                    double my = (a.Y + b.Y) / 2;
                    s.Ln(ix0 + 20, my, ix1 - 20, my, lRidge);
                    s.Txt("קו רכס", (ix0 + ix1) / 2, my + 6, 11, lTxt);
                }
            }

            // ---------- 4. אלמנטים על הגג ----------
            // דוד שמש וקולטים: מלבן 120x200 בפינה הרחוקה מהניקוזים (אם נכנס בגג)
            double sw = 120, sh = 200;
            double sxr = ix1 - 90 - sw, syr = iy1 - 90 - sh;
            if (RoofIn(inner, sxr, syr) && RoofIn(inner, sxr + sw, syr + sh) && RoofIn(inner, sxr, syr + sh) && RoofIn(inner, sxr + sw, syr))
            {
                s.Rect(sxr, syr, sxr + sw, syr + sh, lEq);
                s.Ln(sxr, syr, sxr + sw, syr + sh, lEq);
                s.Ln(sxr, syr + sh, sxr + sw, syr, lEq);
                s.Txt("דוד שמש וקולטים", sxr + sw / 2, syr - 18, 11, lTxt, TextAlignment.BottomCenter);
            }
            // שני צינורות אוורור
            foreach (var vp in new[] { new Vector2(ix0 + 90, iy1 - 90), new Vector2(ix0 + 140, iy1 - 90) })
                if (RoofIn(inner, vp.X, vp.Y)) RoofCircle(s, vp.X, vp.Y, 5, lEq);
            if (RoofIn(inner, ix0 + 90, iy1 - 90))
                s.Txt("צינורות אוורור", ix0 + 90, iy1 - 115, 10, lTxt);

            // גישה לגג: חדר מדרגות אם קיים, אחרת הערת סולם
            var stairs = g.Rooms.FirstOrDefault(r => (r.Name ?? "").Contains("מדרגות"));
            if (stairs != null)
            {
                double cx = (stairs.X + stairs.W / 2 - ox) * K, cy = (stairs.Y + stairs.L / 2 - oy) * K;
                s.Rect(cx - 45, cy - 30, cx + 45, cy + 30, lEq);
                s.Txt("פתח יציאה לגג", cx, cy + 40, 11, lTxt, TextAlignment.BottomCenter);
            }
            else
            {
                s.Txt("גישה לגג: סולם", ix0 + 40, iy0 + 120, 12, lTxt);
            }

            // ---------- 5. מידות ----------
            s.DimH(0, W, -90, ((int)Math.Round(W)).ToString(), lDim, lTxt, 14);
            s.DimV(-90, 0, H, ((int)Math.Round(H)).ToString(), lDim, lTxt, 14);
            s.DimH(ix0, ix1, H + 70, ((int)Math.Round(ix1 - ix0)).ToString(), lDim, lTxt, 12);
            s.Txt("עובי פרפט " + (int)wallT, W + 30, H / 2, 11, lTxt);
            s.Ln(W + 25, H / 2 - 4, W - 2, Math.Min(H / 2, H / 2) + 0, lDim);

            // ---------- 6. כותרת, חץ צפון, מקרא והערות ----------
            double ty = H + 220;
            s.Txt("תוכנית גגות 1:100", W / 2, ty, 35, lTxt, TextAlignment.BottomCenter);
            s.Txt("מידות בס\"מ, מפלסים במטרים", W / 2, ty - 40, 14, lTxt, TextAlignment.BottomCenter);

            // חץ צפון
            double nx = W + 160, ny = H + 80;
            RoofCircle(s, nx, ny, 35, lOut);
            s.Ln(nx, ny - 35, nx, ny + 35, lOut);
            s.Ln(nx, ny + 35, nx - 12, ny + 8, lOut);
            s.Ln(nx, ny + 35, nx + 12, ny + 8, lOut);
            s.Txt("N", nx, ny + 45, 18, lTxt, TextAlignment.BottomCenter);

            // מקרא ומפלסים
            double lx = 0, ly = -230;
            s.Txt("מקרא והערות:", lx, ly, 14, lTxt);
            string[] notes =
            {
                "שיפוע " + slopeTxt + "% לכיוון הניקוזים, קו רכס בין ניקוזים",
                "ניקוז 4\" לכל כ-100 מ\"ר: " + drains.Count + " ניקוזים, פתח גלישה מעל כל ניקוז (5 ס\"מ מעל פני הגג)",
                "פרפט בטון " + (int)wallT + " ס\"מ, גובה " + (int)(ParapetHcm()) + " ס\"מ",
                "איטום ביטומני + שכבת הגנה, כיפוף איטום 30 ס\"מ על הפרפט",
                "מפלסים: תחתית תקרה +2.60, פני תקרה +2.80, ראש פרפט +3.40",
                "שטח גג: " + areaM2.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " מ\"ר"
            };
            for (int i = 0; i < notes.Length; i++)
                s.Txt(notes[i], lx, ly - 24 * (i + 1), 11, lTxt);

            string outputPath = Path.Combine(Path.GetTempPath(), $"Roof_Plan_{Guid.NewGuid()}.dxf");
            HebrewFontFixer.ApplyToDoc(doc);
            doc.Save(outputPath);
            return outputPath;
        }

        private static double ParapetHcm() { return 60.0; }   // כמו ParapetH בחזיתות ובחתכים

        // ----- עזרים לגג -----
        private static Vector2 Unit(double x, double y)
        {
            double l = Math.Sqrt(x * x + y * y);
            return l < 1e-9 ? new Vector2(0, 1) : new Vector2(x / l, y / l);
        }

        private static double RoofArea(List<Vector2> p)
        {
            double a = 0;
            for (int i = 0; i < p.Count; i++) { var u = p[i]; var v = p[(i + 1) % p.Count]; a += u.X * v.Y - v.X * u.Y; }
            return a / 2.0;
        }

        private static bool RoofIn(List<Vector2> p, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
                if ((p[i].Y > y) != (p[j].Y > y) &&
                    x < (p[j].X - p[i].X) * (y - p[i].Y) / (p[j].Y - p[i].Y + 1e-12) + p[i].X)
                    inside = !inside;
            return inside;
        }

        // היסט פנימה של מצולע בעובי t (חיתוך קווים סמוכים)
        private static List<Vector2> RoofInset(List<Vector2> p, double t)
        {
            int n = p.Count;
            double sign = RoofArea(p) >= 0 ? 1 : -1;      // CCW => הפנים משמאל
            var ao = new Vector2[n]; var bo = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                var a = p[i]; var b = p[(i + 1) % n];
                var d = Unit(b.X - a.X, b.Y - a.Y);
                var nrm = new Vector2(-d.Y * sign, d.X * sign);
                ao[i] = new Vector2(a.X + nrm.X * t, a.Y + nrm.Y * t);
                bo[i] = new Vector2(b.X + nrm.X * t, b.Y + nrm.Y * t);
            }
            var res = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                int pr = (i - 1 + n) % n;
                double x1 = ao[pr].X, y1 = ao[pr].Y, x2 = bo[pr].X, y2 = bo[pr].Y;
                double x3 = ao[i].X, y3 = ao[i].Y, x4 = bo[i].X, y4 = bo[i].Y;
                double den = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
                if (Math.Abs(den) < 1e-9) { res.Add(ao[i]); continue; }
                double tt = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / den;
                res.Add(new Vector2(x1 + tt * (x2 - x1), y1 + tt * (y2 - y1)));
            }
            return res;
        }

        // פוצ'ה אלכסוני עם יישור גלובלי (כדי שהפינות יתחברו יפה)
        private static void RoofHatch(Sheet s, double x0, double y0, double x1, double y1, double step, Layer l)
        {
            double c = Math.Ceiling((x0 + y0) / step) * step;
            for (; c < x1 + y1; c += step)
            {
                double yLo = Math.Max(y0, c - x1), yHi = Math.Min(y1, c - x0);
                if (yHi - yLo > 0.5) s.Ln(c - yLo, yLo, c - yHi, yHi, l);
            }
        }

        private static void RoofCircle(Sheet s, double x, double y, double r, Layer l)
        {
            s.Doc.Entities.Add(new Circle(new Vector2(s.OX + x, s.OY + y), r) { Layer = l });
        }

        private static void RoofArrow(Sheet s, double x1, double y1, double x2, double y2, Layer l)
        {
            s.Ln(x1, y1, x2, y2, l);
            var d = Unit(x2 - x1, y2 - y1);
            var nrm = new Vector2(-d.Y, d.X);
            s.Ln(x2, y2, x2 - d.X * 18 + nrm.X * 6, y2 - d.Y * 18 + nrm.Y * 6, l);
            s.Ln(x2, y2, x2 - d.X * 18 - nrm.X * 6, y2 - d.Y * 18 - nrm.Y * 6, l);
        }

        // פתח גלישה: סימון בעובי הפרפט ליד הניקוז
        private static void RoofOverflow(Sheet s, List<Vector2> outer, List<Vector2> inner, Vector2 d, double wallT, Layer l)
        {
            double best = double.MaxValue; int bi = 0; Vector2 foot = d;
            for (int i = 0; i < inner.Count; i++)
            {
                var a = inner[i]; var b = inner[(i + 1) % inner.Count];
                double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
                if (l2 < 1e-9) continue;
                double t = Math.Max(0, Math.Min(1, ((d.X - a.X) * dx + (d.Y - a.Y) * dy) / l2));
                var f = new Vector2(a.X + t * dx, a.Y + t * dy);
                double dist = Math.Sqrt((d.X - f.X) * (d.X - f.X) + (d.Y - f.Y) * (d.Y - f.Y));
                if (dist < best) { best = dist; bi = i; foot = f; }
            }
            var e = Unit(inner[(bi + 1) % inner.Count].X - inner[bi].X, inner[(bi + 1) % inner.Count].Y - inner[bi].Y);
            var o = outer[bi]; var q = outer[(bi + 1) % outer.Count];
            var nOut = new Vector2(-e.Y, e.X);
            // כיוון החוצה = לכיוון הקו החיצוני
            if ((o.X - foot.X) * nOut.X + (o.Y - foot.Y) * nOut.Y < 0) nOut = new Vector2(-nOut.X, -nOut.Y);
            double h = 15;
            var p1 = new Vector2(foot.X - e.X * h, foot.Y - e.Y * h);
            var p2 = new Vector2(foot.X + e.X * h, foot.Y + e.Y * h);
            var p3 = new Vector2(p2.X + nOut.X * wallT, p2.Y + nOut.Y * wallT);
            var p4 = new Vector2(p1.X + nOut.X * wallT, p1.Y + nOut.Y * wallT);
            s.Ln(p1.X, p1.Y, p4.X, p4.Y, l); s.Ln(p2.X, p2.Y, p3.X, p3.Y, l);
            s.Ln(p4.X, p4.Y, p3.X, p3.Y, l);
            s.Txt("פתח גלישה", p3.X + nOut.X * 8, p3.Y + nOut.Y * 8, 10, l);
        }
    }
}