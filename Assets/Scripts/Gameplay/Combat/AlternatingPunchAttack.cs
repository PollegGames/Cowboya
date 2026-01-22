using System;
using UnityEngine;

/// <summary>
/// Legacy procedural punch behaviour that is kept for backward compatibility.
/// The system now relies on animation events to drive hitboxes, so this script
/// immediately disables itself when present on a prefab.
/// </summary>
[AddComponentMenu("")]
[Obsolete("AlternatingPunchAttack has been replaced by animation event driven punches.")]
public sealed class AlternatingPunchAttack : MonoBehaviour
{
    private void Awake()
    {
#if UNITY_EDITOR
        Debug.LogWarning(
            "AlternatingPunchAttack is obsolete and will be removed once prefabs are updated.",
            this);
#endif
        enabled = false;
    }
}
