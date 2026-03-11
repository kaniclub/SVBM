// ----------------------------
// config.json 用
// ----------------------------
using BomberGear.Items;
using StardewModdingAPI.Utilities;

namespace BomberGear.Config;

internal sealed class ModConfig
{
    // ----------------------------
    // ソケットメニューを開くキー
    // ----------------------------
    public KeybindList OpenSocketMenuKey { get; set; } = KeybindList.Parse("K");

    // ----------------------------
    // 爆弾を置いてから爆発するまでの秒数
    // ----------------------------
    public float FuseSeconds { get; set; } = 2.5f;

    // ----------------------------
    // 初期火力
    // 1 = 上下左右1マス
    // ----------------------------
    public int BasePower { get; set; } = 2;

    // ----------------------------
    // 火力の最終上限
    // ----------------------------
    public int MaxPower { get; set; } = SocketLimits.MaxPowerValue;

    // ----------------------------
    // 初期同時設置数
    // ----------------------------
    public int BaseMaxBombs { get; set; } = 1;

    // ----------------------------
    // 同時設置数の最終上限
    // ----------------------------
    public int MaxBombs { get; set; } = SocketLimits.MaxBombValue;

    // ----------------------------
    // プレイヤーへの固定ダメージ
    // 0 にすると無効
    // ----------------------------
    public int PlayerDamage { get; set; } = 20;

    // ----------------------------
    // モンスターへの固定ダメージ
    // 0 にすると無効
    // ----------------------------
    public int MonsterDamage { get; set; } = 100;

    // ----------------------------
    // 爆風ダメージ判定が残る秒数
    // ----------------------------
    public float ExplosionDamageDurationSeconds { get; set; } = 0.8f;

    // ----------------------------
    // 爆弾ダメージを受けた後、
    // 別の爆弾を含めて再度ダメージを受けない秒数
    // ----------------------------
    public float BombDamageInvincibilitySeconds { get; set; } = 1.0f;

    // ----------------------------
    // キック時の1マス移動間隔
    // 値が大きいほど遅い
    // ----------------------------
    public int KickSlideStepTicks { get; set; } = 8;

    // ----------------------------
    // 戦闘不能時にボンバーギアの
    // ソケットを初期化するか
    // ----------------------------
    public bool ResetSocketsOnDeath { get; set; } = true;

    // ----------------------------
    // ソケットアイテムのドロップ設定
    // ----------------------------
    public SocketDropConfig SocketDrops { get; set; } = new();

    // ----------------------------
    // デフォルトへ戻す
    // ----------------------------
    public void ResetToDefaults()
    {
        OpenSocketMenuKey = KeybindList.Parse("K");
        FuseSeconds = 2.5f;
        BasePower = 2;
        MaxPower = SocketLimits.MaxPowerValue;
        BaseMaxBombs = 1;
        MaxBombs = SocketLimits.MaxBombValue;
        PlayerDamage = 20;
        MonsterDamage = 100;
        ExplosionDamageDurationSeconds = 0.8f;
        BombDamageInvincibilitySeconds = 1.0f;
        KickSlideStepTicks = 8;
        ResetSocketsOnDeath = true;
        SocketDrops = new SocketDropConfig();
    }
}