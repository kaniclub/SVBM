// ----------------------------
// ソケット関連の定義
// - 特性値
// - スロット種別
// - ソケットアイテム解決
// - 上限値定義
// ----------------------------
using StardewValley;

namespace BomberGear.Items;

// ----------------------------
// 爆弾の特性スロット値
// ----------------------------
internal static class BombTraitValues
{
    public const string None = "none";
    public const string Remote = "remote";
    public const string Pierce = "pierce";
}

// ----------------------------
// アクションの特性スロット値
// ----------------------------
internal static class ActionTraitValues
{
    public const string None = "none";
    public const string Kick = "kick";
    public const string Throw = "throw";
}

// ----------------------------
// ギア全体の上限定義
// - 特性スロットは各1
// - 火力 / 爆弾数の最終上限は初期値20
// ----------------------------
internal static class SocketLimits
{
    public const int BombTraitPerGear = 1;
    public const int ActionTraitPerGear = 1;
    public const int MaxPowerValue = 20;
    public const int MaxBombValue = 20;
}

// ----------------------------
// ボンバーギアのスロット種別
// ----------------------------
internal enum SocketSlotType
{
    BombTrait,
    ActionTrait,
    Power,
    BombCount
}

// ----------------------------
// 解決済みソケットアイテム情報
// ----------------------------
internal readonly record struct ResolvedSocketItem(
    SocketSlotType SlotType,
    string? TraitValue,
    int CountValue
);

// ----------------------------
// ソケットアイテムの解決
// - どのスロットに入るか
// - どんな効果を持つか
// を判定する
// ----------------------------
internal sealed class SocketItemResolver
{
    // ----------------------------
    // 指定アイテムがソケットアイテムかどうか
    // ----------------------------
    public bool TryResolve(Item item, out ResolvedSocketItem resolved)
    {
        switch (item.ItemId)
        {
            case ItemIds.RemoteBombChip:
                resolved = new ResolvedSocketItem(SocketSlotType.BombTrait, BombTraitValues.Remote, 0);
                return true;

            case ItemIds.PierceBombChip:
                resolved = new ResolvedSocketItem(SocketSlotType.BombTrait, BombTraitValues.Pierce, 0);
                return true;

            case ItemIds.KickChip:
                resolved = new ResolvedSocketItem(SocketSlotType.ActionTrait, ActionTraitValues.Kick, 0);
                return true;

            case ItemIds.ThrowChip:
                resolved = new ResolvedSocketItem(SocketSlotType.ActionTrait, ActionTraitValues.Throw, 0);
                return true;

            case ItemIds.PowerCore:
                resolved = new ResolvedSocketItem(SocketSlotType.Power, null, 1);
                return true;

            case ItemIds.BombBag:
                resolved = new ResolvedSocketItem(SocketSlotType.BombCount, null, 1);
                return true;

            default:
                resolved = default;
                return false;
        }
    }
}