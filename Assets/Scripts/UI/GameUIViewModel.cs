using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class GameUIViewModel : MonoBehaviour
{
    public VisualElement ui;
    private RobotStateController robotBehaviour;
    [SerializeField] private RunMapConfigSO config;
    [SerializeField] private VictorySetup victorySetup;

    private Label robotsSavedLabel;
    private Label robotsKillsLabel;
    private void Awake()
    {
        ui = GetComponent<UIDocument>().rootVisualElement;
        robotsSavedLabel = ui.Q<Label>("robots-saved-label");
        robotsKillsLabel = ui.Q<Label>("robots-kills-label");
        UpdateVictoryStatsUI();
    }

    public void SetPlayer(RobotStateController robot)
    {
        if (robot != null && robot.Stats != null)
        {
            robotBehaviour = robot; // Store the instance reference
            RobotStats robotInfo = robot.Stats;

            // Subscribe to PlayerStats events
            robotInfo.OnEnergyChanged += UpdateEnergyBar;
            robotInfo.OnHealthChanged += UpdateHealthBar;

            // Initial UI update
            UpdateEnergyBar();
            UpdateHealthBar();

            Debug.Log("Health and energy bars bound to PlayerStateController.");
        }
        else
        {
            Debug.LogError("PlayerStateController or PlayerStats is null!");
        }
    }

    private void UpdateEnergyBar()
    {
        if (robotBehaviour != null && robotBehaviour.Stats != null)
        {
            float currentEnergy = robotBehaviour.Stats.CurrentEnergy;
            // Debug.Log("currentEnergy of PlayerStats: " + currentEnergy);
            ui.Q<EnergyBar>().currentEnergy = currentEnergy; // Assuming EnergyBar is a VisualElement
            ui.Q<EnergyBar>().MarkDirtyRepaint();
        }
    }

    private void UpdateHealthBar()
    {
        if (robotBehaviour != null && robotBehaviour.Stats != null)
        {
            float currentHealth = robotBehaviour.Stats.CurrentHealth;
            ui.Q<HealthBar>().currentHealth = currentHealth; // Assuming HealthBar is a VisualElement
            ui.Q<HealthBar>().MarkDirtyRepaint();
        }
    }

    public void UpdateVictoryStatsUI()
    {
        if (victorySetup != null)
        {
            Debug.Log($"Updating victory stats: {victorySetup.currentSaved}/{victorySetup.robotsSavedTarget}, {victorySetup.currentKilled}/{victorySetup.robotsKilledTarget}");
            robotsSavedLabel.text = $"Robots saved {victorySetup.currentSaved}/{victorySetup.robotsSavedTarget}";
            robotsKillsLabel.text = $"Robots kills {victorySetup.currentKilled}/{victorySetup.robotsKilledTarget}";
        }
    }

}
