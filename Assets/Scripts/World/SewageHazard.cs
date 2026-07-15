namespace Signal.World
{
    /// <summary>
    /// Sewage pool hazard: respawns the player at the last checkpoint and (in normal scenes) deals
    /// damage. Pure <see cref="HazardBase"/> reuse — future hazards are added the same way.
    /// </summary>
    public sealed class SewageHazard : HazardBase
    {
    }
}
