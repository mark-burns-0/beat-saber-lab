using UnityEngine;
using EzySlice;

public class CubeController : MonoBehaviour
{
    [Header("Cube Settings")]
    public BeatSaberMapParser.Note NoteData { get; private set; }

    [Header("Movement Settings")]
    public float approachSpeed = 2f;

    [Header("Audio Visualization")]
    public bool enableAudioEffects = true;
    public float maxScaleMultiplier = 1.2f;
    public float maxEmissionIntensity = 2f;

    [Header("Slicing Settings")]
    public bool canBeSliced = true;
    private bool sliced = false;
    public float sliceDetectionDistance = 2f;

    // Сделал поле public для доступа из других скриптов
    public Vector3 cutDirection { get; private set; }

    private Material cubeMaterial;
    private Vector3 originalScale;
    private Color originalColor;
    private AudioVisualizer audioVisualizer;
    private bool isInitialized = false;
    private float spawnZ = 20f;
    private float targetZ = -1f;
    private bool wasSliced = false;
    private Transform cutIndicator;

    public float lifetime = 10f;   // сколько секунд жить кубу, если его не трогают
    private float lifeTimer = 0f;


    // Событие для уведомления о разрубании
    public System.Action<CubeController, Vector3, Vector3> OnCubeSliced;

    void Start()
    {
        audioVisualizer = FindObjectOfType<AudioVisualizer>();

        transform.localScale = new Vector3(15, 15, 15);
        originalScale = transform.localScale;

        if (GetComponent<MeshFilter>() == null)
        {
            Debug.LogError("CubeController: NO MESH FILTER! Добавляю...");
            MeshFilter mf = gameObject.AddComponent<MeshFilter>();
            MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();

            // Используем стандартный меш Куба
            mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            // Назначаем материал
            mr.material = cubeMaterial != null
                ? cubeMaterial
                : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            cubeMaterial = renderer.material;
            originalColor = cubeMaterial.color;

            if (cubeMaterial.HasProperty("_EmissionColor"))
            {
                cubeMaterial.EnableKeyword("_EMISSION");
            }
        }
    }

    void Update()
    {
        if (enableAudioEffects && audioVisualizer != null)
        {
            ApplyAudioEffects();
        }

        // === ТАЙМЕР ЖИЗНИ КУБА ===
        if (!wasSliced)
        {
            lifeTimer += Time.deltaTime;

            if (lifeTimer >= lifetime)
            {
                // Промах — куб прожил слишком долго
                BeatSaberCubeGenerator generator = FindObjectOfType<BeatSaberCubeGenerator>();
                if (generator != null)
                    generator.HandleCubeMissed(this);

                Destroy(gameObject);
                return;
            }
        }
    }

