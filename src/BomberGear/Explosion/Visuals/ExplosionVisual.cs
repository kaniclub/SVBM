using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BomberGear.Explosion.Visuals;

internal sealed class ExplosionVisual
{
    public string LocationName { get; }
    public Vector2 Tile { get; }
    public ExplosionVisualKind Kind { get; }
    public float Rotation { get; }
    public SpriteEffects Effects { get; }
    public int TotalTicks { get; }
    public int TicksLeft { get; set; }

    public ExplosionVisual(
        string locationName,
        Vector2 tile,
        ExplosionVisualKind kind,
        float rotation,
        SpriteEffects effects,
        int totalTicks)
    {
        LocationName = locationName;
        Tile = tile;
        Kind = kind;
        Rotation = rotation;
        Effects = effects;
        TotalTicks = totalTicks;
        TicksLeft = totalTicks;
    }
}