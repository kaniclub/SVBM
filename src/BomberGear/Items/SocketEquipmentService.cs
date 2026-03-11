// ----------------------------
// ソケット装着処理
// - ボンバーギアへソケットアイテムの効果を反映する
// - 特性スロットは差し替え時に旧チップを返却する
// - 火力 / 爆弾数は1個ずつ加算できる
// - 取り外し時は対応するチップを所持品へ戻す
// ----------------------------
using BomberGear.Config;
using StardewValley;
using SObject = StardewValley.Object;

namespace BomberGear.Items;

internal sealed class SocketEquipmentService
{
    private readonly SocketItemResolver resolver;
    private readonly SocketService sockets;

    public SocketEquipmentService(SocketItemResolver resolver, SocketService sockets)
    {
        this.resolver = resolver;
        this.sockets = sockets;
    }

    // ----------------------------
    // 指定アイテムをボンバーギアへ装着できるか
    // ----------------------------
    public bool CanEquip(Item gear, Item socketItem)
    {
        if (gear.ItemId != ItemIds.BomberGear)
            return false;

        return resolver.TryResolve(socketItem, out _);
    }

    // ----------------------------
    // 指定アイテムをボンバーギアへ装着する
    // 成功時は true
    // - 特性スロットは差し替え対応
    // - 火力 / 爆弾数は1個加算
    // - 装着したソケットアイテムは1個消費
    // ----------------------------
    public bool TryEquip(Farmer who, Item gear, Item socketItem, ModConfig config)
    {
        if (!CanEquip(gear, socketItem))
            return false;

        if (!resolver.TryResolve(socketItem, out var resolved))
            return false;

        switch (resolved.SlotType)
        {
            case SocketSlotType.BombTrait:
                return TryEquipBombTrait(who, gear, socketItem, resolved);

            case SocketSlotType.ActionTrait:
                return TryEquipActionTrait(who, gear, socketItem, resolved);

            case SocketSlotType.Power:
                return TryEquipPower(socketItem, gear, who, resolved, config);

            case SocketSlotType.BombCount:
                return TryEquipBombCount(socketItem, gear, who, resolved, config);

            default:
                return false;
        }
    }

    // ----------------------------
    // 爆弾特性を取り外す
    // - 対応チップを所持品へ戻す
    // ----------------------------
    public bool TryRemoveBombTrait(Farmer who, Item gear)
    {
        if (gear.ItemId != ItemIds.BomberGear)
            return false;

        string currentTrait = sockets.GetBombTrait(gear);
        if (currentTrait == BombTraitValues.None)
            return false;

        string? chipItemId = GetBombTraitChipItemId(currentTrait);
        if (chipItemId is null)
            return false;

        if (!TryReturnSocketItem(who, chipItemId))
            return false;

        sockets.SetBombTrait(gear, BombTraitValues.None);
        return true;
    }

    // ----------------------------
    // アクション特性を取り外す
    // - 対応チップを所持品へ戻す
    // ----------------------------
    public bool TryRemoveActionTrait(Farmer who, Item gear)
    {
        if (gear.ItemId != ItemIds.BomberGear)
            return false;

        string currentTrait = sockets.GetActionTrait(gear);
        if (currentTrait == ActionTraitValues.None)
            return false;

        string? chipItemId = GetActionTraitChipItemId(currentTrait);
        if (chipItemId is null)
            return false;

        if (!TryReturnSocketItem(who, chipItemId))
            return false;

        sockets.SetActionTrait(gear, ActionTraitValues.None);
        return true;
    }

    // ----------------------------
    // 火力スロット個数を1減らす
    // - PowerCore を1個返却する
    // ----------------------------
    public bool TryRemovePowerOne(Farmer who, Item gear)
    {
        if (gear.ItemId != ItemIds.BomberGear)
            return false;

        int current = sockets.GetPowerCount(gear);
        if (current <= 0)
            return false;

        if (!TryReturnSocketItem(who, ItemIds.PowerCore))
            return false;

        sockets.SetPowerCount(gear, current - 1);
        return true;
    }

    // ----------------------------
    // 爆弾数スロット個数を1減らす
    // - BombBag を1個返却する
    // ----------------------------
    public bool TryRemoveBombCountOne(Farmer who, Item gear)
    {
        if (gear.ItemId != ItemIds.BomberGear)
            return false;

        int current = sockets.GetBombCount(gear);
        if (current <= 0)
            return false;

        if (!TryReturnSocketItem(who, ItemIds.BombBag))
            return false;

        sockets.SetBombCount(gear, current - 1);
        return true;
    }

