// ----------------------------
// 爆弾を通行不可として扱う Patch
// - isCollidingPosition の 6引数版 / 9引数版の両方へ適用
// - 設置者本人だけは、まだ爆弾から抜け切る前なら通過可
// - NPC / 敵 / その他キャラには常に衝突判定あり
// - キック可能なプレイヤーは、移動方向にある爆弾を蹴り飛ばせる
// ----------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using XTileRectangle = xTile.Dimensions.Rectangle;

namespace BomberGear.Patches;

[HarmonyPatch]
internal static class GameLocationCollisionPatch
{
    // ----------------------------
    // 対象メソッド群を取得
    // ----------------------------
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return AccessTools
            .GetDeclaredMethods(typeof(GameLocation))
            .Where(method => method.Name == nameof(GameLocation.isCollidingPosition))
            .Where(method =>
            {
                var parameters = method.GetParameters();

                if (parameters.Length != 6 && parameters.Length != 9)
                    return false;

                return parameters[0].ParameterType == typeof(Rectangle)
                    && parameters[1].ParameterType == typeof(XTileRectangle)
                    && parameters[2].ParameterType == typeof(bool)
                    && parameters[3].ParameterType == typeof(int)
                    && parameters[4].ParameterType == typeof(bool)
                    && parameters[5].ParameterType == typeof(Character);
            });
    }

    // ----------------------------
    // 通常の衝突判定のあとで
    // 爆弾との衝突を追加判定する
    // - キック可能なら先にキックを試す
    // ----------------------------
    private static void Postfix(
        GameLocation __instance,
        Rectangle position,
        Character? character,
        ref bool __result
    )
    {
        if (__result)
            return;

        if (!Context.IsWorldReady)
            return;

        if (character is null)
            return;

        if (character is Farmer farmer && ModEntry.Instance.CanKickBomb(farmer))
        {
            Vector2 moveDirection = GetMoveDirection(character, position);

            if (moveDirection != Vector2.Zero
                && ModEntry.Instance.Bombs.TryKickBlockingBomb(
                    farmer,
                    __instance,
                    position,
                    moveDirection,
                    ModEntry.Instance.GetKickSlideStepTicks()
                ))
            {
                return;
            }
        }

        if (ModEntry.Instance.Bombs.IntersectsBlockingBomb(character, __instance, position))
            __result = true;
    }

    // ----------------------------
    // 現在位置と次位置から移動方向を求める
    // ----------------------------
    private static Vector2 GetMoveDirection(Character character, Rectangle nextPosition)
    {
        Rectangle currentPosition = character.GetBoundingBox();

        int dx = nextPosition.X - currentPosition.X;
        int dy = nextPosition.Y - currentPosition.Y;

        if (Math.Abs(dx) > Math.Abs(dy))
            return dx > 0 ? new Vector2(1, 0) : new Vector2(-1, 0);

        if (Math.Abs(dy) > 0)
            return dy > 0 ? new Vector2(0, 1) : new Vector2(0, -1);

        return Vector2.Zero;
    }
}