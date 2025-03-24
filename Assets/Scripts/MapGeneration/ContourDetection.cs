using System.Collections.Generic;
using UnityEngine;

public class ContourDetection : MonoBehaviour
{
    public Texture2D mapTexture;
    public float sphereScale = 0.5f; // Adjust the scale of the spheres if needed
    public float mapScale = 0.01f;
    public Vector2 offset =Vector2.zero;
    private List<List<Vector2>> continents;


    void Start()
    {
        List<List<Vector2>> rawContours = GetContours(mapTexture);
        continents = new List<List<Vector2>>();
        foreach (var contour in rawContours)
        {
            List<Vector2> simplifiedContour = LineSimplification.RamerDouglasPeucker(contour, 1.0); // Set your tolerance
            if (simplifiedContour.Count > 300)
            {
                simplifiedContour = LineSimplification.RamerDouglasPeucker(contour, 3.0); // Increase tolerance if still over 300 points
            }
            continents.Add(simplifiedContour);
        }
    }



    List<List<Vector2>> GetContours(Texture2D texture)
    {
        bool[,] visited = new bool[texture.width, texture.height];
        List<List<Vector2>> contours = new List<List<Vector2>>();

        for (int x = 0; x < texture.width; x++)
        {
            for (int y = 0; y < texture.height; y++)
            {
                if (!visited[x, y] && texture.GetPixel(x, y).grayscale > 0.5)
                {
                    List<Vector2> contour = TraceContour(texture, visited, x, y);
                    if (contour.Count > 0)
                    {
                        contours.Add(contour);
                    }
                }
            }
        }

        return contours;
    }

    List<Vector2> TraceContour(Texture2D texture, bool[,] visited, int startX, int startY)
    {
        List<Vector2> contour = new List<Vector2>();
        int x = startX, y = startY;
        do
        {
            contour.Add(new Vector2(x, y));
            visited[x, y] = true;

            // Check neighbors clockwise starting from left
            if (x > 0 && !visited[x - 1, y] && texture.GetPixel(x - 1, y).grayscale > 0.5)
            {
                x--;
            }
            else if (y > 0 && !visited[x, y - 1] && texture.GetPixel(x, y - 1).grayscale > 0.5)
            {
                y--;
            }
            else if (x < texture.width - 1 && !visited[x + 1, y] && texture.GetPixel(x + 1, y).grayscale > 0.5)
            {
                x++;
            }
            else if (y < texture.height - 1 && !visited[x, y + 1] && texture.GetPixel(x, y + 1).grayscale > 0.5)
            {
                y++;
            }
            else
            {
                break;
            }

        } while (x != startX || y != startY); // Loop until you return to the start point

        return contour;
    }

    void DrawContours(List<List<Vector2>> contours)
    {
        foreach (var contour in contours)
        {
            foreach (var point in contour)
            {
                Gizmos.DrawWireSphere(new Vector3((point.x*mapScale)+offset.x, 0, (point.y*mapScale)+offset.y), sphereScale);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if(continents!=null)
        DrawContours(continents);
    }
}
