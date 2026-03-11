// ----------------------------
// ソケットアイテムのドロップ設定
// - 爆風で壊した石 / 鉱石ブロックからの抽選を管理する
// ----------------------------
namespace BomberGear.Config;

internal sealed class SocketDropConfig
{
    // ----------------------------
    // ドロップ全体の有効 / 無効
    // ----------------------------
    public bool Enabled { get; set; } = true;

    // ----------------------------
    // MineShaft 系ロケーションだけに制限する
    // ----------------------------
    public bool OnlyDropInMineLikeLocations { get; set; } = true;

    // ----------------------------
    // 1フロアあたりに期待する
    // 火力 / 爆弾数系グループの総ドロップ数
    // 1.0 = 1フロアでだいたい1個
    // ----------------------------
    public float PowerBombDropsPerMineFloor { get; set; } = 1.0f;

    // ----------------------------
    // 1フロアあたりに期待する
    // 特性系グループの総ドロップ数
    // 0. = 10フロアで1個くらい
    // ----------------------------
    public float ActionChipDropsPerMineFloor { get; set; } = 0.1f;

    // ----------------------------
    // 1フロアで壊す石の想定数
    // ----------------------------
    public float ExpectedStoneBreaksPerMineFloor { get; set; } = 20.0f;

    // ----------------------------
    // 1フロアで壊す鉱石ブロックの想定数
    // ----------------------------
    public float ExpectedOreBreaksPerMineFloor { get; set; } = 5.0f;

    // ----------------------------
    // 石の出現倍率
    // 1.0 = 基準
    // ----------------------------
    public float StoneDropChanceMultiplier { get; set; } = 1.0f;

    // ----------------------------
    // 鉱石ブロックの出現倍率
    // 3 = 石の 3 倍 出やすい
    // ----------------------------
    public float OreDropChanceMultiplier { get; set; } = 3f;

    // ----------------------------
    // ドロップ数上限を適用するか
    // ----------------------------
    public bool ApplyDropCaps { get; set; } = true;
}