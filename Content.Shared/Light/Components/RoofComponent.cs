using Robust.Shared.GameStates;

namespace Content.Shared.Light.Components;

/// <summary>
/// Will draw shadows over tiles flagged as roof tiles on the attached grid.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RoofComponent : Component
{
    public const int ChunkSize = 8;

    [DataField, AutoNetworkedField]
    public Color Color = new (80, 80, 80); //CP14 Black color replaced on Dark gray

    /// <summary>
    /// Chunk origin and bitmask of value in chunk.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<Vector2i, ulong> Data = new();
}
