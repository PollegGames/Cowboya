using System.Collections.Generic;
using UnityEngine;

public interface ICellProcessor
{
    void ProcessCells(Dictionary<Vector2, Cell> cellDataGrid);
}