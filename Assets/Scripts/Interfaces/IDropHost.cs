using UnityEngine;

/// <summary>
/// Provides a container transform where dropped items should be parented.
/// </summary>
public interface IDropHost
{
    Transform DropContainer { get; }
}

