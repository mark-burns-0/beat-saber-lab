using UnityEngine;
using System.Collections;

public class EnvironmentAudioEffects : MonoBehaviour
{
    [Header("Light Sources")]
    public Light directionalLight;
    public Light[] pointLights;
    public Light[] spotLights;
    
    [Header("Environment Objects")]
    public Renderer floorRenderer;
    public Renderer[] wallRenderers;
    public Renderer[] sidePanelRenderers;
    
    [Header("Particle Systems")]
    public ParticleSystem[] backgroundParticles;
    public ParticleSystem[] beatParticles;
    
    [Header("Light Effects")]
    public float maxLightIntensity = 3f;
    public float lightPulseStrength = 2f;
    public Color[] lightColors;
    
    [Header("Material Effects")]
    public float maxEmission = 3f;
    public Color floorEmissionColor = Color.blue;
    public Color wallEmissionColor = Color.red;
    
    [Header("Particle Effects")]
    public float maxParticleRate = 100f;
    public float particleSizeMultiplier = 2f;

    private AudioVisualizer audioVisualizer;
    private float[] originalLightIntensities;
    private Color[] originalLightColors;
    private Color floorOriginalColor;
    private Color[] wallsOriginalColors;

    void Start()
    {
        audioVisualizer = FindObjectOfType<AudioVisualizer>();
        SaveOriginalSettings();
        
        if (audioVisualizer != null)
        {
            audioVisualizer.OnBeatDetected += OnBeatDetected;
            audioVisualizer.OnVolumeChanged += OnVolumeChanged;
        }
    }

    void Update()
    {
        if (audioVisualizer != null)
        {
            UpdateAllEffects();
        }
    }

    private void SaveOriginalSettings()
    {
        // Сохраняем оригинальные настройки освещения
        if (pointLights != null)
        {
            originalLightIntensities = new float[pointLights.Length];
            originalLightColors = new Color[pointLights.Length];
            
            for (int i = 0; i < pointLights.Length; i++)
            {
                if (pointLights[i] != null)
                {
                    originalLightIntensities[i] = pointLights[i].intensity;
                    originalLightColors[i] = pointLights[i].color;
                }
            }
        }
        
        // Сохраняем оригинальные цвета материалов
        if (floorRenderer != null)
        {
            floorOriginalColor = floorRenderer.material.color;
        }
        
        if (wallRenderers != null)
        {
            wallsOriginalColors = new Color[wallRenderers.Length];
            for (int i = 0; i < wallRenderers.Length; i++)
            {
                if (wallRenderers[i] != null)
                {
                    wallsOriginalColors[i] = wallRenderers[i].material.color;
                }
            }
        }
    }

    private void UpdateAllEffects()
    {
        float volume = audioVisualizer.currentVolume * 0.1f;
        float bass = audioVisualizer.lowFrequency;
        float mids = audioVisualizer.midFrequency;
        float highs = audioVisualizer.highFrequency;
        
        UpdateLights(volume, bass, mids, highs);
        UpdateMaterials(volume, bass, mids);
        UpdateParticles(mids, highs);
    }

    private void UpdateLights(float volume, float bass, float mids, float highs)
    {
        // Основной directional light
        if (directionalLight != null)
        {
            directionalLight.intensity = 1f + (volume * maxLightIntensity);
            directionalLight.color = Color.Lerp(Color.white, 
                lightColors[Mathf.FloorToInt(highs * lightColors.Length) % lightColors.Length], 
                volume * 0.5f);
        }
        
        // Point lights
        if (pointLights != null)
        {
            for (int i = 0; i < pointLights.Length; i++)
            {
                if (pointLights[i] != null)
                {
                    // Пульсация на основе басов
                    float pulse = 1f + Mathf.Sin(Time.time * 10f + i) * bass * lightPulseStrength;
                    pointLights[i].intensity = originalLightIntensities[i] + (volume * maxLightIntensity * pulse);
                    
                    // Изменение цвета на основе частот
                    Color targetColor = lightColors[(i + Mathf.FloorToInt(mids * lightColors.Length)) % lightColors.Length];
                    pointLights[i].color = Color.Lerp(originalLightColors[i], targetColor, volume);
                }
            }
        }
        
        // Spot lights
        if (spotLights != null)
        {
            foreach (var spotLight in spotLights)
            {
                if (spotLight != null)
                {
                    spotLight.intensity = 1f + (highs * maxLightIntensity);
                    spotLight.spotAngle = 30f + (bass * 20f);
                }
            }
        }
    }

