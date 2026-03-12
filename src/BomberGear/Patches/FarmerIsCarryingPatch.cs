// ----------------------------
// ボンバーギアで爆弾保持中でかつ、
// ボンバーギアを選択中のときだけ carrying 扱いにする Patch
// - バニラの carry 歩行アニメをそのまま使う
// - ただし、バニラが carrying を禁止する状況は尊重する
// ----------------------------
using BomberGear.Items;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace BomberGear.Patches;

[HarmonyPatch(typeof(Farmer), nameof(Farmer.IsCarrying))]
internal static class FarmerIsCarryingPatch
{
    private static void Postfix(Farmer __instance, ref bool __result)
    {
        if (__result)
            return;

        if (!Context.IsWorldReady)
            return;

        if (__instance.mount is not null)
            return;

        if (__instance.isAnimatingMount)
            return;

        if (__instance.IsSitting())
            return;

        if (__instance.onBridge.Value)
            return;

        if (Game1.eventUp)
            return;

        if (Game1.killScreen)
            return;

        Item? currentItem = __instance.CurrentItem;
        if (currentItem is null || currentItem.ItemId != ItemIds.BomberGear)
            return;

        if (ModEntry.Instance.Bombs.HasHeldBomb(__instance.UniqueMultiplayerID))
            __result = true;
    }
}