using UnityEngine;
using System.Collections;

public class WallStripesController : MonoBehaviour
{
    [Header("Wall Stripes Settings")]
    public Renderer leftWallRenderer;
    public Renderer rightWallRenderer;
    public Material stripesMaterial;
    
    [Header("Stripe Effects")]
    public float stripeSpeed = 2f;
    public float maxStripeIntensity = 3f;
    public Color leftStripeColor = Color.red;
    public Color rightStripeColor = Color.blue;
    public Color beatFlashColor = Color.white;
    
    [Header("Animation Settings")]
    public float stripeWidth = 0.2f;
    public float stripeSpacing = 0.5f;
    public bool animateStripes = true;

    private AudioVisualizer audioVisualizer;
    private Material leftWallMaterial;
    private Material rightWallMaterial;
    private float stripeOffset = 0f;
    private float beatFlashTimer = 0f;

    void Start()
    {
        audioVisualizer = FindObjectOfType<AudioVisualizer>();
        
        // Создаем материалы для стен
        CreateWallMaterials();
        
        if (audioVisualizer != null)
        {
            audioVisualizer.OnBeatDetected += OnBeatDetected;
        }
    }

    void Update()
    {
        if (audioVisualizer != null)
        {
            UpdateStripeEffects();
        }
        
        if (animateStripes)
        {
            AnimateStripes();
        }
    }

    private void CreateWallMaterials()
    {
        // Создаем инстансы материалов для каждой стены
        if (leftWallRenderer != null)
        {
            leftWallMaterial = new Material(stripesMaterial);
            leftWallRenderer.material = leftWallMaterial;
            leftWallMaterial.SetColor("_StripeColor", leftStripeColor);
        }
        
        if (rightWallRenderer != null)
        {
            rightWallMaterial = new Material(stripesMaterial);
            rightWallRenderer.material = rightWallMaterial;
            rightWallMaterial.SetColor("_StripeColor", rightStripeColor);
        }
        
        // Устанавливаем параметры полосок
        SetStripeParameters();
    }

    private void SetStripeParameters()
    {
        if (leftWallMaterial != null)
        {
            leftWallMaterial.SetFloat("_StripeWidth", stripeWidth);
            leftWallMaterial.SetFloat("_StripeSpacing", stripeSpacing);
        }
        
        if (rightWallMaterial != null)
        {
            rightWallMaterial.SetFloat("_StripeWidth", stripeWidth);
            rightWallMaterial.SetFloat("_StripeSpacing", stripeSpacing);
        }
    }

    private void UpdateStripeEffects()
    {
        float volume = audioVisualizer.currentVolume * 0.1f;
        float bass = audioVisualizer.lowFrequency;
        float mids = audioVisualizer.midFrequency;
        
        // Интенсивность свечения на основе громкости
        float emissionIntensity = 1f + (volume * maxStripeIntensity);
        
        if (leftWallMaterial != null)
        {
            // Левая стена реагирует на басы
            float leftIntensity = emissionIntensity * (1f + bass);
            Color leftColor = Color.Lerp(leftStripeColor, beatFlashColor, beatFlashTimer) * leftIntensity;
            leftWallMaterial.SetColor("_EmissionColor", leftColor);
            leftWallMaterial.SetFloat("_PulseSpeed", bass * 5f);
        }
        
        if (rightWallMaterial != null)
        {
            // Правая стена реагирует на средние частоты
            float rightIntensity = emissionIntensity * (1f + mids);
            Color rightColor = Color.Lerp(rightStripeColor, beatFlashColor, beatFlashTimer) * rightIntensity;
            rightWallMaterial.SetColor("_EmissionColor", rightColor);
            rightWallMaterial.SetFloat("_PulseSpeed", mids * 5f);
        }
        
        // Уменьшаем таймер вспышки
        if (beatFlashTimer > 0f)
        {
            beatFlashTimer -= Time.deltaTime * 5f;
        }
    }

    private void AnimateStripes()
    {
        // Анимация движения полосок
        stripeOffset += Time.deltaTime * stripeSpeed;
        
        if (leftWallMaterial != null)
        {
            leftWallMaterial.SetFloat("_StripeOffset", stripeOffset);
        }
        
        if (rightWallMaterial != null)
        {
            rightWallMaterial.SetFloat("_StripeOffset", -stripeOffset); // Противоположное направление
        }
    }

    private void OnBeatDetected(float volume)
    {
        // Вспышка на бит
        beatFlashTimer = 1f;
        
        // Увеличиваем скорость анимации на бит
        stripeSpeed += 1f;
        
        // Запускаем корутину для восстановления скорости
        StartCoroutine(ResetStripeSpeed());
    }

    private IEnumerator ResetStripeSpeed()
    {
        yield return new WaitForSeconds(0.5f);
        stripeSpeed = Mathf.Max(2f, stripeSpeed - 1f);
    }

    // Методы для изменения настроек во время игры
    public void SetStripeColors(Color leftColor, Color rightColor)
    {
        leftStripeColor = leftColor;
        rightStripeColor = rightColor;
        
        if (leftWallMaterial != null)
            leftWallMaterial.SetColor("_StripeColor", leftColor);
        
        if (rightWallMaterial != null)
            rightWallMaterial.SetColor("_StripeColor", rightColor);
    }

    public void SetStripeSpeed(float speed)
    {
        stripeSpeed = speed;
    }

    void OnDestroy()
    {
        if (audioVisualizer != null)
        {
            audioVisualizer.OnBeatDetected -= OnBeatDetected;
        }
    }
}