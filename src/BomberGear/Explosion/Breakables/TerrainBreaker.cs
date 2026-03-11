// ----------------------------
// TerrainFeature を処理する
// 通過できる草は特殊対応で削除し、
// カマ収穫系作物はカマ処理し、
// 木系は一撃化してから斧処理する
// ----------------------------
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace BomberGear.Explosion.Breakables;

internal sealed class TerrainBreaker
{
    private readonly VirtualToolActionHelper toolHelper = new();

    // ----------------------------
    // TerrainFeature の爆風挙動を決める
    // ----------------------------
    public BlastBehavior Resolve(GameLocation location, Vector2 tile, TerrainFeature terrain, int power)
    {
        // 通過できる草は特殊対応で削除
        if (terrain is Grass)
        {
            toolHelper.RemovePassableGrass(location, tile);
            return BlastBehavior.BreakAndPass;
        }

        // HoeDirt + crop -> Scythe
        if (terrain is HoeDirt dirt)
        {
            if (dirt.crop is not null)
            {
                Tool? scythe = toolHelper.CreateScythe();
                if (scythe is not null)
                    toolHelper.UseScytheOnce(Game1.player, location, tile, scythe);

                return BlastBehavior.BreakAndPass;
            }

            return BlastBehavior.Pass;
        }

        // Flooring は通す
        if (terrain is Flooring)
            return BlastBehavior.Pass;

        // Tree は成長段階で扱い分け
        if (terrain is Tree tree)
        {
            if (tree.growthStage.Value >= 3)
            {
                Tool axe = toolHelper.CreateAxe();
                toolHelper.PrepareTerrainForOneHit(terrain);
                toolHelper.UseToolOnce(Game1.player, location, tile, axe);
                return BlastBehavior.BreakAndStop;
            }
            else if (tree.growthStage.Value == 1)
            {
                Tool? scythe = toolHelper.CreateScythe();
                if (scythe is not null)
                    toolHelper.UseScytheOnce(Game1.player, location, tile, scythe);

                return BlastBehavior.BreakAndPass;
            }
            else
            {
                Tool axe = toolHelper.CreateAxe();
                toolHelper.PrepareTerrainForOneHit(terrain);
                toolHelper.UseToolOnce(Game1.player, location, tile, axe);
                return BlastBehavior.BreakAndStop;
            }
        }

        // FruitTree も Axe
        if (terrain is FruitTree)
        {
            Tool axe = toolHelper.CreateAxe();
            toolHelper.PrepareTerrainForOneHit(terrain);
            toolHelper.UseToolOnce(Game1.player, location, tile, axe);
            return BlastBehavior.BreakAndStop;
        }

        return BlastBehavior.Block;
    }
}