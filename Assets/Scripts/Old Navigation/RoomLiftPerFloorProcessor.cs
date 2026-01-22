using System.Collections.Generic;
using UnityEngine;

public class RoomLiftPerFloorProcessor : CellProcessor
{
    public RoomLiftPerFloorProcessor(int gridWidth, int gridHeight) : base(gridWidth, gridHeight) { }

    public override void ProcessCells(Dictionary<Vector2, Cell> grid)
    {
        // Ensure every adjacent floor pair (y, y+1) has at least one vertical lift pair.
        for (int y = 0; y < height - 1; y++)
        {
            // 1) Already paired?
            if (HasVerticalLiftPair(grid, y, y + 1))
                continue;

            // 2) Try align: if one floor has lifts, place counterpart on the other floor under/over same x.
            var xsY   = FindLiftXsOnFloor(grid, y);
            var xsY1  = FindLiftXsOnFloor(grid, y + 1);

            bool paired = false;

            // Align y+1 under existing PathToPOI on y
            foreach (int x in xsY)
            {
                var cBelow = GetCell(grid, x, y + 1);
                if (IsReplaceable(cBelow))
                {
                    SetLift(cBelow);
                    paired = true;
                    break;
                }
            }

            if (!paired)
            {
                // Align y over existing PathToPOI on y+1
                foreach (int x in xsY1)
                {
                    var cAbove = GetCell(grid, x, y);
                    if (IsReplaceable(cAbove))
                    {
                        SetLift(cAbove);
                        paired = true;
                        break;
                    }
                }
            }

            // 3) If neither floor had a lift or alignment failed, create a fresh pair on a column
            if (!paired)
            {
                int chosenX = FindFirstReplaceableColumnForPair(grid, y, y + 1);
                if (chosenX >= 0)
                {
                    var cA = GetCell(grid, chosenX, y);
                    var cB = GetCell(grid, chosenX, y + 1);
                    SetLift(cA);
                    SetLift(cB);
                }
                else
                {
                    Debug.LogError($"RoomLiftPerFloorProcessor: No suitable vertical pair found between floors {y} and {y + 1}");
                }
            }
        }
    }

    // --- helpers ---

    private bool HasVerticalLiftPair(Dictionary<Vector2, Cell> grid, int yA, int yB)
    {
        // same x on both floors with PathToPOI
        for (int x = 0; x < width; x++)
        {
            var a = GetCell(grid, x, yA);
            var b = GetCell(grid, x, yB);
            if (a != null && b != null &&
                a.cellProperties.usageType == UsageType.PathToPOI &&
                b.cellProperties.usageType == UsageType.PathToPOI)
            {
                return true;
            }
        }
        return false;
    }

    private List<int> FindLiftXsOnFloor(Dictionary<Vector2, Cell> grid, int y)
    {
        var list = new List<int>();
        for (int x = 0; x < width; x++)
        {
            var c = GetCell(grid, x, y);
            if (c != null && c.cellProperties.usageType == UsageType.PathToPOI)
                list.Add(x);
        }
        return list;
    }

    private int FindFirstReplaceableColumnForPair(Dictionary<Vector2, Cell> grid, int yA, int yB)
    {
        // preference: center-ish columns first (optional but nicer)
        // simple left-to-right scan is fine too; keep it deterministic.
        for (int x = 0; x < width; x++)
        {
            var a = GetCell(grid, x, yA);
            var b = GetCell(grid, x, yB);
            if (IsReplaceable(a) && IsReplaceable(b))
                return x;
        }
        return -1;
    }

    private Cell GetCell(Dictionary<Vector2, Cell> grid, int x, int y)
    {
        var key = new Vector2(x, y);
        grid.TryGetValue(key, out var cell);
        return cell;
    }

    private bool IsReplaceable(Cell c)
    {
        if (c == null) return false;

        // Adjust this whitelist to your project’s enum:
          // We avoid overwriting Start/End/Doors/etc. Keep what you had (Blocked/POI/Work) plus any "WORK" mapping if needed.
          var t = c.cellProperties.usageType;
          return t == UsageType.Work
              || t == UsageType.Blocked
              || t == UsageType.POI
              || t == UsageType.PathToPOI; // allow aligning existing lifts too
    }

    private void SetLift(Cell c)
    {
        c.cellProperties.usageType = UsageType.PathToPOI;
        // If you track lift metadata (e.g., hasLift=true), set it here as well.
        // c.cellProperties.hasLift = true;
    }
}
