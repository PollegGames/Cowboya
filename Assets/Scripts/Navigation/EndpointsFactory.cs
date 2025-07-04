using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class EndpointsFactory
{
    public void GetCornerEndpoints(int width, int height, out Vector2 start, out Vector2 end)
    {
        Vector2[] corners =
        {
            new Vector2(0,          0),
            new Vector2(width - 1,  0),
            new Vector2(0,          height - 1),
            new Vector2(width - 1,  height - 1)
        };
        int idx1 = Random.Range(0, corners.Length);
        int idx2;
        do idx2 = Random.Range(0, corners.Length);
        while (idx2 == idx1);

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