    public void Initialize(BeatSaberMapParser.Note note, float speed)
    {
        NoteData = note;
        approachSpeed = speed;
        isInitialized = true;

        transform.position = new Vector3(transform.position.x, transform.position.y, spawnZ);

        if (cubeMaterial == null)
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                cubeMaterial = renderer.material;
                originalColor = note._type == 0 ? Color.red : Color.blue;
                cubeMaterial.color = originalColor;

                if (cubeMaterial.HasProperty("_EmissionColor"))
                {
                    cubeMaterial.EnableKeyword("_EMISSION");
                    cubeMaterial.SetColor("_EmissionColor", originalColor * 0.2f);
                }
            }
        }
        else
        {
            cubeMaterial.color = note._type == 0 ? Color.red : Color.blue;
            originalColor = cubeMaterial.color;
        }
    }

    public void UpdatePosition(float songTime)
    {
        if (sliced) return;

        float travelTime = spawnZ / approachSpeed;

        // Когда куб должен быть на позиции
        float appearTime = NoteData._time - travelTime;

        // Сколько времени куб уже летит
        float t = songTime - appearTime;

        if (t < 0)
        {
            // Куб ещё не начинает движение (как Beat Saber)
            return;
        }

        float z = spawnZ - t * approachSpeed;

        if (z < targetZ)
            z = targetZ;

        transform.position = new Vector3(transform.position.x, transform.position.y, z);
    }

    public bool TrySlice(Vector3 slicePosition, Vector3 sliceDirection, SaberType saberType)
    {
        // Минимальные безопасные проверки, чтобы не ломать остальной код
        if (!canBeSliced || wasSliced) return false;

        // Если NoteData отсутствует, считаем, что цветовой фильтр пропускает (удобно для тестов)
        if (NoteData != null)
        {
            // Проверяем цвет сабера (если NoteData задана)
            if (saberType == SaberType.Red && NoteData._type != 0) return false;
            if (saberType == SaberType.Blue && NoteData._type != 1) return false;
        }

        // Если задано требуемое направление реза (не ANY), проверяем угол.
        // При этом учитываем случай, когда sliceDirection очень маленький — считаем это промахом.
        if (cutDirection != Vector3.zero)
        {
            // защитная проверка на нулевую/малую длину направления реза
            if (sliceDirection.sqrMagnitude < 0.0001f)
            {
                // Удар слишком слабый/направление не определено — считаем промахом
                return false;
            }

            float angle = Vector3.Angle(sliceDirection.normalized, cutDirection);
            // Немного увеличим допуск угла, чтобы уменьшить ложные промахи
            if (angle > 60f) // было 45f, сделал мягче
            {
                return false;
            }
        }

        // Успешное разрубание
        wasSliced = true;
        OnCubeSliced?.Invoke(this, slicePosition, sliceDirection);
        OnCut(slicePosition, sliceDirection);

        return true;
    }

    public void SetCutDirection(int cutDirectionID)
    {
        Vector3 direction = GetCutDirectionVector(cutDirectionID);
        cutDirection = direction;

        if (direction != Vector3.zero)
        {
            // стандартный поворот
            Quaternion baseRot = Quaternion.LookRotation(Vector3.back, direction);

            // + поворот на 90 градусов влево
            transform.rotation = baseRot * Quaternion.Euler(0, 0, -90f);
        }
        else
        {
            transform.rotation = Quaternion.identity;
            if (cutIndicator != null)
                cutIndicator.localRotation = Quaternion.identity;
        }
    }


    private Vector3 GetCutDirectionVector(int cutDirectionID)
    {
        switch (cutDirectionID)
        {
            case 0: return Vector3.up;
            case 1: return Vector3.down;
            case 2: return Vector3.left;
            case 3: return Vector3.right;
            case 4: return (Vector3.up + Vector3.left).normalized;
            case 5: return (Vector3.up + Vector3.right).normalized;
            case 6: return (Vector3.down + Vector3.left).normalized;
            case 7: return (Vector3.down + Vector3.right).normalized;
            case 8: return Vector3.zero;
            default: return Vector3.zero;
        }
    }

    private void ApplyAudioEffects()
    {
        if (audioVisualizer == null) return;

        float volume = audioVisualizer.currentVolume * 0.1f;
        float bass = audioVisualizer.lowFrequency;

        float scalePulse = 1f + (bass * 0.1f * maxScaleMultiplier);
        transform.localScale = originalScale * scalePulse;

        if (cubeMaterial != null && cubeMaterial.HasProperty("_EmissionColor"))
        {
            Color emissionColor = originalColor * (1f + volume * maxEmissionIntensity);
            cubeMaterial.SetColor("_EmissionColor", emissionColor);
        }
    }

    public void OnCut(Vector3 slicePosition, Vector3 sliceDirection)
    {
        // Визуальные эффекты при разрубании
        CreateSliceEffect();

        // Разрезаем куб с помощью EzySlice
        SliceCube(slicePosition, sliceDirection);
    }


    private void SliceCube(Vector3 slicePosition, Vector3 sliceDirection)
    {
        // Нормаль плоскости = направление удара
        Vector3 planeNormal = sliceDirection.normalized;

        // Плоскость проходит через куб
        Vector3 planePoint = transform.position;

        // Создаём плоскость
        var plane = new EzySlice.Plane(planePoint, planeNormal);

        // Выполняем разрез
        SlicedHull hull = gameObject.Slice(plane, cubeMaterial);
        if (hull == null)
        {
            Debug.LogWarning("Разрез не сработал!");
            return;
        }

        SetupHull(hull.CreateUpperHull(gameObject, cubeMaterial), planeNormal);
        SetupHull(hull.CreateLowerHull(gameObject, cubeMaterial), -planeNormal);

        Destroy(gameObject);
    }



    private void SetupHull(GameObject hullObj, Vector3 impulseDir)
    {
        hullObj.transform.position = transform.position;
        hullObj.transform.rotation = transform.rotation;
        hullObj.transform.localScale = transform.localScale;

        MeshCollider col = hullObj.AddComponent<MeshCollider>();
        col.convex = true;

        Rigidbody rb = hullObj.AddComponent<Rigidbody>();
        rb.mass = 1.2f;
        float forceMultiplier = transform.localScale.magnitude * 0.1f;
        rb.AddForce((impulseDir + Random.insideUnitSphere * 0.3f).normalized * 3f * forceMultiplier, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.Impulse);

        Destroy(hullObj, 5f);
    }

    public void OnMiss()
    {
        Destroy(gameObject);
    }

    private void CreateSliceEffect()
    {
        // Здесь можно добавить эффекты разрубания
        // Например, частицы, звуки и т.д.
        Debug.Log($"Cube sliced! Position: {transform.position}");
    }

    // Метод для получения информации о кубе (опционально)
    public CubeInfo GetCubeInfo()
    {
        return new CubeInfo
        {
            cutDirection = this.cutDirection,
            cubeType = (NoteData != null) ? (NoteData._type == 0 ? SaberType.Red : SaberType.Blue) : SaberType.Red,
            position = transform.position,
            canBeSliced = this.canBeSliced
        };
    }
}

public enum SaberType
{
    Red,
    Blue
}

// Структура для передачи информации о кубе
public struct CubeInfo
{
    public Vector3 cutDirection;
    public SaberType cubeType;
    public Vector3 position;
    public bool canBeSliced;
}