    // ----------------------------
    // 爆弾特性チップを装着
    // - 差し替え時は旧チップを返却
    // ----------------------------
    private bool TryEquipBombTrait(Farmer who, Item gear, Item socketItem, ResolvedSocketItem resolved)
    {
        if (resolved.TraitValue is null)
            return false;

        string newTrait = resolved.TraitValue;
        string currentTrait = sockets.GetBombTrait(gear);

        if (currentTrait == newTrait)
            return false;

        if (currentTrait != BombTraitValues.None)
        {
            string? oldChipItemId = GetBombTraitChipItemId(currentTrait);
            if (oldChipItemId is null)
                return false;

            if (!TryReturnSocketItem(who, oldChipItemId))
                return false;
        }

        sockets.SetBombTrait(gear, newTrait);
        ConsumeOne(who, socketItem);
        return true;
    }

    // ----------------------------
    // アクション特性チップを装着
    // - 差し替え時は旧チップを返却
    // ----------------------------
    private bool TryEquipActionTrait(Farmer who, Item gear, Item socketItem, ResolvedSocketItem resolved)
    {
        if (resolved.TraitValue is null)
            return false;

        string newTrait = resolved.TraitValue;
        string currentTrait = sockets.GetActionTrait(gear);

        if (currentTrait == newTrait)
            return false;

        if (currentTrait != ActionTraitValues.None)
        {
            string? oldChipItemId = GetActionTraitChipItemId(currentTrait);
            if (oldChipItemId is null)
                return false;

            if (!TryReturnSocketItem(who, oldChipItemId))
                return false;
        }

        sockets.SetActionTrait(gear, newTrait);
        ConsumeOne(who, socketItem);
        return true;
    }

    // ----------------------------
    // 火力チップを装着
    // - 上限未満のときだけ1個加算
    // ----------------------------
    private bool TryEquipPower(Item socketItem, Item gear, Farmer who, ResolvedSocketItem resolved, ModConfig config)
    {
        if (resolved.CountValue <= 0)
            return false;

        int current = sockets.GetPowerCount(gear);
        int maxAddable = sockets.GetMaxAddablePowerCount(config);

        if (current >= maxAddable)
            return false;

        sockets.SetPowerCount(gear, current + resolved.CountValue);
        ConsumeOne(who, socketItem);
        return true;
    }

    // ----------------------------
    // 爆弾数チップを装着
    // - 上限未満のときだけ1個加算
    // ----------------------------
    private bool TryEquipBombCount(Item socketItem, Item gear, Farmer who, ResolvedSocketItem resolved, ModConfig config)
    {
        if (resolved.CountValue <= 0)
            return false;

        int current = sockets.GetBombCount(gear);
        int maxAddable = sockets.GetMaxAddableBombCount(config);

        if (current >= maxAddable)
            return false;

        sockets.SetBombCount(gear, current + resolved.CountValue);
        ConsumeOne(who, socketItem);
        return true;
    }

    // ----------------------------
    // ソケットアイテムを1個所持品へ戻す
    // - 空きがない時は false
    // ----------------------------
    private static bool TryReturnSocketItem(Farmer who, string itemId)
    {
        Item? item = CreateSocketItem(itemId);
        if (item is null)
            return false;

        return who.addItemToInventoryBool(item, false);
    }

    // ----------------------------
    // ソケットアイテムを1個生成
    // ----------------------------
    private static Item? CreateSocketItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        return new SObject(itemId, 1);
    }

    // ----------------------------
    // 持っているソケットアイテムを1個消費
    // ----------------------------
    private static void ConsumeOne(Farmer who, Item socketItem)
    {
        if (socketItem.Stack > 1)
        {
            socketItem.Stack--;
            return;
        }

        who.removeItemFromInventory(socketItem);
    }

    // ----------------------------
    // 爆弾特性から対応チップIDを取得
    // ----------------------------
    private static string? GetBombTraitChipItemId(string traitValue)
    {
        return traitValue switch
        {
            BombTraitValues.Remote => ItemIds.RemoteBombChip,
            BombTraitValues.Pierce => ItemIds.PierceBombChip,
            _ => null
        };
    }

    // ----------------------------
    // アクション特性から対応チップIDを取得
    // ----------------------------
    private static string? GetActionTraitChipItemId(string traitValue)
    {
        return traitValue switch
        {
            ActionTraitValues.Kick => ItemIds.KickChip,
            ActionTraitValues.Throw => ItemIds.ThrowChip,
            _ => null
        };
    }
}