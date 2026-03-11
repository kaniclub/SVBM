// ----------------------------
// Object を処理する
// 石・枝・雑草系・壊せる箱などを対象に、適切な道具で1回だけ処理する
// 通過できない草Object は特殊対応で削除する
// 石は一撃化してからツルハシ処理する
// 発掘ポイントは壁扱いせず、そのまま通す
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
        if (ObjectIds.IsStoneLike(objectId))
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