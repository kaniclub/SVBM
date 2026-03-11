// ----------------------------
// GMCM連携
// ----------------------------
using BomberGear.Items;
using StardewModdingAPI;

namespace BomberGear.Config;

internal sealed class GmcmIntegration
{
    // ----------------------------
    // GMCMが入っていれば設定画面を登録
    // ----------------------------
    public void RegisterIfAvailable(IModHelper helper, IManifest manifest, ModConfig config)
    {
        var api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api is null)
            return;

        api.Register(
            mod: manifest,
            reset: () => config.ResetToDefaults(),
            save: () => helper.WriteConfig(config),
            titleScreenOnly: false
        );

        api.AddKeybindList(
            mod: manifest,
            getValue: () => config.OpenSocketMenuKey,
            setValue: value => config.OpenSocketMenuKey = value,
            name: () => helper.Translation.Get("gmcm.open-socket-menu-key.name"),
            tooltip: () => helper.Translation.Get("gmcm.open-socket-menu-key.tooltip")
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.FuseSeconds,
            setValue: value => config.FuseSeconds = value,
            name: () => helper.Translation.Get("gmcm.fuse-seconds.name"),
            tooltip: () => helper.Translation.Get("gmcm.fuse-seconds.tooltip"),
            min: 0.5f,
            max: 10.0f,
            interval: 0.5f,
            formatValue: value => $"{value:0.0}s"
        );

