using System;
using UnityEngine;

public interface IGameplayInputActions
{
    Vector2 Movement { get; }
    Vector2 Look { get; }
    bool FirePressed { get; }
    bool JumpHeld { get; }
    bool BrakeHeld { get; }
    bool UltimatePressed { get; }
    bool PausePressed { get; }
    bool SubmitPressed { get; }
    bool PointerGesturePressed { get; }
}

public static class GameplayInputActions
{
    private static IGameplayInputActions current = new DesktopInput();

    public static IGameplayInputActions Current => current;

    public static void Configure(IGameplayInputActions inputActions)
    {
        current = inputActions ?? throw new ArgumentNullException(nameof(inputActions));
    }
}
