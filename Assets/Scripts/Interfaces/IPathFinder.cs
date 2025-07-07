using System.Collections.Generic;

public interface IPathFinder
{
    List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end);
    void BuildAllNeighbors();
}
