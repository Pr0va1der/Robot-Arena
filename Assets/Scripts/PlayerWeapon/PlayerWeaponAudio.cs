namespace RobotArena.PlayerWeapon
{
    public enum PlayerWeaponAudioCue
    {
        Volley,
        Ultimate
    }

    public interface IPlayerWeaponAudio
    {
        void Play(PlayerWeaponAudioCue cue);
    }
}
