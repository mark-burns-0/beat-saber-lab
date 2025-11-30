using UnityEngine;
using System.Collections;

public class AudioVisualizer : MonoBehaviour
{
    [Header("Audio Analysis Settings")]
    public AudioSource audioSource;
    public FFTWindow fftWindow = FFTWindow.Hamming;
    [Range(64, 8192)]
    public int sampleSize = 1024;
    public float updateInterval = 0.05f;
    
    [Header("Volume Settings")]
    public float sensitivity = 50f;
    public float smoothness = 0.3f;
    public float beatThreshold = 0.1f;
    public float beatCooldown = 0.2f;

    [Header("Frequency Bands")]
    public int frequencyBands = 8;
    public float[] bandMultipliers = { 10f, 20f, 30f, 40f, 50f, 60f, 70f, 80f };
    
    [Header("Visualization Output")]
    public float currentVolume = 0f;
    public float smoothedVolume = 0f;
    public float peakVolume = 0f;
    public bool beatDetected = false;
    
    [Header("Frequency Data")]
    public float lowFrequency = 0f;      // 0-250Hz (Bass)
    public float midFrequency = 0f;      // 250-2000Hz (Mid)
    public float highFrequency = 0f;     // 2000-20000Hz (High)
    public float[] frequencyBandsData;

    // Audio data arrays
    private float[] samples;
    private float[] spectrum;
    private float[] bandBuffer;
    private float[] bufferDecrease;
    
    // Beat detection
    private float lastBeatTime = 0f;
    private float beatTimer = 0f;
    
    // Events
    public System.Action<float> OnVolumeChanged;
    public System.Action<float> OnBeatDetected;
    public System.Action<float[]> OnFrequencyBandsUpdated;

    void Start()
    {
        InitializeAudioAnalysis();
        StartCoroutine(AudioAnalysisCoroutine());
    }

    private void InitializeAudioAnalysis()
    {
        samples = new float[sampleSize];
        spectrum = new float[sampleSize];
        
        // Initialize frequency bands
        frequencyBandsData = new float[frequencyBands];
        bandBuffer = new float[frequencyBands];
        bufferDecrease = new float[frequencyBands];
        
        for (int i = 0; i < frequencyBands; i++)
        {
            frequencyBandsData[i] = 0f;
            bandBuffer[i] = 0f;
            bufferDecrease[i] = 0.1f + (i * 0.05f);
        }
        
        // If no audio source specified, try to find one
        if (audioSource == null)
        {
            audioSource = FindObjectOfType<AudioSource>();
            if (audioSource == null)
            {
                Debug.LogWarning("No AudioSource found for AudioVisualizer!");
            }
        }
    }

