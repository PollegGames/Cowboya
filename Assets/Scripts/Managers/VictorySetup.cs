using System;
using UnityEngine;

[CreateAssetMenu(fileName = "VictorySetup", menuName = "Map/Victory Setup")]
public class VictorySetup : ScriptableObject
{
    [Header("Win")]
    public int robotsSavedTarget;
    public int robotsKilledTarget;

    [NonSerialized] public int currentSaved = 0;
    [NonSerialized] public int currentKilled = 0;

    public void RegisterRobotSaved() => currentSaved++;
    public void RegisterRobotKilled() => currentKilled++;
}