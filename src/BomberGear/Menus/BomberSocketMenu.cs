// ----------------------------
// ボンバーギア用ソケットメニュー
// - 現在のギア状態を表示する
// - 左クリックで外す
// - 装着は行わない
// - 右側に現在の有効状態をアイコン付きで表示する
// - ツルハシ / オノの有効段階も表示する
// ----------------------------
using System.Collections.Generic;
using System.Linq;
using BomberGear.Config;
using BomberGear.Explosion.Breakables;
using BomberGear.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace BomberGear.Menus;

internal sealed class BomberSocketMenu : IClickableMenu
{
    // ----------------------------
    // 値表示の色  
    // ----------------------------
    private static readonly Color ValueTextColor = new(92, 58, 24);

    // ----------------------------
    // スロット表示情報
    // ----------------------------
    private sealed class SlotView
    {
        public SocketSlotType SlotType { get; }
        public ClickableComponent Slot { get; }
        public ClickableComponent RemoveButton { get; }
        public string LabelI18nKey { get; }
        public string HoverI18nKey { get; }

        public SlotView(
            SocketSlotType slotType,
            ClickableComponent slot,
            ClickableComponent removeButton,
            string labelI18nKey,
            string hoverI18nKey
        )
        {
            SlotType = slotType;
            Slot = slot;
            RemoveButton = removeButton;
            LabelI18nKey = labelI18nKey;
            HoverI18nKey = hoverI18nKey;
        }
    }

    private readonly Item gear;
    private readonly SocketEquipmentService socketEquipment;
    private readonly SocketService sockets;
    private readonly ITranslationHelper i18n;
    private readonly ModConfig config;

    private readonly int bestAxeLevel;
    private readonly int bestPickaxeLevel;

    private readonly List<SlotView> slots;
    private readonly ClickableComponent closeButton;

    private string hoverText = string.Empty;

    public BomberSocketMenu(
        Item gear,
        SocketEquipmentService socketEquipment,
        SocketService sockets,
        ITranslationHelper i18n,
        ModConfig config,
        WorldToolUpgradeService worldToolUpgradeService
    )
        : base(
            (Game1.uiViewport.Width - 980) / 2,
            (Game1.uiViewport.Height - 680) / 2,
            980,
            680,
            true
        )
    {
        this.gear = gear;
        this.socketEquipment = socketEquipment;
        this.sockets = sockets;
        this.i18n = i18n;
        this.config = config;

        bestAxeLevel = worldToolUpgradeService.GetBestAxeLevel();
        bestPickaxeLevel = worldToolUpgradeService.GetBestPickaxeLevel();

        int slotX = xPositionOnScreen + 64;
        int slotY = yPositionOnScreen + 150;
        int slotWidth = 400;
        int slotHeight = 88;
        int slotGap = 22;

        var bombTraitSlot = new ClickableComponent(
            new Rectangle(slotX, slotY, slotWidth, slotHeight),
            "bomb_trait"
        );

        var actionTraitSlot = new ClickableComponent(
            new Rectangle(slotX, slotY + (slotHeight + slotGap), slotWidth, slotHeight),
            "action_trait"
        );

        var powerSlot = new ClickableComponent(
            new Rectangle(slotX, slotY + (slotHeight + slotGap) * 2, slotWidth, slotHeight),
            "power"
        );

        var bombCountSlot = new ClickableComponent(
            new Rectangle(slotX, slotY + (slotHeight + slotGap) * 3, slotWidth, slotHeight),
            "bomb_count"
        );

        slots = new List<SlotView>
        {
            new SlotView(
                SocketSlotType.BombTrait,
                bombTraitSlot,
                CreateRemoveButton(bombTraitSlot, "remove_bomb_trait"),
                "menu.socket.slot.bomb-trait",
                "menu.socket.hover.bomb-trait"
            ),
            new SlotView(
                SocketSlotType.ActionTrait,
                actionTraitSlot,
                CreateRemoveButton(actionTraitSlot, "remove_action_trait"),
                "menu.socket.slot.action-trait",
                "menu.socket.hover.action-trait"
            ),
            new SlotView(
                SocketSlotType.Power,
                powerSlot,
                CreateRemoveButton(powerSlot, "remove_power"),
                "menu.socket.slot.power",
                "menu.socket.hover.power"
            ),
            new SlotView(
                SocketSlotType.BombCount,
                bombCountSlot,
                CreateRemoveButton(bombCountSlot, "remove_bomb_count"),
                "menu.socket.slot.bomb-count",
                "menu.socket.hover.bomb-count"
            )
        };

        closeButton = new ClickableComponent(
            new Rectangle(xPositionOnScreen + width - 80, yPositionOnScreen + 24, 40, 40),
            "close"
        );
    }

