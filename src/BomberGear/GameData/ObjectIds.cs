// ----------------------------
// Stardew Valley 側の object ID 管理
// - 爆風破壊判定
// - ドロップ分類判定
// などで共通利用する
// - ID優先で判定しつつ、未知IDは Name / DisplayName / Description でも補助判定する
// ----------------------------
using System;
using System.Collections.Generic;

namespace BomberGear.GameData;

internal static class ObjectIds
{
    // ----------------------------
    // 発掘ポイント
    // ----------------------------
    public const string ArtifactSpot = "590";

    // ----------------------------
    // 枝
    // ----------------------------
    public const string TwigA = "294";
    public const string TwigB = "295";

    // ----------------------------
    // 通常石
    // - 石そのもの
    // - 見た目は石だが通常石扱いしたいもの
    // ----------------------------
    public const string Stone2 = "2";
    public const string Stone4 = "4";
    public const string Stone75 = "75";
    public const string Stone76 = "76";
    public const string Stone77 = "77";
    public const string Stone290 = "290";
    public const string Stone343 = "343";
    public const string Stone390 = "390";
    public const string Stone450 = "450";
    public const string Stone760 = "760";
    public const string Stone762 = "762";

    // ----------------------------
    // 鉱石入り石
    // - 石ドロップとは分けて扱いたいもの
    // ----------------------------
    public const string Stone668 = "668";
    public const string Stone670 = "670";
    public const string Stone751 = "751";
    public const string Stone764 = "764";
    public const string Stone765 = "765";

    // ----------------------------
    // 雑草系 Object
    // ----------------------------
    public const string Weeds0 = "0";
    public const string Weeds313 = "313";
    public const string Weeds314 = "314";
    public const string Weeds315 = "315";
    public const string Weeds316 = "316";
    public const string Weeds317 = "317";
    public const string Weeds318 = "318";
    public const string Weeds319 = "319";
    public const string Weeds320 = "320";
    public const string Weeds321 = "321";
    public const string Weeds452 = "452";
    public const string Weeds674 = "674";
    public const string Weeds675 = "675";
    public const string Weeds676 = "676";
    public const string Weeds677 = "677";
    public const string Weeds678 = "678";
    public const string Weeds679 = "679";
    public const string Weeds750 = "750";
    public const string Weeds784 = "784";
    public const string Weeds785 = "785";
    public const string Weeds786 = "786";
    public const string Weeds792 = "792";
    public const string Weeds793 = "793";
    public const string Weeds794 = "794";

    // ----------------------------
    // 集合定義
    // ----------------------------
    private static readonly HashSet<string> ArtifactSpotIds = new()
    {
        ArtifactSpot
    };

    private static readonly HashSet<string> TwigIds = new()
    {
        TwigA,
        TwigB
    };

    private static readonly HashSet<string> StoneIds = new()
    {
        Stone2,
        Stone4,
        Stone75,
        Stone76,
        Stone77,
        Stone290,
        Stone343,
        Stone390,
        Stone450,
        Stone760,
        Stone762
    };

    private static readonly HashSet<string> OreStoneIds = new()
    {
        Stone668,
        Stone670,
        Stone751,
        Stone764,
        Stone765
    };

    private static readonly HashSet<string> WeedIds = new()
    {
        Weeds0,
        Weeds313,
        Weeds314,
        Weeds315,
        Weeds316,
        Weeds317,
        Weeds318,
        Weeds319,
        Weeds320,
        Weeds321,
        Weeds452,
        Weeds674,
        Weeds675,
        Weeds676,
        Weeds677,
        Weeds678,
        Weeds679,
        Weeds750,
        Weeds784,
        Weeds785,
        Weeds786,
        Weeds792,
        Weeds793,
        Weeds794
    };

