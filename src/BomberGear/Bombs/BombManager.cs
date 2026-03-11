// ----------------------------
// 爆弾の設置 / 更新 / 起爆管理
// - 設置
// - 更新
// - 爆発
// の中心処理を担当する
// ----------------------------
using BomberGear.Config;
using BomberGear.Drops;
using BomberGear.Explosion;
using BomberGear.Explosion.Breakables;
using BomberGear.Explosion.Combat;
using BomberGear.Explosion.Visuals;
using BomberGear.Items;
using Microsoft.Xna.Framework;
using StardewValley;
using XTileLocation = xTile.Dimensions.Location;

namespace BomberGear.Bombs;

internal sealed partial class BombManager
{
    // ----------------------------
    // 誘爆時の遅延
    // 10 ticks ≒ 0.17秒
    // ----------------------------
    private const int ChainReactionDelayTicks = 10;

    // ----------------------------
    // 破壊フラッシュ時間
    // ----------------------------
    private const int BreakFlashTicks = 8;

    private readonly ExplosionService explosion;
    private readonly ExplosionVisualManager explosionVisuals;
    private readonly ExplosionDamageManager explosionDamageManager = new();
    private readonly WallCollisionChecker wallChecker = new();

    // ----------------------------
    // ソケットアイテムのドロップ処理
    // ----------------------------
    private readonly SocketDropService socketDropService = new();

    // ----------------------------
    // 設置済み爆弾ごとの特性値
    // ----------------------------
    private readonly Dictionary<ActiveBomb, string> bombTraitValues = new();

    // ----------------------------
    // プレイヤーごとの爆弾一覧
    // ----------------------------
    private readonly Dictionary<long, List<ActiveBomb>> bombsByPlayer = new();

    // ----------------------------
    // 座標検索用インデックス
    // ----------------------------
    private readonly Dictionary<BombPositionKey, List<ActiveBomb>> bombsByPosition = new();

    private readonly List<BreakFlash> breakFlashes = new();

    // ----------------------------
    // 座標検索用キー
    // ----------------------------
    private readonly record struct BombPositionKey(string LocationName, int X, int Y);

    public BombManager(ExplosionService explosion, ExplosionVisualManager explosionVisuals)
    {
        this.explosion = explosion;
        this.explosionVisuals = explosionVisuals;
    }

    // ----------------------------
    // 破壊フラッシュ情報
    // ----------------------------
    private sealed class BreakFlash
    {
        public string LocationName { get; }
        public Vector2 Tile { get; }
        public int TicksLeft { get; set; }

        public BreakFlash(string locationName, Vector2 tile, int ticksLeft)
        {
            LocationName = locationName;
            Tile = tile;
            TicksLeft = ticksLeft;
        }
    }

    // ----------------------------
    // 爆弾一覧をクリア
    // ----------------------------
    public void ClearAll()
    {
        foreach (var list in bombsByPlayer.Values)
        {
            foreach (var bomb in list)
                StopFuseSound(bomb);
        }

        bombsByPlayer.Clear();
        bombsByPosition.Clear();
        breakFlashes.Clear();
        bombTraitValues.Clear();
        explosionDamageManager.Clear();
    }

    // ----------------------------
    // 指定プレイヤーの地上爆弾一覧を取得
    // - 持ち上げ中 / 投げ中は除外
    // - 追加順のまま返すため、先頭が最も古い爆弾
    // ----------------------------
    public IReadOnlyList<ActiveBomb> GetBombsForPlayer(long playerId)
    {
        if (!bombsByPlayer.TryGetValue(playerId, out var list))
            return Array.Empty<ActiveBomb>();

        return list
            .Where(bomb => !bomb.IsHeld && !bomb.IsThrown)
            .ToList();
    }

    // ----------------------------
    // 指定した爆弾を起爆
    // - 持ち上げ中 / 投げ中は起爆しない
    // ----------------------------
    public bool TryDetonateBomb(ActiveBomb bomb, Farmer who, ModConfig config)
    {
        if (!ContainsBomb(bomb))
            return false;

        if (bomb.IsHeld || bomb.IsThrown)
            return false;

        DetonateBomb(bomb, who, config);
        return true;
    }

    // ----------------------------
    // 爆弾を足元に置く
    // - bombTraitValue は設置時点の特性値を保存する
    // - 設置者本人は blocking 判定から除外する
    // - リモコンバクダンは導火線音を鳴らさない
    // ----------------------------
    public bool TryPlaceBomb(
        Farmer who,
        GameLocation location,
        int power,
        int fuseTicks,
        int maxBombs,
        string bombTraitValue = BombTraitValues.None)
    {
        Vector2 tile = who.Tile;

        if (!location.isTilePassable(new XTileLocation((int)tile.X, (int)tile.Y), Game1.viewport))
            return false;

        if (HasBlockingObjectAt(location, tile))
            return false;

        if (HasBlockingTerrainFeatureAt(location, tile))
            return false;

        if (HasClumpAt(location, tile))
            return false;

        if (wallChecker.IsHardWall(location, tile))
            return false;

        if (HasBlockingCharacterAt(location, tile, who))
            return false;

        if (HasBombAt(location.Name, tile))
            return false;

        var list = GetOrCreatePlayerList(who.UniqueMultiplayerID);
        if (list.Count >= maxBombs)
            return false;

        var bomb = new ActiveBomb(who.UniqueMultiplayerID, location.Name, tile, power, fuseTicks);

        list.Add(bomb);
        AddBombToPositionIndex(bomb);
        SetBombTraitValue(bomb, bombTraitValue);

        location.playSound("thudStep");

        if (!IsRemoteBomb(bomb))
            StartFuseSound(bomb);

        return true;
    }