    // ----------------------------
    // 左クリック
    // - 外すボタン
    // - ×ボタン
    // ----------------------------
    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (closeButton.containsPoint(x, y))
        {
            Game1.playSound("bigDeSelect");
            exitThisMenu();
            return;
        }

        SlotView? removeTarget = slots.FirstOrDefault(slot => slot.RemoveButton.containsPoint(x, y));
        if (removeTarget is not null)
        {
            TryRemove(removeTarget.SlotType);
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    // ----------------------------
    // 右クリック
    // - メニュー上では何もしない
    // ----------------------------
    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        base.receiveRightClick(x, y, playSound);
    }

    // ----------------------------
    // ホバー表示
    // ----------------------------
    public override void performHoverAction(int x, int y)
    {
        hoverText = string.Empty;

        SlotView? slotTarget = slots.FirstOrDefault(slot => slot.Slot.containsPoint(x, y));
        if (slotTarget is not null)
        {
            hoverText = i18n.Get(slotTarget.HoverI18nKey).ToString();
            base.performHoverAction(x, y);
            return;
        }

        SlotView? removeTarget = slots.FirstOrDefault(slot => slot.RemoveButton.containsPoint(x, y));
        if (removeTarget is not null)
        {
            hoverText = i18n.Get("menu.socket.hover.remove").ToString();
            base.performHoverAction(x, y);
            return;
        }

        if (closeButton.containsPoint(x, y))
            hoverText = i18n.Get("menu.common.close").ToString();

        base.performHoverAction(x, y);
    }

    // ----------------------------
    // ESCで閉じられる
    // ----------------------------
    public override bool readyToClose()
    {
        return true;
    }

    // ----------------------------
    // メニュー描画
    // ----------------------------
    public override void draw(SpriteBatch b)
    {
        b.Draw(
            Game1.fadeToBlackRect,
            new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
            Color.Black * 0.75f
        );

        Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);

        DrawHeader(b);
        DrawEffectiveStatusPanel(b);
        DrawToolStatusPanel(b);

        foreach (SlotView slot in slots)
        {
            DrawSlot(
                b,
                slot,
                i18n.Get(slot.LabelI18nKey).ToString(),
                GetSlotValueText(slot.SlotType),
                HasEquipped(slot.SlotType),
                GetSlotPreviewItem(slot.SlotType)
            );
        }

        DrawFooter(b);
        DrawCloseButton(b);

        if (!string.IsNullOrWhiteSpace(hoverText))
            drawHoverText(b, hoverText, Game1.smallFont);

