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

            var roofLayer = CreateRoofLayer(doc);

            List<Line> cleanWalls = DrawOriginalWallsAndFilter(doc, inputWalls, roofLayer);

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
    }
}