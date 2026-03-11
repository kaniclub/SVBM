// ----------------------------
// ゲーム設定に追従した入力判定
// ----------------------------
using StardewModdingAPI;

namespace BomberGear.Input;

internal static class InputRouter
{
    // ----------------------------
    // 左クリック（Use Tool）
    // ----------------------------
    public static bool IsPlaceBomb(SButton button)
        => button.IsUseToolButton();

    // ----------------------------
    // 右クリック（Action）
    // ----------------------------
    public static bool IsSpecial(SButton button)
        => button.IsActionButton();
}