// ----------------------------
// ソケットドロップ関連の共通モデル
// - enum
// - 壊れたブロック情報
// - デバッグ用カウント情報
// をまとめる
// ----------------------------
using Microsoft.Xna.Framework;

namespace BomberGear.Drops;

// ----------------------------
// ドロップ抽選元の分類
// ----------------------------
internal enum BreakableDropSourceKind
{
    None = 0,
    Stone = 1,
    OreBlock = 2
}

// ----------------------------
// ドロップグループ
// ----------------------------
internal enum SocketDropGroup
{
    PowerBomb = 0,
    ActionChip = 1
}

// ----------------------------
// ドロップするソケットアイテム種別
// ----------------------------
internal enum SocketDropItemKind
{
    RemoteBombChip = 0,
    PierceBombChip = 1,
    KickChip = 2,
    ThrowChip = 3,
    PowerCore = 4,
    BombBag = 5
}

// ----------------------------
// 壊れたブロックの情報
// - どのタイルで
// - 何系のブロックだったか
// ----------------------------
internal sealed class BrokenBlockInfo
{
    public Vector2 Tile { get; }
    public BreakableDropSourceKind SourceKind { get; }

    public BrokenBlockInfo(Vector2 tile, BreakableDropSourceKind sourceKind)
    {
        Tile = tile;
        SourceKind = sourceKind;
    }
}

// ----------------------------
// ソケットアイテム実在数の内訳
// - 手持ち(所持品 + カーソル)
// - チェスト
// - 地面ドロップ
// - Gear装着
// ----------------------------
internal sealed class SocketItemCountBreakdown
{
    public SocketDropItemKind Kind { get; }
    public int HeldCount { get; }
    public int ChestCount { get; }
    public int DebrisCount { get; }
    public int SocketedOnGearCount { get; }

    public int TotalCount => HeldCount + ChestCount + DebrisCount + SocketedOnGearCount;

    public SocketItemCountBreakdown(
        SocketDropItemKind kind,
        int heldCount,
        int chestCount,
        int debrisCount,
        int socketedOnGearCount)
    {
        Kind = kind;
        HeldCount = heldCount;
        ChestCount = chestCount;
        DebrisCount = debrisCount;
        SocketedOnGearCount = socketedOnGearCount;
    }
}