        // ----------------------------
        // 火力設定
        // - Base は Max を超えないようにする
        // - Max は Base 未満にならないようにする
        // ----------------------------
        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.BasePower,
            setValue: value =>
            {
                config.BasePower = ClampInt(value, 1, SocketLimits.MaxPowerValue);

                if (config.MaxPower < config.BasePower)
                    config.MaxPower = config.BasePower;
            },
            name: () => helper.Translation.Get("gmcm.base-power.name"),
            tooltip: () => helper.Translation.Get("gmcm.base-power.tooltip"),
            min: 1,
            max: SocketLimits.MaxPowerValue,
            interval: 1
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.MaxPower,
            setValue: value =>
            {
                config.MaxPower = ClampInt(value, 1, SocketLimits.MaxPowerValue);

                if (config.BasePower > config.MaxPower)
                    config.BasePower = config.MaxPower;
            },
            name: () => helper.Translation.Get("gmcm.max-power.name"),
            tooltip: () => helper.Translation.Get("gmcm.max-power.tooltip"),
            min: 1,
            max: SocketLimits.MaxPowerValue,
            interval: 1
        );

        // ----------------------------
        // 爆弾数設定
        // - Base は Max を超えないようにする
        // - Max は Base 未満にならないようにする
        // ----------------------------
        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.BaseMaxBombs,
            setValue: value =>
            {
                config.BaseMaxBombs = ClampInt(value, 1, SocketLimits.MaxBombValue);

                if (config.MaxBombs < config.BaseMaxBombs)
                    config.MaxBombs = config.BaseMaxBombs;
            },
            name: () => helper.Translation.Get("gmcm.base-max-bombs.name"),
            tooltip: () => helper.Translation.Get("gmcm.base-max-bombs.tooltip"),
            min: 1,
            max: SocketLimits.MaxBombValue,
            interval: 1
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.MaxBombs,
            setValue: value =>
            {
                config.MaxBombs = ClampInt(value, 1, SocketLimits.MaxBombValue);

                if (config.BaseMaxBombs > config.MaxBombs)
                    config.BaseMaxBombs = config.MaxBombs;
            },
            name: () => helper.Translation.Get("gmcm.max-bombs.name"),
            tooltip: () => helper.Translation.Get("gmcm.max-bombs.tooltip"),
            min: 1,
            max: SocketLimits.MaxBombValue,
            interval: 1
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.PlayerDamage,
            setValue: value => config.PlayerDamage = value,
            name: () => helper.Translation.Get("gmcm.player-damage.name"),
            tooltip: () => helper.Translation.Get("gmcm.player-damage.tooltip"),
            min: 0,
            max: 250,
            interval: 1
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.MonsterDamage,
            setValue: value => config.MonsterDamage = value,
            name: () => helper.Translation.Get("gmcm.monster-damage.name"),
            tooltip: () => helper.Translation.Get("gmcm.monster-damage.tooltip"),
            min: 0,
            max: 250,
            interval: 10
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.ExplosionDamageDurationSeconds,
            setValue: value => config.ExplosionDamageDurationSeconds = value,
            name: () => helper.Translation.Get("gmcm.explosion-damage-duration.name"),
            tooltip: () => helper.Translation.Get("gmcm.explosion-damage-duration.tooltip"),
            min: 0.1f,
            max: 10.0f,
            interval: 0.1f,
            formatValue: value => $"{value:0.0}s"
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.BombDamageInvincibilitySeconds,
            setValue: value => config.BombDamageInvincibilitySeconds = value,
            name: () => helper.Translation.Get("gmcm.bomb-damage-invincibility.name"),
            tooltip: () => helper.Translation.Get("gmcm.bomb-damage-invincibility.tooltip"),
            min: 0.0f,
            max: 10.0f,
            interval: 0.1f,
            formatValue: value => $"{value:0.0}s"
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.KickSlideStepTicks,
            setValue: value => config.KickSlideStepTicks = value,
            name: () => helper.Translation.Get("gmcm.kick-slide-step-ticks.name"),
            tooltip: () => helper.Translation.Get("gmcm.kick-slide-step-ticks.tooltip"),
            min: 1,
            max: 30,
            interval: 1
        );

        api.AddBoolOption(
            mod: manifest,
            getValue: () => config.ResetSocketsOnDeath,
            setValue: value => config.ResetSocketsOnDeath = value,
            name: () => helper.Translation.Get("gmcm.reset-sockets-on-death.name"),
            tooltip: () => helper.Translation.Get("gmcm.reset-sockets-on-death.tooltip")
        );

        // ----------------------------
        // ソケットドロップ設定
        // ----------------------------
        api.AddBoolOption(
            mod: manifest,
            getValue: () => config.SocketDrops.Enabled,
            setValue: value => config.SocketDrops.Enabled = value,
            name: () => helper.Translation.Get("gmcm.socket-drops.enabled.name"),
            tooltip: () => helper.Translation.Get("gmcm.socket-drops.enabled.tooltip")
        );

        api.AddBoolOption(
            mod: manifest,
            getValue: () => config.SocketDrops.OnlyDropInMineLikeLocations,
            setValue: value => config.SocketDrops.OnlyDropInMineLikeLocations = value,
            name: () => helper.Translation.Get("gmcm.socket-drops.only-mine-locations.name"),
            tooltip: () => helper.Translation.Get("gmcm.socket-drops.only-mine-locations.tooltip")
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.SocketDrops.PowerBombDropsPerMineFloor,
            setValue: value => config.SocketDrops.PowerBombDropsPerMineFloor = value,
            name: () => helper.Translation.Get("gmcm.socket-drops.power-bomb-per-floor.name"),
            tooltip: () => helper.Translation.Get("gmcm.socket-drops.power-bomb-per-floor.tooltip"),
            min: 0.0f,
            max: 5.0f,
            interval: 0.05f,
            formatValue: value => $"{value:0.00}"
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.SocketDrops.ActionChipDropsPerMineFloor,
            setValue: value => config.SocketDrops.ActionChipDropsPerMineFloor = value,
            name: () => helper.Translation.Get("gmcm.socket-drops.action-chip-per-floor.name"),
            tooltip: () => helper.Translation.Get("gmcm.socket-drops.action-chip-per-floor.tooltip"),
            min: 0.0f,
            max: 1.0f,
            interval: 0.01f,
            formatValue: value => $"{value:0.00}"
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.SocketDrops.ExpectedStoneBreaksPerMineFloor,
            setValue: value => config.SocketDrops.ExpectedStoneBreaksPerMineFloor = value,
            name: () => helper.Translation.Get("gmcm.socket-drops.expected-stone-breaks.name"),
            tooltip: () => helper.Translation.Get("gmcm.socket-drops.expected-stone-breaks.tooltip"),
            min: 1.0f,
            max: 200.0f,
            interval: 1.0f,
            formatValue: value => $"{value:0}"
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.SocketDrops.ExpectedOreBreaksPerMineFloor,
            setValue: value => config.SocketDrops.ExpectedOreBreaksPerMineFloor = value,
            name: () => helper.Translation.Get("gmcm.socket-drops.expected-ore-breaks.name"),
            tooltip: () => helper.Translation.Get("gmcm.socket-drops.expected-ore-breaks.tooltip"),
            min: 1.0f,
            max: 200.0f,
            interval: 1.0f,
            formatValue: value => $"{value:0}"
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.SocketDrops.StoneDropChanceMultiplier,
            setValue: value => config.SocketDrops.StoneDropChanceMultiplier = value,
            name: () => helper.Translation.Get("gmcm.socket-drops.stone-chance-multiplier.name"),
            tooltip: () => helper.Translation.Get("gmcm.socket-drops.stone-chance-multiplier.tooltip"),
            min: 0.0f,
            max: 10.0f,
            interval: 0.1f,
            formatValue: value => $"{value:0.0}x"
        );

        api.AddNumberOption(
            mod: manifest,
            getValue: () => config.SocketDrops.OreDropChanceMultiplier,
            setValue: value => config.SocketDrops.OreDropChanceMultiplier = value,
            name: () => helper.Translation.Get("gmcm.socket-drops.ore-chance-multiplier.name"),
            tooltip: () => helper.Translation.Get("gmcm.socket-drops.ore-chance-multiplier.tooltip"),
            min: 0.0f,
            max: 10.0f,
            interval: 0.1f,
            formatValue: value => $"{value:0.0}x"
        );

        api.AddBoolOption(
            mod: manifest,
            getValue: () => config.SocketDrops.ApplyDropCaps,
            setValue: value => config.SocketDrops.ApplyDropCaps = value,
            name: () => helper.Translation.Get("gmcm.socket-drops.apply-drop-cap.name"),
            tooltip: () => helper.Translation.Get("gmcm.socket-drops.apply-drop-cap.tooltip")
        );
    }

    // ----------------------------
    // int の範囲補正
    // ----------------------------
    private static int ClampInt(int value, int min, int max)
    {
        if (value < min)
            return min;

        if (value > max)
            return max;

        return value;
    }
}