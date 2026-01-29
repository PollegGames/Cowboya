/// <summary>
/// Defines the contract for machine reservation.
/// </summary>
public interface IMachineReservationService
{
    FactoryMachine ReserveFreeMachine(RoomManager room, RobotBrain worker);
    void ReleaseMachine(FactoryMachine machine);
    bool IsMachineReserved(FactoryMachine machine);
}
