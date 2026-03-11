// ----------------------------
// ワールド内に存在する斧 / ツルハシの最大強化段階を調べる
// 0=通常, 1=銅, 2=鋼, 3=金, 4=イリジウム
// - 自分の所持品
// - 他プレイヤーの所持品
// - 各ロケーションのチェスト内
// を対象に確認する
// ----------------------------
using System.Collections.Generic;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Tools;

namespace BomberGear.Explosion.Breakables;

internal sealed class WorldToolUpgradeService
{
    // ----------------------------
    // ワールド内の最大オノ強化段階を返す
    // ----------------------------
    public int GetBestAxeLevel()
    {
        return GetBestToolLevel<Axe>();
    }

    // ----------------------------
    // ワールド内の最大ツルハシ強化段階を返す
    // ----------------------------
    public int GetBestPickaxeLevel()
    {
        return GetBestToolLevel<Pickaxe>();
    }

    // ----------------------------
    // 指定ツール種別の最大強化段階を返す
    // ----------------------------
    private static int GetBestToolLevel<TTool>() where TTool : Tool
    {
        int best = 0;

        foreach (Item item in EnumerateWorldItems())
        {
            if (item is not TTool tool)
                continue;

            if (tool.UpgradeLevel > best)
                best = tool.UpgradeLevel;

            if (best >= 4)
                break;
        }

        return best;
    }

    // ----------------------------
    // ワールド内の全アイテムを列挙する
    // - 全プレイヤーの所持品
    // - 全ロケーションのチェスト内
    // ----------------------------
    private static IEnumerable<Item> EnumerateWorldItems()
    {
        foreach (Farmer farmer in Game1.getAllFarmers())
        {
            foreach (Item item in EnumerateItemList(farmer.Items))
                yield return item;
        }

        foreach (GameLocation location in EnumerateAllLocations())
        {
            foreach (StardewValley.Object obj in location.Objects.Values)
            {
                if (obj is not Chest chest)
                    continue;

                foreach (Item item in EnumerateItemList(chest.Items))
                    yield return item;
            }
        }
    }

    // ----------------------------
    // Item 一覧を列挙する
    // ----------------------------
    private static IEnumerable<Item> EnumerateItemList(IEnumerable<Item?> items)
    {
        foreach (Item? item in items)
        {
            if (item is null)
                continue;

            yield return item;

            if (item is Chest chest)
            {
                foreach (Item nested in EnumerateItemList(chest.Items))
                    yield return nested;
            }
        }
    }

    // ----------------------------
    // すべてのロケーションを列挙する
    // 建物内も再帰的に含める
    // ----------------------------
    private static IEnumerable<GameLocation> EnumerateAllLocations()
    {
        var visited = new HashSet<GameLocation>();

        foreach (GameLocation location in Game1.locations)
        {
            foreach (GameLocation nested in EnumerateLocationRecursive(location, visited))
                yield return nested;
        }
    }

    // ----------------------------
    // ロケーションを再帰的にたどる
    // ----------------------------
    private static IEnumerable<GameLocation> EnumerateLocationRecursive(
        GameLocation location,
        HashSet<GameLocation> visited)
    {
        if (!visited.Add(location))
            yield break;

        yield return location;

        foreach (var building in location.buildings)
        {
            GameLocation? indoors = building.indoors.Value;
            if (indoors is null)
                continue;

            foreach (GameLocation nested in EnumerateLocationRecursive(indoors, visited))
                yield return nested;
        }
    }
}