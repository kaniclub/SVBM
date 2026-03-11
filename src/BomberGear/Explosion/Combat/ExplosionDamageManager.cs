// ----------------------------
// 爆風の持続ダメージ判定管理
// - 爆発ごとに一定時間だけダメージゾーンを保持
// - その間に爆風へ入った対象にもダメージが入る
// - 同じ爆発では同じ対象に1回だけ当てる
// - さらに、別の爆弾を含めた共通の無敵時間を持つ
// - 爆弾側からも「このタイルに残留爆風があるか」を参照できる
// ----------------------------
using System.Collections.Generic;
using System.Linq;
using BomberGear.Config;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Monsters;

namespace BomberGear.Explosion.Combat;

internal sealed class ExplosionDamageManager
{
    private long currentTick;

    private readonly List<ActiveDamageZone> activeZones = new();
    private readonly Dictionary<long, long> playerImmuneUntilTicks = new();
    private readonly Dictionary<Character, long> monsterImmuneUntilTicks = new();

    // ----------------------------
    // 持続中ダメージゾーン
    // ----------------------------
    private sealed class ActiveDamageZone
    {
        public string LocationName { get; }
        public IReadOnlyList<Vector2> AffectedTiles { get; }
        public Farmer SourceFarmer { get; }
        public int PlayerDamage { get; }
        public int MonsterDamage { get; }
        public int TicksLeft { get; set; }

        // 同じ爆発内での多段ヒット防止
        public HashSet<long> HitPlayerIds { get; } = new();
        public HashSet<Character> HitMonsters { get; } = new();

        public ActiveDamageZone(
            string locationName,
            IReadOnlyList<Vector2> affectedTiles,
            Farmer sourceFarmer,
            int playerDamage,
            int monsterDamage,
            int ticksLeft
        )
        {
            LocationName = locationName;
            AffectedTiles = affectedTiles;
            SourceFarmer = sourceFarmer;
            PlayerDamage = playerDamage;
            MonsterDamage = monsterDamage;
            TicksLeft = ticksLeft;
        }
    }

    // ----------------------------
    // ダメージゾーンを追加
    // ----------------------------
    public void Spawn(
        GameLocation location,
        IReadOnlyList<Vector2> affectedTiles,
        Farmer sourceFarmer,
        int playerDamage,
        int monsterDamage,
        float durationSeconds
    )
    {
        if (affectedTiles.Count == 0)
            return;

        int durationTicks = SecondsToTicks(durationSeconds);
        if (durationTicks <= 0)
            durationTicks = 1;

        activeZones.Add(
            new ActiveDamageZone(
                locationName: location.Name,
                affectedTiles: affectedTiles.ToList(),
                sourceFarmer: sourceFarmer,
                playerDamage: playerDamage,
                monsterDamage: monsterDamage,
                ticksLeft: durationTicks
            )
        );
    }

    // ----------------------------
    // 指定タイルに残留爆風があるか
    // - TicksLeft が残っている間は true
    // ----------------------------
    public bool IsTileInActiveZone(string locationName, Vector2 tile)
    {
        foreach (var zone in activeZones)
        {
            if (zone.LocationName != locationName)
                continue;

            if (zone.TicksLeft <= 0)
                continue;

            if (zone.AffectedTiles.Contains(tile))
                return true;
        }

        return false;
    }

    // ----------------------------
    // 毎tick更新
    // ----------------------------
    public void Update(ModConfig config)
    {
        currentTick++;

        CleanupExpiredImmunity();

        foreach (var zone in activeZones.ToList())
        {
            GameLocation? location = Game1.getLocationFromName(zone.LocationName);
            if (location is not null)
            {
                DamagePlayersInZone(location, zone, config.BombDamageInvincibilitySeconds);
                DamageMonstersInZone(location, zone, config.BombDamageInvincibilitySeconds);
            }

            zone.TicksLeft--;
            if (zone.TicksLeft <= 0)
                activeZones.Remove(zone);
        }
    }

    // ----------------------------
    // 全消去
    // ----------------------------
    public void Clear()
    {
        activeZones.Clear();
        playerImmuneUntilTicks.Clear();
        monsterImmuneUntilTicks.Clear();
        currentTick = 0;
    }