    private void UpdateMaterials(float volume, float bass, float mids)
    {
        // Пол
        if (floorRenderer != null)
        {
            Material floorMat = floorRenderer.material;
            
            // Изменение emission
            if (floorMat.HasProperty("_EmissionColor"))
            {
                Color emissionColor = floorEmissionColor * (1f + volume * maxEmission);
                floorMat.SetColor("_EmissionColor", emissionColor);
            }
            
            // Пульсация цвета
            Color floorColor = Color.Lerp(floorOriginalColor, floorEmissionColor, bass);
            floorMat.color = floorColor;
            
            // Смещение текстуры для эффекта движения
            if (floorMat.HasProperty("_MainTex"))
            {
                float offset = Time.time * volume * 0.1f;
                floorMat.SetTextureOffset("_MainTex", new Vector2(offset, offset));
            }
        }
        
        // Стены
        if (wallRenderers != null)
        {
            for (int i = 0; i < wallRenderers.Length; i++)
            {
                if (wallRenderers[i] != null)
                {
                    Material wallMat = wallRenderers[i].material;
                    
                    if (wallMat.HasProperty("_EmissionColor"))
                    {
                        Color emissionColor = wallEmissionColor * (1f + mids * maxEmission);
                        wallMat.SetColor("_EmissionColor", emissionColor);
                    }
                    
                    // Разные стены пульсируют с разной фазой
                    float phase = Mathf.Sin(Time.time * 5f + i) * volume;
                    Color wallColor = Color.Lerp(wallsOriginalColors[i], wallEmissionColor, phase);
                    wallMat.color = wallColor;
                }
            }
        }
        
        // Боковые панели
        if (sidePanelRenderers != null)
        {
            foreach (var panel in sidePanelRenderers)
            {
                if (panel != null)
                {
                    Material panelMat = panel.material;
                    if (panelMat.HasProperty("_EmissionColor"))
                    {
                        Color emissionColor = Color.HSVToRGB(volume, 1f, 1f) * maxEmission;
                        panelMat.SetColor("_EmissionColor", emissionColor);
                    }
                }
            }
        }
    }

    private void UpdateParticles(float mids, float highs)
    {
        // Фоновые частицы
        if (backgroundParticles != null)
        {
            foreach (var ps in backgroundParticles)
            {
                if (ps != null)
                {
                    var emission = ps.emission;
                    var main = ps.main;
                    
                    emission.rateOverTime = mids * maxParticleRate;
                    main.startSize = 0.1f + (highs * particleSizeMultiplier);
                    
                    // Изменение цвета частиц
                    main.startColor = Color.HSVToRGB(highs, 0.8f, 1f);
                }
            }
        }
    }

    private void OnBeatDetected(float volume)
    {
        // Вспышка освещения на бит
        StartCoroutine(BeatFlash(volume));
        
        // Запуск частиц на бит
        if (beatParticles != null)
        {
            foreach (var ps in beatParticles)
            {
                if (ps != null)
                {
                    ps.Emit(20); // Выбрасываем частицы
                }
            }
        }
    }

    private void OnVolumeChanged(float volume)
    {
        // Дополнительные эффекты при изменении громкости
        // Можно добавить анимации объектов и т.д.
    }

    private IEnumerator BeatFlash(float intensity)
    {
        // Вспышка всех источников света
        float flashIntensity = intensity * maxLightIntensity;
        
        // Увеличиваем интенсивность
        if (directionalLight != null)
            directionalLight.intensity += flashIntensity;
            
        if (pointLights != null)
        {
            foreach (var light in pointLights)
            {
                if (light != null)
                    light.intensity += flashIntensity;
            }
        }
        
        yield return new WaitForSeconds(0.1f);
        
        // Возвращаем к нормальной интенсивности
        if (directionalLight != null)
            directionalLight.intensity = 1f + (audioVisualizer.currentVolume * 0.1f * maxLightIntensity);
    }

    void OnDestroy()
    {
        if (audioVisualizer != null)
        {
            audioVisualizer.OnBeatDetected -= OnBeatDetected;
            audioVisualizer.OnVolumeChanged -= OnVolumeChanged;
        }
    }
}