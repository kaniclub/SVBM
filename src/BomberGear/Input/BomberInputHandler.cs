// ----------------------------
// ボンバーギア入力処理
// - ソケット装着
// - 爆弾設置
// - パワーグローブアクション
// - 特殊発動の短押し判定
// を担当する
// ----------------------------
using System;
using System.Linq;
using BomberGear.Bombs;
using BomberGear.BombTraits;
using BomberGear.Config;
using BomberGear.Items;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;

namespace BomberGear.Input;

internal sealed class BomberInputHandler
{
    // ----------------------------
    // 特殊発動の短押し判定
    // これ以下なら短押し、それを超えたら長押し
    // ----------------------------
    private const double SpecialShortPressThresholdMs = 220;

    // ----------------------------
    // 右クリック押下中の状態
    // suppress 中は ButtonReleased に頼らず、
    // IsSuppressed の解除で物理リリースを判定する
    // ----------------------------
    private sealed class PendingSpecialPress
    {
        public SButton Button { get; }
        public DateTime PressedAtUtc { get; }

        public PendingSpecialPress(SButton button, DateTime pressedAtUtc)
        {
            Button = button;
            PressedAtUtc = pressedAtUtc;
        }
    }

    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly ITranslationHelper i18n;
    private readonly SocketItemResolver socketResolver;
    private readonly SocketEquipmentService socketEquipment;
    private readonly SocketService sockets;
    private readonly BombManager bombs;
    private readonly BombTraitSpecialHandler bombTraitSpecials;
    private readonly BomberGearLocator gearLocator;
    private readonly Func<ModConfig> getConfig;

    private PendingSpecialPress? pendingSpecialPress;