    // ----------------------------
    // 名前ベースの石判定
    // - 部分一致にすると Stone Fence / Stone Floor まで巻き込むので完全一致にする
    // ----------------------------
    private static readonly HashSet<string> StoneNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Stone",
        "Stone Base"
    };

    // ----------------------------
    // 説明文ベースの鉱石入り石判定
    // - 未知IDでも、説明文に ore 系の文言があれば鉱石入り石として扱う
    // ----------------------------
    private static readonly string[] OreStoneDescriptionHints =
    {
        "ore in this stone",
        "stone ore in this stone",
        "copper ore",
        "iron ore",
        "gold ore",
        "iridium ore"
    };

    // ----------------------------
    // 発掘ポイントか
    // ----------------------------
    public static bool IsArtifactSpot(string objectId)
    {
        return ArtifactSpotIds.Contains(objectId);
    }

    // ----------------------------
    // 枝か
    // ----------------------------
    public static bool IsTwig(string objectId)
    {
        return TwigIds.Contains(objectId);
    }

    // ----------------------------
    // 通常石か
    // - 既知IDを優先
    // - 未知IDは名前で補助判定
    // - 鉱石入り石っぽい説明なら通常石にはしない
    // ----------------------------
    public static bool IsStone(string objectId)
    {
        return StoneIds.Contains(objectId);
    }

    public static bool IsStone(string objectId, string? internalName, string? displayName, string? description)
    {
        if (IsStone(objectId))
            return true;

        if (IsOreStone(objectId))
            return false;

        if (!HasStoneName(internalName, displayName))
            return false;

        return !HasOreStoneHint(description);
    }

    // ----------------------------
    // 鉱石入り石か
    // - 既知IDを優先
    // - 未知IDは「石の名前 + oreっぽい説明文」で補助判定
    // ----------------------------
    public static bool IsOreStone(string objectId)
    {
        return OreStoneIds.Contains(objectId);
    }

    public static bool IsOreStone(string objectId, string? internalName, string? displayName, string? description)
    {
        if (IsOreStone(objectId))
            return true;

        if (!HasStoneName(internalName, displayName))
            return false;

        return HasOreStoneHint(description);
    }

    // ----------------------------
    // 石系か
    // - 通常石 + 鉱石入り石
    // ----------------------------
    public static bool IsStoneLike(string objectId)
    {
        return IsStone(objectId) || IsOreStone(objectId);
    }

    public static bool IsStoneLike(string objectId, string? internalName, string? displayName, string? description)
    {
        return IsStone(objectId, internalName, displayName, description)
            || IsOreStone(objectId, internalName, displayName, description);
    }

    // ----------------------------
    // 雑草系 Object か
    // ----------------------------
    public static bool IsWeedLike(string objectId)
    {
        return WeedIds.Contains(objectId);
    }

    // ----------------------------
    // ItemId / QualifiedItemId から object ID を取り出す
    // - ItemId があればそれを優先
    // ----------------------------
    public static string ExtractObjectId(string? itemId, string? qualifiedItemId)
    {
        if (!string.IsNullOrWhiteSpace(itemId))
            return itemId;

        if (string.IsNullOrWhiteSpace(qualifiedItemId))
            return string.Empty;

        const string objectPrefix = "(O)";
        if (qualifiedItemId.StartsWith(objectPrefix, StringComparison.Ordinal))
            return qualifiedItemId.Substring(objectPrefix.Length);

        return qualifiedItemId;
    }

    // ----------------------------
    // 石の名前か
    // - internalName と displayName の両方を見る
    // ----------------------------
    private static bool HasStoneName(string? internalName, string? displayName)
    {
        string normalizedInternalName = NormalizeText(internalName);
        string normalizedDisplayName = NormalizeText(displayName);

        return StoneNames.Contains(normalizedInternalName)
            || StoneNames.Contains(normalizedDisplayName);
    }

    // ----------------------------
    // 鉱石入り石っぽい説明か
    // ----------------------------
    private static bool HasOreStoneHint(string? description)
    {
        string normalizedDescription = NormalizeText(description);
        if (normalizedDescription.Length == 0)
            return false;

        foreach (string hint in OreStoneDescriptionHints)
        {
            if (normalizedDescription.Contains(hint, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    // ----------------------------
    // 文字列の前後空白を除去
    // ----------------------------
    private static string NormalizeText(string? text)
    {
        return text?.Trim() ?? string.Empty;
    }
}