using System.Collections.Generic;
using UnityEngine;

public static class LineSimplification
{
    public static List<Vector2> RamerDouglasPeucker(List<Vector2> points, double epsilon)
    {
        if (points == null || points.Count < 3)
            return points;

        int firstPoint = 0;
        int lastPoint = points.Count - 1;
        List<int> pointIndexsToKeep = new List<int>();

        // Add the first and last index to the keepers
        pointIndexsToKeep.Add(firstPoint);
        pointIndexsToKeep.Add(lastPoint);

        // The first and the last point cannot be the same
        while (points[firstPoint].Equals(points[lastPoint]))
        {
            lastPoint--;
        }

        SimplifySection(points, firstPoint, lastPoint, epsilon, ref pointIndexsToKeep);

        List<Vector2> returnPoints = new List<Vector2>();
        pointIndexsToKeep.Sort();
        foreach (int index in pointIndexsToKeep)
        {
            returnPoints.Add(points[index]);
        }

        return returnPoints;
    }

    private static void SimplifySection(List<Vector2> points, int firstPoint, int lastPoint, double epsilon, ref List<int> pointIndexsToKeep)
    {
        double maxDistance = 0;
        int indexFarthest = 0;

        for (int index = firstPoint; index < lastPoint; index++)
        {
            double distance = PerpendicularDistance(points[firstPoint], points[lastPoint], points[index]);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                indexFarthest = index;
            }
        }

        if (maxDistance > epsilon && indexFarthest != 0)
        {
            // Add the index of the point with the maximum distance
            pointIndexsToKeep.Add(indexFarthest);
            SimplifySection(points, firstPoint, indexFarthest, epsilon, ref pointIndexsToKeep);
            SimplifySection(points, indexFarthest, lastPoint, epsilon, ref pointIndexsToKeep);
        }
    }

    public static double PerpendicularDistance(Vector2 Point1, Vector2 Point2, Vector2 Point)
    {
        // Area = |(1/2)(x1y2 + x2y3 + x3y1 - y1x2 - y2x3 - y3x1)|
        double area = Mathf.Abs((float)(.5 * (Point1.x * Point2.y + Point2.x * Point.y + Point.x * Point1.y - Point1.y * Point2.x - Point2.y * Point.x - Point.y * Point1.x)));
        double bottom = Mathf.Sqrt(Mathf.Pow(Point1.x - Point2.x, 2) + Mathf.Pow(Point1.y - Point2.y, 2));
        double height = area / bottom * 2;

        return height;
    }
}