    public BomberInputHandler(
        IModHelper helper,
        IMonitor monitor,
        ITranslationHelper i18n,
        SocketItemResolver socketResolver,
        SocketEquipmentService socketEquipment,
        SocketService sockets,
        BombManager bombs,
        BombTraitSpecialHandler bombTraitSpecials,
        BomberGearLocator gearLocator,
        Func<ModConfig> getConfig)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.i18n = i18n;
        this.socketResolver = socketResolver;
        this.socketEquipment = socketEquipment;
        this.sockets = sockets;
        this.bombs = bombs;
        this.bombTraitSpecials = bombTraitSpecials;
        this.gearLocator = gearLocator;
        this.getConfig = getConfig;
    }

    // ----------------------------
    // 保留中の特殊発動入力をクリア
    // ----------------------------
    public void ClearPendingSpecialPress()
    {
        pendingSpecialPress = null;
    }

    // ----------------------------
    // 押した瞬間の入力処理
    // ----------------------------
    public void HandleButtonPressed(ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (!Context.IsPlayerFree)
            return;

        Farmer who = Game1.player;
        Item? item = who.CurrentItem;

        if (TryEquipHeldSocket(e, who, item))
            return;

        if (item is null || item.ItemId != ItemIds.BomberGear)
            return;

        if (TryPlaceBomb(e, who, item))
            return;

        if (TryHandleThrowAction(e, who, item))
            return;

        TryBeginSpecialPress(e, who, item);
    }

    // ----------------------------
    // suppress 解除で特殊発動の短押しを確定する
    // 長押しは何もしない
    // ----------------------------
    public void ResolvePendingSpecialPress()
    {
        if (pendingSpecialPress is null)
            return;

        if (helper.Input.IsSuppressed(pendingSpecialPress.Button))
            return;

        PendingSpecialPress press = pendingSpecialPress;
        pendingSpecialPress = null;

        if (!Context.IsWorldReady)
            return;

        if (!Context.IsPlayerFree)
            return;

        Farmer who = Game1.player;
        Item? item = who.CurrentItem;

        if (item is null || item.ItemId != ItemIds.BomberGear)
            return;

        string bombTraitValue = sockets.GetBombTrait(item);

        if (!bombTraitSpecials.HasSpecial(bombTraitValue))
            return;

        double heldMs = (DateTime.UtcNow - press.PressedAtUtc).TotalMilliseconds;

        if (heldMs > SpecialShortPressThresholdMs)
            return;

        bombTraitSpecials.TryHandle(who, who.currentLocation, bombTraitValue, getConfig());
    }

    // ----------------------------
    // ソケットを手に持っている時の右クリック装着
    // ----------------------------
    private bool TryEquipHeldSocket(ButtonPressedEventArgs e, Farmer who, Item? item)
    {
        if (item is null)
            return false;

        if (!InputRouter.IsSpecial(e.Button))
            return false;

        if (!socketResolver.TryResolve(item, out var resolved))
            return false;

        if (HasPriorityAction(who))
            return true;

        helper.Input.Suppress(e.Button);

        if (!gearLocator.TryGet(out Item gear))
        {
            Game1.showRedMessage(i18n.Get("menu.open.no-gear").ToString());
            return true;
        }

        ModConfig config = getConfig();

        if (resolved.SlotType == SocketSlotType.Power
            && sockets.GetPowerCount(gear) >= sockets.GetMaxAddablePowerCount(config))
        {
            Game1.playSound("cancel");
            Game1.showRedMessage(i18n.Get("menu.socket.error.power-cap-reached").ToString());
            return true;
        }

        if (resolved.SlotType == SocketSlotType.BombCount
            && sockets.GetBombCount(gear) >= sockets.GetMaxAddableBombCount(config))
        {
            Game1.playSound("cancel");
            Game1.showRedMessage(i18n.Get("menu.socket.error.bomb-count-cap-reached").ToString());
            return true;
        }

        bool equipped = socketEquipment.TryEquip(who, gear, item, config);
        if (!equipped)
        {
            Game1.playSound("cancel");
            Game1.showRedMessage(i18n.Get("menu.socket.error.equip-failed").ToString());
            return true;
        }

        Game1.playSound("smallSelect");
        return true;
    }

    // ----------------------------
    // 爆弾設置
    // ----------------------------
    private bool TryPlaceBomb(ButtonPressedEventArgs e, Farmer who, Item gear)
    {
        if (!InputRouter.IsPlaceBomb(e.Button))
            return false;

        ModConfig config = getConfig();

        helper.Input.Suppress(e.Button);

        int power = sockets.GetPower(gear, config);
        int maxBombs = sockets.GetMaxBombs(gear, config);
        int fuseTicks = Math.Max(1, (int)(config.FuseSeconds * 60f));
        string bombTraitValue = sockets.GetBombTrait(gear);

        bool placed = bombs.TryPlaceBomb(
            who,
            who.currentLocation,
            power,
            fuseTicks,
            maxBombs,
            bombTraitValue
        );

        if (!placed)
            monitor.Log("爆弾を置けませんでした。", LogLevel.Trace);

        return true;
    }

    // ----------------------------
    // パワーグローブアクション処理
    // - throw 装備時のみ
    // - 持ち上げ中なら投げる
    // - 持ち上げていなければ、正面の爆弾を拾う
    // - 投げ速度はキックと同じ設定値を使う
    // ----------------------------
    private bool TryHandleThrowAction(ButtonPressedEventArgs e, Farmer who, Item gear)
    {
        if (!InputRouter.IsSpecial(e.Button))
            return false;

        if (!sockets.CanThrowBomb(gear))
            return false;

        if (bombs.HasHeldBomb(who.UniqueMultiplayerID))
        {
            helper.Input.Suppress(e.Button);

            ModConfig config = getConfig();
            int throwStepTicks = config.KickSlideStepTicks < 1
                ? 1
                : config.KickSlideStepTicks;

            bool thrown = bombs.TryThrowHeldBomb(
                who,
                who.currentLocation,
                throwStepTicks
            );

            if (!thrown)
                Game1.playSound("cancel");

            return true;
        }

        bool pickedUp = bombs.TryPickUpFrontBomb(who, who.currentLocation);
        if (pickedUp)
        {
            helper.Input.Suppress(e.Button);
            return true;
        }

        return false;
    }

    // ----------------------------
    // 特殊発動の短押し判定を開始
    // - 実際に特殊発動できるときだけ右クリックを抑止する
    // - Remote だけは、鉱山の階段/縦穴を優先する
    // - 草や枝や木の近くでは、ガードより BomberGear 動作を優先する
    // ----------------------------
    private void TryBeginSpecialPress(ButtonPressedEventArgs e, Farmer who, Item gear)
    {
        if (!InputRouter.IsSpecial(e.Button))
            return;

        string bombTraitValue = sockets.GetBombTrait(gear);

        if (!bombTraitSpecials.HasSpecial(bombTraitValue))
            return;

        if (bombTraitValue == BombTraitValues.Remote
            && ShouldPreferMineDescentAction(who))
        {
            return;
        }

        if (HasSpecialActionPriority(who))
            return;

        helper.Input.Suppress(e.Button);

        pendingSpecialPress = new PendingSpecialPress(
            button: e.Button,
            pressedAtUtc: DateTime.UtcNow
        );
    }

    // ----------------------------
    // 足元 / 正面 / カーソル位置をまとめて返す
    // ----------------------------
    private (Vector2 Current, Vector2 Front, Vector2 Cursor) GetInteractionTiles(Farmer who)
    {
        return (
            who.Tile,
            GetFrontTile(who),
            helper.Input.GetCursorPosition().GrabTile
        );
    }

    // ----------------------------
    // 鉱山の下降アクションを優先すべきか
    // - 足元
    // - 正面1マス
    // - カーソル位置（1マス以内のみ）
    // を見る
    // ----------------------------
    private bool ShouldPreferMineDescentAction(Farmer who)
    {
        if (who.currentLocation is not MineShaft mine)
            return false;

        var tiles = GetInteractionTiles(who);

        if (IsMineDescentTile(mine, tiles.Current))
            return true;

        if (IsMineDescentTile(mine, tiles.Front))
            return true;

        if (IsWithinOneTile(tiles.Current, tiles.Cursor)
            && IsMineDescentTile(mine, tiles.Cursor))
        {
            return true;
        }

        return false;
    }

    // ----------------------------
    // 指定タイルが鉱山の下降タイルか
    // - Buildings レイヤーのタイル番号で判定する
    // ----------------------------
    private static bool IsMineDescentTile(MineShaft mine, Vector2 tile)
    {
        var buildings = mine.Map?.GetLayer("Buildings");
        if (buildings is null)
            return false;

        int x = (int)tile.X;
        int y = (int)tile.Y;

        if (x < 0 || y < 0 || x >= buildings.LayerWidth || y >= buildings.LayerHeight)
            return false;

        var mapTile = buildings.Tiles[x, y];
        if (mapTile is null)
            return false;

        return mapTile.TileIndex is 173 or 174;
    }

    // ----------------------------
    // ソケット装着時に通常アクションを優先すべき状況か
    // - 足元
    // - 正面1マス
    // - カーソル位置
    // を見て判定する
    // ----------------------------
    private bool HasPriorityAction(Farmer who)
    {
        GameLocation location = who.currentLocation;
        var tiles = GetInteractionTiles(who);

        return HasPriorityActionAtTile(location, tiles.Current)
            || HasPriorityActionAtTile(location, tiles.Front)
            || HasPriorityActionAtTile(location, tiles.Cursor);
    }

    // ----------------------------
    // 特殊発動時だけ、vanilla の右クリックを優先すべき状況か
    // - 草・枝・木のような「ガードになるだけ」の対象は含めない
    // - 本当に右クリックで意味のある対象だけを見る
    // ----------------------------
    private bool HasSpecialActionPriority(Farmer who)
    {
        GameLocation location = who.currentLocation;
        var tiles = GetInteractionTiles(who);

        return HasSpecialActionPriorityAtTile(location, who, tiles.Current)
            || HasSpecialActionPriorityAtTile(location, who, tiles.Front)
            || HasSpecialActionPriorityAtTile(location, who, tiles.Cursor);
    }

    // ----------------------------
    // 指定タイルに通常アクション優先対象があるか
    // - ソケット装着用なので広めに見る
    // ----------------------------
    private bool HasPriorityActionAtTile(GameLocation location, Vector2 tile)
    {
        Rectangle tileRect = GetTileRect(tile);

        if (HasActionTile(location, tile))
            return true;

        if (location.Objects.ContainsKey(tile))
            return true;

        if (location.terrainFeatures.ContainsKey(tile))
            return true;

        if (location.furniture.Any(furniture => furniture.GetBoundingBox().Intersects(tileRect)))
            return true;

        if (location.characters.Any(character => character.GetBoundingBox().Intersects(tileRect)))
            return true;

        if (location is Farm farm && farm.getBuildingAt(tile) is not null)
            return true;

        if (location is Farm animalFarm
            && animalFarm.animals.Values.Any(animal => animal.GetBoundingBox().Intersects(tileRect)))
        {
            return true;
        }

        return false;
    }

    // ----------------------------
    // 特殊発動時だけ、指定タイルに vanilla 優先対象があるか
    // - Action タイル
    // - 実際に右クリックアクションを持つ Object
    // - 家具
    // - キャラ
    // - 建物
    // - 動物
    // を見る
    // ----------------------------
    private bool HasSpecialActionPriorityAtTile(GameLocation location, Farmer who, Vector2 tile)
    {
        Rectangle tileRect = GetTileRect(tile);

        if (HasActionTile(location, tile))
            return true;

        if (location.Objects.TryGetValue(tile, out StardewValley.Object? obj)
            && obj.checkForAction(who, true))
        {
            return true;
        }

        if (location.furniture.Any(furniture => furniture.GetBoundingBox().Intersects(tileRect)))
            return true;

        if (location.characters.Any(character => character.GetBoundingBox().Intersects(tileRect)))
            return true;

        if (location is Farm farm && farm.getBuildingAt(tile) is not null)
            return true;

        if (location is Farm animalFarm
            && animalFarm.animals.Values.Any(animal => animal.GetBoundingBox().Intersects(tileRect)))
        {
            return true;
        }

        return false;
    }

    // ----------------------------
    // クリック用の通常 Action があるか
    // ----------------------------
    private static bool HasActionTile(GameLocation location, Vector2 tile)
    {
        int x = (int)tile.X;
        int y = (int)tile.Y;

        return location.doesTileHaveProperty(x, y, "Action", "Back") is not null
            || location.doesTileHaveProperty(x, y, "Action", "Buildings") is not null
            || location.doesTileHaveProperty(x, y, "Action", "Front") is not null
            || location.doesTileHaveProperty(x, y, "Action", "AlwaysFront") is not null;
    }

    // ----------------------------
    // 1マス以内か
    // - 斜め含む 3x3 範囲
    // ----------------------------
    private static bool IsWithinOneTile(Vector2 origin, Vector2 target)
    {
        return Math.Abs((int)(origin.X - target.X)) <= 1
            && Math.Abs((int)(origin.Y - target.Y)) <= 1;
    }

    // ----------------------------
    // プレイヤー正面1マスのタイルを返す
    // ----------------------------
    private static Vector2 GetFrontTile(Farmer who)
    {
        Vector2 tile = who.Tile;

        return who.FacingDirection switch
        {
            0 => new Vector2(tile.X, tile.Y - 1),
            1 => new Vector2(tile.X + 1, tile.Y),
            2 => new Vector2(tile.X, tile.Y + 1),
            3 => new Vector2(tile.X - 1, tile.Y),
            _ => tile
        };
    }

    // ----------------------------
    // タイル矩形を作る
    // ----------------------------
    private static Rectangle GetTileRect(Vector2 tile)
    {
        return new Rectangle(
            (int)tile.X * Game1.tileSize,
            (int)tile.Y * Game1.tileSize,
            Game1.tileSize,
            Game1.tileSize
        );
    }
}