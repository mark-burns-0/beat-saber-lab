using UnityEngine;
using System.IO;
using System.Collections;
using UnityEngine.Networking;

public class BeatSaberGameManager : MonoBehaviour
{
    [Header("References")]
    public BeatSaberCubeGenerator cubeGenerator;
    public AudioSource musicPlayer;

    [Header("Map Settings")]
    public string mapsFolder = "Beatmaps";
    public string songFolder = "ExampleSong";
    public string mapFileName = "ExpertStandard.dat";
    
    [Header("Audio Settings")]
    public bool autoFindAudio = true;
    public string audioFileName = "song"; // Без расширения

    private string songPath;
    private AudioClip loadedAudioClip;

    void Start()
    {
        LoadAndStartGame();
    }

    private void LoadAndStartGame()
    {
        songPath = Path.Combine(Application.streamingAssetsPath, mapsFolder, songFolder);
        string mapPath = Path.Combine(songPath, mapFileName);
        
        Debug.Log($"Loading map from: {mapPath}");
        
        if (File.Exists(mapPath))
        {
            StartCoroutine(LoadMapWithAudio(mapPath));
        }
        else
        {
            Debug.LogError("Map file not found: " + mapPath);
            // Создаем тестовую карту для демонстрации
            CreateTestMap();
        }
    }

    private IEnumerator LoadMapWithAudio(string mapPath)
    {
        // Загружаем карту
        var map = BeatSaberMapParser.ParseMap(mapPath);
        
        if (map == null)
        {
            Debug.LogError("Failed to parse map");
            yield break;
        }

        // Загружаем аудио
        yield return StartCoroutine(LoadAudioClip());

        if (loadedAudioClip != null)
        {
            // Запускаем игру
            cubeGenerator.LoadAndPlayMap(mapPath, loadedAudioClip);
            Debug.Log("Game started successfully with audio!");
        }
        else
        {
            Debug.LogWarning("Starting without audio");
            cubeGenerator.LoadAndPlayMap(mapPath, null);
        }
    }

    private IEnumerator LoadAudioClip()
    {
        string[] supportedFormats = { ".ogg", ".wav", ".mp3" };
        
        foreach (string format in supportedFormats)
        {
            string audioPath = Path.Combine(songPath, audioFileName + format);
            
            if (File.Exists(audioPath))
            {
                Debug.Log($"Found audio file: {audioPath}");
                yield return StartCoroutine(LoadAudioFile(audioPath));
                
                if (loadedAudioClip != null)
                {
                    yield break; // Успешно загрузили
                }
            }
        }

        // Если не нашли по имени, ищем любой аудиофайл
        if (autoFindAudio)
        {
            yield return StartCoroutine(FindAndLoadAnyAudio());
        }
    }

    private IEnumerator FindAndLoadAnyAudio()
    {
        string[] supportedExtensions = { "*.ogg", "*.wav", "*.mp3" };
        
        foreach (string extension in supportedExtensions)
        {
            string[] audioFiles = Directory.GetFiles(songPath, extension);
            
            if (audioFiles.Length > 0)
            {
                string audioPath = audioFiles[0];
                Debug.Log($"Auto-found audio file: {audioPath}");
                yield return StartCoroutine(LoadAudioFile(audioPath));
                
                if (loadedAudioClip != null)
                {
                    yield break;
                }
            }
        }
        
        Debug.LogError("No audio files found in: " + songPath);
    }

    private IEnumerator LoadAudioFile(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();
        AudioType audioType = GetAudioType(extension);

        if (audioType == AudioType.UNKNOWN)
        {
            Debug.LogError($"Unsupported audio format: {extension}");
            yield break;
        }

        string url = "file://" + filePath;
        
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                loadedAudioClip = DownloadHandlerAudioClip.GetContent(www);
                loadedAudioClip.name = Path.GetFileName(filePath);
                Debug.Log($"Audio loaded successfully: {loadedAudioClip.name}, length: {loadedAudioClip.length}s");
            }
            else
            {
                Debug.LogError($"Failed to load audio: {www.error}");
            }
        }
    }

    private AudioType GetAudioType(string extension)
    {
        switch (extension)
        {
            case ".ogg": return AudioType.OGGVORBIS;
            case ".wav": return AudioType.WAV;
            case ".mp3": return AudioType.MPEG;
            default: return AudioType.UNKNOWN;
        }
    }

    void Update()
    {
        // Управление паузой
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Time.timeScale == 0)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        // Перезапуск игры
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartGame();
        }

        // Пропуск вперед (для тестирования)
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            SkipForward(10f);
        }
    }

    public void PauseGame()
    {
        Time.timeScale = 0;
        cubeGenerator.PauseSong();
        Debug.Log("Game Paused");
    }

    public void ResumeGame()
    {
        Time.timeScale = 1;
        cubeGenerator.ResumeSong();
        Debug.Log("Game Resumed");
    }

    public void RestartGame()
    {
        Debug.Log("Restarting Game...");
        
        // Очищаем загруженное аудио
        if (loadedAudioClip != null)
        {
            Destroy(loadedAudioClip);
            loadedAudioClip = null;
        }
        
        // Перезагружаем сцену или инициализацию
        LoadAndStartGame();
    }

    private void SkipForward(float seconds)
    {
        if (musicPlayer != null && musicPlayer.isPlaying)
        {
            musicPlayer.time += seconds;
            Debug.Log($"Skipped forward to: {musicPlayer.time}s");
        }
    }

    private void CreateTestMap()
    {
        Debug.Log("Creating test map for demonstration...");
        // Здесь можно создать простую тестовую карту
        // если основная карта не загрузилась
    }

    void OnDestroy()
    {
        // Очистка ресурсов
        if (loadedAudioClip != null)
        {
            Destroy(loadedAudioClip);
        }
    }
}