        drawMouse(b);
    }

    // ----------------------------
    // スロットから取り外す
    // ----------------------------
    private void TryRemove(SocketSlotType slotType)
    {
        if (!HasEquipped(slotType))
        {
            ShowFailure(i18n.Get("menu.socket.error.no-equipped-socket").ToString());
            return;
        }

        bool removed = slotType switch
        {
            SocketSlotType.BombTrait => socketEquipment.TryRemoveBombTrait(Game1.player, gear),
            SocketSlotType.ActionTrait => socketEquipment.TryRemoveActionTrait(Game1.player, gear),
            SocketSlotType.Power => socketEquipment.TryRemovePowerOne(Game1.player, gear),
            SocketSlotType.BombCount => socketEquipment.TryRemoveBombCountOne(Game1.player, gear),
            _ => false
        };

        if (!removed)
        {
            ShowFailure(i18n.Get("menu.socket.error.remove-failed").ToString());
            return;
        }

        Game1.playSound("coin");
    }

    // ----------------------------
    // 指定スロットの表示文字
    // ----------------------------
    private string GetSlotValueText(SocketSlotType slotType)
    {
        return slotType switch
        {
            SocketSlotType.BombTrait => GetBombTraitDisplay(),
            SocketSlotType.ActionTrait => GetActionTraitDisplay(),
            SocketSlotType.Power => sockets.GetPowerCount(gear).ToString(),
            SocketSlotType.BombCount => sockets.GetBombCount(gear).ToString(),
            _ => i18n.Get("menu.socket.value.none").ToString()
        };
    }

    // ----------------------------
    // 指定スロットに何か入っているか
    // ----------------------------
    private bool HasEquipped(SocketSlotType slotType)
    {
        return slotType switch
        {
            SocketSlotType.BombTrait => sockets.GetBombTrait(gear) != BombTraitValues.None,
            SocketSlotType.ActionTrait => sockets.GetActionTrait(gear) != ActionTraitValues.None,
            SocketSlotType.Power => sockets.GetPowerCount(gear) > 0,
            SocketSlotType.BombCount => sockets.GetBombCount(gear) > 0,
            _ => false
        };
    }

    // ----------------------------
    // ヘッダー表示
    // ----------------------------
    private void DrawHeader(SpriteBatch b)
    {
        string title = i18n.Get("menu.socket.title").ToString();
        string sub = i18n.Get("menu.socket.subtitle").ToString();

        Vector2 titleSize = Game1.dialogueFont.MeasureString(title);

        b.DrawString(
            Game1.dialogueFont,
            title,
            new Vector2(xPositionOnScreen + width / 2f - titleSize.X / 2f, yPositionOnScreen + 36),
            Game1.textColor
        );

        b.DrawString(
            Game1.smallFont,
            sub,
            new Vector2(xPositionOnScreen + 64, yPositionOnScreen + 96),
            Game1.textColor
        );
    }

    // ----------------------------
    // 右側の有効状態表示
    // - 装備中の効果をアイコン付きで表示する
    // ----------------------------
    private void DrawEffectiveStatusPanel(SpriteBatch b)
    {
        int panelX = xPositionOnScreen + 500;
        int panelY = yPositionOnScreen + 142;
        int panelWidth = 410;
        int panelHeight = 286;

        DrawPanelBox(b, new Rectangle(panelX, panelY, panelWidth, panelHeight));

        b.DrawString(
            Game1.smallFont,
            i18n.Get("menu.socket.status.title").ToString(),
            new Vector2(panelX + 20, panelY + 16),
            Game1.textColor
        );

        DrawStatusRow(
            b,
            panelX + 18,
            panelY + 52,
            i18n.Get("menu.socket.slot.bomb-trait").ToString(),
            GetBombTraitDisplay(),
            CreateBombTraitPreviewItem()
        );

        DrawStatusRow(
            b,
            panelX + 18,
            panelY + 114,
            i18n.Get("menu.socket.slot.action-trait").ToString(),
            GetActionTraitDisplay(),
            CreateActionTraitPreviewItem()
        );

        DrawStatusRow(
            b,
            panelX + 18,
            panelY + 176,
            i18n.Get("menu.socket.slot.power").ToString(),
            sockets.GetPower(gear, config).ToString(),
            CreateObjectPreviewItem(ItemIds.PowerCore)
        );

        DrawStatusRow(
            b,
            panelX + 18,
            panelY + 238,
            i18n.Get("menu.socket.slot.bomb-count").ToString(),
            sockets.GetMaxBombs(gear, config).ToString(),
            CreateObjectPreviewItem(ItemIds.BombBag)
        );
    }

    // ----------------------------
    // ツール状態表示
    // - 実際の破壊判定に使うワールド内最大段階を表示する
    // ----------------------------
    private void DrawToolStatusPanel(SpriteBatch b)
    {
        int panelX = xPositionOnScreen + 500;
        int panelY = yPositionOnScreen + 446;
        int panelWidth = 410;
        int panelHeight = 150;

        DrawPanelBox(b, new Rectangle(panelX, panelY, panelWidth, panelHeight));

        b.DrawString(
            Game1.smallFont,
            i18n.Get("menu.socket.tool-status.title").ToString(),
            new Vector2(panelX + 20, panelY + 16),
            Game1.textColor
        );

        DrawToolCard(
            b,
            new Rectangle(panelX + 18, panelY + 52, 178, 78),
            i18n.Get("menu.socket.tool.pickaxe").ToString(),
            CreatePickaxePreview(bestPickaxeLevel),
            GetToolLevelDisplay(bestPickaxeLevel)
        );

        DrawToolCard(
            b,
            new Rectangle(panelX + 212, panelY + 52, 178, 78),
            i18n.Get("menu.socket.tool.axe").ToString(),
            CreateAxePreview(bestAxeLevel),
            GetToolLevelDisplay(bestAxeLevel)
        );
    }

    // ----------------------------
    // 各スロット表示
    // ----------------------------
    private void DrawSlot(
        SpriteBatch b,
        SlotView slot,
        string label,
        string value,
        bool canRemove,
        Item? previewItem
    )
    {
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            slot.Slot.bounds.X,
            slot.Slot.bounds.Y,
            slot.Slot.bounds.Width,
            slot.Slot.bounds.Height,
            Color.White,
            1f,
            false
        );

        DrawItemPreviewIcon(
            b,
            new Rectangle(slot.Slot.bounds.X + 16, slot.Slot.bounds.Y + 18, 38, 38),
            previewItem,
            HasEquipped(slot.SlotType),
            isToolCard: false
        );

        b.DrawString(
            Game1.smallFont,
            label,
            new Vector2(slot.Slot.bounds.X + 74, slot.Slot.bounds.Y + 14),
            Game1.textColor
        );

        b.DrawString(
            Game1.smallFont,
            value,
            new Vector2(slot.Slot.bounds.X + 74, slot.Slot.bounds.Y + 46),
            ValueTextColor
        );

        DrawRemoveButton(b, slot.RemoveButton, canRemove);
    }

    // ----------------------------
    // 状態1行を描画する
    // ----------------------------
    private void DrawStatusRow(
        SpriteBatch b,
        int x,
        int y,
        string label,
        string value,
        Item? previewItem
    )
    {
        DrawItemPreviewIcon(
            b,
            new Rectangle(x, y, 36, 36),
            previewItem,
            previewItem is not null,
            isToolCard: false
        );

        b.DrawString(
            Game1.smallFont,
            label,
            new Vector2(x + 50, y),
            Game1.textColor
        );

        b.DrawString(
            Game1.smallFont,
            value,
            new Vector2(x + 50, y + 18),
            ValueTextColor
        );
    }

    // ----------------------------
    // ツールカードを描画する
    // ----------------------------
    private void DrawToolCard(
        SpriteBatch b,
        Rectangle area,
        string label,
        Item? previewItem,
        string value
    )
    {
        DrawPanelBox(b, area);

        DrawItemPreviewIcon(
            b,
            new Rectangle(area.X + 10, area.Y + 12, 34, 34),
            previewItem,
            previewItem is not null,
            isToolCard: true
        );

        b.DrawString(
            Game1.smallFont,
            label,
            new Vector2(area.X + 52, area.Y + 12),
            Game1.textColor
        );

        b.DrawString(
            Game1.smallFont,
            value,
            new Vector2(area.X + 52, area.Y + 34),
            ValueTextColor
        );
    }

    // ----------------------------
    // アイコン表示
    // ----------------------------
    private static void DrawItemPreviewIcon(
        SpriteBatch b,
        Rectangle area,
        Item? previewItem,
        bool enabled,
        bool isToolCard
    )
    {
        Color boxColor = enabled ? Color.White : Color.White * 0.45f;

        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            area.X,
            area.Y,
            area.Width,
            area.Height,
            boxColor,
            0.65f,
            false
        );

        if (previewItem is null)
        {
            Vector2 dashSize = Game1.smallFont.MeasureString("-");
            b.DrawString(
                Game1.smallFont,
                "-",
                new Vector2(
                    area.Center.X - dashSize.X / 2f,
                    area.Center.Y - dashSize.Y / 2f
                ),
                Color.Gray
            );
            return;
        }

        float scale = previewItem is Tool
            ? (isToolCard ? 0.42f : 0.50f)
            : 0.56f;

        float offsetX = previewItem is Tool ? -16f : -14f;
        float offsetY = previewItem is Tool ? -16f : -14f;

        Vector2 drawPos = new(
            area.X + offsetX,
            area.Y + offsetY
        );

        previewItem.drawInMenu(
            b,
            drawPos,
            scale,
            enabled ? 1f : 0.45f,
            0.86f,
            StackDrawType.Hide,
            Color.White,
            true
        );
    }

    // ----------------------------
    // 汎用パネル枠
    // ----------------------------
    private static void DrawPanelBox(SpriteBatch b, Rectangle area)
    {
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            area.X,
            area.Y,
            area.Width,
            area.Height,
            Color.White,
            0.9f,
            false
        );
    }

    // ----------------------------
    // 外すボタン
    // ----------------------------
    private void DrawRemoveButton(SpriteBatch b, ClickableComponent button, bool enabled)
    {
        Color color = enabled ? Color.White : Color.LightGray;
        Color textColor = enabled ? Color.Red : Color.Gray;

        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            button.bounds.X,
            button.bounds.Y,
            button.bounds.Width,
            button.bounds.Height,
            color,
            0.8f,
            false
        );

        string label = i18n.Get("menu.socket.button.remove").ToString();
        Vector2 size = Game1.smallFont.MeasureString(label);

        b.DrawString(
            Game1.smallFont,
            label,
            new Vector2(
                button.bounds.Center.X - size.X / 2,
                button.bounds.Center.Y - size.Y / 2
            ),
            textColor
        );
    }

    // ----------------------------
    // 下部説明
    // ----------------------------
    private void DrawFooter(SpriteBatch b)
    {
        string footer = i18n.Get("menu.socket.footer").ToString();

        b.DrawString(
            Game1.smallFont,
            footer,
            new Vector2(xPositionOnScreen + 64, yPositionOnScreen + height - 74),
            Game1.textColor
        );
    }

    // ----------------------------
    // 閉じるボタン
    // ----------------------------
    private void DrawCloseButton(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            closeButton.bounds.X,
            closeButton.bounds.Y,
            closeButton.bounds.Width,
            closeButton.bounds.Height,
            Color.White,
            0.7f,
            false
        );

        Vector2 size = Game1.smallFont.MeasureString("X");

        b.DrawString(
            Game1.smallFont,
            "X",
            new Vector2(
                closeButton.bounds.Center.X - size.X / 2,
                closeButton.bounds.Center.Y - size.Y / 2
            ),
            Color.Red
        );
    }

    // ----------------------------
    // 爆弾特性表示文字
    // ----------------------------
    private string GetBombTraitDisplay()
    {
        return sockets.GetBombTrait(gear) switch
        {
            var value when value == BombTraitValues.Remote => i18n.Get("menu.socket.value.bomb-trait.remote").ToString(),
            var value when value == BombTraitValues.Pierce => i18n.Get("menu.socket.value.bomb-trait.pierce").ToString(),
            _ => i18n.Get("menu.socket.value.none").ToString()
        };
    }

    // ----------------------------
    // アクション特性表示文字
    // ----------------------------
    private string GetActionTraitDisplay()
    {
        return sockets.GetActionTrait(gear) switch
        {
            var value when value == ActionTraitValues.Kick => i18n.Get("menu.socket.value.action-trait.kick").ToString(),
            var value when value == ActionTraitValues.Throw => i18n.Get("menu.socket.value.action-trait.throw").ToString(),
            _ => i18n.Get("menu.socket.value.none").ToString()
        };
    }

    // ----------------------------
    // ツール段階表示文字
    // ----------------------------
    private string GetToolLevelDisplay(int level)
    {
        return level switch
        {
            1 => i18n.Get("menu.socket.tool.level.copper").ToString(),
            2 => i18n.Get("menu.socket.tool.level.steel").ToString(),
            3 => i18n.Get("menu.socket.tool.level.gold").ToString(),
            4 => i18n.Get("menu.socket.tool.level.iridium").ToString(),
            _ => i18n.Get("menu.socket.tool.level.normal").ToString()
        };
    }

    // ----------------------------
    // スロット用のプレビューアイテム
    // ----------------------------
    private Item? GetSlotPreviewItem(SocketSlotType slotType)
    {
        return slotType switch
        {
            SocketSlotType.BombTrait => CreateBombTraitPreviewItem(),
            SocketSlotType.ActionTrait => CreateActionTraitPreviewItem(),
            SocketSlotType.Power => CreateObjectPreviewItem(ItemIds.PowerCore),
            SocketSlotType.BombCount => CreateObjectPreviewItem(ItemIds.BombBag),
            _ => null
        };
    }

    // ----------------------------
    // 爆弾特性用のプレビューアイテム
    // ----------------------------
    private Item? CreateBombTraitPreviewItem()
    {
        return sockets.GetBombTrait(gear) switch
        {
            var value when value == BombTraitValues.Remote => CreateObjectPreviewItem(ItemIds.RemoteBombChip),
            var value when value == BombTraitValues.Pierce => CreateObjectPreviewItem(ItemIds.PierceBombChip),
            _ => null
        };
    }

    // ----------------------------
    // アクション特性用のプレビューアイテム
    // ----------------------------
    private Item? CreateActionTraitPreviewItem()
    {
        return sockets.GetActionTrait(gear) switch
        {
            var value when value == ActionTraitValues.Kick => CreateObjectPreviewItem(ItemIds.KickChip),
            var value when value == ActionTraitValues.Throw => CreateObjectPreviewItem(ItemIds.ThrowChip),
            _ => null
        };
    }

    // ----------------------------
    // Object アイコン用プレビュー生成
    // ----------------------------
    private static Item? CreateObjectPreviewItem(string itemId)
    {
        return ItemRegistry.Create($"(O){itemId}");
    }

    // ----------------------------
    // ツルハシの見た目生成
    // ----------------------------
    private static Item CreatePickaxePreview(int level)
    {
        return new Pickaxe
        {
            UpgradeLevel = level
        };
    }

    // ----------------------------
    // オノの見た目生成
    // ----------------------------
    private static Item CreateAxePreview(int level)
    {
        return new Axe
        {
            UpgradeLevel = level
        };
    }

    // ----------------------------
    // 外すボタン生成
    // ----------------------------
    private static ClickableComponent CreateRemoveButton(ClickableComponent slot, string name)
    {
        return new ClickableComponent(
            new Rectangle(slot.bounds.Right - 108, slot.bounds.Y + 22, 88, 40),
            name
        );
    }

    // ----------------------------
    // エラー表示
    // ----------------------------
    private static void ShowFailure(string message)
    {
        Game1.playSound("cancel");
        Game1.showRedMessage(message);
    }
}