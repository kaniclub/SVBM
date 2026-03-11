// ----------------------------
// 爆弾特性の特殊発動処理
// - 入力で直接発動する特性を担当
// ----------------------------
using System.Linq;
using BomberGear.Bombs;
using BomberGear.Config;
using BomberGear.Items;
using StardewValley;

namespace BomberGear.BombTraits;

internal sealed class BombTraitSpecialHandler
{
    private readonly BombManager bombs;

    public BombTraitSpecialHandler(BombManager bombs)
    {
        this.bombs = bombs;
    }

    // ----------------------------
    // 指定特性に特殊発動処理があるか
    // - 現在は remote のみ true
    // ----------------------------
    public bool HasSpecial(string bombTraitValue)
    {
        return bombTraitValue == BombTraitValues.Remote;
    }

    // ----------------------------
    // 特性の特殊発動を試す
    // - 発動対象が無い場合も false
    // - 未対応特性は false
    // ----------------------------
    public bool TryHandle(Farmer who, GameLocation location, string bombTraitValue, ModConfig config)
    {
        _ = location;

        return bombTraitValue switch
        {
            BombTraitValues.Remote => TryHandleRemoteDetonate(who, config),
            _ => false
        };
    }

    // ----------------------------
    // リモコン起爆
    // - 設置中の最も古い爆弾1個を起爆する
    // ----------------------------
    private bool TryHandleRemoteDetonate(Farmer who, ModConfig config)
    {
        var placedBombs = bombs.GetBombsForPlayer(who.UniqueMultiplayerID);
        var oldestBomb = placedBombs.FirstOrDefault();

        if (oldestBomb is null)
            return false;

        return bombs.TryDetonateBomb(oldestBomb, who, config);
    }
}