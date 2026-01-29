using UnityEngine;

public class RobotFootstepAudio : MonoBehaviour
{
    [SerializeField] private float volume = 1f;

    // Call this from an Animation Event at foot contact.
    public void PlayFootstep()
    {
        AudioManager.Instance?.PlayFootstep(volume);
    }
}
