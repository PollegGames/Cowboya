public static class GameMessages
{
    public static class Hints
    {
        public static GameMessage MovementEnergy => new("[WASD] to move. Moving drains energy.", MessageSpeaker.Narrator);
        public static GameMessage TargetAttack => new("Hold [Right Click] to target. Press [Left Click] to attack.", MessageSpeaker.Narrator);
        public static GameMessage InteractGrab => new("[Right Click] to interact or grab.", MessageSpeaker.Narrator);
        public static GameMessage Health => new("Health critical? Find [batteries] to recharge.", MessageSpeaker.Narrator);
        public static GameMessage MachinesSaving => new("Use [machines] to save progress.", MessageSpeaker.Narrator);
        public static GameMessage Security => new("Security bots drop [badges] that unlock restricted doors.", MessageSpeaker.Narrator);
        public static GameMessage Alarm => new("Triggered [alarm]? Hide or fight.", MessageSpeaker.Narrator);
        public static GameMessage ObjectiveExit => new("Check the [objective] and head to the [exit].", MessageSpeaker.Narrator);
        public static GameMessage FinalRoomReminder => new("Final room: remember the [exit].", MessageSpeaker.Narrator);
    }

    public static class System
    {
        public static GameMessage Start => new("Boot sequence complete. Good luck.", MessageSpeaker.Narrator);
        public static GameMessage GameOver => new("System failure. Run terminated.", MessageSpeaker.Narrator);
        public static GameMessage Victory => new("Mission accomplished.", MessageSpeaker.Narrator);
    }
}
