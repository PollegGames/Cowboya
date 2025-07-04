using System.Collections.Generic;

public interface INeighborConnector
{
    void Connect(IEnumerable<RoomWaypoint> allWaypoints);
}
