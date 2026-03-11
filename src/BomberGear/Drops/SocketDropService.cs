using System;
using System.Collections.Generic;
using System.Linq;
using BomberGear.Config;
using BomberGear.Items;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace BomberGear.Drops;

internal sealed class SocketDropService
{
    // ----------------------------
    // 壊れたブロック一覧からドロップを試行
    // ----------------------------
    public void TryDropFromBrokenBlocks(
        GameLocation location,
        Farmer who,
        IReadOnlyList<BrokenBlockInfo> brokenBlocks,
        ModConfig config)
    {
        _ = who;

        if (brokenBlocks.Count == 0)
            return;

        if (!config.SocketDrops.Enabled)
            return;

        if (!CanDropInLocation(location, config.SocketDrops))
            return;

        foreach (var brokenBlock in brokenBlocks)
        {
            if (brokenBlock.SourceKind == BreakableDropSourceKind.None)
                continue;

            // ----------------------------
            // 1ブロックにつき最大1個だけ落とす
            // まず火力 / 爆弾数系を試し、
            // 外れたら特性系を試す
            // ----------------------------
            if (TryDropOne(location, brokenBlock, SocketDropGroup.PowerBomb, config))
                continue;

            TryDropOne(location, brokenBlock, SocketDropGroup.ActionChip, config);
        }
    }

    // ----------------------------
    // 1グループ分のドロップを試行
    // ----------------------------
    private bool TryDropOne(
        GameLocation location,
        BrokenBlockInfo brokenBlock,
        SocketDropGroup group,
        ModConfig config)
    {
        float chance = GetChancePerBrokenBlock(group, brokenBlock.SourceKind, config.SocketDrops);
        if (chance <= 0.0f)
            return false;

        if (Game1.random.NextDouble() > chance)
            return false;

        SocketDropItemKind? selected = SelectDropItem(group, config);
        if (!selected.HasValue)
            return false;

        Item? item = CreateDropItem(selected.Value);
        if (item is null)
            return false;

        Vector2 pixelPosition = (brokenBlock.Tile * 64f) + new Vector2(32f, 32f);
        Game1.createItemDebris(item, pixelPosition, -1, location);

        return true;
    }

    // ----------------------------
    // 1ブロックあたりの抽選率
    // - 1フロアでどれくらい落としたいかを想定破壊数で割り戻す
    // ----------------------------
    private static float GetChancePerBrokenBlock(
        SocketDropGroup group,
        BreakableDropSourceKind sourceKind,
        SocketDropConfig config)
    {
        if (sourceKind == BreakableDropSourceKind.None)
            return 0.0f;

        float weightedBreakCount =
            (config.ExpectedStoneBreaksPerMineFloor * config.StoneDropChanceMultiplier) +
            (config.ExpectedOreBreaksPerMineFloor * config.OreDropChanceMultiplier);

        if (weightedBreakCount <= 0.0f)
            return 0.0f;

        float sourceMultiplier = sourceKind == BreakableDropSourceKind.OreBlock
            ? config.OreDropChanceMultiplier
            : config.StoneDropChanceMultiplier;

        float dropsPerFloor = group == SocketDropGroup.PowerBomb
            ? config.PowerBombDropsPerMineFloor
            : config.ActionChipDropsPerMineFloor;

        float chance = (dropsPerFloor * sourceMultiplier) / weightedBreakCount;
        return Clamp01(chance);
    }

    // ----------------------------
    // 候補から均等抽選
    // - 上限適用ONのときだけ
    //   ワールド内の実在数上限をチェックする
    // ----------------------------
    private SocketDropItemKind? SelectDropItem(SocketDropGroup group, ModConfig config)
    {
        var candidates = GetCandidates(group).ToList();

        if (config.SocketDrops.ApplyDropCaps)
        {
            int playerCount = GetCurrentWorldPlayerCount();

            candidates = candidates
                .Where(kind => !HasReachedWorldCap(kind, config, playerCount))
                .ToList();
        }

        if (candidates.Count == 0)
            return null;

        int index = Game1.random.Next(candidates.Count);
        return candidates[index];
    }

