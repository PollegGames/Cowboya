public interface IMachineReservationService
{
    FactoryMachine ReserveFreeMachine(RoomManager room, EnemyWorkerController worker);
    void ReleaseMachine(FactoryMachine machine);
    bool IsMachineReserved(FactoryMachine machine);
}
