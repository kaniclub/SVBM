// ----------------------------
// 破壊前の object を見て
// 石か鉱石ブロックかをアイテムIDで判定する
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

        if (ObjectIds.IsOreStone(objectId))
            return BreakableDropSourceKind.OreBlock;

        if (ObjectIds.IsStone(objectId))
            return BreakableDropSourceKind.Stone;

        return BreakableDropSourceKind.None;
    }
}