    private IEnumerator AudioAnalysisCoroutine()
    {
        while (true)
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                AnalyzeAudio();
            }
            yield return new WaitForSeconds(updateInterval);
        }
    }

    private void AnalyzeAudio()
    {
        // Get raw audio data
        audioSource.GetOutputData(samples, 0);
        
        // Calculate current volume (RMS)
        float newVolume = CalculateRMSVolume();
        
        // Smooth volume
        smoothedVolume = Mathf.Lerp(smoothedVolume, newVolume, smoothness);
        currentVolume = smoothedVolume * sensitivity;
        
        // Update peak volume
        if (currentVolume > peakVolume)
        {
            peakVolume = currentVolume;
        }
        else
        {
            peakVolume = Mathf.Lerp(peakVolume, currentVolume, 0.1f);
        }
        
        // Get frequency spectrum
        audioSource.GetSpectrumData(spectrum, 0, fftWindow);
        
        // Analyze frequency bands
        AnalyzeFrequencyBands();
        ProcessFrequencyBuffers();
        AnalyzeMainFrequencyRanges();
        
        // Beat detection
        DetectBeat();
        
        // Trigger events
        OnVolumeChanged?.Invoke(currentVolume);
        OnFrequencyBandsUpdated?.Invoke(frequencyBandsData);
    }

    private float CalculateRMSVolume()
    {
        float sum = 0f;
        foreach (float sample in samples)
        {
            sum += sample * sample;
        }
        return Mathf.Sqrt(sum / samples.Length);
    }

    private void AnalyzeFrequencyBands()
    {
        int count = 0;
        float average = 0;
        int sampleCount = 1;
        
        for (int i = 0; i < frequencyBands; i++)
        {
            average = 0;
            
            if (i == 7)
            {
                sampleCount = spectrum.Length - count;
            }
            else
            {
                sampleCount = (int)Mathf.Pow(2, i) * 2;
            }
            
            if (sampleCount > 0)
            {
                for (int j = 0; j < sampleCount; j++)
                {
                    average += spectrum[count] * (count + 1);
                    count++;
                }
                
                average /= count;
                frequencyBandsData[i] = average * bandMultipliers[i];
            }
        }
    }

    private void ProcessFrequencyBuffers()
    {
        for (int i = 0; i < frequencyBands; i++)
        {
            if (frequencyBandsData[i] > bandBuffer[i])
            {
                bandBuffer[i] = frequencyBandsData[i];
                bufferDecrease[i] = 0.005f;
            }
            
            if (frequencyBandsData[i] < bandBuffer[i])
            {
                bandBuffer[i] -= bufferDecrease[i];
                bufferDecrease[i] *= 1.2f;
            }
        }
    }

    private void AnalyzeMainFrequencyRanges()
    {
        lowFrequency = 0f;
        midFrequency = 0f;
        highFrequency = 0f;
        
        // Bass frequencies: 0-250Hz (approximately bands 0-1)
        for (int i = 0; i < 2 && i < frequencyBandsData.Length; i++)
        {
            lowFrequency += frequencyBandsData[i];
        }
        lowFrequency /= 2;
        
        // Mid frequencies: 250-2000Hz (approximately bands 2-5)
        int midStart = 2;
        int midEnd = Mathf.Min(5, frequencyBandsData.Length - 1);
        int midCount = midEnd - midStart + 1;
        
        for (int i = midStart; i <= midEnd; i++)
        {
            midFrequency += frequencyBandsData[i];
        }
        if (midCount > 0) midFrequency /= midCount;
        
        // High frequencies: 2000-20000Hz (remaining bands)
        int highStart = 6;
        if (highStart < frequencyBandsData.Length)
        {
            int highCount = frequencyBandsData.Length - highStart;
            for (int i = highStart; i < frequencyBandsData.Length; i++)
            {
                highFrequency += frequencyBandsData[i];
            }
            if (highCount > 0) highFrequency /= highCount;
        }
    }

    private void DetectBeat()
    {
        beatTimer += updateInterval;
        
        // Reset beat flag
        beatDetected = false;
        
        // Check if enough time has passed since last beat
        if (beatTimer - lastBeatTime < beatCooldown)
            return;
        
        // Simple beat detection based on volume threshold
        if (currentVolume > beatThreshold && currentVolume > peakVolume * 0.8f)
        {
            beatDetected = true;
            lastBeatTime = beatTimer;
            OnBeatDetected?.Invoke(currentVolume);
        }
    }

    public float GetFrequencyBand(int bandIndex)
    {
        if (bandIndex >= 0 && bandIndex < frequencyBandsData.Length)
        {
            return frequencyBandsData[bandIndex];
        }
        return 0f;
    }

    public float GetBandBuffer(int bandIndex)
    {
        if (bandIndex >= 0 && bandIndex < bandBuffer.Length)
        {
            return bandBuffer[bandIndex];
        }
        return 0f;
    }

    public float GetNormalizedVolume()
    {
        return Mathf.Clamp01(currentVolume);
    }

    public float GetNormalizedFrequencyBand(int bandIndex)
    {
        float value = GetFrequencyBand(bandIndex);
        return Mathf.Clamp01(value / 100f);
    }

    public bool IsBeat()
    {
        return beatDetected;
    }

    public float GetBeatStrength()
    {
        if (beatDetected)
        {
            return Mathf.Clamp01(currentVolume / Mathf.Max(peakVolume, 0.1f));
        }
        return 0f;
    }

    // Debug visualization in editor
    void OnDrawGizmos()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying) return;
        
        // Draw volume meter
        DrawVolumeGizmo();
        
        // Draw frequency spectrum
        DrawSpectrumGizmo();
        #endif
    }

    private void DrawVolumeGizmo()
    {
        Vector3 position = transform.position + Vector3.up * 2f;
        float width = 2f;
        float height = 0.5f;
        
        // Background
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(position, new Vector3(width, height, 0.1f));
        
        // Volume level
        Gizmos.color = Color.Lerp(Color.green, Color.red, GetNormalizedVolume());
        float volumeWidth = width * GetNormalizedVolume();
        Gizmos.DrawCube(position - new Vector3((width - volumeWidth) * 0.5f, 0, 0), 
                       new Vector3(volumeWidth, height * 0.8f, 0.05f));
        
        // Beat indicator
        if (beatDetected)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(position + Vector3.up * 0.3f, 0.2f);
        }
    }

    private void DrawSpectrumGizmo()
    {
        if (frequencyBandsData == null) return;
        
        Vector3 startPos = transform.position + Vector3.up * 1f;
        float bandWidth = 0.2f;
        float maxHeight = 1f;
        
        for (int i = 0; i < frequencyBandsData.Length; i++)
        {
            float height = Mathf.Clamp01(frequencyBandsData[i] / 100f) * maxHeight;
            Vector3 bandPos = startPos + new Vector3(i * bandWidth, 0, 0);
            Vector3 size = new Vector3(bandWidth * 0.8f, height, 0.1f);
            
            // Color based on frequency band
            Color bandColor = Color.HSVToRGB((float)i / frequencyBandsData.Length, 1f, 1f);
            Gizmos.color = bandColor;
            Gizmos.DrawCube(bandPos + new Vector3(0, height * 0.5f, 0), size);
            
            // Buffer level
            float bufferHeight = Mathf.Clamp01(bandBuffer[i] / 100f) * maxHeight;
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(bandPos + new Vector3(0, bufferHeight * 0.5f, 0), 
                               new Vector3(bandWidth * 0.8f, bufferHeight, 0.1f));
        }
    }

    // Public methods for external control
    public void SetSensitivity(float newSensitivity)
    {
        sensitivity = Mathf.Clamp(newSensitivity, 1f, 100f);
    }

    public void SetBeatThreshold(float newThreshold)
    {
        beatThreshold = Mathf.Clamp01(newThreshold);
    }

    public void ResetPeakVolume()
    {
        peakVolume = 0f;
    }

    // Cleanup
    void OnDestroy()
    {
        StopAllCoroutines();
    }
}