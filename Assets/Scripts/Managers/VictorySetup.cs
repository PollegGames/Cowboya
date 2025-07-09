using System;
using UnityEngine;

[CreateAssetMenu(fileName = "VictorySetup", menuName = "Map/Victory Setup")]
public class VictorySetup : ScriptableObject
{
    [Header("Win")]
    public int robotsSavedTarget;
    public int robotsKilledTarget;

    public int currentSaved;
    public int currentKilled;
}