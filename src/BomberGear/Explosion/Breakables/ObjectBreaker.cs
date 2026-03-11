// ----------------------------
// Object を処理する
// 石・枝・雑草系・壊せる箱などを対象に、適切な道具で1回だけ処理する
// 通過できない草Object は特殊対応で削除する
// 石は一撃化してからツルハシ処理する
// 発掘ポイントは壁扱いせず、そのまま通す
// - 石判定は ID 優先
// - 未知IDでも Name / DisplayName / Description で補助判定する
// ----------------------------
using BomberGear.GameData;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;

namespace BomberGear.Explosion.Breakables;

internal sealed class ObjectBreaker
{
    private readonly VirtualToolActionHelper toolHelper = new();

    // ----------------------------
    // Object の爆風挙動を決める
    // ----------------------------
    public BlastBehavior Resolve(GameLocation location, Vector2 tile, StardewValley.Object obj, int power)
    {
        _ = power;

        string objectId = ObjectIds.ExtractObjectId(obj.ItemId, obj.QualifiedItemId);
        string objectName = obj.Name ?? string.Empty;
        string objectDisplayName = obj.DisplayName ?? string.Empty;
        string objectDescription = obj.getDescription() ?? string.Empty;

        // 発掘ポイントは壁扱いしない
        // 壊さず、そのまま爆風を通す
        if (ObjectIds.IsArtifactSpot(objectId))
            return BlastBehavior.Pass;

        // 家具は壁
        if (obj is Furniture)
            return BlastBehavior.Block;

        // big craftable は壁
        if (obj.bigCraftable.Value)
            return BlastBehavior.Block;

        // 通過できない草・雑草系は特殊対応で削除
        if (ObjectIds.IsWeedLike(objectId))
        {
            toolHelper.RemoveBlockingGrassObject(location, tile);
            return BlastBehavior.BreakAndPass;
        }

        // 石系 -> Pickaxe
        // 未知IDでも Stone / 石 / Stone Base などの名前なら対象にする
        if (ObjectIds.IsStoneLike(objectId, objectName, objectDisplayName, objectDescription))
        {
            Tool tool = toolHelper.CreatePickaxe();
            toolHelper.PrepareObjectForOneHit(obj);
            toolHelper.UseToolOnce(Game1.player, location, tile, tool);
            return BlastBehavior.BreakAndStop;
        }

        // 枝 -> Axe
        if (ObjectIds.IsTwig(objectId))
        {
            Tool tool = toolHelper.CreateAxe();
            toolHelper.UseToolOnce(Game1.player, location, tile, tool);
            return BlastBehavior.BreakAndStop;
        }

        // BreakableContainer
        if (obj is BreakableContainer)
        {
            Tool tool = toolHelper.CreatePickaxe();
            toolHelper.UseToolOnce(Game1.player, location, tile, tool);
            return BlastBehavior.BreakAndStop;
        }

        return BlastBehavior.Block;
    }
}