    // ----------------------------
    // グループごとの候補一覧
    // ----------------------------
    private static IReadOnlyList<SocketDropItemKind> GetCandidates(SocketDropGroup group)
    {
        if (group == SocketDropGroup.PowerBomb)
        {
            return new[]
            {
                SocketDropItemKind.PowerCore,
                SocketDropItemKind.BombBag
            };
        }

        return new[]
        {
            SocketDropItemKind.RemoteBombChip,
            SocketDropItemKind.PierceBombChip,
            SocketDropItemKind.KickChip,
            SocketDropItemKind.ThrowChip
        };
    }

    // ----------------------------
    // ドロップアイテムの qualified item ID
    // ----------------------------
    private static string GetQualifiedItemId(SocketDropItemKind kind)
    {
        return kind switch
        {
            SocketDropItemKind.RemoteBombChip => "(O)kaniclub.BomberGear_RemoteBombChip",
            SocketDropItemKind.PierceBombChip => "(O)kaniclub.BomberGear_PierceBombChip",
            SocketDropItemKind.KickChip => "(O)kaniclub.BomberGear_KickChip",
            SocketDropItemKind.ThrowChip => "(O)kaniclub.BomberGear_ThrowChip",
            SocketDropItemKind.PowerCore => "(O)kaniclub.BomberGear_PowerCore",
            SocketDropItemKind.BombBag => "(O)kaniclub.BomberGear_BombBag",
            _ => string.Empty
        };
    }

    // ----------------------------
    // item metadata から item instance を生成
    // ----------------------------
    private static Item? CreateDropItem(SocketDropItemKind kind)
    {
        string qualifiedItemId = GetQualifiedItemId(kind);
        if (string.IsNullOrWhiteSpace(qualifiedItemId))
            return null;

        var metadata = ItemRegistry.GetMetadata(qualifiedItemId);
        return metadata?.CreateItem();
    }

    // ----------------------------
    // このロケーションで判定するか
    // ----------------------------
    private static bool CanDropInLocation(GameLocation location, SocketDropConfig config)
    {
        if (!config.OnlyDropInMineLikeLocations)
            return true;

        return location is MineShaft;
    }

    // ----------------------------
    // 現在ワールドに存在しているプレイヤー数を返す
    // - 現在オンライン中の人数で計算する
    // ----------------------------
    private static int GetCurrentWorldPlayerCount()
    {
        int count = Game1.getOnlineFarmers().Count();
        return Math.Max(1, count);
    }

    // ----------------------------
    // ワールド内実在数ベースで上限到達判定
    // - 手持ち
    // - チェスト
    // - 地面ドロップ
    // - ボンバーギア装着済み
    // を合算する
    // ----------------------------
    private static bool HasReachedWorldCap(SocketDropItemKind kind, ModConfig config, int playerCount)
    {
        int current = CountExistingSocketItems(kind);
        int cap = GetCapPerPlayer(kind, config) * playerCount;
        return current >= Math.Max(0, cap);
    }

    // ----------------------------
    // ワールド内に実在する指定ソケット数を数える
    // ----------------------------
    private static int CountExistingSocketItems(SocketDropItemKind kind)
    {
        return BuildCountBreakdown(kind).TotalCount;
    }

    // ----------------------------
    // 実在数の内訳を構築
    // ----------------------------
    private static SocketItemCountBreakdown BuildCountBreakdown(SocketDropItemKind kind)
    {
        int heldCount = CountHeldItems(kind);
        int chestCount = CountChestItems(kind);
        int debrisCount = CountDebrisItems(kind);
        int socketedOnGearCount = CountSocketedItemsOnBomberGear(kind);

        return new SocketItemCountBreakdown(
            kind,
            heldCount,
            chestCount,
            debrisCount,
            socketedOnGearCount);
    }

