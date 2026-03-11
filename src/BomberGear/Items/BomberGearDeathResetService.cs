// ----------------------------
// 戦闘不能時のボンバーギア初期化
// - 所持中のボンバーギアを新品へ差し替える
// - 結果としてソケット状態を初期化する
// ----------------------------
using StardewModdingAPI;
using StardewValley;

namespace BomberGear.Items;

internal sealed class BomberGearDeathResetService
{
    // ----------------------------
    // ボンバーギアの qualified item id
    // ----------------------------
    private const string BomberGearQualifiedItemId = "(W)kaniclub.BomberGear_BomberGear";

    private readonly IMonitor monitor;

    public BomberGearDeathResetService(IMonitor monitor)
    {
        this.monitor = monitor;
    }

    // ----------------------------
    // プレイヤー所持中のボンバーギアを
    // 新品へ差し替えてソケット初期化
    // ----------------------------
    public bool ResetSockets(Farmer player)
    {
        bool replacedAny = false;

        for (int i = 0; i < player.Items.Count; i++)
        {
            Item? item = player.Items[i];

            if (item is null || item.ItemId != ItemIds.BomberGear)
                continue;

            Item? freshGear = ItemRegistry.Create(BomberGearQualifiedItemId);
            if (freshGear is null)
            {
                monitor.Log("BomberGear: ボンバーギアの再生成に失敗したため、死亡時初期化をスキップしました。", LogLevel.Warn);
                continue;
            }

            player.Items[i] = freshGear;
            replacedAny = true;
        }

        return replacedAny;
    }
}