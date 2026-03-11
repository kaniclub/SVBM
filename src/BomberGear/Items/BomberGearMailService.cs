// ----------------------------
// ボンバーギア配布メール管理
// - Data/mail へメール本文を追加
// - ボンバーギアの所有者タグを管理
// - ギアが無いときだけメールを郵便受けへ入れる
// - ギアがあるときは未読メールを取り除く
// ----------------------------
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace BomberGear.Items;

internal sealed class BomberGearMailService
{
    // ----------------------------
    // メールID
    // ----------------------------
    private const string BomberGearMailId = "kaniclub.BomberGear.BomberGearMail";

    // ----------------------------
    // ボンバーギアの qualified item id
    // ----------------------------
    private const string BomberGearQualifiedItemId = "(W)kaniclub.BomberGear_BomberGear";

    // ----------------------------
    // ボンバーギア所有者の modData キー
    // ----------------------------
    private const string BomberGearOwnerIdKey = "kaniclub.BomberGear/BomberGearOwnerId";

    private readonly IMonitor monitor;
    private readonly ITranslationHelper translation;

    public BomberGearMailService(IMonitor monitor, ITranslationHelper translation)
    {
        this.monitor = monitor;
        this.translation = translation;
    }

    // ----------------------------
    // Data/mail にボンバーギアメールを追加
    // ----------------------------
    public void OnAssetRequested(AssetRequestedEventArgs e)
    {
        if (!e.NameWithoutLocale.IsEquivalentTo("Data/mail"))
            return;

        e.Edit(asset =>
        {
            var data = asset.AsDictionary<string, string>().Data;

            string title = translation.Get("mail.bomber_gear.title").ToString();
            string body = translation.Get("mail.bomber_gear.body").ToString();

            data[BomberGearMailId] =
                "@player_name@^" +
                body + " " +
                $"%item id {BomberGearQualifiedItemId} %%" +
                "[#]" + title;
        });
    }

    // ----------------------------
    // ローカルプレイヤーのギア所有情報を更新
    // - 手持ち / カーソル所持中の未タグギアへ所有者を付与
    // ----------------------------
    public void RefreshOwnershipForLocalPlayer()
    {
        if (!Context.IsWorldReady)
            return;

        Farmer player = Game1.player;
        StampBomberGearOwnershipInInventory(player);
    }

    // ----------------------------
    // ローカルプレイヤーの郵便受け状態を同期
    // - 自分のギアが存在するなら未読メールを消す
    // - 自分のギアが存在しないならメールを入れる
    // ----------------------------
    public void SyncMailboxForLocalPlayer()
    {
        if (!Context.IsWorldReady)
            return;

        RefreshOwnershipForLocalPlayer();

        Farmer player = Game1.player;
        bool hasOwnedGear = HasOwnedBomberGearAnywhere(player.UniqueMultiplayerID);

        if (hasOwnedGear)
        {
            if (player.mailbox.Contains(BomberGearMailId))
                player.mailbox.Remove(BomberGearMailId);

            return;
        }

        if (player.mailbox.Contains(BomberGearMailId))
            return;

        player.mailReceived.Remove(BomberGearMailId);
        player.mailForTomorrow.Remove(BomberGearMailId);
        player.mailbox.Add(BomberGearMailId);
    }

    // ----------------------------
    // 手持ち / カーソル所持中の未タグギアへ所有者を付与
    // ----------------------------
    private void StampBomberGearOwnershipInInventory(Farmer player)
    {
        foreach (Item? item in player.Items)
        {
            if (item is null)
                continue;

            StampOwnershipIfNeeded(item, player.UniqueMultiplayerID);
        }

        if (player.CursorSlotItem is not null)
            StampOwnershipIfNeeded(player.CursorSlotItem, player.UniqueMultiplayerID);
    }

    // ----------------------------
    // 必要なら所有者タグを付与
    // - 既に他人の所有タグがあるものは上書きしない
    // ----------------------------
    private void StampOwnershipIfNeeded(Item item, long ownerId)
    {
        if (!IsBomberGear(item))
            return;

        if (TryGetOwnerId(item, out _))
            return;

        item.modData[BomberGearOwnerIdKey] = ownerId.ToString();
    }

    // ----------------------------
    // 指定プレイヤー所有のボンバーギアが世界のどこかにあるか
    // - プレイヤー手持ち
    // - チェスト内
    // - その他ネストされた所持先
    // を含めて全体検索する
    // ----------------------------
    private bool HasOwnedBomberGearAnywhere(long ownerId)
    {
        bool found = false;

        Utility.ForEachItem(item =>
        {
            if (found || item is null)
                return false;

            if (!IsBomberGear(item))
                return true;

            if (!TryGetOwnerId(item, out long itemOwnerId))
                return true;

            if (itemOwnerId != ownerId)
                return true;

            found = true;
            return false;
        });

        return found;
    }

    // ----------------------------
    // ボンバーギアか判定
    // ----------------------------
    private static bool IsBomberGear(Item? item)
    {
        if (item is null)
            return false;

        return item.ItemId == ItemIds.BomberGear
            || item.QualifiedItemId == BomberGearQualifiedItemId;
    }

    // ----------------------------
    // 所有者IDを取得
    // ----------------------------
    private static bool TryGetOwnerId(Item item, out long ownerId)
    {
        ownerId = 0;

        if (!item.modData.TryGetValue(BomberGearOwnerIdKey, out string? raw))
            return false;

        return long.TryParse(raw, out ownerId);
    }
}