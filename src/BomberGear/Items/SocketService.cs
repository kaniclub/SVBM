// ----------------------------
// ボンバーギアの最終パラメータを計算
// ----------------------------
using System;
using BomberGear.Config;
using StardewValley;

namespace BomberGear.Items;

internal sealed class SocketService
{
    // ----------------------------
    // 最終火力
    // 火力スロットに入っている個数ぶん加算する
    // ----------------------------
    public int GetPower(Item? gear, ModConfig config)
    {
        int count = Math.Min(GetPowerCount(gear), GetMaxAddablePowerCount(config));
        int value = config.BasePower + count;
        return Clamp(value, 1, GetMaxPowerCap(config));
    }

    // ----------------------------
    // 最終同時設置数
    // 数量スロットに入っている個数ぶん加算する
    // ----------------------------
    public int GetMaxBombs(Item? gear, ModConfig config)
    {
        int count = Math.Min(GetBombCount(gear), GetMaxAddableBombCount(config));
        int value = config.BaseMaxBombs + count;
        return Clamp(value, 1, GetMaxBombsCap(config));
    }

    // ----------------------------
    // 火力の最終上限
    // ----------------------------
    public int GetMaxPowerCap(ModConfig config)
    {
        return Clamp(config.MaxPower, 1, SocketLimits.MaxPowerValue);
    }

    // ----------------------------
    // 爆弾数の最終上限
    // ----------------------------
    public int GetMaxBombsCap(ModConfig config)
    {
        return Clamp(config.MaxBombs, 1, SocketLimits.MaxBombValue);
    }

    // ----------------------------
    // 追加できる火力ソケット数の上限
    // - 最終上限 - 初期値
    // ----------------------------
    public int GetMaxAddablePowerCount(ModConfig config)
    {
        int maxPower = GetMaxPowerCap(config);
        int basePower = Clamp(config.BasePower, 1, maxPower);
        return Math.Max(0, maxPower - basePower);
    }

    // ----------------------------
    // 追加できる爆弾数ソケット数の上限
    // - 最終上限 - 初期値
    // ----------------------------
    public int GetMaxAddableBombCount(ModConfig config)
    {
        int maxBombs = GetMaxBombsCap(config);
        int baseMaxBombs = Clamp(config.BaseMaxBombs, 1, maxBombs);
        return Math.Max(0, maxBombs - baseMaxBombs);
    }

    // ----------------------------
    // 爆弾の特性
    // 初期値は none
    // ----------------------------
    public string GetBombTrait(Item? gear)
    {
        string value = ReadString(gear, SocketKeys.BombTrait, BombTraitValues.None);
        return NormalizeBombTrait(value);
    }

    // ----------------------------
    // アクションの特性
    // 初期値は none
    // ----------------------------
    public string GetActionTrait(Item? gear)
    {
        string value = ReadString(gear, SocketKeys.ActionTrait, ActionTraitValues.None);
        return NormalizeActionTrait(value);
    }

    // ----------------------------
    // 火力スロットに入っている個数
    // ----------------------------
    public int GetPowerCount(Item? gear)
    {
        int value = ReadInt(gear, SocketKeys.PowerCount, 0);
        return Math.Max(0, value);
    }

    // ----------------------------
    // 数量スロットに入っている個数
    // ----------------------------
    public int GetBombCount(Item? gear)
    {
        int value = ReadInt(gear, SocketKeys.BombCount, 0);
        return Math.Max(0, value);
    }

    // ----------------------------
    // 爆弾特性の設定
    // none のときはキー自体を消す
    // ----------------------------
    public void SetBombTrait(Item? gear, string value)
    {
        string normalized = NormalizeBombTrait(value);
        WriteString(gear, SocketKeys.BombTrait, normalized, BombTraitValues.None);
    }

    // ----------------------------
    // アクション特性の設定
    // none のときはキー自体を消す
    // ----------------------------
    public void SetActionTrait(Item? gear, string value)
    {
        string normalized = NormalizeActionTrait(value);
        WriteString(gear, SocketKeys.ActionTrait, normalized, ActionTraitValues.None);
    }

    // ----------------------------
    // 火力スロット個数の設定
    // 0 以下ならキーを消す
    // ----------------------------
    public void SetPowerCount(Item? gear, int count)
    {
        WriteInt(gear, SocketKeys.PowerCount, Math.Max(0, count));
    }

    // ----------------------------
    // 数量スロット個数の設定
    // 0 以下ならキーを消す
    // ----------------------------
    public void SetBombCount(Item? gear, int count)
    {
        WriteInt(gear, SocketKeys.BombCount, Math.Max(0, count));
    }

    // ----------------------------
    // 爆弾特性の判定
    // ----------------------------
    public bool HasRemoteBomb(Item? gear)
    {
        return GetBombTrait(gear) == BombTraitValues.Remote;
    }

    public bool HasPierceBomb(Item? gear)
    {
        return GetBombTrait(gear) == BombTraitValues.Pierce;
    }

    // ----------------------------
    // アクション特性の判定
    // ----------------------------
    public bool CanKickBomb(Item? gear)
    {
        return GetActionTrait(gear) == ActionTraitValues.Kick;
    }

    public bool CanThrowBomb(Item? gear)
    {
        return GetActionTrait(gear) == ActionTraitValues.Throw;
    }

    // ----------------------------
    // 爆弾特性の正規化
    // ----------------------------
    private static string NormalizeBombTrait(string? value)
    {
        return value switch
        {
            BombTraitValues.Remote => BombTraitValues.Remote,
            BombTraitValues.Pierce => BombTraitValues.Pierce,
            _ => BombTraitValues.None
        };
    }

    // ----------------------------
    // アクション特性の正規化
    // ----------------------------
    private static string NormalizeActionTrait(string? value)
    {
        return value switch
        {
            ActionTraitValues.Kick => ActionTraitValues.Kick,
            ActionTraitValues.Throw => ActionTraitValues.Throw,
            _ => ActionTraitValues.None
        };
    }

    // ----------------------------
    // int読み取り
    // ----------------------------
    private static int ReadInt(Item? item, string key, int defaultValue)
    {
        if (item is null)
            return defaultValue;

        if (item.modData.TryGetValue(key, out string? raw) && int.TryParse(raw, out int value))
            return value;

        return defaultValue;
    }

    // ----------------------------
    // string読み取り
    // ----------------------------
    private static string ReadString(Item? item, string key, string defaultValue)
    {
        if (item is null)
            return defaultValue;

        if (item.modData.TryGetValue(key, out string? raw) && !string.IsNullOrWhiteSpace(raw))
            return raw;

        return defaultValue;
    }

    // ----------------------------
    // int書き込み
    // 0 以下ならキー削除
    // ----------------------------
    private static void WriteInt(Item? item, string key, int value)
    {
        if (item is null)
            return;

        if (value <= 0)
        {
            item.modData.Remove(key);
            return;
        }

        item.modData[key] = value.ToString();
    }

    // ----------------------------
    // string書き込み
    // none や空ならキー削除
    // ----------------------------
    private static void WriteString(Item? item, string key, string value, string noneValue)
    {
        if (item is null)
            return;

        if (string.IsNullOrWhiteSpace(value) || value == noneValue)
        {
            item.modData.Remove(key);
            return;
        }

        item.modData[key] = value;
    }

    // ----------------------------
    // 共通Clamp
    // ----------------------------
    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
            return min;

        if (value > max)
            return max;

        return value;
    }
}