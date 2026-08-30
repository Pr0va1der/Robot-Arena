using UnityEngine;

public sealed class DesktopInput : IGameplayInputActions
{
    public const string MoveHorizontalAction = "Horizontal";
    public const string MoveVerticalAction = "Vertical";
    public const string LookHorizontalAction = "Mouse X";
    public const string LookVerticalAction = "Mouse Y";
    public const string SubmitAction = "Submit";

    public Vector2 Movement => new Vector2(
        Input.GetAxisRaw(MoveHorizontalAction),
        Input.GetAxisRaw(MoveVerticalAction));

    public Vector2 Look => new Vector2(
        Input.GetAxis(LookHorizontalAction),
        Input.GetAxis(LookVerticalAction));

    public bool FirePressed => Input.GetMouseButtonDown(0);
    public bool JumpHeld => Input.GetKey(KeyCode.Space);
    public bool BrakeHeld => Input.GetKey(KeyCode.LeftShift);
    public bool UltimatePressed => Input.GetKeyDown(KeyCode.Q);
    public bool PausePressed => Input.GetKeyDown(KeyCode.Escape);
    public bool SubmitPressed => Input.GetButtonDown(SubmitAction) ||
                                 Input.GetKeyDown(KeyCode.Return) ||
                                 Input.GetKeyDown(KeyCode.KeypadEnter);
    public bool PointerGesturePressed => Input.GetMouseButtonDown(0);
}
