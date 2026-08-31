using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "RobotArenaUiFont", menuName = "Robot Arena/UI Font Library")]
public sealed class RobotArenaUiFontLibrary : ScriptableObject
{
    [SerializeField]
    private TMP_FontAsset font;

    public TMP_FontAsset Font => font;
}
