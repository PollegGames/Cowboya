using UnityEngine;

[CreateAssetMenu(fileName = "SquareMovementData", menuName = "Test/SquareMovementData")]
public class SquareMovementData : ScriptableObject
{
    public float currentSpeed; // Current speed of the square
    public float maxSpeed = 100f; // Maximum speed for the square
}
