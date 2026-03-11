// ----------------------------
// 爆風から仮想的に道具を使うための補助クラス
// 斧・ツルハシ・カマを生成し、
// 1回だけ DoFunction する処理、
// 草だけを特殊対応で削除しつつ簡易演出を出す処理をまとめる
// ----------------------------
using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace BomberGear.Explosion.Breakables;

internal sealed class VirtualToolActionHelper
{
    private const int MaxUpgradeLevel = 4;

    // ----------------------------
    // 指定強化段階のツルハシを作る
    // 省略時は最大強化
    // ----------------------------
    public Tool CreatePickaxe(int upgradeLevel = MaxUpgradeLevel)
    {
        return new Pickaxe
        {
            UpgradeLevel = ClampUpgradeLevel(upgradeLevel)
        };
    }

    // ----------------------------
    // 指定強化段階の斧を作る
    // 省略時は最大強化
    // ----------------------------
    public Tool CreateAxe(int upgradeLevel = MaxUpgradeLevel)
    {
        return new Axe
        {
            UpgradeLevel = ClampUpgradeLevel(upgradeLevel)
        };
    }

    // ----------------------------
    // Scythe を作る
    // ----------------------------
    public Tool? CreateScythe()
    {
        Type? meleeWeaponType = typeof(Pickaxe).Assembly.GetType("StardewValley.Tools.MeleeWeapon");
        if (meleeWeaponType is null)
            return null;

        if (!typeof(Tool).IsAssignableFrom(meleeWeaponType))
            return null;

        ConstructorInfo? stringCtor = meleeWeaponType.GetConstructor(new[] { typeof(string) });
        if (stringCtor is not null)
        {
            foreach (string arg in new[] { "47", "(W)47" })
            {
                try
                {
                    if (stringCtor.Invoke(new object[] { arg }) is Tool tool)
                        return tool;
                }
                catch
                {
                }
            }
        }

        ConstructorInfo? intCtor = meleeWeaponType.GetConstructor(new[] { typeof(int) });
        if (intCtor is not null)
        {
            try
            {
                if (intCtor.Invoke(new object[] { 47 }) is Tool tool)
                    return tool;
            }
            catch
            {
            }
        }

        ConstructorInfo? emptyCtor = meleeWeaponType.GetConstructor(Type.EmptyTypes);
        if (emptyCtor is not null)
        {
            try
            {
                if (emptyCtor.Invoke(Array.Empty<object>()) is Tool tool)
                    return tool;
            }
            catch
            {
            }
        }

        return null;
    }

    // ----------------------------
    // 石系を一撃破壊状態にする
    // ----------------------------
    public void PrepareObjectForOneHit(StardewValley.Object obj)
    {
        obj.MinutesUntilReady = 0;
    }

    // ----------------------------
    // 木を一撃破壊状態にする
    // ----------------------------
    public void PrepareTerrainForOneHit(TerrainFeature terrain)
    {
        if (terrain is Tree tree && tree.health.Value > 1)
            tree.health.Value = 1;
        else if (terrain is FruitTree fruitTree && fruitTree.health.Value > 1)
            fruitTree.health.Value = 1;
    }

    // ----------------------------
    // 大きい石 / 丸太 / 切り株を一撃破壊状態にする
    // ----------------------------
    public void PrepareClumpForOneHit(ResourceClump clump)
    {
        if (clump.health.Value > 0)
            clump.health.Value = 0;
    }

    // ----------------------------
    // Tool.DoFunction を1回だけ呼ぶ
    // 石・木・大物用
    // ----------------------------
    public bool UseToolOnce(Farmer who, GameLocation location, Vector2 tile, Tool tool)
    {
        if (who is null || location is null || tool is null)
            return false;

        Tool? prevTool = who.CurrentTool;
        int prevIndex = who.CurrentToolIndex;

        try
        {
            who.CurrentTool = tool;

            int pixelX = (int)tile.X * Game1.tileSize + Game1.tileSize / 2;
            int pixelY = (int)tile.Y * Game1.tileSize + Game1.tileSize / 2;

            tool.DoFunction(location, pixelX, pixelY, 0, who);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            who.CurrentTool = prevTool;
            who.CurrentToolIndex = prevIndex;
        }
    }

    // ----------------------------
    // カマだけは beginUsing + DoFunction を使う
    // ただし、位置や向きは一切変更しない
    // 実行後にアクション状態だけ解除する
    // ----------------------------
    public bool UseScytheOnce(Farmer who, GameLocation location, Vector2 tile, Tool tool)
    {
        if (who is null || location is null || tool is null)
            return false;

        Tool? prevTool = who.CurrentTool;
        int prevIndex = who.CurrentToolIndex;
        bool prevUsingTool = who.UsingTool;

        try
        {
            who.CurrentTool = tool;

            int pixelX = (int)tile.X * Game1.tileSize + Game1.tileSize / 2;
            int pixelY = (int)tile.Y * Game1.tileSize + Game1.tileSize / 2;

            tool.beginUsing(location, pixelX, pixelY, who);
            tool.DoFunction(location, pixelX, pixelY, 0, who);

            who.completelyStopAnimatingOrDoingAction();
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            who.UsingTool = prevUsingTool;
            who.CurrentTool = prevTool;
            who.CurrentToolIndex = prevIndex;
        }
    }

    // ----------------------------
    // 通過できる草を特殊対応で削除する
    // ドロップは出さず、簡易な草刈り演出だけ出す
    // ----------------------------
    public void RemovePassableGrass(GameLocation location, Vector2 tile)
    {
        if (!location.terrainFeatures.ContainsKey(tile))
            return;

        location.terrainFeatures.Remove(tile);
        PlayGrassBreakEffect(location, tile);
    }

    // ----------------------------
    // 通過できない草・雑草系 Object を特殊対応で削除する
    // ドロップは出さず、簡易な草刈り演出だけ出す
    // ----------------------------
    public void RemoveBlockingGrassObject(GameLocation location, Vector2 tile)
    {
        if (!location.Objects.ContainsKey(tile))
            return;

        location.Objects.Remove(tile);
        PlayGrassBreakEffect(location, tile);
    }

    // ----------------------------
    // 強化段階をゲーム想定範囲へ丸める
    // ----------------------------
    private static int ClampUpgradeLevel(int upgradeLevel)
    {
        if (upgradeLevel < 0)
            return 0;

        if (upgradeLevel > MaxUpgradeLevel)
            return MaxUpgradeLevel;

        return upgradeLevel;
    }

    // ----------------------------
    // 草削除時の簡易演出
    // ----------------------------
    private void PlayGrassBreakEffect(GameLocation location, Vector2 tile)
    {
        location.playSound("cut");

        Vector2 pixel = tile * Game1.tileSize;

        for (int i = 0; i < 3; i++)
        {
            var sprite = new TemporaryAnimatedSprite(
                6,
                pixel + new Vector2(Game1.random.Next(-8, 9), Game1.random.Next(-8, 9)),
                Color.White,
                8,
                Game1.random.NextDouble() < 0.5,
                40f
            )
            {
                motion = new Vector2(
                    (float)(Game1.random.NextDouble() - 0.5) * 2f,
                    -1.2f - (float)Game1.random.NextDouble()
                ),
                acceleration = new Vector2(0f, 0.08f),
                alphaFade = 0.02f,
                layerDepth = (tile.Y + 1f) * 64f / 10000f
            };

            location.temporarySprites.Add(sprite);
        }
    }
}