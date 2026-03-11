// ----------------------------
// 十字爆風の計算
// - 爆風が通ったタイル一覧を返す
// - 壊れたタイル一覧を返す
// - pierce のときだけ BreakAndStop を BreakAndPass に変換する
// - 爆弾に当たったら誘爆予約して、その方向の爆風を止める
// - ドロップ用に「何を壊したか」も返す
// - 中心タイルも破壊判定する
// ----------------------------
using System;
using System.Collections.Generic;
using BomberGear.Config;
using BomberGear.Drops;
using BomberGear.Explosion.Breakables;
using BomberGear.Items;
using Microsoft.Xna.Framework;
using StardewValley;

namespace BomberGear.Explosion;

// ----------------------------
// 爆発結果
// ----------------------------
internal sealed class ExplosionResult
{
    public IReadOnlyList<Vector2> AffectedTiles { get; }
    public IReadOnlyList<Vector2> BrokenTiles { get; }
    public IReadOnlyList<BrokenBlockInfo> BrokenBlocks { get; }

    public ExplosionResult(
        IReadOnlyList<Vector2> affectedTiles,
        IReadOnlyList<Vector2> brokenTiles,
        IReadOnlyList<BrokenBlockInfo> brokenBlocks)
    {
        AffectedTiles = affectedTiles;
        BrokenTiles = brokenTiles;
        BrokenBlocks = brokenBlocks;
    }
}

internal sealed class ExplosionService
{
    private readonly BreakableResolver resolver = new();
    private readonly WallCollisionChecker wallChecker = new();

    // ----------------------------
    // 十字爆発を実行し、爆風が通ったタイル一覧と
    // 壊れたタイル一覧を返す
    // - onBombHit が true を返したら、その方向の爆風は止まる
    // ----------------------------
    public ExplosionResult ExplodeCross(
        GameLocation location,
        Vector2 originTile,
        int power,
        Farmer who,
        ModConfig config,
        string bombTraitValue = BombTraitValues.None,
        Func<Vector2, bool>? onBombHit = null)
    {
        _ = who;
        _ = config;

        int range = Clamp(power, 1, 20);
        var affectedTiles = new List<Vector2> { originTile };
        var brokenTiles = new List<Vector2>();
        var brokenBlocks = new List<BrokenBlockInfo>();

        location.playSound("explosion");

        // ----------------------------
        // 中心タイルも破壊判定する
        // - 設置した雑草や草が残らないようにする
        // - 中心は爆風の開始点なので、ここで止めない
        // ----------------------------
        ResolveOriginTile(
            location: location,
            originTile: originTile,
            power: power,
            brokenTiles: brokenTiles,
            brokenBlocks: brokenBlocks,
            bombTraitValue: bombTraitValue
        );

        var directions = new[]
        {
            new Vector2( 1, 0),
            new Vector2(-1, 0),
            new Vector2( 0, 1),
            new Vector2( 0,-1)
        };

        foreach (var direction in directions)
        {
            TraceDirection(
                location: location,
                originTile: originTile,
                direction: direction,
                range: range,
                power: power,
                brokenTiles: brokenTiles,
                brokenBlocks: brokenBlocks,
                affectedTiles: affectedTiles,
                bombTraitValue: bombTraitValue,
                onBombHit: onBombHit
            );
        }

        return new ExplosionResult(affectedTiles, brokenTiles, brokenBlocks);
    }

    // ----------------------------
    // 中心タイルの破壊判定
    // - 置いた場所の雑草 / 草 / 壊せる物を処理する
    // - 爆風の起点なので、BreakAndStop でも方向爆風は止めない
    // ----------------------------
    private void ResolveOriginTile(
        GameLocation location,
        Vector2 originTile,
        int power,
        List<Vector2> brokenTiles,
        List<BrokenBlockInfo> brokenBlocks,
        string bombTraitValue)
    {
        var sourceKind = MineObjectDropSourceClassifier.Classify(location, originTile);

        var behavior = resolver.Resolve(location, originTile, power);
        behavior = ApplyBombTraitToBehavior(bombTraitValue, behavior);

        if (behavior == BlastBehavior.BreakAndPass || behavior == BlastBehavior.BreakAndStop)
        {
            brokenTiles.Add(originTile);
            AddBrokenBlockInfo(brokenBlocks, originTile, sourceKind);
        }
    }

    // ----------------------------
    // 1方向ぶんの爆風を伸ばす
    // - 爆弾に当たったらそのタイルまで届いて止まる
    // ----------------------------
    private void TraceDirection(
        GameLocation location,
        Vector2 originTile,
        Vector2 direction,
        int range,
        int power,
        List<Vector2> brokenTiles,
        List<BrokenBlockInfo> brokenBlocks,
        List<Vector2> affectedTiles,
        string bombTraitValue,
        Func<Vector2, bool>? onBombHit)
    {
        for (int i = 1; i <= range; i++)
        {
            var tile = originTile + direction * i;

            // ----------------------------
            // Resolve が破壊を実行する前に
            // 何系ブロックかだけ先に拾っておく
            // ----------------------------
            var sourceKind = MineObjectDropSourceClassifier.Classify(location, tile);

            var behavior = resolver.Resolve(location, tile, power);
            behavior = ApplyBombTraitToBehavior(bombTraitValue, behavior);

            if (behavior == BlastBehavior.Block)
                break;

            if (behavior == BlastBehavior.BreakAndPass)
            {
                brokenTiles.Add(tile);
                affectedTiles.Add(tile);
                AddBrokenBlockInfo(brokenBlocks, tile, sourceKind);

                if (onBombHit?.Invoke(tile) == true)
                    break;

                continue;
            }

            if (behavior == BlastBehavior.BreakAndStop)
            {
                brokenTiles.Add(tile);
                affectedTiles.Add(tile);
                AddBrokenBlockInfo(brokenBlocks, tile, sourceKind);
                break;
            }

            // ----------------------------
            // Pass のときだけ実壁判定
            // ----------------------------
            if (wallChecker.IsHardWall(location, tile))
                break;

            affectedTiles.Add(tile);

            if (onBombHit?.Invoke(tile) == true)
                break;
        }
    }

    // ----------------------------
    // ドロップ対象の壊れブロックだけ記録
    // ----------------------------
    private static void AddBrokenBlockInfo(
        List<BrokenBlockInfo> brokenBlocks,
        Vector2 tile,
        BreakableDropSourceKind sourceKind)
    {
        if (sourceKind == BreakableDropSourceKind.None)
            return;

        brokenBlocks.Add(new BrokenBlockInfo(tile, sourceKind));
    }

    // ----------------------------
    // 爆弾特性に応じて爆風挙動を補正
    // - pierce は「壊して止まる」を「壊して通る」に変える
    // - 壁(Block)はそのまま止める
    // ----------------------------
    private static BlastBehavior ApplyBombTraitToBehavior(string bombTraitValue, BlastBehavior behavior)
    {
        if (bombTraitValue == BombTraitValues.Pierce && behavior == BlastBehavior.BreakAndStop)
            return BlastBehavior.BreakAndPass;

        return behavior;
    }

    // ----------------------------
    // 値を範囲内へ収める
    // ----------------------------
    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
            return min;

        if (value > max)
            return max;

        return value;
    }
}