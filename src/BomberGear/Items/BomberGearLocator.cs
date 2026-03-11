// ----------------------------
// ボンバーギア探索
// - 選択中を優先
// - 無ければ所持品から探す
// ----------------------------
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace BomberGear.Items;

internal sealed class BomberGearLocator
{
    // ----------------------------
    // ボンバーギアを取得
    // ----------------------------
    public bool TryGet(out Item gear)
    {
        gear = null!;

        if (!Context.IsWorldReady)
            return false;

        Farmer who = Game1.player;

        if (who.CurrentItem is Item currentItem && currentItem.ItemId == ItemIds.BomberGear)
        {
            gear = currentItem;
            return true;
        }

        Item? inventoryGear = who.Items.FirstOrDefault(item => item is not null && item.ItemId == ItemIds.BomberGear);
        if (inventoryGear is not null)
        {
            gear = inventoryGear;
            return true;
        }

        return false;
    }
}