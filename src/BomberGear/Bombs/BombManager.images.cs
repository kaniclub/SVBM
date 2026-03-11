// ----------------------------
// 爆弾の画像 / 描画処理
// - 通常 / 貫通 / リモコンの描画切り替え
// ----------------------------
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace BomberGear.Bombs;

internal sealed partial class BombManager
{
    // ----------------------------
    // 爆弾の描画タイプ
    // ----------------------------
    private enum BombDrawType
    {
        Normal,
        Pierce,
        Remote
    }

    // ----------------------------
    // 1タイル(64px)相当で描く倍率
    // ----------------------------
    private const float BombSpriteBaseScale = 1f;

    // ----------------------------
    // 爆弾の描画情報
    // ----------------------------
    private readonly record struct BombSpriteDrawInfo(
        Texture2D Texture,
        Rectangle? SourceRect,
        Vector2 Origin
    );

    // ----------------------------
    // 専用スプライト
    // ----------------------------
    private Texture2D normalBombSprite = null!;
    private Texture2D pierceBombSprite = null!;
    private Texture2D remoteBombSprite = null!;

    // ----------------------------
    // 専用スプライトを設定する
    // ----------------------------
    public void SetBombSprites(
        Texture2D normalBombSprite,
        Texture2D pierceBombSprite,
        Texture2D remoteBombSprite)
    {
        this.normalBombSprite = normalBombSprite;
        this.pierceBombSprite = pierceBombSprite;
        this.remoteBombSprite = remoteBombSprite;
    }

    // ----------------------------
    // ワールド上の爆弾描画
    // - 滑走中 / 投げ中は補間して描く
    // - 持ち上げ中はプレイヤー頭上へ描く
    // ----------------------------
    public void Draw(SpriteBatch spriteBatch, GameLocation currentLocation)
    {
        DrawBreakFlashes(spriteBatch, currentLocation);

        foreach (var list in bombsByPlayer.Values)
        {
            foreach (var bomb in list)
            {
                Farmer? holder = null;

                if (bomb.IsHeld)
                {
                    holder = FindOwnerFarmer(bomb.HeldByPlayerId);
                    if (holder is null)
                        continue;

                    if (holder.currentLocation?.Name != currentLocation.Name)
                        continue;
                }
                else
                {
                    if (bomb.LocationName != currentLocation.Name)
                        continue;
                }

                BombSpriteDrawInfo spriteInfo = ResolveBombSprite(bomb);

                float shakeX =
                    MathF.Sin(bomb.AgeTicks * 1.25f) * 1.4f +
                    MathF.Sin(bomb.AgeTicks * 2.4f) * 0.7f;

                float shakeY =
                    MathF.Cos(bomb.AgeTicks * 1.5f) * 1.1f +
                    MathF.Sin(bomb.AgeTicks * 2.8f) * 0.5f;

                float shrinkCycle = (MathF.Sin(bomb.AgeTicks / 12f) + 1f) * 0.5f;
                float pulseScale = 1.0f - (shrinkCycle * 0.08f);

                Color color = Color.White;
                if (bomb.TicksLeft <= 60)
                {
                    bool flash = (bomb.AgeTicks / 3) % 2 == 0;
                    color = flash ? Color.OrangeRed : Color.White;

                    pulseScale -= 0.03f;
                    shakeX *= 1.6f;
                    shakeY *= 1.6f;
                }

                Vector2 screenCenter;
                float layerDepth;

                if (bomb.IsHeld && holder is not null)
                {
                    Rectangle holderBox = holder.GetBoundingBox();

                    // ----------------------------
                    // 頭上まで持ち上げる
                    // ----------------------------
                    Vector2 worldCenter = new(
                        holderBox.Center.X + shakeX,
                        holderBox.Top - 96f + shakeY
                    );

                    screenCenter = Game1.GlobalToLocal(Game1.viewport, worldCenter);
                    layerDepth = Math.Max(0f, (holder.Position.Y - 48f) / 10000f);
                }
                else
                {
                    Vector2 drawTile = bomb.GetDrawTile();
                    float throwVisualOffsetY = bomb.GetThrownVisualOffsetY();

                    Vector2 worldCenter = drawTile * 64f + new Vector2(
                        32f + shakeX,
                        32f + shakeY + throwVisualOffsetY
                    );

                    screenCenter = Game1.GlobalToLocal(Game1.viewport, worldCenter);
                    layerDepth = Math.Max(0f, ((drawTile.Y * 64f) + 32f + throwVisualOffsetY) / 10000f);
                }

                spriteBatch.Draw(
                    texture: spriteInfo.Texture,
                    position: screenCenter,
                    sourceRectangle: spriteInfo.SourceRect,
                    color: color,
                    rotation: 0f,
                    origin: spriteInfo.Origin,
                    scale: BombSpriteBaseScale * pulseScale,
                    effects: SpriteEffects.None,
                    layerDepth: layerDepth
                );
            }
        }
    }

    // ----------------------------
    // 壊れたタイルの赤フラッシュ描画
    // ----------------------------
    private void DrawBreakFlashes(SpriteBatch spriteBatch, GameLocation currentLocation)
    {
        foreach (var flash in breakFlashes)
        {
            if (flash.LocationName != currentLocation.Name)
                continue;

            float alpha = flash.TicksLeft / (float)BreakFlashTicks;
            Vector2 worldPixel = flash.Tile * 64f;
            Vector2 screenPixel = Game1.GlobalToLocal(Game1.viewport, worldPixel);

            spriteBatch.Draw(
                texture: Game1.staminaRect,
                destinationRectangle: new Rectangle((int)screenPixel.X, (int)screenPixel.Y, 64, 64),
                sourceRectangle: null,
                color: Color.Red * (0.45f * alpha)
            );
        }
    }

    // ----------------------------
    // 爆弾の特性値から描画タイプを決める
    // - 空なら通常
    // - remote を含めばリモコン
    // - pierce を含めば貫通
    // - それ以外は通常
    // ----------------------------
    private BombDrawType ResolveBombDrawType(ActiveBomb bomb)
    {
        string traitValue = GetBombTraitValue(bomb);

        if (string.IsNullOrWhiteSpace(traitValue))
            return BombDrawType.Normal;

        if (traitValue.Contains("remote", StringComparison.OrdinalIgnoreCase))
            return BombDrawType.Remote;

        if (traitValue.Contains("pierce", StringComparison.OrdinalIgnoreCase))
            return BombDrawType.Pierce;

        return BombDrawType.Normal;
    }

    // ----------------------------
    // 使用するスプライトを返す
    // ----------------------------
    private BombSpriteDrawInfo ResolveBombSprite(ActiveBomb bomb)
    {
        BombDrawType drawType = ResolveBombDrawType(bomb);

        Texture2D texture = drawType switch
        {
            BombDrawType.Pierce => pierceBombSprite,
            BombDrawType.Remote => remoteBombSprite,
            _ => normalBombSprite
        };

        return new BombSpriteDrawInfo(
            Texture: texture,
            SourceRect: null,
            Origin: new Vector2(texture.Width / 2f, texture.Height / 2f)
        );
    }
}