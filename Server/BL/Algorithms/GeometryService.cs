using Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BL.Algorithms
{
    public class GeometryService
    {
        public Dictionary<Point3D, List<Point3D>> BuildGraph(List<Wall> walls)
        {
            var graph = new Dictionary<Point3D, List<Point3D>>();

            foreach (var wall in walls)
            {
                AddEdge(graph, wall.StartPoint, wall.EndPoint);
                AddEdge(graph, wall.EndPoint, wall.StartPoint);
            }
            return graph;
        }

        public void AddEdge(Dictionary<Point3D, List<Point3D>> graph, Point3D from, Point3D to)
        {
            if (!graph.ContainsKey(from))
            {
                graph[from] = new List<Point3D>();
            }

            graph[from].Add(to);
        }

        public List<List<Point3D>> FindRooms(Dictionary<Point3D, List<Point3D>> graph)
        {
            var RoomList = new List<List<Point3D>>();
            var visitedPoints = new HashSet<Point3D>();

            foreach (var entry in graph)
            {
                Point3D startPoint = entry.Key;
                if (visitedPoints.Contains(startPoint)) 
                    continue;
                List<Point3D> newRoom = TraceRoom(startPoint, graph, visitedPoints);
                if (newRoom != null) 
                {
                    RoomList.Add(newRoom);
                }
            }
            return RoomList;
        }

        public List<Point3D>? TraceRoom(Point3D startPoint ,Dictionary<Point3D, List<Point3D>> graph, HashSet<Point3D> visitedPoints)
        {
            var currentPath = new List<Point3D>();
            currentPath.Add(startPoint);
            Point3D currentPoint=startPoint;
			Point3D PreviousPoint = new Point3D(0, startPoint.X - 1, startPoint.Y, 0);
			while (true)
            {
                var possibleNeighbors = new List<Point3D>(graph[currentPoint]);
                possibleNeighbors.Remove(PreviousPoint);

                if (possibleNeighbors.Count == 0)
                    return null;

                double baseAngle = Math.Atan2(PreviousPoint.Y - currentPoint.Y, PreviousPoint.X - currentPoint.X);
                Point3D? bestNeighbor = null;
                double minDiff = double.MaxValue;

                foreach (var neighbor in possibleNeighbors)
                {
                    double neighborAngle = Math.Atan2(neighbor.Y - currentPoint.Y, neighbor.X - currentPoint.X);

                    double diff = neighborAngle - baseAngle;

                    if (diff < 0)
                        diff += 2 * Math.PI;

                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        bestNeighbor = neighbor;
                    }
                }
                if (bestNeighbor!.Equals(startPoint))
                {
                    return currentPath;
                }

                currentPath.Add(bestNeighbor);
                visitedPoints.Add(bestNeighbor);

                PreviousPoint = currentPoint;
                currentPoint = bestNeighbor;

                if (currentPath.Count > 500) 
                    return null;
            }
        }

        public double CalculateArea(List<Point3D> points)
        {
            double area = 0;
            int j = points.Count - 1; 

            for (int i = 0; i < points.Count; i++)
            {
                area += (points[j].X + points[i].X) * (points[j].Y - points[i].Y);
                j = i;
            }

            return Math.Abs(area / 2.0);
        }

        public double CalculatePerimeter(List<Point3D> points)
        {
            double perimeter = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var current = points[i];
                var next = points[(i + 1) % points.Count];

                double distance = Math.Sqrt(Math.Pow(next.X - current.X, 2) + Math.Pow(next.Y - current.Y, 2));
                perimeter += distance;
            }
            return perimeter;
        }

        public bool IsPointInRoom(Point3D point, List<Point3D> roomPolygon)
        {
            bool isInside = false;
            for (int i = 0, j = roomPolygon.Count - 1; i < roomPolygon.Count; j = i++)
            {
                if (roomPolygon[i].Y > point.Y != roomPolygon[j].Y > point.Y &&
                    point.X < (roomPolygon[j].X - roomPolygon[i].X) * (point.Y - roomPolygon[i].Y) / (roomPolygon[j].Y - roomPolygon[i].Y) + roomPolygon[i].X)
                {
                    isInside = !isInside;
                }
            }
            return isInside;
        }

		public Point3D GetCentroid(List<Point3D> vertices)
		{
			if (vertices == null || vertices.Count == 0) return new Point3D(0, 0, 0, 0);
            double x = vertices.Average(v => v.X);
            double y = vertices.Average(v => v.Y);
            return new Point3D(0, x, y, 0 );
        }

        public bool IsPointInPolygon(Models.Point3D point, List<Models.Point3D> polygon)
        {
            bool isInside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if (((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y)) &&
                    (point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                {
                    isInside = !isInside;
                }
            }
            return isInside;
        }
    }
}
