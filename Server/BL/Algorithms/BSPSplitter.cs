using CadApplication.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BL.Algorithms
{
    public class BSPSplitter
    {
        public List<ArchitecturalRoom> Run(Rectangle2D plot, List<ArchitecturalRoom> rooms)
        {
            var placedRooms = new List<ArchitecturalRoom>();
            if (rooms == null || rooms.Count == 0) return placedRooms;

            var livingRoom = rooms.FirstOrDefault(r => r.Name != null && r.Name.Contains("סלון"));
            var otherRooms = rooms.Where(r => r != livingRoom).OrderByDescending(r => r.Area).ToList();

            if (livingRoom == null || otherRooms.Count == 0)
            {
                SplitMosaic(plot, rooms.OrderByDescending(r => r.Area).ToList(), placedRooms);
                return placedRooms;
            }

            double totalArea = rooms.Sum(r => r.Area);
            double livingShare = Math.Max(0.15, Math.Min(0.4, livingRoom.Area / totalArea));

            // 🌟 הסלון תופס צד שלם (לא מרכז!) - כמו קיר קדמי אמיתי, עם גישה טבעית לכניסה
            bool splitHorizontally = plot.Width >= plot.Length;

            Rectangle2D livingRect, restRect;
            if (splitHorizontally)
            {
                double livingHeight = plot.Length * livingShare;
                livingRect = new Rectangle2D { X = plot.X, Y = plot.Y, Width = plot.Width, Length = livingHeight };
                restRect = new Rectangle2D { X = plot.X, Y = plot.Y + livingHeight, Width = plot.Width, Length = plot.Length - livingHeight };
            }
            else
            {
                double livingWidth = plot.Width * livingShare;
                livingRect = new Rectangle2D { X = plot.X, Y = plot.Y, Width = livingWidth, Length = plot.Length };
                restRect = new Rectangle2D { X = plot.X + livingWidth, Y = plot.Y, Width = plot.Width - livingWidth, Length = plot.Length };
            }

            livingRoom.PositionX = livingRect.X;
            livingRoom.PositionY = livingRect.Y;
            livingRoom.Width = livingRect.Width;
            livingRoom.Length = livingRect.Length;
            placedRooms.Add(livingRoom);

            // 🌟 שאר החדרים מתחלקים במוזאיקה מאוזנת (לא רצועה ארוכה אחת!) - כל פיצול הוא לשניים, וחוזר רקורסיבית
            SplitMosaic(restRect, otherRooms, placedRooms);
            return placedRooms;
        }

        private void SplitMosaic(Rectangle2D area, List<ArchitecturalRoom> rooms, List<ArchitecturalRoom> result)
        {
            if (rooms.Count == 0) return;

            if (rooms.Count == 1)
            {
                var only = rooms[0];
                only.PositionX = area.X;
                only.PositionY = area.Y;
                only.Width = area.Width;
                only.Length = area.Length;
                result.Add(only);
                return;
            }

            double totalArea = rooms.Sum(r => r.Area);
            double running = 0;
            int splitIndex = 1;
            for (int i = 0; i < rooms.Count; i++)
            {
                running += rooms[i].Area;
                if (running >= totalArea / 2.0) { splitIndex = i + 1; break; }
            }
            if (splitIndex >= rooms.Count) splitIndex = rooms.Count - 1;

            var group1 = rooms.Take(splitIndex).ToList();
            var group2 = rooms.Skip(splitIndex).ToList();
            double group1Area = group1.Sum(r => r.Area);
            double ratio = Math.Max(0.25, Math.Min(0.75, group1Area / totalArea));

            bool horizontal = area.Width >= area.Length;
            Rectangle2D rect1, rect2;
            if (horizontal)
            {
                double w = area.Width * ratio;
                rect1 = new Rectangle2D { X = area.X, Y = area.Y, Width = w, Length = area.Length };
                rect2 = new Rectangle2D { X = area.X + w, Y = area.Y, Width = area.Width - w, Length = area.Length };
            }
            else
            {
                double l = area.Length * ratio;
                rect1 = new Rectangle2D { X = area.X, Y = area.Y, Width = area.Width, Length = l };
                rect2 = new Rectangle2D { X = area.X, Y = area.Y + l, Width = area.Width, Length = area.Length - l };
            }

            SplitMosaic(rect1, group1, result);
            SplitMosaic(rect2, group2, result);
        }
    }
}