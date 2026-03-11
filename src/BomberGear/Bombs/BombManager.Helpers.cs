// ----------------------------
// BombManager の補助処理
// - インデックス管理
// - trait 管理
// - 所有者探索
// - fuse 音制御
// - その他内部補助
// ----------------------------
using BomberGear.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using StardewValley;

namespace BomberGear.Bombs;

internal sealed partial class BombManager
{
    // ----------------------------
    // 設置者 Farmer を探す
    // ----------------------------
    private static Farmer? FindOwnerFarmer(long ownerId)
    {
        return Game1.getAllFarmers().FirstOrDefault(f => f.UniqueMultiplayerID == ownerId);
    }

    // ----------------------------
    // fuse音を開始
    // ----------------------------
    private static void StartFuseSound(ActiveBomb bomb)
    {
        StopFuseSound(bomb);

        try
        {
            var cue = Game1.soundBank?.GetCue("fuse");
            if (cue is null)
                return;

            cue.Play();
            bomb.FuseCue = cue;
        }
        catch
        {
            bomb.FuseCue = null;
        }
    }

    // ----------------------------
    // fuse音を停止
    // ----------------------------
    private static void StopFuseSound(ActiveBomb bomb)
    {
        try
        {
            bomb.FuseCue?.Stop(AudioStopOptions.Immediate);
        }
        catch
        {
        }
        finally
        {
            bomb.FuseCue = null;
        }
    }

    // ----------------------------
    // その場所にある爆弾を探す
    // ----------------------------
    private IEnumerable<ActiveBomb> FindBombsAt(string locationName, Vector2 tile)
    {
        var key = GetPositionKey(locationName, tile);

        if (bombsByPosition.TryGetValue(key, out var list))
            return list;

        return Enumerable.Empty<ActiveBomb>();
    }

    // ----------------------------
    // その場所に爆弾があるか
    // ----------------------------
    private bool HasBombAt(string locationName, Vector2 tile)
    {
        var key = GetPositionKey(locationName, tile);
        return bombsByPosition.TryGetValue(key, out var list) && list.Count > 0;
    }

    // ----------------------------
    // 位置インデックスへ爆弾を追加
    // ----------------------------
    private void AddBombToPositionIndex(ActiveBomb bomb)
    {
        var key = GetPositionKey(bomb.LocationName, bomb.Tile);

        if (!bombsByPosition.TryGetValue(key, out var list))
        {
            list = new List<ActiveBomb>();
            bombsByPosition[key] = list;
        }

        list.Add(bomb);
    }

    // ----------------------------
    // 位置インデックスから爆弾を削除
    // ----------------------------
    private void RemoveBombFromPositionIndex(ActiveBomb bomb)
    {
        var key = GetPositionKey(bomb.LocationName, bomb.Tile);

        if (!bombsByPosition.TryGetValue(key, out var list))
            return;

        list.Remove(bomb);

        if (list.Count == 0)
            bombsByPosition.Remove(key);
    }

    // ----------------------------
    // 設置済み爆弾へ特性値を保存
    // ----------------------------
    private void SetBombTraitValue(ActiveBomb bomb, string bombTraitValue)
    {
        bombTraitValues[bomb] = NormalizeBombTraitValue(bombTraitValue);
    }

    // ----------------------------
    // 設置済み爆弾の特性値を取得
    // ----------------------------
    private string GetBombTraitValue(ActiveBomb bomb)
    {
        if (bombTraitValues.TryGetValue(bomb, out var bombTraitValue))
            return bombTraitValue;

        return BombTraitValues.None;
    }

    // ----------------------------
    // 設置済み爆弾の特性値を削除
    // ----------------------------
    private void RemoveBombTraitValue(ActiveBomb bomb)
    {
        bombTraitValues.Remove(bomb);
    }

    // ----------------------------
    // 爆弾特性値を正規化
    // ----------------------------
    private static string NormalizeBombTraitValue(string? bombTraitValue)
    {
        return bombTraitValue switch
        {
            BombTraitValues.Remote => BombTraitValues.Remote,
            BombTraitValues.Pierce => BombTraitValues.Pierce,
            _ => BombTraitValues.None
        };
    }

    // ----------------------------
    // 位置インデックス用キーを作る
    // ----------------------------
    private static BombPositionKey GetPositionKey(string locationName, Vector2 tile)
    {
        return new BombPositionKey(locationName, (int)tile.X, (int)tile.Y);
    }

    // ----------------------------
    // その爆弾がまだ管理対象に残っているか
    // ----------------------------
    private bool ContainsBomb(ActiveBomb bomb)
    {
        return bombsByPlayer.TryGetValue(bomb.OwnerId, out var list) && list.Contains(bomb);
    }

    // ----------------------------
    // 爆弾を一覧から外す
    // ----------------------------
    private void RemoveBomb(ActiveBomb bomb)
    {
        if (bombsByPlayer.TryGetValue(bomb.OwnerId, out var list))
        {
            list.Remove(bomb);

            if (list.Count == 0)
                bombsByPlayer.Remove(bomb.OwnerId);
        }

        RemoveBombFromPositionIndex(bomb);
        RemoveBombTraitValue(bomb);
        bomb.StopSliding();
        bomb.ClearCarryState();
    }

    // ----------------------------
    // プレイヤーごとの爆弾リストを取得
    // ----------------------------
    private List<ActiveBomb> GetOrCreatePlayerList(long playerId)
    {
        if (!bombsByPlayer.TryGetValue(playerId, out var list))
        {
            list = new List<ActiveBomb>();
            bombsByPlayer[playerId] = list;
        }

        return list;
    }

    // ----------------------------
    // clump占有チェック
    // ----------------------------
    private static bool HasClumpAt(GameLocation location, Vector2 tile)
    {
        foreach (var clump in location.resourceClumps)
        {
            if (clump.occupiesTile((int)tile.X, (int)tile.Y))
                return true;
        }

        return false;
    }
}