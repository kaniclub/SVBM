// ----------------------------
// Modの入口
// - 初期化
// - イベント購読
// - 更新
// - 描画
// を担当
// ----------------------------
using BomberGear.Bombs;
using BomberGear.BombTraits;
using BomberGear.Config;
using BomberGear.Explosion;
using BomberGear.Explosion.Visuals;
using BomberGear.Input;
using BomberGear.Items;
using BomberGear.Menus;
using BomberGear.Drops;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace BomberGear;

internal sealed class ModEntry : Mod
{
    internal static ModEntry Instance { get; private set; } = null!;
    internal BombManager Bombs => bombs;

    // ----------------------------
    // 現在装備でキック可能か
    // - ボンバーギア装備中 かつ
    // - アクション特性が kick のときだけ true
    // ----------------------------
    internal bool CanKickBomb(Farmer farmer)
    {
        Item? currentItem = farmer.CurrentItem;

        if (currentItem is null || currentItem.ItemId != ItemIds.BomberGear)
            return false;

        return sockets.CanKickBomb(currentItem);
    }

    // ----------------------------
    // キック時の1マス移動間隔を返す
    // - 値が大きいほど遅い
    // ----------------------------
    internal int GetKickSlideStepTicks()
    {
        return config.KickSlideStepTicks < 1
            ? 1
            : config.KickSlideStepTicks;
    }

    // ----------------------------
    // ローカルプレイヤーが戦闘不能になったときの処理
    // - 同じ死亡処理中に複数回走らないようロックする
    // - config が有効なときだけソケット初期化を行う
    // ----------------------------
    internal void HandleLocalPlayerDeath(Farmer player)
    {
        if (!Context.IsWorldReady)
            return;

        if (Game1.player is null)
            return;

        if (player.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID)
            return;

        if (deathSocketResetLocked)
            return;

        deathSocketResetLocked = true;

        if (!config.ResetSocketsOnDeath)
            return;

        if (bomberGearDeathReset.ResetSockets(player))
            bomberGearMail.RefreshOwnershipForLocalPlayer();
    }

    private readonly GmcmIntegration gmcm = new();
    private ModConfig config = null!;
    private SocketService sockets = null!;
    private SocketItemResolver socketResolver = null!;
    private SocketEquipmentService socketEquipment = null!;
    private BomberGearLocator gearLocator = null!;
    private BomberGearMailService bomberGearMail = null!;
    private BomberGearDeathResetService bomberGearDeathReset = null!;
    private BombManager bombs = null!;
    private BombTraitSpecialHandler bombTraitSpecials = null!;
    private ExplosionVisualManager explosionVisuals = null!;
    private BomberInputHandler bombInputHandler = null!;
    private BomberMenuService bomberMenuService = null!;
    private SocketDropService socketDrops = null!;

    // ----------------------------
    // 死亡時ソケット初期化の多重実行防止
    // ----------------------------
    private bool deathSocketResetLocked;

    // ----------------------------
    // 専用爆弾スプライト
    // ----------------------------
    private Texture2D normalBombSprite = null!;
    private Texture2D pierceBombSprite = null!;
    private Texture2D remoteBombSprite = null!;

    public override void Entry(IModHelper helper)
    {
        Instance = this;

        InitializeConfig(helper);
        InitializeCoreServices(helper);
        InitializeAssets(helper);
        RegisterEvents(helper);
        ApplyHarmonyPatch();
    }

    // ----------------------------
    // config 読み込み
    // ----------------------------
    private void InitializeConfig(IModHelper helper)
    {
        config = helper.ReadConfig<ModConfig>();
    }

    // ----------------------------
    // 中核サービス初期化
    // ----------------------------
    private void InitializeCoreServices(IModHelper helper)
    {
        explosionVisuals = new ExplosionVisualManager(helper);

        sockets = new SocketService();
        socketResolver = new SocketItemResolver();
        socketEquipment = new SocketEquipmentService(socketResolver, sockets);
        gearLocator = new BomberGearLocator();
        bomberGearMail = new BomberGearMailService(Monitor, helper.Translation);
        bomberGearDeathReset = new BomberGearDeathResetService(Monitor);

        socketDrops = new SocketDropService();

        bombs = new BombManager(new ExplosionService(), explosionVisuals);
        bombTraitSpecials = new BombTraitSpecialHandler(bombs);

        bomberMenuService = new BomberMenuService(
            gearLocator,
            socketEquipment,
            sockets,
            helper.Translation,
            () => config
        );

        bombInputHandler = new BomberInputHandler(
            helper,
            Monitor,
            helper.Translation,
            socketResolver,
            socketEquipment,
            sockets,
            bombs,
            bombTraitSpecials,
            gearLocator,
            () => config
        );
    }