    // ----------------------------
    // 設置を塞ぐ Object があるか
    // - 通過できる雑草などは設置OK
    // - 通過できない Object だけ設置NG
    // ----------------------------
    private bool HasBlockingObjectAt(GameLocation location, Vector2 tile)
    {
        if (!location.Objects.TryGetValue(tile, out StardewValley.Object? obj))
            return false;

        return !obj.isPassable();
    }

    // ----------------------------
    // 設置を塞ぐ TerrainFeature があるか
    // - 草 / 床など通過できるものは設置OK
    // - 木 / 作物など通過できないものは設置NG
    // ----------------------------
    private bool HasBlockingTerrainFeatureAt(GameLocation location, Vector2 tile)
    {
        if (!location.terrainFeatures.TryGetValue(tile, out var feature))
            return false;

        return !feature.isPassable();
    }

    // ----------------------------
    // キックでぶつかった爆弾を滑らせる
    // - 最初の1マスは即時に動かす
    // - その後は Update で継続滑走する
    // - 蹴り先が壁やキャラなら動かない
    // ----------------------------
    public bool TryKickBlockingBomb(
        Farmer who,
        GameLocation location,
        Rectangle nextPosition,
        Vector2 direction,
        int slideStepTicks)
    {
        if (direction == Vector2.Zero)
            return false;

        ActiveBomb? bomb = FindBlockingBombForCharacter(who, location.Name, nextPosition);
        if (bomb is null)
            return false;

        if (bomb.IsSliding || bomb.IsHeld || bomb.IsThrown)
            return false;

        Vector2 normalizedDirection = NormalizeDirection(direction);
        if (normalizedDirection == Vector2.Zero)
            return false;

        Vector2 targetTile = bomb.Tile + normalizedDirection;
        if (!TryMoveBombTo(bomb, location, targetTile, slideStepTicks))
            return false;

        bomb.StartSliding(normalizedDirection, slideStepTicks);
        location.playSound("woodyHit");

        return true;
    }

    // ----------------------------
    // 毎tick更新
    // - 位置更新
    // - 投げ / 滑走 / 持ち上げ更新
    // - 残留爆風による誘爆判定
    // - タイマー進行
    // を行う
    // ----------------------------
    public void Update(ModConfig config)
    {
        var readyBombs = new List<ActiveBomb>();

        foreach (var pair in bombsByPlayer)
        {
            foreach (var bomb in pair.Value.ToList())
            {
                UpdateHeldBombState(bomb);

                if (!bomb.IsHeld && !bomb.IsThrown)
                    UpdateOwnerPassThroughState(bomb);

                UpdateSlidingBombState(bomb);
                UpdateThrownBombState(bomb);
                bomb.UpdateVisualMotion();
                bomb.AgeTicks++;
            }
        }

        // ----------------------------
        // 爆風ダメージが残っている間は、
        // そのタイルへ入った爆弾にも誘爆判定を残す
        // - 持ち上げ中 / 投げ中は除外
        // ----------------------------
        ApplyLingeringChainReactions();

        // ----------------------------
        // タイマー進行
        // - リモコンバクダンは通常時は進めない
        // - 誘爆予約済みなら進める
        // ----------------------------
        foreach (var pair in bombsByPlayer)
        {
            foreach (var bomb in pair.Value.ToList())
            {
                if (!ShouldAdvanceBombTimer(bomb))
                    continue;

                bomb.TicksLeft--;

                if (bomb.TicksLeft <= 0)
                    readyBombs.Add(bomb);
            }
        }

        foreach (var flash in breakFlashes.ToList())
        {
            flash.TicksLeft--;
            if (flash.TicksLeft <= 0)
                breakFlashes.Remove(flash);
        }

        foreach (var bomb in readyBombs)
        {
            if (!ContainsBomb(bomb))
                continue;

            Farmer sourceFarmer = FindOwnerFarmer(bomb.OwnerId) ?? Game1.player;
            DetonateBomb(bomb, sourceFarmer, config);
        }

        explosionDamageManager.Update(config);
    }

