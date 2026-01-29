using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] private AudioClip uiClickClip;
    [SerializeField] private AudioClip menuMusicClip;
    [SerializeField] private AudioClip gameplayMusicClip;
    [SerializeField] private AudioClip footstepClip;

    [Header("Sources")]
    [SerializeField] private AudioSource musicSourceA;
    [SerializeField] private AudioSource musicSourceB;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource uiSource;

    private AudioSource activeMusicSource;
    private Coroutine musicFadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (musicSourceA == null) musicSourceA = gameObject.AddComponent<AudioSource>();
        if (musicSourceB == null) musicSourceB = gameObject.AddComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        if (uiSource == null) uiSource = gameObject.AddComponent<AudioSource>();

        musicSourceA.loop = true;
        musicSourceB.loop = true;

        activeMusicSource = musicSourceA;
    }

    public void PlayUI(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        uiSource.PlayOneShot(clip, volume);
    }

    public void PlayUIClick(float volume = 1f)
    {
        PlayUI(uiClickClip, volume);
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, volume);
    }

    public void PlayFootstep(float volume = 1f)
    {
        PlaySFX(footstepClip, volume);
    }

    public void PlayMusic(AudioClip clip, float fadeTime = 0.5f, float volume = 1f)
    {
        if (clip == null) return;

        if (activeMusicSource.clip == clip && activeMusicSource.isPlaying)
        {
            return;
        }

        if (musicFadeRoutine != null)
        {
            StopCoroutine(musicFadeRoutine);
        }

        musicFadeRoutine = StartCoroutine(FadeToMusic(clip, fadeTime, volume));
    }

    public void PlayMenuMusic(float fadeTime = 0.5f, float volume = 1f)
    {
        PlayMusic(menuMusicClip, fadeTime, volume);
    }

    public void PlayGameplayMusic(float fadeTime = 0.5f, float volume = 1f)
    {
        PlayMusic(gameplayMusicClip, fadeTime, volume);
    }

    public void StopMusic(float fadeTime = 0.5f)
    {
        if (musicFadeRoutine != null)
        {
            StopCoroutine(musicFadeRoutine);
        }

        musicFadeRoutine = StartCoroutine(FadeOut(activeMusicSource, fadeTime));
    }

    private IEnumerator FadeToMusic(AudioClip newClip, float fadeTime, float targetVolume)
    {
        AudioSource nextSource = activeMusicSource == musicSourceA ? musicSourceB : musicSourceA;

        nextSource.clip = newClip;
        nextSource.volume = 0f;
        nextSource.Play();

        float t = 0f;
        float startVolume = activeMusicSource.volume;

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float normalized = fadeTime <= 0f ? 1f : t / fadeTime;

            activeMusicSource.volume = Mathf.Lerp(startVolume, 0f, normalized);
            nextSource.volume = Mathf.Lerp(0f, targetVolume, normalized);

            yield return null;
        }

        activeMusicSource.Stop();
        activeMusicSource.volume = targetVolume;
        activeMusicSource = nextSource;
    }

    private IEnumerator FadeOut(AudioSource source, float fadeTime)
    {
        if (source == null || !source.isPlaying) yield break;

        float startVolume = source.volume;
        float t = 0f;

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float normalized = fadeTime <= 0f ? 1f : t / fadeTime;
            source.volume = Mathf.Lerp(startVolume, 0f, normalized);
            yield return null;
        }

        source.Stop();
        source.volume = startVolume;
    }
}
