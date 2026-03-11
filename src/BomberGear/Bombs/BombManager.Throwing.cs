// ----------------------------
// BombManager のパワーグローブ処理
// - 目の前の爆弾を持ち上げる
// - 持ち上げ中にアクションで投げる
// - 最初は2マス先へ投げる
// - その先で設置可能マスまで進む
// - マップ外へ出たら反対側から継続する
// ----------------------------
using Microsoft.Xna.Framework;
using StardewValley;

namespace BomberGear.Bombs;

internal sealed partial class BombManager
{
    // ----------------------------
    // 最初の投げで何マス進めるか
    // ----------------------------
    private const int ThrowInitialLeapTiles = 2;

    // ----------------------------
    // 指定プレイヤーが爆弾を持ち上げ中か
    // ----------------------------
    public bool HasHeldBomb(long playerId)
    {
        return TryGetHeldBomb(playerId, out _);
    }

    // ----------------------------
    // 目の前の爆弾を持ち上げる
    // - 右クリック時に使用
    // - タイマーは停止し、再開時は残り時間をそのまま使う
    // ----------------------------
    public bool TryPickUpFrontBomb(Farmer who, GameLocation location)
    {
        if (TryGetHeldBomb(who.UniqueMultiplayerID, out _))
            return false;

        Vector2 frontTile = GetFrontTile(who);
        ActiveBomb? bomb = FindGroundBombAt(location.Name, frontTile);

        if (bomb is null)
            return false;

        if (bomb.IsSliding)
            return false;

        StopFuseSound(bomb);
        RemoveBombFromPositionIndex(bomb);
        bomb.PickUp(who.UniqueMultiplayerID);

        who.showCarrying();
        location.playSound("pickUpItem");
        return true;
    }

    // ----------------------------
    // 持ち上げ中の爆弾を投げる
    // - 速度はキックと同じ設定値を使う
    // - 最初の移動は2マス跳躍
    // ----------------------------
    public bool TryThrowHeldBomb(Farmer who, GameLocation location, int speedStepTicks)
    {
        if (!TryGetHeldBomb(who.UniqueMultiplayerID, out ActiveBomb bomb))
            return false;

        Vector2 throwDirection = FacingDirectionToVector(who.FacingDirection);
        if (throwDirection == Vector2.Zero)
            return false;

        int stepTicks = speedStepTicks < 1 ? 1 : speedStepTicks;

        bomb.StartThrow(
            playerTile: who.Tile,
            direction: throwDirection,
            stepTicks: stepTicks
        );

        location.playSound("woodyHit");
        return true;
    }

    // ----------------------------
    // 描画直前に、保持中プレイヤーへ carry pose を強制
    // ----------------------------
    public void PrepareHeldBombCarryPose(GameLocation currentLocation)
    {
        foreach (var pair in bombsByPlayer)
        {
            foreach (var bomb in pair.Value)
            {
                if (!bomb.IsHeld)
                    continue;

                Farmer? holder = FindOwnerFarmer(bomb.HeldByPlayerId);
                if (holder is null)
                    continue;

                if (holder.currentLocation?.Name != currentLocation.Name)
                    continue;

                holder.showCarrying();
            }
        }
    }

    // ----------------------------
    // 持ち上げ中爆弾の更新
    // - プレイヤー移動に追従
    // - マップ移動にも追従
    // ----------------------------
    private void UpdateHeldBombState(ActiveBomb bomb)
    {
        if (!bomb.IsHeld)
            return;

        Farmer? holder = FindOwnerFarmer(bomb.HeldByPlayerId);
        if (holder is null)
        {
            bomb.ClearCarryState();
            return;
        }

        if (holder.currentLocation is null)
            return;

        bomb.SetLocationName(holder.currentLocation.Name);
        bomb.MoveTo(holder.Tile);
    }

    // ----------------------------
    // 投げ中爆弾の更新
    // - 最初は2マス先へ跳躍する
    // - その後は1マスずつ進める
    // - 設置可能なマスで着地する
    // - マップ外へ出たら反対側へラップする
    // ----------------------------
    private void UpdateThrownBombState(ActiveBomb bomb)
    {
        if (!bomb.IsThrown)
            return;

        bomb.ThrowStepTicksLeft--;
        if (bomb.ThrowStepTicksLeft > 0)
            return;

        GameLocation? location = Game1.getLocationFromName(bomb.LocationName);
        if (location is null)
        {
            bomb.ClearCarryState();
            return;
        }

        bool isLaunchStep = bomb.ShouldUseInitialThrowLeap();
        int moveTiles = isLaunchStep ? ThrowInitialLeapTiles : 1;

        Vector2 nextTile = AdvanceTileWithWrap(
            location,
            bomb.Tile,
            bomb.ThrowDirection,
            moveTiles
        );

        bomb.AdvanceThrownStep(nextTile, moveTiles, isLaunchStep);

        if (isLaunchStep)
            bomb.MarkInitialThrowLeapUsed();

        if (CanBombLandAt(location, nextTile))
        {
            FinalizeThrownBombLanding(bomb, location);
            return;
        }

        bomb.ThrowStepTicksLeft = bomb.ThrowStepIntervalTicks * moveTiles;
    }