    // ----------------------------
    // 爆弾との衝突判定
    // - NPC / 敵 / 他人は常にブロック
    // - 設置者本人だけは、まだ爆弾タイルに重なっている間だけ通過可
    // ----------------------------
    public bool IntersectsBlockingBomb(Character character, GameLocation location, Rectangle nextPosition)
    {
        int left = nextPosition.Left / Game1.tileSize;
        int right = (nextPosition.Right - 1) / Game1.tileSize;
        int top = nextPosition.Top / Game1.tileSize;
        int bottom = (nextPosition.Bottom - 1) / Game1.tileSize;

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (IsBlockedByBombForCharacter(character, location.Name, new Vector2(x, y)))
                    return true;
            }
        }

        return false;
    }

    // ----------------------------
    // 爆弾を1個爆発させる
    // - 爆発結果から石/鉱石ブロックのドロップ判定も行う
    // ----------------------------
    private void DetonateBomb(ActiveBomb bomb, Farmer who, ModConfig config)
    {
        if (!ContainsBomb(bomb))
            return;

        string bombTraitValue = GetBombTraitValue(bomb);

        StopFuseSound(bomb);
        RemoveBomb(bomb);

        var location = Game1.getLocationFromName(bomb.LocationName);
        if (location is null)
            return;

        var result = explosion.ExplodeCross(
            location: location,
            originTile: bomb.Tile,
            power: bomb.Power,
            who: who,
            config: config,
            bombTraitValue: bombTraitValue,
            onBombHit: tile => TryTriggerBombAt(location.Name, tile)
        );

        // ----------------------------
        // 壊れた石 / 鉱石ブロックから
        // ソケットアイテムのドロップ判定
        // ----------------------------
        socketDropService.TryDropFromBrokenBlocks(
            location: location,
            who: who,
            brokenBlocks: result.BrokenBlocks,
            config: config
        );

        explosionDamageManager.Spawn(
            location: location,
            affectedTiles: result.AffectedTiles,
            sourceFarmer: who,
            playerDamage: config.PlayerDamage,
            monsterDamage: config.MonsterDamage,
            durationSeconds: config.ExplosionDamageDurationSeconds
        );

        foreach (var brokenTile in result.BrokenTiles)
            breakFlashes.Add(new BreakFlash(location.Name, brokenTile, BreakFlashTicks));

        explosionVisuals.SpawnCross(
            locationName: location.Name,
            originTile: bomb.Tile,
            power: bomb.Power,
            affectedTiles: result.AffectedTiles
        );
    }

    // ----------------------------
    // 指定タイルの爆弾を誘爆予約する
    // - 爆弾があれば true を返す
    // - true のとき、爆風側はその方向をそこで止める
    // ----------------------------
    private bool TryTriggerBombAt(string locationName, Vector2 tile)
    {
        ActiveBomb? hitBomb = FindBombsAt(locationName, tile)
            .FirstOrDefault(bomb => !bomb.IsHeld && !bomb.IsThrown);

        if (hitBomb is null)
            return false;

        TriggerChainReaction(hitBomb);
        return true;
    }

    // ----------------------------
    // 残留爆風の上にいる爆弾へ継続的に誘爆判定を行う
    // - 爆風ダメージが残っている間は誘爆判定も残す
    // - 持ち上げ中 / 投げ中は除外
    // ----------------------------
    private void ApplyLingeringChainReactions()
    {
        foreach (var pair in bombsByPlayer)
        {
            foreach (var bomb in pair.Value.ToList())
            {
                if (bomb.IsHeld || bomb.IsThrown)
                    continue;

                if (explosionDamageManager.IsTileInActiveZone(bomb.LocationName, bomb.Tile))
                    TriggerChainReaction(bomb);
            }
        }
    }

    // ----------------------------
    // この爆弾のタイマーを進めるべきか
    // - 通常爆弾 / 貫通爆弾は通常通り進める
    // - リモコンバクダンは通常時は進めない
    // - ただし誘爆予約済みなら進める
    // ----------------------------
    private bool ShouldAdvanceBombTimer(ActiveBomb bomb)
    {
        if (!bomb.CanTickTimer())
            return false;

        if (bomb.IsChainTriggered)
            return true;

        return !IsRemoteBomb(bomb);
    }

    // ----------------------------
    // リモコンバクダンかどうか
    // - trait 値に remote を含めばリモコン扱い
    // ----------------------------
    private bool IsRemoteBomb(ActiveBomb bomb)
    {
        string traitValue = GetBombTraitValue(bomb);

        if (string.IsNullOrWhiteSpace(traitValue))
            return false;

        return traitValue.Contains("remote", StringComparison.OrdinalIgnoreCase);
    }

    // ----------------------------
    // 爆弾へ誘爆予約を入れる
    // - 既に誘爆待ちなら何もしない
    // - 持ち上げ中 / 投げ中は誘爆しない
    // - まだ通常導火線中なら、より早い方を優先する
    // - リモコンバクダンは誘爆時も導火線音を鳴らさない
    // ----------------------------
    private void TriggerChainReaction(ActiveBomb bomb)
    {
        if (bomb.IsHeld || bomb.IsThrown)
            return;

        if (bomb.IsChainTriggered)
            return;

        bomb.IsChainTriggered = true;
        bomb.TicksLeft = Math.Min(bomb.TicksLeft, ChainReactionDelayTicks);

        if (!IsRemoteBomb(bomb))
            StartFuseSound(bomb);
    }
}