using UnityEngine;

/// <summary>
/// Provides look direction information for characters.
/// X &lt; 0 indicates left, X &gt; 0 indicates right.
/// When idle, the last non-zero direction should be returned.
/// </summary>
public interface ILookDirectionProvider
{
    /// <summary>
    /// Gets the current look direction.
    /// </summary>
    Vector2 LookDirection { get; }
}
