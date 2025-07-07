using System.Collections.Generic;
using UnityEngine;

public interface IGridBuilder
{
    Dictionary<Vector2, Cell> BuildGrid(int width, int height, int wallCount, int poiCount);
}
