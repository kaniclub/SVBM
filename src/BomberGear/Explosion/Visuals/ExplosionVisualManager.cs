using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace BomberGear.Explosion.Visuals;

internal sealed class ExplosionVisualManager
{
    // ----------------------------
    // 1秒表示
    // ----------------------------
    private const int VisualDurationTicks = 60;

    // ----------------------------
    // 32x32画像を1タイル(64px)で描く倍率
    // ----------------------------
    private const float TextureBaseScale = 2f;

    private readonly List<ExplosionVisual> visuals = new();

    private readonly Texture2D centerTexture;
    private readonly Texture2D middleTexture;
    private readonly Texture2D endTexture;

    public ExplosionVisualManager(IModHelper helper)
    {
        centerTexture = helper.ModContent.Load<Texture2D>("assets/explosion_center.png");
        middleTexture = helper.ModContent.Load<Texture2D>("assets/explosion_middle.png");
        endTexture = helper.ModContent.Load<Texture2D>("assets/explosion_end.png");
    }

    // ----------------------------
    // 爆発ビジュアル一覧をクリア
    // ----------------------------
    public void Clear()
    {
        visuals.Clear();
    }

    // ----------------------------
    // 毎tick更新
    // ----------------------------
    public void Update()
    {
        for (int i = visuals.Count - 1; i >= 0; i--)
        {
            visuals[i].TicksLeft--;

            if (visuals[i].TicksLeft <= 0)
                visuals.RemoveAt(i);
        }
    }

    // ----------------------------
    // 十字爆発のビジュアルを生成
    // - Center は中心
    // - Middle は途中
    // - End は「最大距離まで届いた時だけ」
    // - 途中で物に当たって止まった時は Middle
    // ----------------------------
    public void SpawnCross(string locationName, Vector2 originTile, int power, IReadOnlyList<Vector2> affectedTiles)
    {
        var affectedSet = new HashSet<Vector2>(affectedTiles);

        // ----------------------------
        // 中心
        // ----------------------------
        visuals.Add(new ExplosionVisual(
            locationName: locationName,
            tile: originTile,
            kind: ExplosionVisualKind.Center,
            rotation: 0f,
            effects: SpriteEffects.None,
            totalTicks: VisualDurationTicks
        ));

        var directions = new[]
        {
            new Vector2( 1, 0),
            new Vector2(-1, 0),
            new Vector2( 0, 1),
            new Vector2( 0,-1)
        };

        foreach (var dir in directions)
        {
            var drawParams = GetDirectionalDrawParams(dir);

            for (int distance = 1; distance <= power; distance++)
            {
                var tile = originTile + dir * distance;

                if (!affectedSet.Contains(tile))
                    break;

                bool nextExists = affectedSet.Contains(originTile + dir * (distance + 1));
                bool reachedMax = distance == power;

                var kind = reachedMax
                    ? ExplosionVisualKind.End
                    : ExplosionVisualKind.Middle;

                visuals.Add(new ExplosionVisual(
                    locationName: locationName,
                    tile: tile,
                    kind: kind,
                    rotation: drawParams.Rotation,
                    effects: drawParams.Effects,
                    totalTicks: VisualDurationTicks
                ));

                // ----------------------------
                // この方向がここで終わりなら終了
                // distance < power で終わった場合は Middle のまま
                // ----------------------------
                if (!nextExists)
                    break;
            }
        }
    }

    // ----------------------------
    // 爆発ビジュアル描画
    // - alpha は 100% → 0%
    // - Center は縮小しない
    // - 途中 / 先端は厚みだけ 100% → 60%
    // - 縦は90度回転した画像に対して、横と同じ縮み方を使う
    // ----------------------------
    public void Draw(SpriteBatch spriteBatch, GameLocation currentLocation)
    {
        foreach (var visual in visuals)
        {
            if (visual.LocationName != currentLocation.Name)
                continue;

            float progress = 1f - (visual.TicksLeft / (float)visual.TotalTicks);
            float alpha = 1f - progress;
            float thicknessScale = 1f - (0.4f * progress);

            Texture2D texture;
            Vector2 origin;
            Vector2 scale;

            if (visual.Kind == ExplosionVisualKind.Center)
            {
                texture = centerTexture;
                origin = new Vector2(texture.Width / 2f, texture.Height / 2f);

                // ----------------------------
                // 中心は縮小しない
                // ----------------------------
                scale = new Vector2(TextureBaseScale, TextureBaseScale);
            }
            else
            {
                texture = visual.Kind == ExplosionVisualKind.Middle
                    ? middleTexture
                    : endTexture;

                origin = new Vector2(texture.Width / 2f, texture.Height / 2f);

                // ----------------------------
                // 横でも縦でも同じ縮み方を使う
                // - 横のときは上下が縮む
                // - 縦のときは画像を90度回転しているので左右が縮む
                // ----------------------------
                scale = new Vector2(TextureBaseScale, TextureBaseScale * thicknessScale);
            }

            Vector2 worldCenter = visual.Tile * 64f + new Vector2(32f, 32f);
            Vector2 screenCenter = Game1.GlobalToLocal(Game1.viewport, worldCenter);

            spriteBatch.Draw(
                texture: texture,
                position: screenCenter,
                sourceRectangle: null,
                color: Color.White * alpha,
                rotation: visual.Rotation,
                origin: origin,
                scale: scale,
                effects: visual.Effects,
                layerDepth: Math.Max(0f, (visual.Tile.Y * 64f + 36f) / 10000f)
            );
        }
    }

    // ----------------------------
    // 方向ごとの描画情報を返す
    // explosion_end.png は「右向き先端」を基準にする
    // - 右 : そのまま
    // - 左 : 左右反転
    // - 下 : 90度回転
    // - 上 : -90度回転 + 上下反転
    // ----------------------------
    private static DirectionalDrawParams GetDirectionalDrawParams(Vector2 dir)
    {
        if (dir == new Vector2(1, 0))
        {
            return new DirectionalDrawParams(
                rotation: 0f,
                effects: SpriteEffects.None
            );
        }

        if (dir == new Vector2(-1, 0))
        {
            return new DirectionalDrawParams(
                rotation: 0f,
                effects: SpriteEffects.FlipHorizontally
            );
        }

        if (dir == new Vector2(0, 1))
        {
            return new DirectionalDrawParams(
                rotation: MathF.PI / 2f,
                effects: SpriteEffects.None
            );
        }

        return new DirectionalDrawParams(
            rotation: -MathF.PI / 2f,
            effects: SpriteEffects.FlipVertically
        );
    }

    // ----------------------------
    // 方向別の描画設定
    // ----------------------------
    private readonly struct DirectionalDrawParams
    {
        public float Rotation { get; }
        public SpriteEffects Effects { get; }

        public DirectionalDrawParams(float rotation, SpriteEffects effects)
        {
            Rotation = rotation;
            Effects = effects;
        }
    }
}