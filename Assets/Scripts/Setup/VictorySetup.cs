using System;
using UnityEngine;

[CreateAssetMenu(fileName = "VictorySetup", menuName = "Map/Victory Setup")]
public class VictorySetup : ScriptableObject
{
    private const int FirstVictoryGoalLevel = 7;
    private const int InitialSavedTarget = 2;
    private const int InitialKilledTarget = 1;

    [Header("Win")]
    public int robotsSavedTarget;
    public int robotsKilledTarget;

    public int currentSaved;
    public int currentKilled;

    /// <summary>
    /// Configures victory targets for a normal playable level.
    /// Levels before seven have an immediately available exit. From level seven,
    /// both targets grow by one per level and morality adds one to one target.
    /// </summary>
    public void ConfigureForLevel(int playableLevel, float morality)
    {
        if (playableLevel < FirstVictoryGoalLevel)
        {
            robotsSavedTarget = 0;
            robotsKilledTarget = 0;
            return;
        }

        int levelIncrease = playableLevel - FirstVictoryGoalLevel;
        robotsSavedTarget = InitialSavedTarget + levelIncrease;
        robotsKilledTarget = InitialKilledTarget + levelIncrease;

        if (morality < 0f)
            robotsKilledTarget++;
        else
            robotsSavedTarget++;
    }

    /// <summary>
    /// Returns true when the exit is freely available or an enabled goal is reached.
    /// </summary>
    public bool HasMetAnyGoal()
    {
        if (robotsSavedTarget <= 0 && robotsKilledTarget <= 0)
            return true;

        bool hasMetKillGoal = robotsKilledTarget > 0 && currentKilled >= robotsKilledTarget;
        bool hasMetSaveGoal = robotsSavedTarget > 0 && currentSaved >= robotsSavedTarget;
        return hasMetKillGoal || hasMetSaveGoal;
    }
}