    // ----------------------------
    // 投げた爆弾を着地させる
    // - 地上インデックスへ戻す
    // - 導火線とタイマーを再開する
    // ----------------------------
    private void FinalizeThrownBombLanding(ActiveBomb bomb, GameLocation location)
    {
        bomb.FinishThrow();
        AddBombToPositionIndex(bomb);
        StartFuseSound(bomb);
        location.playSound("thudStep");
    }

    // ----------------------------
    // 指定プレイヤーが持っている爆弾を取得
    // ----------------------------
    private bool TryGetHeldBomb(long playerId, out ActiveBomb heldBomb)
    {
        heldBomb = null!;

        if (!bombsByPlayer.TryGetValue(playerId, out var list))
            return false;

        ActiveBomb? found = list.FirstOrDefault(bomb => bomb.IsHeld && bomb.HeldByPlayerId == playerId);
        if (found is null)
            return false;

        heldBomb = found;
        return true;
    }

    // ----------------------------
    // 地上にある爆弾を1個取得
    // - 持ち上げ中 / 投げ中は除外
    // ----------------------------
    private ActiveBomb? FindGroundBombAt(string locationName, Vector2 tile)
    {
        return FindBombsAt(locationName, tile)
            .FirstOrDefault(bomb => !bomb.IsHeld && !bomb.IsThrown);
    }

    // ----------------------------
    // そのタイルへ爆弾を着地できるか
    // - 壁、オブジェクト、地形、clump、キャラ、他爆弾がある場所は不可
    // ----------------------------
    private bool CanBombLandAt(GameLocation location, Vector2 tile)
    {
        if (wallChecker.IsHardWall(location, tile))
            return false;

        if (!location.isTilePassable(new xTile.Dimensions.Location((int)tile.X, (int)tile.Y), Game1.viewport))
            return false;

        if (location.Objects.ContainsKey(tile))
            return false;

        if (location.terrainFeatures.ContainsKey(tile))
            return false;

        if (HasClumpAt(location, tile))
            return false;

        if (HasBlockingCharacterAt(location, tile))
            return false;

        if (HasBombAt(location.Name, tile))
            return false;

        return true;
    }

    // ----------------------------
    // プレイヤー正面タイルを取得
    // ----------------------------
    private static Vector2 GetFrontTile(Farmer who)
    {
        return who.Tile + FacingDirectionToVector(who.FacingDirection);
    }

    // ----------------------------
    // 向きを移動ベクトルへ変換
    // 0=上 1=右 2=下 3=左
    // ----------------------------
    private static Vector2 FacingDirectionToVector(int facingDirection)
    {
        return facingDirection switch
        {
            0 => new Vector2(0, -1),
            1 => new Vector2(1, 0),
            2 => new Vector2(0, 1),
            3 => new Vector2(-1, 0),
            _ => Vector2.Zero
        };
    }

    // ----------------------------
    // 指定方向へ stepCount マス進める
    // - マップ外へ出たら反対側へラップする
    // ----------------------------
    private static Vector2 AdvanceTileWithWrap(
        GameLocation location,
        Vector2 startTile,
        Vector2 direction,
        int stepCount)
    {
        Vector2 tile = startTile;

        for (int i = 0; i < stepCount; i++)
            tile = WrapTileToMap(location, tile + direction);

        return tile;
    }

    // ----------------------------
    // マップ外へ出たタイルを反対側へラップする
    // - 左右は左右でつながる
    // - 上下は上下でつながる
    // ----------------------------
    private static Vector2 WrapTileToMap(GameLocation location, Vector2 tile)
    {
        int width = GetMapWidth(location);
        int height = GetMapHeight(location);

        if (width <= 0 || height <= 0)
            return tile;

        int x = (int)tile.X;
        int y = (int)tile.Y;

        if (x < 0)
            x = width - 1;
        else if (x >= width)
            x = 0;

        if (y < 0)
            y = height - 1;
        else if (y >= height)
            y = 0;

        return new Vector2(x, y);
    }

    // ----------------------------
    // マップ幅を取得
    // ----------------------------
    private static int GetMapWidth(GameLocation location)
    {
        return location.Map?.Layers.Count > 0
            ? location.Map.Layers[0].LayerWidth
            : 0;
    }

    // ----------------------------
    // マップ高さを取得
    // ----------------------------
    private static int GetMapHeight(GameLocation location)
    {
        return location.Map?.Layers.Count > 0
            ? location.Map.Layers[0].LayerHeight
            : 0;
    }
}