using TMPro;
using UnityEngine;

public static class RobotArenaUiFont
{
    private const string ResourcePath = "RobotArenaUiFont";

    private static TMP_FontAsset cachedFont;

    public static TMP_FontAsset Get()
    {
        if (cachedFont != null)
        {
            return cachedFont;
        }

        RobotArenaUiFontLibrary library = Resources.Load<RobotArenaUiFontLibrary>(ResourcePath);
        TMP_FontAsset font = library == null ? null : library.Font;
        if (font == null)
        {
            Debug.LogError("Robot Arena UI font resource is missing or incomplete.");
            return null;
        }

        cachedFont = font;
        return font;
    }
}
