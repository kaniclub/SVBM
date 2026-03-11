using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;

namespace BomberGear.Explosion.Breakables;

internal sealed class WallCollisionChecker
{
    // ----------------------------
    // このタイルを壁として扱うか
    // - Lookup Anything の Building 相当: location.buildings
    // - Buildings / Front / AlwaysFront の非通行タイル
    // - Mailbox / ShippingBin などの Action / TouchAction タイル
    // ----------------------------
    public bool IsHardWall(GameLocation location, Vector2 tile)
    {
        int x = (int)tile.X;
        int y = (int)tile.Y;

        // 実在する Building
        if (IsBuildingTile(location, x, y))
            return true;

        // 建物レイヤーや前景レイヤーの壁タイル
        if (IsBlockingLayerTile(location, x, y, "Buildings"))
            return true;

        if (IsBlockingLayerTile(location, x, y, "Front"))
            return true;

        if (IsBlockingLayerTile(location, x, y, "AlwaysFront"))
            return true;

        // ポスト・出荷箱などのアクション付きタイル
        if (HasBlockingActionTile(location, x, y, "Buildings"))
            return true;

        if (HasBlockingActionTile(location, x, y, "Front"))
            return true;

        if (HasBlockingActionTile(location, x, y, "AlwaysFront"))
            return true;

        return false;
    }

    // ----------------------------
    // location.buildings 上の建物占有判定
    // ----------------------------
    private static bool IsBuildingTile(GameLocation location, int x, int y)
    {
        foreach (Building building in location.buildings)
        {
            int left = building.tileX.Value;
            int top = building.tileY.Value;
            int right = left + building.tilesWide.Value - 1;
            int bottom = top + building.tilesHigh.Value - 1;

            if (x >= left && x <= right && y >= top && y <= bottom)
                return true;
        }

        return false;
    }

    // ----------------------------
    // レイヤーにタイルがあり、Passable / Shadow が無ければ壁
    // ----------------------------
    private static bool IsBlockingLayerTile(GameLocation location, int x, int y, string layerName)
    {
        if (location.Map?.GetLayer(layerName) is null)
            return false;

        if (location.getTileIndexAt(x, y, layerName) < 0)
            return false;

        string? passable = location.doesTileHaveProperty(x, y, "Passable", layerName);
        if (!string.IsNullOrEmpty(passable))
            return false;

        string? shadow = location.doesTileHaveProperty(x, y, "Shadow", layerName);
        if (!string.IsNullOrEmpty(shadow))
            return false;

        return true;
    }

    // ----------------------------
    // Action / TouchAction を持つタイルは壁扱い
    // ポストや出荷箱のような固定オブジェクトを想定
    // ----------------------------
    private static bool HasBlockingActionTile(GameLocation location, int x, int y, string layerName)
    {
        if (location.Map?.GetLayer(layerName) is null)
            return false;

        if (location.getTileIndexAt(x, y, layerName) < 0)
            return false;

        string? action = location.doesTileHaveProperty(x, y, "Action", layerName);
        if (!string.IsNullOrEmpty(action))
            return true;

        string? touchAction = location.doesTileHaveProperty(x, y, "TouchAction", layerName);
        if (!string.IsNullOrEmpty(touchAction))
            return true;

        return false;
    }
}