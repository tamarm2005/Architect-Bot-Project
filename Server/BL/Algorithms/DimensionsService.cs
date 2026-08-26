using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

namespace BL.Algorithms
{
    // תוכנית מידות
    public class DimensionsService
    {
        public string GenerateDimensionsPlan(string inputDxfPath)
        {
            // בדיקה אם הקובץ קיים
            DxfDocument doc;
            try
            {
                doc = DxfDocument.Load(inputDxfPath);
                if (doc == null) return "Error: לא ניתן לטעון את קובץ ה-DXF.";
            }
            // טיפול בשגיאות טעינה של קובץ DXF
            catch (Exception ex)
            {
                return $"Error: קובץ פגום או לא קריא - {ex.Message}";
            }

            // הגדרת שכבת המידות - צבע ירוק (3) !
            Layer dimLayer = new Layer("A-ANNO-DIMS") { Color = new AciColor(3) };
            if (!doc.Layers.Contains(dimLayer.Name)) doc.Layers.Add(dimLayer);

            // יצירת סגנון מידות נקי
            DimensionStyle archStyle = CreateArchitecturalDimStyle(doc);

            // שליפת כל הקירות מהשרטוט לפי שכבות
            var walls = doc.Entities.Lines.Where(l =>
                l.Layer.Name.ToUpper().Contains("WALL") ||
                l.Layer.Name.Contains("קיר") ||
                l.Layer.Name.ToUpper().Contains("EXT") ||
                l.Layer.Name.ToUpper().Contains("INT")).ToList();

            // גיבוי: אם אין שכבות שנקראות קירות, ניקח את כל הקווים בשרטוט
            if (!walls.Any())
            {
                // גיבוי: אם אין שכבות שנקראות קירות, ניקח את כל הקווים בשרטוט
                walls = doc.Entities.Lines.ToList();
                if (!walls.Any()) return "Error: לא זוהו קווים בשרטוט לצורך מתן מידות.";
            }

            // 4. שליפת פתחים (חלונות ודלתות) מתוך הבלוקים (Inserts) בתוכנית הריהוט שלך
            var inserts = doc.Entities.Inserts.Where(i =>
                i.Layer.Name.Contains("דלת") || i.Layer.Name.Contains("חלון") ||
                i.Layer.Name.ToUpper().Contains("DOOR") || i.Layer.Name.ToUpper().Contains("WINDOW")).ToList();

            // 5. מציאת גבולות המבנה
            double minX = walls.Min(w => Math.Min(w.StartPoint.X, w.EndPoint.X));
            double maxX = walls.Max(w => Math.Max(w.StartPoint.X, w.EndPoint.X));
            double minY = walls.Min(w => Math.Min(w.StartPoint.Y, w.EndPoint.Y));
            double maxY = walls.Max(w => Math.Max(w.StartPoint.Y, w.EndPoint.Y));

            // יצירת 3 הרצועות האוטומטיות (Tiers)
            DrawTiers(doc, walls, inserts, minX, maxX, minY, maxY, archStyle, dimLayer);

            string outputFileName = Path.Combine(Path.GetTempPath(), $"Dimensions_Plan_{Guid.NewGuid()}.dxf");
            doc.Save(outputFileName);

            return outputFileName;
        }

