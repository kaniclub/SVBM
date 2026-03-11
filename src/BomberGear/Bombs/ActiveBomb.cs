// ----------------------------
// 設置中爆弾の状態
// - キックで滑る状態
// - パワーグローブ用の保持 / 飛行状態
// - 描画補間状態
// を持つ
// ----------------------------
using Microsoft.Xna.Framework;
using StardewValley;

namespace BomberGear.Bombs;

internal sealed class ActiveBomb
{
    private const float ThrowArcHeightPixels = 28f;
    private const float ThrowLaunchOffsetYPixels = 92f;

    public long OwnerId { get; }
    public string LocationName { get; private set; }
    public Vector2 Tile { get; private set; }
    public int Power { get; }
    public int TicksLeft { get; set; }

    // ----------------------------
    // 誘爆待ち状態かどうか
    // 同じ爆弾に何度も誘爆予約を入れないために使う
    // ----------------------------
    public bool IsChainTriggered { get; set; }

    // ----------------------------
    // 設置後の経過tick
    // 描画アニメーションに使う
    // ----------------------------
    public int AgeTicks { get; set; }

    // ----------------------------
    // 設置者だけが、まだ爆弾から抜け切る前に
    // 一時的に通過できる状態かどうか
    // ----------------------------
    public bool OwnerCanPassThrough { get; set; }

    // ----------------------------
    // キックで滑走中か
    // ----------------------------
    public bool IsSliding { get; private set; }

    // ----------------------------
    // 滑走方向
    // ----------------------------
    public Vector2 SlideDirection { get; private set; }

    // ----------------------------
    // 次の1マス移動までの残りtick
    // ----------------------------
    public int SlideStepTicksLeft { get; set; }

    // ----------------------------
    // 滑走の1マス移動間隔
    // ----------------------------
    public int SlideStepIntervalTicks { get; private set; }

    // ----------------------------
    // プレイヤーに持ち上げられているか
    // ----------------------------
    public bool IsHeld { get; private set; }

    // ----------------------------
    // 持ち上げているプレイヤーID
    // ----------------------------
    public long HeldByPlayerId { get; private set; }

    // ----------------------------
    // 投げられて飛行中か
    // ----------------------------
    public bool IsThrown { get; private set; }

    // ----------------------------
    // 投げた方向
    // ----------------------------
    public Vector2 ThrowDirection { get; private set; }

    // ----------------------------
    // 次の移動までの残りtick
    // ----------------------------
    public int ThrowStepTicksLeft { get; set; }

    // ----------------------------
    // 投げ移動の基準1マス移動間隔
    // ----------------------------
    public int ThrowStepIntervalTicks { get; private set; }

    // ----------------------------
    // 投げ開始直後に、最初の2マス跳躍をまだ使っていないか
    // ----------------------------
    public bool HasPendingInitialThrowLeap { get; private set; }

    // ----------------------------
    // 補間描画用の移動元タイル
    // ----------------------------
    public Vector2 VisualFromTile { get; private set; }

    // ----------------------------
    // 補間描画用の残りtick
    // ----------------------------
    public int VisualMoveTicksLeft { get; private set; }

    // ----------------------------
    // 補間描画用の総tick
    // ----------------------------
    public int VisualMoveTicksTotal { get; private set; }

    // ----------------------------
    // 直近の移動を投げ演出として描くか
    // ----------------------------
    private bool UseThrowArcVisual { get; set; }

    // ----------------------------
    // 直近の移動が投げ開始直後か
    // - 頭上から飛び出す補正に使う
    // ----------------------------
    private bool UseThrowLaunchVisual { get; set; }

    // ----------------------------
    // 導火線音の Cue
    // 爆発時に止めるため保持する
    // ----------------------------
    public ICue? FuseCue { get; set; }

    public ActiveBomb(long ownerId, string locationName, Vector2 tile, int power, int ticksLeft)
    {
        OwnerId = ownerId;
        LocationName = locationName;
        Tile = tile;
        Power = power;
        TicksLeft = ticksLeft;
        IsChainTriggered = false;
        AgeTicks = 0;
        OwnerCanPassThrough = true;

        IsSliding = false;
        SlideDirection = Vector2.Zero;
        SlideStepTicksLeft = 0;
        SlideStepIntervalTicks = 0;

        IsHeld = false;
        HeldByPlayerId = 0;

        IsThrown = false;
        ThrowDirection = Vector2.Zero;
        ThrowStepTicksLeft = 0;
        ThrowStepIntervalTicks = 0;
        HasPendingInitialThrowLeap = false;

        VisualFromTile = tile;
        VisualMoveTicksLeft = 0;
        VisualMoveTicksTotal = 0;
        UseThrowArcVisual = false;
        UseThrowLaunchVisual = false;

        FuseCue = null;
    }

    // ----------------------------
    // 爆弾位置を更新
    // ----------------------------
    public void MoveTo(Vector2 tile, int animationTicks = 0)
    {
        Vector2 fromTile = Tile;
        Tile = tile;

        if (animationTicks > 0)
        {
            VisualFromTile = fromTile;
            VisualMoveTicksTotal = animationTicks;
            VisualMoveTicksLeft = animationTicks;
            return;
        }

        VisualFromTile = tile;
        VisualMoveTicksTotal = 0;
        VisualMoveTicksLeft = 0;
    }

