using UnityEngine;

public class Cell
{
    public Vector2 position;
    public CellProperties cellProperties;

    public int fCost, gCost, hCost;

    public Cell(Vector2 pos, UsageType usageType = UsageType.Empty)
    {
        position = pos;
        gCost = int.MaxValue;
        this.cellProperties = new CellProperties()
        {
            usageType = usageType,
        };
    }
}