        private void DrawTiers(DxfDocument doc, List<Line> walls, List<Insert> inserts, double minX, double maxX, double minY, double maxY, DimensionStyle style, Layer layer)
        {
            double spacing = 60.0; // מרווח נקי ואסתטי בין קווי המידה

            // איסוף נקודות לגושים (X ו-Y של כל קודקודי הקירות)
            var wallX = new List<double>();
            var wallY = new List<double>();
            foreach (var w in walls)
            {
                wallX.Add(w.StartPoint.X); wallX.Add(w.EndPoint.X);
                wallY.Add(w.StartPoint.Y); wallY.Add(w.EndPoint.Y);
            }

            // איסוף נקודות לפתחים (מיקומי חלונות ודלתות + רוחב משוער)
            var tier3X = new List<double>(wallX);
            var tier3Y = new List<double>(wallY);
            foreach (var ins in inserts)
            {
                // מניחה שרוחב חלון הוא 100 ודלת היא 80
                double width = ins.Layer.Name.Contains("חלון") || ins.Layer.Name.ToUpper().Contains("WINDOW") ? 100.0 : 80.0;
                tier3X.Add(ins.Position.X);
                tier3X.Add(ins.Position.X + width);
                tier3Y.Add(ins.Position.Y);
                tier3Y.Add(ins.Position.Y + width);
            }

            // פונקציית עזר שזורקת את קווי המידה על הגריד
            Action<List<double>, double, double, double, bool> drawConsecutive = (coords, refPoint, offset, rotation, isVertical) =>
            {
                // מסננים נקודות קרובות מ-50 ס"מ אחת לשנייה
                var distinct = coords.Select(c => Math.Round(c, 1)).Distinct().OrderBy(c => c)
                             .Aggregate(new List<double>(), (list, val) => {
                                 if (!list.Any() || val - list.Last() >= 50.0) list.Add(val);
                                 return list;
                             });
                // יוצרים מידות בין כל זוג נקודות
                for (int i = 0; i < distinct.Count - 1; i++)
                {
                    double c1 = distinct[i];
                    double c2 = distinct[i + 1];
                    if (Math.Abs(c2 - c1) < 10.0) continue; // דילוג על מידות קטנות משמעותית למניעת עומס

                    Vector2 p1 = isVertical ? new Vector2(refPoint, c1) : new Vector2(c1, refPoint);
                    Vector2 p2 = isVertical ? new Vector2(refPoint, c2) : new Vector2(c2, refPoint);

                    doc.Entities.Add(new LinearDimension(p1, p2, offset, rotation, style) { Layer = layer });
                }
            };

            // ---- חזית צפונית (למעלה) - Rotation 0 ----
            doc.Entities.Add(new LinearDimension(new Vector2(minX, maxY), new Vector2(maxX, maxY), spacing * 3, 0, style) { Layer = layer });
            drawConsecutive(wallX, maxY, spacing * 2, 0, false);
            drawConsecutive(tier3X, maxY, spacing * 1, 0, false);


            // ---- חזית דרומית (למטה) - Rotation 0 ----
            doc.Entities.Add(new LinearDimension(new Vector2(minX, minY), new Vector2(maxX, minY), -(spacing * 3), 0, style) { Layer = layer });
            drawConsecutive(wallX, minY, -(spacing * 2), 0, false);
            drawConsecutive(tier3X, minY, -(spacing * 1), 0, false);


            // ---- חזית מזרחית (ימין) - Rotation 90 ----
            // תוקן: המינוס כאן מעיף את המידות החוצה לימין ולא לתוך המבנה!
            doc.Entities.Add(new LinearDimension(new Vector2(maxX, minY), new Vector2(maxX, maxY), -(spacing * 3), 90, style) { Layer = layer });
            drawConsecutive(wallY, maxX, -(spacing * 2), 90, true);
            drawConsecutive(tier3Y, maxX, -(spacing * 1), 90, true);


            // ---- חזית מערבית (שמאל) - Rotation 90 ----
            // תוקן: הפלוס כאן מעיף את המידות החוצה לשמאל ולא לתוך המבנה!
            doc.Entities.Add(new LinearDimension(new Vector2(minX, minY), new Vector2(minX, maxY), spacing * 3, 90, style) { Layer = layer });
            drawConsecutive(wallY, minX, spacing * 2, 90, true);
            drawConsecutive(tier3Y, minX, spacing * 1, 90, true);
        }

        // יצירת סגנון מידות אדריכלי נקי
        private DimensionStyle CreateArchitecturalDimStyle(DxfDocument doc)
        {
            // בדיקה אם סגנון מידות כבר קיים
            string styleName = "Arch_Standard";
            if (doc.DimensionStyles.Contains(styleName))
                return doc.DimensionStyles[styleName];
            // יצירת סגנון מידות חדש
            DimensionStyle style = new DimensionStyle(styleName)
            {
                DimScaleOverall = 1.0,
                TextHeight = 15.0,
                TextColor = AciColor.ByLayer,
                ArrowSize = 10.0,
                TextInsideAlign = false,
                TextOffset = 5.0,
                // תוספת שמעלימה את הקווים הארוכים שיורדים למטה ועושים בלאגן בשרטוט
                ExtLine1Off = true,
                ExtLine2Off = true
            };

            // הוספת סגנון המידות למסמך
            doc.DimensionStyles.Add(style);
            return style;
        }
    }
}