    // ----------------------------
    // 手持ち数を数える
    // - 所持品スロット
    // - カーソル保持中
    // - Stack数で数える
    // ----------------------------
    private static int CountHeldItems(SocketDropItemKind kind)
    {
        string qualifiedItemId = GetQualifiedItemId(kind);
        string itemId = ExtractItemIdFromQualifiedItemId(qualifiedItemId);

        int count = 0;

        foreach (Farmer farmer in Game1.getOnlineFarmers())
        {
            foreach (Item? item in farmer.Items)
                count += GetItemStackIfSameSocketItem(item, itemId, qualifiedItemId);

            count += GetItemStackIfSameSocketItem(farmer.CursorSlotItem, itemId, qualifiedItemId);
        }

        return count;
    }

    // ----------------------------
    // チェスト内数を数える
    // - 配置済みチェスト系のみ対象
    // - Stack数で数える
    // ----------------------------
    private static int CountChestItems(SocketDropItemKind kind)
    {
        string qualifiedItemId = GetQualifiedItemId(kind);
        string itemId = ExtractItemIdFromQualifiedItemId(qualifiedItemId);

        int count = 0;

        Utility.ForEachLocation(location =>
        {
            foreach (var obj in location.Objects.Values)
            {
                if (obj is not Chest chest)
                    continue;

                foreach (Item? storedItem in chest.Items)
                    count += GetItemStackIfSameSocketItem(storedItem, itemId, qualifiedItemId);
            }

            return true;
        });

        return count;
    }

    // ----------------------------
    // 地面ドロップ中の debris item 数を数える
    // - Stack数で数える
    // ----------------------------
    private static int CountDebrisItems(SocketDropItemKind kind)
    {
        string qualifiedItemId = GetQualifiedItemId(kind);
        string itemId = ExtractItemIdFromQualifiedItemId(qualifiedItemId);

        int count = 0;

        Utility.ForEachLocation(location =>
        {
            foreach (Debris debris in location.debris)
                count += GetItemStackIfSameSocketItem(debris.item, itemId, qualifiedItemId);

            return true;
        });

        return count;
    }

    // ----------------------------
    // ボンバーギアに装着済みの指定ソケット数を数える
    // - gear の保存値から直接数える
    // ----------------------------
    private static int CountSocketedItemsOnBomberGear(SocketDropItemKind kind)
    {
        int count = 0;

        Utility.ForEachItem(item =>
        {
            count += CountSocketedItemsOnSingleGear(item, kind);
            return true;
        });

        foreach (Farmer farmer in Game1.getOnlineFarmers())
            count += CountSocketedItemsOnSingleGear(farmer.CursorSlotItem, kind);

        return count;
    }

    // ----------------------------
    // 単一のボンバーギアに入っている対象ソケット数を数える
    // ----------------------------
    private static int CountSocketedItemsOnSingleGear(Item? item, SocketDropItemKind kind)
    {
        if (item is null)
            return 0;

        if (item.ItemId != ItemIds.BomberGear)
            return 0;

        return kind switch
        {
            SocketDropItemKind.RemoteBombChip => HasBombTrait(item, BombTraitValues.Remote) ? 1 : 0,
            SocketDropItemKind.PierceBombChip => HasBombTrait(item, BombTraitValues.Pierce) ? 1 : 0,
            SocketDropItemKind.KickChip => HasActionTrait(item, ActionTraitValues.Kick) ? 1 : 0,
            SocketDropItemKind.ThrowChip => HasActionTrait(item, ActionTraitValues.Throw) ? 1 : 0,
            SocketDropItemKind.PowerCore => GetNonNegativeIntModData(item, SocketKeys.PowerCount),
            SocketDropItemKind.BombBag => GetNonNegativeIntModData(item, SocketKeys.BombCount),
            _ => 0
        };
    }

    // ----------------------------
    // 爆弾特性一致判定
    // ----------------------------
    private static bool HasBombTrait(Item gear, string expected)
    {
        return gear.modData.TryGetValue(SocketKeys.BombTrait, out string? value)
            && string.Equals(value, expected, StringComparison.Ordinal);
    }

