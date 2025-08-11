using UnityEngine;

[CreateAssetMenu(fileName = "PlayerRunStats", menuName = "Player/RunStats")]
public class PlayerRunStats : ScriptableObject
{
    public float currentHealth;
    public float morality;
    private bool hasValues;

    public bool HasValues => hasValues;

    /// <summary>
    /// Captures temporary stats from the source robot.
    /// </summary>
    /// <param name="source">Robot providing current values.</param>
    public void Capture(RobotStats source)
    {
        if (source == null)
        {
            return;
        }

        currentHealth = Mathf.Clamp(source.CurrentHealth, 0f, source.MaxHealth);
        morality = source.Morality;
        hasValues = true;
    }

    /// <summary>
    /// Applies captured stats to the target robot.
    /// </summary>
    /// <param name="target">Robot receiving stored values.</param>
    public void Apply(RobotStats target)
    {
        if (target == null || !hasValues)
        {
            return;
        }

        float healthTarget = Mathf.Clamp(currentHealth, 0f, target.MaxHealth);
        target.UpdateHealth(healthTarget - target.CurrentHealth);
        target.UpdateMorality(morality - target.Morality);
    }

    /// <summary>
    /// Clears the stored run statistics.
    /// </summary>
    public void Reset()
    {
        currentHealth = 0f;
        morality = 0f;
        hasValues = false;
    }
}
