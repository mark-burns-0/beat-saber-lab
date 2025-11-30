using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class BeatSaberCubeGenerator : MonoBehaviour
{
    [Header("References")]
    public GameObject redCubePrefab;
    public GameObject blueCubePrefab;
    public GameObject obstaclePrefab;
    public Transform cubesParent;
    public Transform obstaclesParent;
    public AudioSource musicPlayer;
    public BeatSaberGameManager gameManager;

    [Header("Game Settings")]
    public float approachSpeed = 2f;
    public float noteSpawnDistance = 10f;
    public float noteDestroyDistance = 2f;
    public float songStartDelay = 1f;
    public float audioOffset = 0f;

    [Header("Arena Settings")]
    public float laneWidth = 1f;
    public float layerHeight = 0.5f;
    public int lanesCount = 4;
    public int layersCount = 3;

    [Header("Scoring")]
    public int score = 0;
    public int combo = 0;
    public int maxCombo = 0;
    public int cubesSliced = 0;
    public int cubesMissed = 0;
    public int baseScore = 100;
    public int comboMultiplier = 10;

    private BeatSaberMapParser.BeatSaberMap currentMap;
    private List<GameObject> activeCubes = new List<GameObject>();
    private List<GameObject> activeObstacles = new List<GameObject>();
    private List<CubeController> registeredCubes = new List<CubeController>();
    private float songTime = 0f;
    private bool isPlaying = false;
    private Coroutine spawnCoroutine;
    private bool mapLoaded = false;

    // События для UI и визуальной обратной связи
    public System.Action<int> OnScoreChanged;
    public System.Action<int> OnComboChanged;
    public System.Action<int> OnCubeSlicedEvent;
    public System.Action<int> OnCubeMissedEvent;

    void Update()
    {
        if (!isPlaying || !mapLoaded) return;

        // Синхронизируем время с аудио
        if (musicPlayer != null && musicPlayer.clip != null && musicPlayer.isPlaying)
        {
            songTime = musicPlayer.time - audioOffset;
        }
        else
        {
            songTime += Time.deltaTime;
        }

        UpdateCubesPosition();
        CleanupPassedObjects();
    }

    public void LoadAndPlayMap(string jsonPath, AudioClip musicClip = null)
    {
        currentMap = BeatSaberMapParser.ParseMap(jsonPath);

        if (currentMap != null)
        {
            mapLoaded = true;

            if (musicClip != null && musicPlayer != null)
            {
                musicPlayer.clip = musicClip;
            }

            StartSong();
        }
        else
        {
            Debug.LogError("Failed to load map");
            mapLoaded = false;
        }
    }

    private void StartSong()
    {
        songTime = -songStartDelay;
        isPlaying = true;

        // Сброс статистики
        score = 0;
        combo = 0;
        maxCombo = 0;
        cubesSliced = 0;
        cubesMissed = 0;

        ClearAllObjects();

        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnCubesCoroutine());

        if (musicPlayer != null && musicPlayer.clip != null)
        {
            musicPlayer.PlayDelayed(songStartDelay);
            Debug.Log($"Audio started: {musicPlayer.clip.name}, length: {musicPlayer.clip.length}s");
        }

        Debug.Log("Song started!");

        // Уведомляем о сбросе счета
        OnScoreChanged?.Invoke(score);
        OnComboChanged?.Invoke(combo);
    }

    private IEnumerator SpawnCubesCoroutine()
    {
        if (currentMap._notes == null) yield break;

        currentMap._notes.Sort((a, b) => a._time.CompareTo(b._time));

        int noteIndex = 0;
        Debug.Log($"Total notes to spawn: {currentMap._notes.Count}");

        while (isPlaying && noteIndex < currentMap._notes.Count)
        {
            float futureTime = songTime + (noteSpawnDistance / approachSpeed);

            while (noteIndex < currentMap._notes.Count &&
                   currentMap._notes[noteIndex]._time <= futureTime)
            {
                if (!IsNoteSpawned(currentMap._notes[noteIndex]))
                {
                    SpawnCube(currentMap._notes[noteIndex]);
                }
                noteIndex++;
            }

            yield return new WaitForSeconds(0.05f);
        }

        Debug.Log("Finished spawning all notes");
    }

    private bool IsNoteSpawned(BeatSaberMapParser.Note note)
    {
        foreach (var cube in activeCubes)
        {
            if (cube != null)
            {
                CubeController controller = cube.GetComponent<CubeController>();
                if (controller != null && controller.NoteData.Equals(note))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void SpawnCube(BeatSaberMapParser.Note note)
    {
        Vector3 spawnPosition = CalculateCubePosition(note);
        GameObject cubePrefab = note._type == 0 ? redCubePrefab : blueCubePrefab;

        if (cubePrefab == null)
        {
            Debug.LogError("Cube prefab is null!");
            return;
        }

        GameObject cube = Instantiate(cubePrefab, spawnPosition, Quaternion.identity, cubesParent);
        cube.transform.localScale = new Vector3(40, 40, 40);

        // Проверяем или создаем CubeController
        CubeController controller = cube.GetComponent<CubeController>();
        if (controller == null)
        {
            controller = cube.AddComponent<CubeController>();
            Debug.LogWarning($"CubeController не найден на префабе {cube.name}. Добавлен автоматически.");
        }
        else
        {
            Debug.Log($"CubeController найден на префабе {cube.name}.");
        }

        // Инициализация
        controller.Initialize(note, approachSpeed);
        controller.SetCutDirection(note._cutDirection);
        RegisterCube(controller);

        activeCubes.Add(cube);

        Debug.Log($"✓ Cube spawned: {cube.name} at {spawnPosition}, type: {(note._type == 0 ? "Red" : "Blue")}, cutDirection: {note._cutDirection}, songTime: {songTime:F2}");
    }

    public void RegisterCube(CubeController cube)
    {
        if (!registeredCubes.Contains(cube))
        {
            registeredCubes.Add(cube);
            cube.OnCubeSliced += OnCubeSlicedHandler;
        }
    }

    private void OnCubeSlicedHandler(CubeController cube, Vector3 position, Vector3 direction)
    {
        // Вычисляем очки
        int sliceScore = CalculateSliceScore(cube, direction);
        score += sliceScore;

        // Обновляем комбо
        combo++;
        maxCombo = Mathf.Max(maxCombo, combo);
        cubesSliced++;

        // Убираем куб из списков
        if (activeCubes.Contains(cube.gameObject))
        {
            activeCubes.Remove(cube.gameObject);
        }
        registeredCubes.Remove(cube);

        // Уведомляем UI
        OnScoreChanged?.Invoke(score);
        OnComboChanged?.Invoke(combo);
        OnCubeSlicedEvent?.Invoke(cubesSliced);

        Debug.Log($"Cube sliced! Score: +{sliceScore}, Combo: {combo}, Total: {score}");
    }

    // Переименован метод чтобы избежать конфликта с событием
    public void HandleCubeMissed(CubeController cube)
    {
        cubesMissed++;
        combo = 0; // Сбрасываем комбо при промахе

        if (registeredCubes.Contains(cube))
        {
            registeredCubes.Remove(cube);
        }

        // Уведомляем UI
        OnComboChanged?.Invoke(combo);
        OnCubeMissedEvent?.Invoke(cubesMissed);

        Debug.Log($"Cube missed! Combo reset. Total missed: {cubesMissed}");
    }

    private int CalculateSliceScore(CubeController cube, Vector3 sliceDirection)
    {
        int calculatedScore = baseScore;

        // Бонус за точность направления
        if (cube.cutDirection != Vector3.zero)
        {
            float accuracy = Vector3.Dot(sliceDirection.normalized, cube.cutDirection.normalized);
            calculatedScore += Mathf.RoundToInt(calculatedScore * accuracy);
        }

        // Бонус за комбо
        calculatedScore += combo * comboMultiplier;

        return calculatedScore;
    }

    private Vector3 CalculateCubePosition(BeatSaberMapParser.Note note)
    {
        // X: от -0.75 до 0.75 (4 дорожки)
        float x = 0f;
        switch (note._lineIndex)
        {
            case 0: x = -0.75f; break;  // Левая дорожка (-0.75)
            case 1: x = -0.25f; break;  // Центр-лево (-0.25)
            case 2: x = 0.25f; break;   // Центр-право (0.25)
            case 3: x = 0.75f; break;   // Правая дорожка (0.75)
        }

        // Y: 1 до 3 (3 слоя)
        float y = 0f;
        switch (note._lineLayer)
        {
            case 0: y = 0f; break;   // Нижний слой (1)
            case 1: y = 0.5f; break;   // Средний слой (2)  
            case 2: y = 1.5f; break;   // Верхний слой (3)
        }

        // Z: 10 - точка появления (кубы летят от +10 к 0)
        float z = 10f;

        Vector3 position = new Vector3(x, y, z);
        return position;
    }

    private void UpdateCubesPosition()
    {
        for (int i = activeCubes.Count - 1; i >= 0; i--)
        {
            if (activeCubes[i] == null)
            {
                activeCubes.RemoveAt(i);
                continue;
            }

            CubeController controller = activeCubes[i].GetComponent<CubeController>();
            if (controller != null)
            {
                controller.UpdatePosition(songTime);
            }
            else
            {
                Debug.LogWarning($"CubeController missing on {activeCubes[i].name}");
            }
        }
    }


    private void CleanupPassedObjects()
    {
        for (int i = activeCubes.Count - 1; i >= 0; i--)
        {
            GameObject obj = activeCubes[i];
            if (obj != null)
            {
                Debug.Log($"  {obj.name} Z = {obj.transform.position.z}");
                if (obj.transform.position.z < noteDestroyDistance)
                {
                    Debug.Log($"test");
                    CubeController controller = obj.GetComponent<CubeController>();
                    if (controller != null)
                    {
                        HandleCubeMissed(controller);
                        controller.OnMiss();
                    }
                    else
                    {
                        Destroy(obj);
                    }
                    activeCubes.RemoveAt(i);
                }
            }
        }
    }


    private void ClearAllObjects()
    {
        foreach (GameObject cube in activeCubes)
        {
            if (cube != null) Destroy(cube);
        }
        foreach (GameObject obstacle in activeObstacles)
        {
            if (obstacle != null) Destroy(obstacle);
        }

        activeCubes.Clear();
        activeObstacles.Clear();
        registeredCubes.Clear();
    }

    public void StopSong()
    {
        isPlaying = false;
        mapLoaded = false;
        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        if (musicPlayer != null) musicPlayer.Stop();
        ClearAllObjects();
    }

    public void PauseSong()
    {
        isPlaying = false;
        if (musicPlayer != null) musicPlayer.Pause();
    }

    public void ResumeSong()
    {
        isPlaying = true;
        if (musicPlayer != null) musicPlayer.UnPause();
    }

    // Методы для управления из UI
    public void PlayTestMap()
    {
        LoadAndPlayMap("test_map.json");
    }

    public void SetApproachSpeed(float speed)
    {
        approachSpeed = speed;
    }

    public void SetAudioOffset(float offset)
    {
        audioOffset = offset;
    }

    // Геттеры для информации о текущем состоянии
    public int GetActiveCubesCount()
    {
        return activeCubes.Count;
    }

    public float GetSongProgress()
    {
        if (musicPlayer != null && musicPlayer.clip != null)
        {
            return musicPlayer.time / musicPlayer.clip.length;
        }
        return 0f;
    }

    public bool IsSongPlaying()
    {
        return isPlaying && mapLoaded;
    }

    public void ResetGameStats()
    {
        score = 0;
        combo = 0;
        maxCombo = 0;
        cubesSliced = 0;
        cubesMissed = 0;
        registeredCubes.Clear();

        OnScoreChanged?.Invoke(score);
        OnComboChanged?.Invoke(combo);
    }

    void OnGUI()
    {
        if (mapLoaded)
        {
            GUI.Box(new Rect(10, 10, 300, 160), "Beat Saber Clone");
            GUI.Label(new Rect(20, 40, 280, 20), $"Notes: {activeCubes.Count}");
            GUI.Label(new Rect(20, 60, 280, 20), $"Song Time: {songTime:F2}s");
            GUI.Label(new Rect(20, 80, 280, 20), isPlaying ? "Status: Playing" : "Status: Paused");
            GUI.Label(new Rect(20, 100, 280, 20), $"Score: {score}");
            GUI.Label(new Rect(20, 120, 280, 20), $"Combo: {combo} (Max: {maxCombo})");
            GUI.Label(new Rect(20, 140, 280, 20), $"Sliced: {cubesSliced} Missed: {cubesMissed}");
        }
        else
        {
            GUI.Box(new Rect(10, 10, 200, 50), "Beat Saber Clone");
            GUI.Label(new Rect(20, 35, 180, 20), "No map loaded");
        }
    }
}