// ----------------------------
// 爆風が当たったタイルに対して、
// TerrainFeature / Object / ResourceClump のどれを処理するかを振り分ける
// ----------------------------
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace BomberGear.Explosion.Breakables;

internal sealed class BreakableResolver
{
    private readonly TerrainBreaker terrainBreaker = new();
    private readonly ObjectBreaker objectBreaker = new();
    private readonly ClumpBreaker clumpBreaker = new();

    // ----------------------------
    // そのタイルの爆風挙動を判定して必要なら破壊処理も行う
    // ----------------------------
    public BlastBehavior Resolve(GameLocation location, Vector2 tile, int power)
    {
        var terrain = FindTerrain(location, tile);
        if (terrain is not null)
            return terrainBreaker.Resolve(location, tile, terrain, power);

        var obj = FindObject(location, tile);
        if (obj is not null)
            return objectBreaker.Resolve(location, tile, obj, power);

        var clump = FindClump(location, tile);
        if (clump is not null)
            return clumpBreaker.Resolve(location, clump, tile, power);

        return BlastBehavior.Pass;
    }

    // ----------------------------
    // terrain feature を探す
    // ----------------------------
    private static TerrainFeature? FindTerrain(GameLocation location, Vector2 tile)
    {
        if (location.terrainFeatures.TryGetValue(tile, out var terrain))
            return terrain;

        return null;
    }

    // ----------------------------
    // object を探す
    // ----------------------------
    private static StardewValley.Object? FindObject(GameLocation location, Vector2 tile)
    {
        if (location.Objects.TryGetValue(tile, out var obj))
            return obj;

        return null;
    }

    // ----------------------------
    // resource clump を探す
    // ----------------------------
    private static ResourceClump? FindClump(GameLocation location, Vector2 tile)
    {
        foreach (var clump in location.resourceClumps)
        {
            if (clump.occupiesTile((int)tile.X, (int)tile.Y))
                return clump;
        }

        return null;
    }
}