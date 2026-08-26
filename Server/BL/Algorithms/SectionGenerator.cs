using netDxf;
using netDxf.Entities;
using netDxf.Header;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BL.Algorithms
{
    public class SectionGenerator
    {
        // --- הגדרות חוק ותקנים (בסנטימטרים) ---
        private const double FloorThickness = 20.0;     // עובי רצפה
        private const double CeilingHeight = 260.0;     // גובה תקרה נטו מריצוף לריצוף (תקני)
        private const double CeilingThickness = 20.0;   // עובי תקרה/גג
        private const double GroundLevel = -20.0;       // מפלס פיתוח החוץ (אדמה) ביחס ל-0.00 של הבית

        // פונקציה ראשית שמייצרת את שני החתכים באופן אוטומטי
        public Dictionary<string, DxfDocument> GenerateAutoSections(DxfDocument planDxf)
        {
            var result = new Dictionary<string, DxfDocument>();

            // 1. מציאת גבולות המבנה בשרטוט (Bounding Box) כדי לדעת איפה האמצע
            var allLines = planDxf.Entities.Lines.ToList();
            if (!allLines.Any()) return result; // אם השרטוט ריק

            double minX = allLines.Min(l => Math.Min(l.StartPoint.X, l.EndPoint.X));
            double maxX = allLines.Max(l => Math.Max(l.StartPoint.X, l.EndPoint.X));
            double minY = allLines.Min(l => Math.Min(l.StartPoint.Y, l.EndPoint.Y));
            double maxY = allLines.Max(l => Math.Max(l.StartPoint.Y, l.EndPoint.Y));

            double midX = (minX + maxX) / 2.0; // אמצע המבנה בציר X
            double midY = (minY + maxY) / 2.0; // אמצע המבנה בציר Y
            double margin = 100.0; // שוליים של מטר מחוץ למבנה לקווי החתך

            // 2. יצירת חתך AA (אופקי - חותך את המבנה לרוחב/אורך)
            Vector2 startAA = new Vector2(minX - margin, midY);
            Vector2 endAA = new Vector2(maxX + margin, midY);
            result.Add("Section_AA", CreateSingleSection(planDxf, startAA, endAA, isHorizontal: true));

            // 3. יצירת חתך BB (אנכי - חותך את המבנה לאורך/רוחב)
            Vector2 startBB = new Vector2(midX, minY - margin);
            Vector2 endBB = new Vector2(midX, maxY + margin);
            result.Add("Section_BB", CreateSingleSection(planDxf, startBB, endBB, isHorizontal: false));

            return result;
        }

        // פונקציה שמייצרת חתך בודד לפי קו חיתוך שמועבר אליה
        private DxfDocument CreateSingleSection(DxfDocument planDxf, Vector2 cutStart, Vector2 cutEnd, bool isHorizontal)
        {
            DxfDocument sectionDoc = new DxfDocument();
            double sectionLength = Vector2.Distance(cutStart, cutEnd);

            // 1. שרטוט קווי בסיס: רצפה ותקרה
            // קו ריצוף 0.00
            sectionDoc.Entities.Add(new Line(new Vector2(0, 0), new Vector2(sectionLength, 0)));
            // תחתית רצפה (בטון)
            sectionDoc.Entities.Add(new Line(new Vector2(0, -FloorThickness), new Vector2(sectionLength, -FloorThickness)));
            // קו תקרה נטו
            sectionDoc.Entities.Add(new Line(new Vector2(0, CeilingHeight), new Vector2(sectionLength, CeilingHeight)));
            // גג עליון
            sectionDoc.Entities.Add(new Line(new Vector2(0, CeilingHeight + CeilingThickness), new Vector2(sectionLength, CeilingHeight + CeilingThickness)));

            // 2. המצאת טופוגרפיה (מפלסי מגרש הגיוניים בצדדים)
            DrawTopography(sectionDoc, sectionLength);

            // 3. מציאת קירות נחתכים!
            // מומלץ לסנן כאן לפי שכבה, למשל: planDxf.Lines.Where(l => l.Layer.Name == "Walls")
            var walls = planDxf.Entities.Lines.ToList();
            List<double> hitPositions = new List<double>();

            foreach (var line in walls)
            {
                Vector2 lineStart = new Vector2(line.StartPoint.X, line.StartPoint.Y);
                Vector2 lineEnd = new Vector2(line.EndPoint.X, line.EndPoint.Y);

                // אם קו החתך פוגע בקו של הקיר
                if (TryGetIntersection(cutStart, cutEnd, lineStart, lineEnd, out Vector2 intersectionPoint))
                {
                    // חישוב המיקום של הקיר בתוך השרטוט של החתך החדש
                    double positionX = isHorizontal ? (intersectionPoint.X - cutStart.X) : (intersectionPoint.Y - cutStart.Y);
                    hitPositions.Add(positionX);
                }
            }

            // מיון נקודות החיתוך משמאל לימין
            hitPositions.Sort();

            // 4. ציור הקירות האנכיים בחתך
            // מכיוון שבתוכנית אדריכלית לקיר יש 2 קווים (פנים וחוץ), הקוד יפגע בשניהם ויצייר שני קווים אוטומטית!
            foreach (var posX in hitPositions)
            {
                Line verticalWallLine = new Line(new Vector2(posX, -FloorThickness), new Vector2(posX, CeilingHeight + CeilingThickness));
                sectionDoc.Entities.Add(verticalWallLine);
            }

            return sectionDoc;
        }

        // פונקציה שמציירת מפלסי אדמה מחוץ לבית (נותן תחושה של מגרש ולא בית באוויר)
        private void DrawTopography(DxfDocument doc, double length)
        {
            // אדמה בצד שמאל (יורדת במדרגה)
            doc.Entities.Add(new Line(new Vector2(-100, GroundLevel - 30), new Vector2(0, GroundLevel)));
            doc.Entities.Add(new Line(new Vector2(0, GroundLevel), new Vector2(0, -FloorThickness))); // חיבור לבניין

            // אדמה בצד ימין (יורדת בשיפוע קל)
            doc.Entities.Add(new Line(new Vector2(length, -FloorThickness), new Vector2(length, GroundLevel)));
            doc.Entities.Add(new Line(new Vector2(length, GroundLevel), new Vector2(length + 100, GroundLevel - 20)));
        }

        // אלגוריתם מתמטי למציאת נקודת חיתוך בין שני קטעים (Line Segments) בדו-מימד
        private bool TryGetIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 intersection)
        {
            intersection = new Vector2();
            double denominator = (p1.X - p2.X) * (p3.Y - p4.Y) - (p1.Y - p2.Y) * (p3.X - p4.X);

            // אם המכנה קרוב ל-0, הקווים מקבילים ולעולם לא יחתכו זה את זה
            if (Math.Abs(denominator) < 0.001) return false;

            double t = ((p1.X - p3.X) * (p3.Y - p4.Y) - (p1.Y - p3.Y) * (p3.X - p4.X)) / denominator;
            double u = -((p1.X - p2.X) * (p1.Y - p3.Y) - (p1.Y - p2.Y) * (p1.X - p3.X)) / denominator;

            // אם t ו-u נמצאים בין 0 ל-1, קיימת נקודת חיתוך ממשית על שני הקטעים
            if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
            {
                intersection = new Vector2(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
                return true;
            }

            return false;
        }
    }
}