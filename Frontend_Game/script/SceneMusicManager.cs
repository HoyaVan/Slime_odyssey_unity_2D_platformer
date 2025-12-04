using UnityEngine;

/// <summary>
/// Simple wrapper component for scene-specific background music.
/// Add this to any scene GameObject and assign the music clip.
/// This component automatically calls AudioPlayer to play the music when the scene loads.
/// </summary>
public class SceneMusicManager : MonoBehaviour
{
    [Header("Background Music")]
    [SerializeField] AudioClip backgroundMusic;
    [SerializeField] bool loopMusic = true;

    void Start()
    {
        if (AudioPlayer.Instance != null && backgroundMusic != null)
        {
            // AudioPlayer controls volume - SceneMusicManager only specifies which music to play
            AudioPlayer.Instance.PlayMusic(backgroundMusic, loopMusic);
        }
        else if (backgroundMusic == null)
        {
            Debug.LogWarning($"SceneMusicManager: No background music assigned for scene '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'");
        }
    }
}

