using UnityEngine;

[CreateAssetMenu(fileName = "RobotArenaMusic", menuName = "Robot Arena/Music Library")]
public sealed class MusicLibrary : ScriptableObject
{
    [SerializeField]
    private AudioClip calmIntro;

    [SerializeField]
    private AudioClip calmLoop;

    [SerializeField]
    private AudioClip combatIntro;

    [SerializeField]
    private AudioClip combatLoop;

    [SerializeField]
    private AudioClip death;

    public AudioClip CalmIntro => calmIntro;
    public AudioClip CalmLoop => calmLoop;
    public AudioClip CombatIntro => combatIntro;
    public AudioClip CombatLoop => combatLoop;
    public AudioClip Death => death;
}