    // ----------------------------
    // ロケーション名を更新
    // - 持ち上げ中にプレイヤーが別マップへ移動しても追従できるようにする
    // ----------------------------
    public void SetLocationName(string locationName)
    {
        LocationName = locationName;
    }

    // ----------------------------
    // 滑走開始
    // ----------------------------
    public void StartSliding(Vector2 direction, int stepTicks)
    {
        int intervalTicks = stepTicks < 1 ? 1 : stepTicks;

        IsSliding = true;
        SlideDirection = direction;
        SlideStepIntervalTicks = intervalTicks;
        SlideStepTicksLeft = intervalTicks;
    }

    // ----------------------------
    // 滑走停止
    // ----------------------------
    public void StopSliding()
    {
        IsSliding = false;
        SlideDirection = Vector2.Zero;
        SlideStepTicksLeft = 0;
        SlideStepIntervalTicks = 0;
    }

    // ----------------------------
    // 持ち上げ開始
    // - タイマー停止は BombManager 側で扱う
    // ----------------------------
    public void PickUp(long holderPlayerId)
    {
        StopSliding();

        IsHeld = true;
        HeldByPlayerId = holderPlayerId;

        IsThrown = false;
        ThrowDirection = Vector2.Zero;
        ThrowStepTicksLeft = 0;
        ThrowStepIntervalTicks = 0;
        HasPendingInitialThrowLeap = false;
    }

    // ----------------------------
    // 投げ開始
    // - playerTile を投げ始点として使う
    // ----------------------------
    public void StartThrow(Vector2 playerTile, Vector2 direction, int stepTicks)
    {
        MoveTo(playerTile);

        IsHeld = false;
        HeldByPlayerId = 0;

        IsThrown = true;
        ThrowDirection = direction;
        ThrowStepIntervalTicks = stepTicks < 1 ? 1 : stepTicks;
        ThrowStepTicksLeft = 1;
        HasPendingInitialThrowLeap = true;
    }

    // ----------------------------
    // 最初の2マス跳躍を使うべきか
    // ----------------------------
    public bool ShouldUseInitialThrowLeap()
    {
        return HasPendingInitialThrowLeap;
    }

    // ----------------------------
    // 最初の2マス跳躍を消費
    // ----------------------------
    public void MarkInitialThrowLeapUsed()
    {
        HasPendingInitialThrowLeap = false;
    }

    // ----------------------------
    // 投げ中の1歩進行
    // ----------------------------
    public void AdvanceThrownStep(Vector2 tile, int distanceTiles, bool isLaunchStep)
    {
        int animationTicks = ThrowStepIntervalTicks * Math.Max(1, distanceTiles);
        MoveTo(tile, animationTicks);

        UseThrowArcVisual = true;
        UseThrowLaunchVisual = isLaunchStep;
    }

    // ----------------------------
    // 投げから着地
    // ----------------------------
    public void FinishThrow()
    {
        IsThrown = false;
        ThrowDirection = Vector2.Zero;
        ThrowStepTicksLeft = 0;
        ThrowStepIntervalTicks = 0;
        HasPendingInitialThrowLeap = false;
    }

    // ----------------------------
    // 保持・投げ状態を全解除
    // ----------------------------
    public void ClearCarryState()
    {
        IsHeld = false;
        HeldByPlayerId = 0;
        FinishThrow();

        UseThrowArcVisual = false;
        UseThrowLaunchVisual = false;
        VisualMoveTicksLeft = 0;
        VisualMoveTicksTotal = 0;
    }

    // ----------------------------
    // タイマー進行してよいか
    // - 持ち上げ中 / 投げ中は停止
    // ----------------------------
    public bool CanTickTimer()
    {
        return !IsHeld && !IsThrown;
    }

    // ----------------------------
    // 補間描画の残りtickを更新
    // ----------------------------
    public void UpdateVisualMotion()
    {
        if (VisualMoveTicksLeft > 0)
        {
            VisualMoveTicksLeft--;

            if (VisualMoveTicksLeft <= 0)
            {
                UseThrowArcVisual = false;
                UseThrowLaunchVisual = false;
            }
        }
    }

    // ----------------------------
    // 描画用タイル座標を取得
    // - 補間中なら移動元と現在地の間を返す
    // ----------------------------
    public Vector2 GetDrawTile()
    {
        if (VisualMoveTicksLeft <= 0 || VisualMoveTicksTotal <= 0)
            return Tile;

        float progress = 1f - (VisualMoveTicksLeft / (float)VisualMoveTicksTotal);
        return Vector2.Lerp(VisualFromTile, Tile, progress);
    }

    // ----------------------------
    // 投げ用の描画オフセット
    // - 通常の跳ねる弧
    // - 投げ開始直後は、頭上から飛び出す補正
    // ----------------------------
    public float GetThrownVisualOffsetY()
    {
        if (!UseThrowArcVisual)
            return 0f;

        if (VisualMoveTicksLeft <= 0 || VisualMoveTicksTotal <= 0)
            return 0f;

        float progress = 1f - (VisualMoveTicksLeft / (float)VisualMoveTicksTotal);

        float arcHeight = UseThrowLaunchVisual
            ? ThrowArcHeightPixels + 8f
            : ThrowArcHeightPixels;

        float arcOffset = -MathF.Sin(progress * MathF.PI) * arcHeight;

        float launchOffset = 0f;
        if (UseThrowLaunchVisual)
            launchOffset = -(1f - progress) * ThrowLaunchOffsetYPixels;

        return arcOffset + launchOffset;
    }
}