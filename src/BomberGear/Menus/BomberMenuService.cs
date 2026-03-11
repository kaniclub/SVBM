// ----------------------------
// ボンバーギア用メニュー管理
// - ソケットメニューを開く
// ----------------------------
using System;
using BomberGear.Config;
using BomberGear.Explosion.Breakables;
using BomberGear.Items;
using StardewModdingAPI;
using StardewValley;

namespace BomberGear.Menus;

internal sealed class BomberMenuService
{
    private readonly BomberGearLocator gearLocator;
    private readonly SocketEquipmentService socketEquipment;
    private readonly SocketService sockets;
    private readonly ITranslationHelper i18n;
    private readonly Func<ModConfig> getConfig;
    private readonly WorldToolUpgradeService worldToolUpgradeService;

    public BomberMenuService(
        BomberGearLocator gearLocator,
        SocketEquipmentService socketEquipment,
        SocketService sockets,
        ITranslationHelper i18n,
        Func<ModConfig> getConfig)
    {
        this.gearLocator = gearLocator;
        this.socketEquipment = socketEquipment;
        this.sockets = sockets;
        this.i18n = i18n;
        this.getConfig = getConfig;
        worldToolUpgradeService = new WorldToolUpgradeService();
    }

    // ----------------------------
    // ソケットメニューを開く
    // ----------------------------
    public void OpenSocketMenu()
    {
        if (!Context.IsWorldReady)
            return;

        if (!gearLocator.TryGet(out Item gear))
        {
            Game1.showRedMessage(i18n.Get("menu.open.no-gear").ToString());
            return;
        }

        Game1.activeClickableMenu = new BomberSocketMenu(
            gear,
            socketEquipment,
            sockets,
            i18n,
            getConfig(),
            worldToolUpgradeService
        );
    }
}