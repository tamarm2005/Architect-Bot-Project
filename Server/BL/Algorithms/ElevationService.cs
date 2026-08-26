using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

namespace BL.Algorithms
{
    // חזיתות
    public class ElevationService
    {
        private const double TOLERANCE = 50.0; // סבילות לזיהוי קיר חיצוני

        public string GenerateElevationsPlan(string inputDxfPath)
        {
            DxfDocument planDoc = DxfDocument.Load(inputDxfPath);
            DxfDocument elevDoc = new DxfDocument();

            // חישוב גבולות המבנה האמיתיים
            double minX = planDoc.Entities.Lines.Min(l => Math.Min(l.StartPoint.X, l.EndPoint.X));
            double maxX = planDoc.Entities.Lines.Max(l => Math.Max(l.StartPoint.X, l.EndPoint.X));
            double minY = planDoc.Entities.Lines.Min(l => Math.Min(l.StartPoint.Y, l.EndPoint.Y));
            double maxY = planDoc.Entities.Lines.Max(l => Math.Max(l.StartPoint.Y, l.EndPoint.Y));

            double bWidth = maxX - minX;
            double bDepth = maxY - minY;
            double fHeight = 300.0; // גובה קומה
            double parapet = 50.0;  // גובה קרקוב

            // הגדרת מיקום החזיתות בדף (מרווחים קבועים כדי לא לחפוף)
            double spacing = 1000.0;

            // יצירת חזיתות (כל חזית היא מלבן סגור)
            DrawFacadeBox(elevDoc, "חזית דרומית", 0, 0, bWidth, fHeight, parapet);
            DrawFacadeBox(elevDoc, "חזית צפונית", 0, fHeight + spacing, bWidth, fHeight, parapet);
            DrawFacadeBox(elevDoc, "חזית מזרחית", bWidth + spacing, 0, bDepth, fHeight, parapet);
            DrawFacadeBox(elevDoc, "חזית מערבית", bWidth + spacing, fHeight + spacing, bDepth, fHeight, parapet);

            // הטלת אלמנטים
            foreach (var entity in planDoc.Entities.All)
            {
                if (entity is Insert ins)
                {
                    ProjectOpening(elevDoc, ins, minX, maxX, minY, maxY, bWidth, fHeight, spacing);
                }
            }

            string outputPath = Path.Combine(Path.GetTempPath(), $"Arch_Elevations_{Guid.NewGuid()}.dxf");
            elevDoc.Save(outputPath);
            return outputPath;
        }

        private void DrawFacadeBox(DxfDocument doc, string name, double x, double y, double w, double h, double p)
        {
            // שכבות וצבעים
            Layer wallLayer = new Layer("A-ELEV-WALL") { Color = new AciColor(7) }; // קירות לבן
            Layer groundLayer = new Layer("A-ELEV-GROUND") { Color = AciColor.Green, Lineweight = Lineweight.W50 }; // קרקע ירוק

            doc.Layers.Add(wallLayer); doc.Layers.Add(groundLayer);

            // קווי גבול (הקופסה שביקשת)
            doc.Entities.Add(new Line(new Vector2(x, y), new Vector2(x + w, y)) { Layer = groundLayer }); // קרקע
            doc.Entities.Add(new Line(new Vector2(x, y), new Vector2(x, y + h + p)) { Layer = wallLayer }); // צד שמאל
            doc.Entities.Add(new Line(new Vector2(x + w, y), new Vector2(x + w, y + h + p)) { Layer = wallLayer }); // צד ימין
            doc.Entities.Add(new Line(new Vector2(x, y + h), new Vector2(x + w, y + h)) { Layer = wallLayer }); // תקרה
            doc.Entities.Add(new Line(new Vector2(x, y + h + p), new Vector2(x + w, y + h + p)) { Layer = wallLayer }); // קרקוב

            // מידות מפלסים
            doc.Entities.Add(new Text("+0.00", new Vector2(x - 50, y), 20));
            doc.Entities.Add(new Text("+3.00", new Vector2(x - 50, y + h), 20));
            doc.Entities.Add(new Text(name, new Vector2(x, y - 100), 30));
        }

        private void ProjectOpening(DxfDocument doc, Insert ins, double minX, double maxX, double minY, double maxY, double bWidth, double fHeight, double spacing)
        {
            bool isWindow = ins.Block.Name.ToUpper().Contains("WINDOW") || ins.Block.Name.ToUpper().Contains("חלון");
            bool isDoor = ins.Block.Name.ToUpper().Contains("DOOR") || ins.Block.Name.ToUpper().Contains("דלת");
            if (!isWindow && !isDoor) return;

            Layer layer = isWindow ? new Layer("A-ELEV-WIND") { Color = AciColor.Blue } : new Layer("A-ELEV-DOOR") { Color = AciColor.Red };
            doc.Layers.Add(layer);

            double w = 90.0, h = isWindow ? 110.0 : 210.0, s = isWindow ? 100.0 : 0.0;
            double posX = 0, posY = 0;

            // זיהוי הקיר והצבת האלמנט
            if (Math.Abs(ins.Position.Y - minY) < TOLERANCE) // דרום
            { posX = ins.Position.X - minX; posY = 0; }
            else if (Math.Abs(ins.Position.Y - maxY) < TOLERANCE) // צפון
            { posX = (maxX - ins.Position.X); posY = fHeight + spacing; }
            else if (Math.Abs(ins.Position.X - maxX) < TOLERANCE) // מזרח
            { posX = bWidth + spacing + (ins.Position.Y - minY); posY = 0; }
            else if (Math.Abs(ins.Position.X - minX) < TOLERANCE) // מערב
            { posX = bWidth + spacing + (maxY - ins.Position.Y); posY = fHeight + spacing; }
            else return;

            DrawRect(doc, posX, posY + s, w, h, layer);
        }

        private void DrawRect(DxfDocument doc, double x, double y, double w, double h, Layer l)
        {
            doc.Entities.Add(new Line(new Vector2(x, y), new Vector2(x + w, y)) { Layer = l });
            doc.Entities.Add(new Line(new Vector2(x + w, y), new Vector2(x + w, y + h)) { Layer = l });
            doc.Entities.Add(new Line(new Vector2(x + w, y + h), new Vector2(x, y + h)) { Layer = l });
            doc.Entities.Add(new Line(new Vector2(x, y + h), new Vector2(x, y)) { Layer = l });
        }
    }
}