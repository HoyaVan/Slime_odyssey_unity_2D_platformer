using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioPlayer : MonoBehaviour
{
    public static AudioPlayer Instance { get; private set; }

    [Header("Music")]
    private AudioSource musicSource;
    [Range(0f, 1f)]
    [SerializeField] float defaultMusicVolume = 1f;

    [Header("SFX")]
    private AudioSource sfxSource;
    [Range(0f, 1f)][SerializeField] float sfxVolume = 1f;

    [Header("SFX Clips")]
    [SerializeField] AudioClip buttonClickSound;          // Button clicks
    [SerializeField] AudioClip coinCollectSound;         // Collecting coins
    [SerializeField] AudioClip jumpSound;                // Jump sounds
    [SerializeField] AudioClip slimeHurtSound;           // Slime hurt sound
    [SerializeField] AudioClip endGameSound;             // Ending game sound

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Get or create AudioSource components
        SetupAudioSources();

        // Subscribe to scene loaded event to stop music when scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void SetupAudioSources()
    {
        // Get all AudioSource components
        AudioSource[] sources = GetComponents<AudioSource>();

        // Find or create music source (first AudioSource)
        if (sources.Length > 0)
        {
            musicSource = sources[0];
        }
        else
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        // Configure music source
        musicSource.playOnAwake = false;
        musicSource.loop = false; // Will be set by PlayMusic()
        musicSource.volume = defaultMusicVolume; // Set initial volume

        // Stop any music that might be playing from previous scene
        if (musicSource.isPlaying)
        {
            musicSource.Stop();
        }

        // Find or create SFX source (second AudioSource, or create new one)
        if (sources.Length > 1)
        {
            sfxSource = sources[1];
        }
        else
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        // Configure SFX source
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Stop music when scene changes (each scene will start its own music)
        StopMusic();
    }

    private void OnValidate()
    {
        // Update music volume if music is currently playing
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.volume = defaultMusicVolume;
        }

        // Note: SFX volume changes will apply to new sounds played, not currently playing ones
        // This is normal behavior since SFX are typically short sounds
    }

    // ---- Music ----
    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (clip == null)
        {
            Debug.LogWarning($"AudioPlayer: Attempted to play null music clip for scene '{SceneManager.GetActiveScene().name}'");
            return;
        }

        // Stop current music before playing new one
        StopMusic();

        musicSource.clip = clip;
        musicSource.volume = defaultMusicVolume; // Always use AudioPlayer's volume setting
        musicSource.loop = loop;
        musicSource.Play();

        Debug.Log($"AudioPlayer: Playing music '{clip.name}' for scene '{SceneManager.GetActiveScene().name}'");
    }

    public void SetMusicVolume(float volume)
    {
        defaultMusicVolume = Mathf.Clamp01(volume);
        if (musicSource != null)
        {
            musicSource.volume = defaultMusicVolume;
        }
    }

    public float GetMusicVolume()
    {
        return defaultMusicVolume;
    }

    public void StopMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Stop();
        }
    }

    // ---- SFX ----
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;

        sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }

    public float GetSFXVolume()
    {
        return sfxVolume;
    }

    // Convenience methods for common SFX
    public void PlayButtonClick() => PlaySFX(buttonClickSound);
    public void PlayCoinCollect() => PlaySFX(coinCollectSound);
    public void PlayJump() => PlaySFX(jumpSound);
    public void PlaySlimeHurt() => PlaySFX(slimeHurtSound);
    public void PlayEndGame() => PlaySFX(endGameSound);
}
