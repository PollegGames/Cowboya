using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the contract for cell.
/// </summary>
public interface ICellProcessor
{
    void ProcessCells(Dictionary<Vector2, Cell> cellDataGrid);
}
