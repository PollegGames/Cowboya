public static class GameMessages
{
    public static class Intro
    {
        public static GameMessage Welcome => new("Welcome to the factory. Stay hidden.", MessageSpeaker.DrHex);
        public static GameMessage InstallModules => new("Install your first modules here.", MessageSpeaker.DrHex);
        public static GameMessage FirstAttack => new("Try your punch on the test robot.", MessageSpeaker.DrHex);
    }

    public static class Room
    {
        public static GameMessage EnterNeutralRoom => new("This is a neutral room. Watch the robots.", MessageSpeaker.Player);
        public static GameMessage EnterSecurityRoom => new("Security guards are here. Stay sharp.", MessageSpeaker.Player);
        public static GameMessage ElevatorHint => new("This lift seems functional.", MessageSpeaker.DrHex);
    }

    public static class Alarm
    {
        public static GameMessage AlarmTriggered => new("Alert triggered. Hide or fight!", MessageSpeaker.DrHex);
        public static GameMessage AlarmCleared => new("Alarm deactivated. Good job.", MessageSpeaker.DrHex);
    }

    public static class Combat
    {
        public static GameMessage LowEnergy => new("I'm losing energy...", MessageSpeaker.Player);
        public static GameMessage EnemyDefeated => new("One down!", MessageSpeaker.Player);
    }

    public static class Lift
    {
        public static GameMessage GoingUp => new("Going up.", MessageSpeaker.DrHex);
        public static GameMessage BlockedLift => new("Something's blocking the lift.", MessageSpeaker.Player);
    }

    public static class POI
    {
        public static GameMessage WorkstationFound => new("A workstation... could be useful.", MessageSpeaker.Player);
        public static GameMessage RestZone => new("Looks like a rest zone for the bots.", MessageSpeaker.Player);
    }

    public static class System
    {
        public static GameMessage Start => new("Boot sequence complete. Good luck.", MessageSpeaker.Narrator);
        public static GameMessage GameOver => new("System failure. Run terminated.", MessageSpeaker.Narrator);
        public static GameMessage Victory => new("Mission accomplished.", MessageSpeaker.Narrator);
    }
}
