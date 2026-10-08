using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData : ISerializationCallbackReceiver
{
    public const int CurrentVersion = 1;
    private const string NameDefaultBot = "CowboyBot";

    public int SaveVersion = CurrentVersion;
    public string SaveName;
    public float MaxHealth = 100f;
    public float CurrentHealth = 100f;
    public float MaxEnergy = 100f;
    public float CurrentEnergy = 100f;
    public float EnergyRechargeRate = 5f;
    public float AttackEnergyCost = 5f;
    public float Morality;
    public List<string> UnlockedAttacks = new();
    public List<string> AttackOrder = new();
    public int Gears;
    public List<string> CollectedRobotParts = new();
    public List<string> UnlockedCharacters = new() { NameDefaultBot };
    public string LastChosenCharacter = NameDefaultBot;
    public float Volume = 0.5f;
    public bool FullScreen = true;

    [NonSerialized] public Dictionary<string, int> SpecialResources = new();
    [NonSerialized] public Dictionary<string, bool> MoralAlignmentInfluences = new();

    [SerializeField] private List<StringIntEntry> specialResourceEntries = new();
    [SerializeField] private List<StringBoolEntry> moralAlignmentEntries = new();

    public SaveData()
    {
        SaveName = Guid.NewGuid().ToString();
    }

    public void OnBeforeSerialize()
    {
        specialResourceEntries.Clear();
        foreach (KeyValuePair<string, int> entry in SpecialResources)
        {
            specialResourceEntries.Add(new StringIntEntry(entry.Key, entry.Value));
        }

        moralAlignmentEntries.Clear();
        foreach (KeyValuePair<string, bool> entry in MoralAlignmentInfluences)
        {
            moralAlignmentEntries.Add(new StringBoolEntry(entry.Key, entry.Value));
        }
    }

    public void OnAfterDeserialize()
    {
        SpecialResources = new Dictionary<string, int>();
        foreach (StringIntEntry entry in specialResourceEntries ?? new List<StringIntEntry>())
        {
            if (!string.IsNullOrWhiteSpace(entry.Key))
            {
                SpecialResources[entry.Key] = entry.Value;
            }
        }

        MoralAlignmentInfluences = new Dictionary<string, bool>();
        foreach (StringBoolEntry entry in moralAlignmentEntries ?? new List<StringBoolEntry>())
        {
            if (!string.IsNullOrWhiteSpace(entry.Key))
            {
                MoralAlignmentInfluences[entry.Key] = entry.Value;
            }
        }

        Normalize();
    }

    /// <summary>
    /// Restores safe defaults for fields missing from an older save version.
    /// </summary>
    public void Normalize()
    {
        SaveVersion = CurrentVersion;
        SaveName = string.IsNullOrWhiteSpace(SaveName) ? Guid.NewGuid().ToString() : SaveName;
        MaxHealth = Mathf.Max(1f, MaxHealth);
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, MaxHealth);
        MaxEnergy = Mathf.Max(0f, MaxEnergy);
        CurrentEnergy = Mathf.Clamp(CurrentEnergy, 0f, MaxEnergy);
        EnergyRechargeRate = Mathf.Max(0f, EnergyRechargeRate);
        AttackEnergyCost = Mathf.Max(0f, AttackEnergyCost);
        UnlockedAttacks ??= new List<string>();
        AttackOrder ??= new List<string>();
        CollectedRobotParts ??= new List<string>();
        UnlockedCharacters ??= new List<string>();
        if (!UnlockedCharacters.Contains(NameDefaultBot))
        {
            UnlockedCharacters.Add(NameDefaultBot);
        }

        LastChosenCharacter = string.IsNullOrWhiteSpace(LastChosenCharacter)
            ? NameDefaultBot
            : LastChosenCharacter;
        SpecialResources ??= new Dictionary<string, int>();
        MoralAlignmentInfluences ??= new Dictionary<string, bool>();
        Volume = Mathf.Clamp01(Volume);
    }

    [Serializable]
    private class StringIntEntry
    {
        public string Key;
        public int Value;

        public StringIntEntry(string key, int value)
        {
            Key = key;
            Value = value;
        }
    }

    [Serializable]
    private class StringBoolEntry
    {
        public string Key;
        public bool Value;

        public StringBoolEntry(string key, bool value)
        {
            Key = key;
            Value = value;
        }
    }
}