    // ----------------------------
    // 使用アセット読み込み
    // ----------------------------
    private void InitializeAssets(IModHelper helper)
    {
        normalBombSprite = helper.ModContent.Load<Texture2D>("assets/bomb_normal.png");
        pierceBombSprite = helper.ModContent.Load<Texture2D>("assets/bomb_pierce.png");
        remoteBombSprite = helper.ModContent.Load<Texture2D>("assets/bomb_remote.png");

        bombs.SetBombSprites(
            normalBombSprite: normalBombSprite,
            pierceBombSprite: pierceBombSprite,
            remoteBombSprite: remoteBombSprite
        );
    }

    // ----------------------------
    // イベント購読
    // ----------------------------
    private void RegisterEvents(IModHelper helper)
    {
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;

        helper.Events.Content.AssetRequested += OnAssetRequested;

        helper.Events.Player.InventoryChanged += OnInventoryChanged;
        helper.Events.Player.Warped += OnWarped;

        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.Input.ButtonPressed += OnButtonPressed;

        helper.Events.Display.RenderedWorld += OnRenderedWorld;
    }

    // ----------------------------
    // Harmony Patch 適用
    // ----------------------------
    private void ApplyHarmonyPatch()
    {
        var harmony = new Harmony(ModManifest.UniqueID);
        harmony.PatchAll();
    }

    // ----------------------------
    // GMCM登録
    // ----------------------------
    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        gmcm.RegisterIfAvailable(Helper, ModManifest, config);
    }

    // ----------------------------
    // セーブ読込時
    // - 設置中爆弾と爆発ビジュアルをクリア
    // ----------------------------
    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        bombs.ClearAll();
        explosionVisuals.Clear();
        bombInputHandler.ClearPendingSpecialPress();
        deathSocketResetLocked = false;
    }

    // ----------------------------
    // 朝開始時
    // - 所有情報を補正
    // - 郵便受け状態を同期
    // - 配布判定を行う
    //   - 受け取っていないプレイヤーには配布
    // ----------------------------
    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        deathSocketResetLocked = false;
        bomberGearMail.RefreshOwnershipForLocalPlayer();
        bomberGearMail.SyncMailboxForLocalPlayer();
    }

    // ----------------------------
    // ワープ時
    // - 死亡処理のロック解除
    // ----------------------------
    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (!e.IsLocalPlayer)
            return;

        deathSocketResetLocked = false;
    }

    // ----------------------------
    // メール本文アセット差し込み
    // ----------------------------
    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        bomberGearMail.OnAssetRequested(e);
    }

    // ----------------------------
    // 手持ちが変わったとき
    // - 受け取ったギアへ所有者タグを付与
    // ----------------------------
    private void OnInventoryChanged(object? sender, InventoryChangedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (!e.IsLocalPlayer)
            return;

        bomberGearMail.RefreshOwnershipForLocalPlayer();
    }

    // ----------------------------
    // 導火線更新 / 爆発ビジュアル更新 / 特殊発動短押し確定
    // ----------------------------
    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        bombs.Update(config);
        explosionVisuals.Update();
        bombInputHandler.ResolvePendingSpecialPress();
    }

    // ----------------------------
    // 設定キーでソケットメニューを開く
    // ----------------------------
    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (!Context.IsPlayerFree)
            return;

        if (Game1.activeClickableMenu is not null)
            return;

        if (!config.OpenSocketMenuKey.JustPressed())
            return;

        bomberMenuService.OpenSocketMenu();
    }

    // ----------------------------
    // 押した瞬間の入力処理
    // ----------------------------
    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        bombInputHandler.HandleButtonPressed(e);
    }

    // ----------------------------
    // ワールド描画
    // ----------------------------
    private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        bombs.Draw(e.SpriteBatch, Game1.currentLocation);
        explosionVisuals.Draw(e.SpriteBatch, Game1.currentLocation);
    }
}