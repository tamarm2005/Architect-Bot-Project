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
                SplitStrip(plot, rooms.OrderByDescending(r => r.Area).ToList(), placedRooms);
                return placedRooms;
            }

            double totalArea = rooms.Sum(r => r.Area);
            double livingShare = Math.Max(0.2, Math.Min(0.5, livingRoom.Area / totalArea));

            double livingWidth = plot.Width * Math.Sqrt(livingShare);
            double livingLength = plot.Length * Math.Sqrt(livingShare);
            double livingX = plot.X + (plot.Width - livingWidth) / 2;
            double livingY = plot.Y + (plot.Length - livingLength) / 2;

            livingRoom.PositionX = livingX;
            livingRoom.PositionY = livingY;
            livingRoom.Width = livingWidth;
            livingRoom.Length = livingLength;
            placedRooms.Add(livingRoom);

            // 🌟 4 רצועות סביב הסלון - כל רצועה צמודה ישירות אליו, בלי מסדרון בכלל
            double topH = livingY - plot.Y;
            double bottomH = (plot.Y + plot.Length) - (livingY + livingLength);
            double leftW = livingX - plot.X;
            double rightW = (plot.X + plot.Width) - (livingX + livingWidth);

            var strips = new List<Rectangle2D>
            {
                new Rectangle2D { X = plot.X, Y = plot.Y, Width = plot.Width, Length = topH },
                new Rectangle2D { X = plot.X, Y = livingY + livingLength, Width = plot.Width, Length = bottomH },
                new Rectangle2D { X = plot.X, Y = livingY, Width = leftW, Length = livingLength },
                new Rectangle2D { X = livingX + livingWidth, Y = livingY, Width = rightW, Length = livingLength }
            }.Where(s => s.Width > 0.3 && s.Length > 0.3).OrderByDescending(s => s.Area).ToList();

            if (strips.Count == 0) strips.Add(plot);

            var stripAssignments = strips.Select(s => new List<ArchitecturalRoom>()).ToList();
            for (int i = 0; i < otherRooms.Count; i++)
            {
                stripAssignments[i % strips.Count].Add(otherRooms[i]);
            }

            for (int i = 0; i < strips.Count; i++)
            {
                SplitStrip(strips[i], stripAssignments[i], placedRooms);
            }

            return placedRooms;
        }

        // ממלא רצועה ברהיטים ברצף לאורך הציר הארוך שלה - כל חדר ברצועה נוגע ישירות בסלון כי הרצועה עצמה צמודה אליו
        private void SplitStrip(Rectangle2D area, List<ArchitecturalRoom> rooms, List<ArchitecturalRoom> result)
        {
            if (rooms.Count == 0) return;

            bool horizontal = area.Width >= area.Length;
            double longDim = horizontal ? area.Width : area.Length;
            double shortDim = horizontal ? area.Length : area.Width;

            // 🌟 אם הרצועה ארוכה מדי ביחס לרוחבה ויש כמה חדרים - מפצלים לשתי שורות במקום שורה אחת ארוכה
            if (rooms.Count > 1 && longDim / Math.Max(shortDim, 0.1) > 2.2)
            {
                int half = Math.Max(1, rooms.Count / 2);
                var group1 = rooms.Take(half).ToList();
                var group2 = rooms.Skip(half).ToList();

                Rectangle2D sub1, sub2;
                if (horizontal)
                {
                    sub1 = new Rectangle2D { X = area.X, Y = area.Y, Width = area.Width, Length = area.Length / 2 };
                    sub2 = new Rectangle2D { X = area.X, Y = area.Y + area.Length / 2, Width = area.Width, Length = area.Length / 2 };
                }
                else
                {
                    sub1 = new Rectangle2D { X = area.X, Y = area.Y, Width = area.Width / 2, Length = area.Length };
                    sub2 = new Rectangle2D { X = area.X + area.Width / 2, Y = area.Y, Width = area.Width / 2, Length = area.Length };
                }

                SplitStrip(sub1, group1, result);
                SplitStrip(sub2, group2, result);
                return;
            }

            double remainingArea = rooms.Sum(r => r.Area);
            double totalLength = longDim;
            double usedLength = 0;

            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                bool isLast = i == rooms.Count - 1;
                double sliceLength = isLast ? (totalLength - usedLength) : (totalLength - usedLength) * (room.Area / remainingArea);

                if (horizontal)
                {
                    room.PositionX = area.X + usedLength;
                    room.PositionY = area.Y;
                    room.Width = sliceLength;
                    room.Length = area.Length;
                }
                else
                {
                    room.PositionX = area.X;
                    room.PositionY = area.Y + usedLength;
                    room.Width = area.Width;
                    room.Length = sliceLength;
                }

                usedLength += sliceLength;
                remainingArea -= room.Area;
                result.Add(room);
            }
        }
    }
}