    // ----------------------------
    // アクション特性一致判定
    // ----------------------------
    private static bool HasActionTrait(Item gear, string expected)
    {
        return gear.modData.TryGetValue(SocketKeys.ActionTrait, out string? value)
            && string.Equals(value, expected, StringComparison.Ordinal);
    }

    // ----------------------------
    // modData から 0以上の整数を読む
    // ----------------------------
    private static int GetNonNegativeIntModData(Item gear, string key)
    {
        if (!gear.modData.TryGetValue(key, out string? raw))
            return 0;

        if (!int.TryParse(raw, out int value))
            return 0;

        return Math.Max(0, value);
    }

    // ----------------------------
    // 指定 item が対象ソケットなら Stack数を返す
    // ----------------------------
    private static int GetItemStackIfSameSocketItem(Item? item, string itemId, string qualifiedItemId)
    {
        if (!IsSameSocketItem(item, itemId, qualifiedItemId))
            return 0;

        return Math.Max(0, item!.Stack);
    }

    // ----------------------------
    // qualified item id から素の item id を取り出す
    // ----------------------------
    private static string ExtractItemIdFromQualifiedItemId(string qualifiedItemId)
    {
        if (string.IsNullOrWhiteSpace(qualifiedItemId))
            return string.Empty;

        int endOfType = qualifiedItemId.IndexOf(')');
        if (qualifiedItemId.StartsWith("(") && endOfType >= 0 && endOfType + 1 < qualifiedItemId.Length)
            return qualifiedItemId.Substring(endOfType + 1);

        return qualifiedItemId;
    }

    // ----------------------------
    // 指定 item が対象ソケットか判定
    // ----------------------------
    private static bool IsSameSocketItem(Item? item, string itemId, string qualifiedItemId)
    {
        if (item is null)
            return false;

        return string.Equals(item.QualifiedItemId, qualifiedItemId, StringComparison.Ordinal)
            || string.Equals(item.ItemId, itemId, StringComparison.Ordinal);
    }

    // ----------------------------
    // プレイヤー1人あたりの総上限
    // - 特性系は各1
    // - 火力 / 爆弾数は
    //   ギア設定の最終上限 - 初期値
    // ----------------------------
    private static int GetCapPerPlayer(SocketDropItemKind kind, ModConfig config)
    {
        return kind switch
        {
            SocketDropItemKind.RemoteBombChip => SocketLimits.BombTraitPerGear,
            SocketDropItemKind.PierceBombChip => SocketLimits.BombTraitPerGear,
            SocketDropItemKind.KickChip => SocketLimits.ActionTraitPerGear,
            SocketDropItemKind.ThrowChip => SocketLimits.ActionTraitPerGear,
            SocketDropItemKind.PowerCore => GetPowerCoreCapPerPlayer(config),
            SocketDropItemKind.BombBag => GetBombBagCapPerPlayer(config),
            _ => 0
        };
    }

    // ----------------------------
    // 火力コア上限
    // ----------------------------
    private static int GetPowerCoreCapPerPlayer(ModConfig config)
    {
        int maxPower = ClampInt(config.MaxPower, 1, SocketLimits.MaxPowerValue);
        int basePower = ClampInt(config.BasePower, 1, maxPower);
        return Math.Max(0, maxPower - basePower);
    }

    // ----------------------------
    // 爆弾バッグ上限
    // ----------------------------
    private static int GetBombBagCapPerPlayer(ModConfig config)
    {
        int maxBombs = ClampInt(config.MaxBombs, 1, SocketLimits.MaxBombValue);
        int baseMaxBombs = ClampInt(config.BaseMaxBombs, 1, maxBombs);
        return Math.Max(0, maxBombs - baseMaxBombs);
    }

    // ----------------------------
    // int Clamp
    // ----------------------------
    private static int ClampInt(int value, int min, int max)
    {
        if (value < min)
            return min;

        if (value > max)
            return max;

        return value;
    }

    // ----------------------------
    // 0.0〜1.0 に丸める
    // ----------------------------
    private static float Clamp01(float value)
    {
        if (value < 0.0f)
            return 0.0f;

        if (value > 1.0f)
            return 1.0f;

        return value;
    }
}