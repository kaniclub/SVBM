// ----------------------------
// Generic Mod Config Menu API
// ----------------------------
using System;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace BomberGear.Config;

public interface IGenericModConfigMenuApi
{
    // ----------------------------
    // MODをGMCMへ登録
    // ----------------------------
    void Register(
        IManifest mod,
        Action reset,
        Action save,
        bool titleScreenOnly = false
    );

    // ----------------------------
    // キー設定
    // ----------------------------
    void AddKeybindList(
        IManifest mod,
        Func<KeybindList> getValue,
        Action<KeybindList> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        string? fieldId = null
    );

    // ----------------------------
    // ON/OFF 項目
    // ----------------------------
    void AddBoolOption(
        IManifest mod,
        Func<bool> getValue,
        Action<bool> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        string? fieldId = null
    );

    // ----------------------------
    // 数値項目（int）
    // ----------------------------
    void AddNumberOption(
        IManifest mod,
        Func<int> getValue,
        Action<int> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        int? min = null,
        int? max = null,
        int? interval = null,
        Func<int, string>? formatValue = null,
        string? fieldId = null
    );

    // ----------------------------
    // 数値項目（float）
    // ----------------------------
    void AddNumberOption(
        IManifest mod,
        Func<float> getValue,
        Action<float> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        float? min = null,
        float? max = null,
        float? interval = null,
        Func<float, string>? formatValue = null,
        string? fieldId = null
    );
}