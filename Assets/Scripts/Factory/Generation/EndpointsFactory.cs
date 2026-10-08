using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Creates endpoints instances.
/// </summary>
public class EndpointsFactory
{
    public void GetCornerEndpoints(int width, int height, out Vector2 start, out Vector2 end)
    {
        Vector2[] corners = new[]
        {
            new Vector2(0,          0),
            new Vector2(width - 1,  0),
            new Vector2(0,          height - 1),
            new Vector2(width - 1,  height - 1)
        }.Distinct().ToArray();
        if (corners.Length < 2)
            throw new ArgumentException("A generated map requires dimensions that provide at least two distinct cells.");

        int idx1 = Random.Range(0, corners.Length);
        int idx2 = Random.Range(0, corners.Length - 1);
        if (idx2 >= idx1)
            idx2++;

        start = corners[idx1];
        end   = corners[idx2];
    }

    public List<Vector2> GetRandomPoints(int width, int height, int count, HashSet<Vector2> exclude)
    {
        var list = new List<Vector2>();
        for (int i = 0; i < count; i++)
        {
            list.Add(GetDistinctPosition(exclude, width, height));
        }
        return list;
    }

    private Vector2 GetDistinctPosition(HashSet<Vector2> used, int w, int h)
    {
        Vector2 pos;
        do
        {
            pos = new Vector2(Random.Range(0, w), Random.Range(0, h));
        } while (used.Contains(pos));
        
        used.Add(pos);
        return pos;
    }
}