    // ----------------------------
    // プレイヤーへダメージ
    // ----------------------------
    private void DamagePlayersInZone(
        GameLocation location,
        ActiveDamageZone zone,
        float invincibilitySeconds
    )
    {
        if (zone.PlayerDamage <= 0)
            return;

        foreach (Farmer farmer in Game1.getAllFarmers())
        {
            if (farmer.currentLocation?.Name != location.Name)
                continue;

            if (farmer.health <= 0)
                continue;

            if (zone.HitPlayerIds.Contains(farmer.UniqueMultiplayerID))
                continue;

            if (IsPlayerBombDamageImmune(farmer.UniqueMultiplayerID))
                continue;

            if (!IsCharacterHitByExplosion(farmer, zone.AffectedTiles))
                continue;

            farmer.takeDamage(zone.PlayerDamage, true, null);

            zone.HitPlayerIds.Add(farmer.UniqueMultiplayerID);
            playerImmuneUntilTicks[farmer.UniqueMultiplayerID] =
                currentTick + SecondsToTicks(invincibilitySeconds);
        }
    }

    // ----------------------------
    // モンスターへダメージ
    // ----------------------------
    private void DamageMonstersInZone(
        GameLocation location,
        ActiveDamageZone zone,
        float invincibilitySeconds
    )
    {
        if (zone.MonsterDamage <= 0)
            return;

        foreach (Monster monster in location.characters.OfType<Monster>().ToList())
        {
            if (zone.HitMonsters.Contains(monster))
                continue;

            if (IsMonsterBombDamageImmune(monster))
                continue;

            if (!IsCharacterHitByExplosion(monster, zone.AffectedTiles))
                continue;

            location.damageMonster(
                areaOfEffect: monster.GetBoundingBox(),
                minDamage: zone.MonsterDamage,
                maxDamage: zone.MonsterDamage,
                isBomb: true,
                knockBackModifier: 1f,
                addedPrecision: 0,
                critChance: 0f,
                critMultiplier: 1f,
                triggerMonsterInvincibleTimer: true,
                who: zone.SourceFarmer
            );

            zone.HitMonsters.Add(monster);
            monsterImmuneUntilTicks[monster] =
                currentTick + SecondsToTicks(invincibilitySeconds);
        }
    }

    // ----------------------------
    // キャラが爆風タイルに重なっているか
    // ----------------------------
    private static bool IsCharacterHitByExplosion(Character character, IReadOnlyList<Vector2> affectedTiles)
    {
        Rectangle characterBox = character.GetBoundingBox();

        foreach (Vector2 tile in affectedTiles)
        {
            Rectangle tileRect = new(
                (int)tile.X * Game1.tileSize,
                (int)tile.Y * Game1.tileSize,
                Game1.tileSize,
                Game1.tileSize
            );

            if (characterBox.Intersects(tileRect))
                return true;
        }

        return false;
    }

    // ----------------------------
    // プレイヤーが爆弾ダメージ無敵中か
    // ----------------------------
    private bool IsPlayerBombDamageImmune(long playerId)
    {
        return playerImmuneUntilTicks.TryGetValue(playerId, out long immuneUntil)
            && immuneUntil > currentTick;
    }

    // ----------------------------
    // モンスターが爆弾ダメージ無敵中か
    // ----------------------------
    private bool IsMonsterBombDamageImmune(Character monster)
    {
        return monsterImmuneUntilTicks.TryGetValue(monster, out long immuneUntil)
            && immuneUntil > currentTick;
    }

    // ----------------------------
    // 期限切れの無敵情報を掃除
    // ----------------------------
    private void CleanupExpiredImmunity()
    {
        foreach (long playerId in playerImmuneUntilTicks
                     .Where(pair => pair.Value <= currentTick)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            playerImmuneUntilTicks.Remove(playerId);
        }

        foreach (Character monster in monsterImmuneUntilTicks
                     .Where(pair => pair.Value <= currentTick)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            monsterImmuneUntilTicks.Remove(monster);
        }
    }

    // ----------------------------
    // 秒を tick に変換
    // ----------------------------
    private static int SecondsToTicks(float seconds)
    {
        if (seconds <= 0f)
            return 0;

        return (int)(seconds * 60f);
    }
}