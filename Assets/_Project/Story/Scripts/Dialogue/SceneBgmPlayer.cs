using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class SceneBgmPlayer : MonoBehaviour
{
    [SerializeField] private string resourcePath = "StoryAudio/BGM/4. Frostbound";
    [SerializeField] [Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private bool loop = true;
    [SerializeField] private bool playOnStart = true;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = loop;
        audioSource.volume = volume;
    }

    private void Start()
    {
        if (!playOnStart)
        {
            return;
        }

        Play();
    }

    public void Play()
    {
        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null)
        {
            Debug.LogWarning($"SceneBgmPlayer: 未找到 Resources/{resourcePath}", this);
            return;
        }

        audioSource.clip = clip;
        audioSource.loop = loop;
        audioSource.volume = volume;
        audioSource.Play();
    }
}
