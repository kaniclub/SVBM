// ----------------------------
// BombManager の移動系処理
// - キック
// - 滑走
// - キャラ衝突
// - 移動可能判定
// ----------------------------
using Microsoft.Xna.Framework;
using StardewValley;
using XTileLocation = xTile.Dimensions.Location;

namespace BomberGear.Bombs;

internal sealed partial class BombManager
{
    // ----------------------------
    // キック滑走の更新
    // ----------------------------
    private void UpdateSlidingBombState(ActiveBomb bomb)
    {
        if (!bomb.IsSliding)
            return;

        bomb.SlideStepTicksLeft--;
        if (bomb.SlideStepTicksLeft > 0)
            return;

        if (TryAdvanceSlidingBomb(bomb))
        {
            bomb.SlideStepTicksLeft = bomb.SlideStepIntervalTicks;
            return;
        }

        bomb.StopSliding();
    }

    // ----------------------------
    // 滑走中の爆弾を1マス進める
    // - 壁やキャラでも止まる
    // ----------------------------
    private bool TryAdvanceSlidingBomb(ActiveBomb bomb)
    {
        var location = Game1.getLocationFromName(bomb.LocationName);
        if (location is null)
            return false;

        Vector2 targetTile = bomb.Tile + bomb.SlideDirection;
        return TryMoveBombTo(bomb, location, targetTile, bomb.SlideStepIntervalTicks);
    }

    // ----------------------------
    // 爆弾を指定タイルへ動かす
    // ----------------------------
    private bool TryMoveBombTo(ActiveBomb bomb, GameLocation location, Vector2 targetTile, int animationTicks)
    {
        if (!CanBombMoveTo(location, bomb, targetTile))
            return false;

        RemoveBombFromPositionIndex(bomb);
        bomb.MoveTo(targetTile, animationTicks);
        AddBombToPositionIndex(bomb);
        return true;
    }

    // ----------------------------
    // 爆弾がそのタイルへ進めるか
    // - 壁判定の場所でも止める
    // - プレイヤー / NPC / 敵 / 動物がいる場所でも止める
    // ----------------------------
    private bool CanBombMoveTo(GameLocation location, ActiveBomb movingBomb, Vector2 tile)
    {
        if (wallChecker.IsHardWall(location, tile))
            return false;

        if (!location.isTilePassable(new XTileLocation((int)tile.X, (int)tile.Y), Game1.viewport))
            return false;

        if (location.Objects.ContainsKey(tile))
            return false;

        if (location.terrainFeatures.ContainsKey(tile))
            return false;

        if (HasClumpAt(location, tile))
            return false;

        if (HasBlockingCharacterAt(location, tile))
            return false;

        foreach (var bomb in FindBombsAt(location.Name, tile))
        {
            if (!ReferenceEquals(bomb, movingBomb))
                return false;
        }

        return true;
    }

    // ----------------------------
    // 指定タイルにプレイヤー / NPC / 敵 / 動物がいるか
    // ----------------------------
    private static bool HasBlockingCharacterAt(GameLocation location, Vector2 tile, Character? ignoreCharacter = null)
    {
        Rectangle tileRect = new(
            (int)tile.X * Game1.tileSize,
            (int)tile.Y * Game1.tileSize,
            Game1.tileSize,
            Game1.tileSize
        );

        foreach (Farmer farmer in Game1.getAllFarmers())
        {
            if (ReferenceEquals(farmer, ignoreCharacter))
                continue;

            if (farmer.currentLocation?.Name != location.Name)
                continue;

            if (farmer.GetBoundingBox().Intersects(tileRect))
                return true;
        }

        foreach (Character character in location.characters)
        {
            if (ReferenceEquals(character, ignoreCharacter))
                continue;

            if (character.GetBoundingBox().Intersects(tileRect))
                return true;
        }

        if (location is Farm farm)
        {
            foreach (FarmAnimal animal in farm.animals.Values)
            {
                if (animal.GetBoundingBox().Intersects(tileRect))
                    return true;
            }
        }

        return false;
    }

    // ----------------------------
    // 指定キャラにとって実際にブロック中の爆弾を探す
    // ----------------------------
    private ActiveBomb? FindBlockingBombForCharacter(Character character, string locationName, Rectangle nextPosition)
    {
        int left = nextPosition.Left / Game1.tileSize;
        int right = (nextPosition.Right - 1) / Game1.tileSize;
        int top = nextPosition.Top / Game1.tileSize;
        int bottom = (nextPosition.Bottom - 1) / Game1.tileSize;

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                Vector2 tile = new(x, y);

                foreach (var bomb in FindBombsAt(locationName, tile))
                {
                    if (character is Farmer farmer && farmer.UniqueMultiplayerID == bomb.OwnerId)
                    {
                        if (bomb.OwnerCanPassThrough && IsCharacterOverlappingBombTile(farmer, bomb))
                            continue;

                        bomb.OwnerCanPassThrough = false;
                    }

                    return bomb;
                }
            }
        }

        return null;
    }

    // ----------------------------
    // 方向を正規化
    // - 対角が来ても縦横どちらかへ寄せる
    // ----------------------------
    private static Vector2 NormalizeDirection(Vector2 direction)
    {
        if (Math.Abs(direction.X) > Math.Abs(direction.Y))
            return direction.X > 0 ? new Vector2(1, 0) : new Vector2(-1, 0);

        if (Math.Abs(direction.Y) > 0)
            return direction.Y > 0 ? new Vector2(0, 1) : new Vector2(0, -1);

        return Vector2.Zero;
    }

    // ----------------------------
    // 設置者がもう爆弾タイルから抜けたかを更新
    // - 完全に離れたら以後は通過不可
    // ----------------------------
    private void UpdateOwnerPassThroughState(ActiveBomb bomb)
    {
        if (!bomb.OwnerCanPassThrough)
            return;

        Farmer? owner = FindOwnerFarmer(bomb.OwnerId);
        if (owner is null)
        {
            bomb.OwnerCanPassThrough = false;
            return;
        }

        if (!IsCharacterOverlappingBombTile(owner, bomb))
            bomb.OwnerCanPassThrough = false;
    }

    // ----------------------------
    // 指定キャラに対して、そのタイルの爆弾が通行不可か
    // ----------------------------
    private bool IsBlockedByBombForCharacter(Character character, string locationName, Vector2 tile)
    {
        foreach (var bomb in FindBombsAt(locationName, tile))
        {
            if (character is Farmer farmer && farmer.UniqueMultiplayerID == bomb.OwnerId)
            {
                if (bomb.OwnerCanPassThrough && IsCharacterOverlappingBombTile(farmer, bomb))
                    continue;

                bomb.OwnerCanPassThrough = false;
                return true;
            }

            return true;
        }

        return false;
    }

    // ----------------------------
    // キャラがまだ爆弾タイルに重なっているか
    // - タイル一致ではなく当たり判定矩形で見る
    // ----------------------------
    private static bool IsCharacterOverlappingBombTile(Character character, ActiveBomb bomb)
    {
        if (character.currentLocation?.Name != bomb.LocationName)
            return false;

        Rectangle characterBox = character.GetBoundingBox();
        Rectangle bombTileRect = new(
            (int)bomb.Tile.X * Game1.tileSize,
            (int)bomb.Tile.Y * Game1.tileSize,
            Game1.tileSize,
            Game1.tileSize
        );

        return characterBox.Intersects(bombTileRect);
    }
}