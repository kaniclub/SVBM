// ----------------------------
// 破壊前の object を見て
// 石か鉱石ブロックかを分類する
// - 既知IDを優先
// - 未知IDでも Name / DisplayName / Description で補助判定する
// ----------------------------
using BomberGear.GameData;
using Microsoft.Xna.Framework;
using StardewValley;

namespace BomberGear.Drops;

internal static class MineObjectDropSourceClassifier
{
    // ----------------------------
    // タイル上の object を分類
    // ----------------------------
    public static BreakableDropSourceKind Classify(GameLocation location, Vector2 tile)
    {
        if (!location.Objects.TryGetValue(tile, out var obj) || obj is null)
            return BreakableDropSourceKind.None;

        string objectId = ObjectIds.ExtractObjectId(obj.ItemId, obj.QualifiedItemId);
        if (string.IsNullOrWhiteSpace(objectId))
            return BreakableDropSourceKind.None;

        string objectName = obj.Name ?? string.Empty;
        string objectDisplayName = obj.DisplayName ?? string.Empty;
        string objectDescription = obj.getDescription() ?? string.Empty;

        if (ObjectIds.IsOreStone(objectId, objectName, objectDisplayName, objectDescription))
            return BreakableDropSourceKind.OreBlock;

        if (ObjectIds.IsStone(objectId, objectName, objectDisplayName, objectDescription))
            return BreakableDropSourceKind.Stone;

        return BreakableDropSourceKind.None;
    }
}