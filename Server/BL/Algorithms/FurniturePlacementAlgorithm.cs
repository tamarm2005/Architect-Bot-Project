using System;
using System.Collections.Generic;
using System.Linq;
using Models;

namespace BL.Algorithms
{
    public class FurniturePlacementAlgorithm
    {
        private RoomSolver _roomSolver = new RoomSolver();

        public void RunPlacement(Room room)
        {
            if (room?.furniture == null || room.furniture.Count == 0) return;

            // 🌟 רהיטים "קבועים" (כמו רצף מקרר-כיור-כיריים) לא עוברים דרך האלגוריתם - הם כבר במקום הנכון
            var fixedFurniture = room.furniture.Where(f => f.IsFixed).ToList();
            var sortedFurniture = room.furniture.Where(f => !f.IsFixed).OrderByDescending(f => f.Width * f.Length).ToList();
            var placedFurniture = new List<Furniture>(fixedFurniture); // מתחילים עם הקבועים כבר "תפוסים" למניעת התנגשות איתם
            var unplaceable = new List<Furniture>();

            foreach (var f in sortedFurniture)
            {
                double bestScore = double.MinValue;
                double bestX = 0, bestY = 0, bestRotation = 0;
                bool foundValidPlacement = false;

                for (double x = 0; x <= room.Width - f.Width; x += 0.20)
                {
                    for (double y = 0; y <= room.Length - f.Length; y += 0.20)
                    {
                        foreach (double rotation in new double[] { 0, 90, 180, 270 })
                        {
                            f.Location = new Point3D(0, x, y, 0 );
                            f.Rotation = rotation;

                            if (_roomSolver.IsInsideRoom(f, room))
                            {
                                double currentScore = _roomSolver.CalculateScore(room, placedFurniture, f);
                                if (currentScore > bestScore)
                                {
                                    bestScore = currentScore;
                                    bestX = x; bestY = y; bestRotation = rotation;
                                    foundValidPlacement = true;
                                }
                            }
                        }
                    }
                }

                if (foundValidPlacement)
                {
                    f.Location = new Point3D(0, bestX, bestY, 0 );
                    f.Rotation = bestRotation;

                    // 🌟 גם אם נמצא מיקום "בתוך החדר", בודקים שהוא לא חופף בפועל למשהו שכבר שם
                    if (!_roomSolver.HasCollisionWithPlaced(placedFurniture, f))
                    {
                        placedFurniture.Add(f);
                    }
                    else
                    {
                        unplaceable.Add(f);
                    }
                }
                else
                {
                    unplaceable.Add(f);
                }
            }

            foreach (var f in unplaceable) room.furniture.Remove(f);
        }
    }
}