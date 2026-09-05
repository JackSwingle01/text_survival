using Raylib_cs;

namespace text_survival.Desktop.Audio;

/// <summary>
/// Static manager for background music and audio.
/// Gracefully handles missing audio files.
/// </summary>
public static class AudioManager
{
    private static Music _backgroundMusic;
    private static bool _musicLoaded;
    private static bool _musicPlaying;
    private static bool _muted;

    public static bool IsMuted => _muted;

    private static readonly string[] MusicPaths =
    [
        "Assets/Audio/Music/background.ogg",
        "Assets/Audio/Music/background.mp3",
        "Assets/Audio/Music/background.wav"
    ];

    /// <summary>
    /// Initialize the audio manager and load music if available.
    /// Call after Raylib.InitAudioDevice().
    /// </summary>
    public static void Initialize()
    {
        string? musicPath = MusicPaths.FirstOrDefault(File.Exists);

        if (musicPath == null)
        {
            Console.WriteLine("[AudioManager] No music file found. Checked:");
            foreach (var path in MusicPaths)
                Console.WriteLine($"  - {path}");
            Console.WriteLine("[AudioManager] Game will run without background music.");
            _musicLoaded = false;
            return;
        }

        try
        {
            _backgroundMusic = Raylib.LoadMusicStream(musicPath);
            _musicLoaded = true;
            Console.WriteLine($"[AudioManager] Loaded: {musicPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AudioManager] Failed to load music: {ex.Message}");
            _musicLoaded = false;
        }
    }

    /// <summary>
    /// Start playing background music (loops automatically).
    /// </summary>
    public static void PlayMusic()
    {
        if (!_musicLoaded || _musicPlaying) return;

        Raylib.PlayMusicStream(_backgroundMusic);
        Raylib.SetMusicVolume(_backgroundMusic, _muted ? 0f : 1f);
        _musicPlaying = true;
    }

    /// <summary>
    /// Update music stream - must be called every frame for streaming to work.
    /// </summary>
    public static void Update()
    {
        if (!_musicLoaded || !_musicPlaying) return;

        Raylib.UpdateMusicStream(_backgroundMusic);
    }

    /// <summary>
    /// Toggle mute. Music keeps playing silently while muted so it stays in sync.
    /// </summary>
    public static void ToggleMute()
    {
        _muted = !_muted;
        if (_musicLoaded)
            Raylib.SetMusicVolume(_backgroundMusic, _muted ? 0f : 1f);
    }

    /// <summary>
    /// Clean up audio resources.
    /// Call before Raylib.CloseAudioDevice().
    /// </summary>
    public static void Shutdown()
    {
        if (_musicPlaying)
        {
            Raylib.StopMusicStream(_backgroundMusic);
            _musicPlaying = false;
        }

        if (_musicLoaded)
        {
            Raylib.UnloadMusicStream(_backgroundMusic);
            _musicLoaded = false;
        }
    }
}
