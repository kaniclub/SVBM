// ----------------------------
// 大きい石・大きい切り株・丸太などの ResourceClump を処理する
// - 斧系 clump か ツルハシ系 clump かだけを判定する
// - 必要強化段階の決め打ちはしない
// - ワールド内に存在する実際の強化段階の道具を使い、
//   最終的な破壊可否はゲーム本体の処理に任せる
// ----------------------------
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace BomberGear.Explosion.Breakables;

internal sealed class ClumpBreaker
{
    private readonly VirtualToolActionHelper toolHelper = new();
    private readonly WorldToolUpgradeService worldToolUpgradeService = new();

    // ----------------------------
    // ResourceClump を処理する
    // 600, 602 -> 斧系
    // 622, 672, 752, 754, 756, 758 -> ツルハシ系
    // ----------------------------
    public BlastBehavior Resolve(GameLocation location, ResourceClump clump, Vector2 tile, int power)
    {
        _ = power;

        int index = clump.parentSheetIndex.Value;

        return index switch
        {
            600 or 602 => BreakWithAxe(
                location,
                clump,
                tile,
                worldToolUpgradeService.GetBestAxeLevel()
            ),

            622 or 672 or 752 or 754 or 756 or 758 => BreakWithPickaxe(
                location,
                clump,
                tile,
                worldToolUpgradeService.GetBestPickaxeLevel()
            ),

            _ => BlastBehavior.Block
        };
    }

    // ----------------------------
    // 斧で clump を壊せるか試す
    // ----------------------------
    private BlastBehavior BreakWithAxe(
        GameLocation location,
        ResourceClump clump,
        Vector2 tile,
        int axeLevel)
    {
        Tool axe = toolHelper.CreateAxe(axeLevel);
        return TryBreakClump(location, clump, tile, axe);
    }

    // ----------------------------
    // ツルハシで clump を壊せるか試す
    // ----------------------------
    private BlastBehavior BreakWithPickaxe(
        GameLocation location,
        ResourceClump clump,
        Vector2 tile,
        int pickaxeLevel)
    {
        Tool pickaxe = toolHelper.CreatePickaxe(pickaxeLevel);
        return TryBreakClump(location, clump, tile, pickaxe);
    }

    // ----------------------------
    // 実際に clump 破壊を試す
    // - 一撃化して DoFunction を1回だけ呼ぶ
    // - 壊れなかった場合は health を元へ戻す
    // ----------------------------
    private BlastBehavior TryBreakClump(
        GameLocation location,
        ResourceClump clump,
        Vector2 tile,
        Tool tool)
    {
        float originalHealth = clump.health.Value;

        toolHelper.PrepareClumpForOneHit(clump);
        bool used = toolHelper.UseToolOnce(Game1.player, location, tile, tool);

        if (!used)
        {
            clump.health.Value = originalHealth;
            return BlastBehavior.Block;
        }

        if (!location.resourceClumps.Contains(clump))
            return BlastBehavior.BreakAndStop;

        clump.health.Value = originalHealth;
        return BlastBehavior.Block;
    }
}