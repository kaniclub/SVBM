// ----------------------------
// プレイヤー被ダメージ監視
// - HP が正数から 0 以下へ落ちた瞬間だけ拾う
// - ローカルプレイヤーの死亡時処理へ渡す
// ----------------------------
using HarmonyLib;
using StardewValley;

namespace BomberGear.Patches;

[HarmonyPatch(typeof(Farmer), nameof(Farmer.takeDamage))]
internal static class FarmerTakeDamagePatch
{
    // ----------------------------
    // ダメージ前HPを保持
    // ----------------------------
    private static void Prefix(Farmer __instance, out int __state)
    {
        __state = __instance.health;
    }

    // ----------------------------
    // HP が 0 以下へ落ちた瞬間を死亡として扱う
    // ----------------------------
    private static void Postfix(Farmer __instance, int __state)
    {
        if (__state <= 0)
            return;

        if (__instance.health > 0)
            return;

        ModEntry.Instance.HandleLocalPlayerDeath(__instance);
    }
}