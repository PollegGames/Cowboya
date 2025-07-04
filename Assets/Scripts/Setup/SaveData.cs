using System;
using System.Collections.Generic;
using UnityEngine;

public class SaveData
{
    [field: SerializeField] public string SaveName { get; set; }
    [field: SerializeField] public float MaxHealth = 1f;
    [field: SerializeField] public float CurrentHealth = 1f;
    [field: SerializeField] public float MaxEnergy { get; set; } = 1f;
    [field: SerializeField] public float EnergyRechargeRate { get; set; } = 1f;
    [field: SerializeField] public float AttackEnergyCost { get; set; } = 10f;
    [field: SerializeField] public List<string> UnlockedAttacks { get; set; }
    [field: SerializeField] public List<string> AttackOrder { get; set; } // Customizable order of attacks
    [field: SerializeField] public int Gears { get; set; } = 0;// Total gears stored at the camp
    [field: SerializeField] public Dictionary<string, int> SpecialResources { get; set; } // e.g., "Rare Metal": 5, "Power Core": 2
    [field: SerializeField] public List<string> CollectedRobotParts { get; set; } // Parts collected for unlocking new characters
    [field: SerializeField] public List<string> UnlockedCharacters { get; set; } // e.g., "CowboyBot", "NinjaBot"
    [field: SerializeField] public string LastChosenCharacter { get; set; } // Character used in the last session
    [field: SerializeField] public Dictionary<string, bool> MoralAlignmentInfluences { get; set; } // e.g., "MercyKillUnlocked": true
    [field: SerializeField] public float Volume { get; set; } = 0.5f; // Audio settings
    [field: SerializeField] public bool FullScreen { get; set; } =true; // Screen mode

    protected const string NameDefaultBot = "CowboyBot";

    // Constructor to initialize all objects
    public SaveData()
    {
        Guid g = Guid.NewGuid();
        SaveName = g.ToString();
        UnlockedAttacks = new List<string>(); // Start with no unlocked attacks
        AttackOrder = new List<string>(); // Start with no attack order
        SpecialResources = new Dictionary<string, int>(); // Empty special resources
        CollectedRobotParts = new List<string>(); // No parts collected initially
        UnlockedCharacters = new List<string> { NameDefaultBot }; // Default character unlocked
        LastChosenCharacter = NameDefaultBot; // Default chosen character
        MoralAlignmentInfluences = new Dictionary<string, bool>(); // No influences initially
    }
}
