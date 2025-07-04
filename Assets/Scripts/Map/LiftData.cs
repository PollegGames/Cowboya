using UnityEngine;

[CreateAssetMenu(fileName = "LiftSetup", menuName = "Map/LiftData")]
public class LiftData : ScriptableObject
{
    public Vector2 moveDirection = Vector2.up; // up or down
    public float moveDistance = 5f; // how much to move
    public float moveSpeed = 2f;
    [HideInInspector]
    public bool shouldMove = false; // NEW! Shared movement trigger
}