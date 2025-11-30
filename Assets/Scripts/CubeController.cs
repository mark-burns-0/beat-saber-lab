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


    // private void SliceCube(Vector3 slicePosition, Vector3 sliceDirection)
    // {
    //     Debug.Log("=== НАЧАЛО РАЗРЕЗА КУБА ===");

    //     // Логируем входные параметры
    //     Debug.Log($"Позиция разреза: {slicePosition}");
    //     Debug.Log($"Направление разреза: {sliceDirection} (длина: {sliceDirection.magnitude})");
    //     Debug.Log($"Позиция куба: {transform.position}");
    //     Debug.Log($"Вращение куба: {transform.rotation.eulerAngles}");
    //     Debug.Log($"Масштаб куба: {transform.localScale}");

    //     // Нормаль плоскости = направление удара
    //     Vector3 planeNormal = sliceDirection.normalized;
    //     Debug.Log($"Нормаль плоскости после нормализации: {planeNormal} (длина: {planeNormal.magnitude})");

    //     // ДОПОЛНИТЕЛЬНАЯ ДИАГНОСТИКА УГЛОВ
    //     float angleToY = Vector3.Angle(planeNormal, Vector3.up);
    //     float angleToZ = Vector3.Angle(planeNormal, Vector3.forward);
    //     float angleToX = Vector3.Angle(planeNormal, Vector3.right);
    //     Debug.Log($"Углы нормали: Y={angleToY}°, Z={angleToZ}°, X={angleToX}°");

    //     // Проверяем валидность нормали
    //     if (planeNormal.magnitude < 0.9f)
    //     {
    //         Debug.LogError($"ПРОБЛЕМА: Нормаль слишком короткая! Возможно нулевое направление.");
    //     }

    //     // Плоскость проходит через куб
    //     Vector3 planePoint = transform.position;
    //     Debug.Log($"Точка плоскости (центр куба): {planePoint}");

    //     // Логируем относительные векторы
    //     Vector3 toSlicePoint = slicePosition - planePoint;
    //     Debug.Log($"Вектор от центра куба к точке разреза: {toSlicePoint} (длина: {toSlicePoint.magnitude})");

    //     float dotProduct = Vector3.Dot(planeNormal, toSlicePoint.normalized);
    //     Debug.Log($"Скалярное произведение нормали и направления к точке: {dotProduct}");

    //     // ПРОСТАЯ ВИЗУАЛИЗАЦИЯ - только нормаль и точка
    //     Debug.DrawRay(planePoint, planeNormal * 3f, Color.red, 5f);
    //     Debug.DrawRay(slicePosition, Vector3.up * 0.5f, Color.green, 5f); // Точка удара

    //     // Создаём плоскость
    //     var plane = new EzySlice.Plane(planePoint, planeNormal);
    //     Debug.Log($"Создана плоскость: точка={planePoint}, нормаль={planeNormal}");

    //     // ДОПОЛНИТЕЛЬНАЯ ПРОВЕРКА: границы куба
    //     Renderer cubeRenderer = GetComponent<Renderer>();
    //     if (cubeRenderer != null)
    //     {
    //         Bounds bounds = cubeRenderer.bounds;
    //         Debug.Log($"Границы куба: min={bounds.min}, max={bounds.max}, size={bounds.size}");

    //         // Проверяем пересечение плоскости с границами куба
    //         bool intersects = PlaneIntersectsBounds(planePoint, planeNormal, bounds);
    //         Debug.Log($"Плоскость пересекает границы куба: {intersects}");
    //     }

    //     // Выполняем разрез
    //     SlicedHull hull = gameObject.Slice(plane, cubeMaterial);
    //     if (hull == null)
    //     {
    //         Debug.LogWarning("РАЗРЕЗ НЕ СРАБОТАЛ! Плоскость, вероятно, не пересекает меш.");
    //         Debug.LogWarning($"Дополнительная информация:");
    //         Debug.LogWarning($"- Нормаль: {planeNormal}");
    //         Debug.LogWarning($"- Точка плоскости: {planePoint}");
    //         Debug.LogWarning($"- Углы: Y={angleToY}°, Z={angleToZ}°");

    //         // Пробуем альтернативный подход - смещаем точку плоскости
    //         Debug.Log("Пробуем альтернативный подход со смещенной точкой...");
    //         Vector3 alternativePoint = slicePosition; // Используем точку удара вместо центра куба
    //         var alternativePlane = new EzySlice.Plane(alternativePoint, planeNormal);
    //         hull = gameObject.Slice(alternativePlane, cubeMaterial);

    //         if (hull != null)
    //         {
    //             Debug.Log("Альтернативный подход сработал! Используем точку удара.");
    //             plane = alternativePlane;
    //         }
    //     }

    //     if (hull == null)
    //     {
    //         Debug.LogError("Разрез не удался даже с альтернативным подходом.");
    //         return;
    //     }

    //     Debug.Log("Разрез успешен, создаем части...");

    //     // Создаем верхнюю и нижнюю части
    //     GameObject upperHull = hull.CreateUpperHull(gameObject, cubeMaterial);
    //     GameObject lowerHull = hull.CreateLowerHull(gameObject, cubeMaterial);

    //     Debug.Log($"Созданы части: Upper={upperHull != null}, Lower={lowerHull != null}");

    //     if (upperHull != null)
    //     {
    //         Debug.Log($"Настройка UpperHull с нормалью: {planeNormal}");
    //         SetupHull(upperHull, planeNormal);

    //         Renderer upperRenderer = upperHull.GetComponent<Renderer>();
    //         if (upperRenderer != null)
    //         {
    //             Debug.Log($"UpperHull границы: {upperRenderer.bounds}");
    //             Debug.Log($"UpperHull центр: {upperRenderer.bounds.center}");
    //         }
    //     }
    //     else
    //     {
    //         Debug.LogWarning("UpperHull не создан!");
    //     }

    //     if (lowerHull != null)
    //     {
    //         Debug.Log($"Настройка LowerHull с нормалью: {-planeNormal}");
    //         SetupHull(lowerHull, -planeNormal);

    //         Renderer lowerRenderer = lowerHull.GetComponent<Renderer>();
    //         if (lowerRenderer != null)
    //         {
    //             Debug.Log($"LowerHull границы: {lowerRenderer.bounds}");
    //             Debug.Log($"LowerHull центр: {lowerRenderer.bounds.center}");
    //         }
    //     }
    //     else
    //     {
    //         Debug.LogWarning("LowerHull не создан!");
    //     }

    //     // Логируем итоговую информацию перед уничтожением
    //     if (upperHull != null && lowerHull != null)
    //     {
    //         float distanceBetweenParts = Vector3.Distance(
    //             upperHull.transform.position,
    //             lowerHull.transform.position
    //         );
    //         Debug.Log($"Расстояние между частями: {distanceBetweenParts}");
    //     }

    //     Debug.Log("Уничтожаем оригинальный куб");
    //     Destroy(gameObject);

    //     Debug.Log("=== ЗАВЕРШЕНИЕ РАЗРЕЗА КУБА ===");
    // }

    // // ИСПРАВЛЕННЫЙ МЕТОД - используем стандартную математику плоскости
    // private bool PlaneIntersectsBounds(Vector3 planePoint, Vector3 planeNormal, Bounds bounds)
    // {
    //     // Получаем все 8 вершин куба
    //     Vector3[] vertices = new Vector3[8];
    //     vertices[0] = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
    //     vertices[1] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
    //     vertices[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
    //     vertices[3] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
    //     vertices[4] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
    //     vertices[5] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
    //     vertices[6] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
    //     vertices[7] = new Vector3(bounds.max.x, bounds.max.y, bounds.max.z);

    //     // Проверяем, есть ли точки по разные стороны плоскости
    //     bool hasPositive = false;
    //     bool hasNegative = false;

    //     foreach (Vector3 vertex in vertices)
    //     {
    //         // Вычисляем расстояние от точки до плоскости вручную
    //         float distance = Vector3.Dot(vertex - planePoint, planeNormal);

    //         if (distance > 0.01f) hasPositive = true;
    //         if (distance < -0.01f) hasNegative = true;

    //         if (hasPositive && hasNegative)
    //         {
    //             Debug.Log($"Плоскость пересекает: вершина {vertex} имеет расстояние {distance}");
    //             return true;
    //         }
    //     }

    //     Debug.Log($"Плоскость НЕ пересекает: все вершины по одну сторону");
    //     return false;
    // }

    private void SliceCube(Vector3 slicePosition, Vector3 sliceDirection)
    {
        // 1. ДОБАВИЛ ПРОВЕРКУ И КОРРЕКЦИЮ НОРМАЛИ
        if (sliceDirection.magnitude < 0.001f)
        {
            Debug.LogWarning("Направление разреза слишком мало!");
            return;
        }

        Vector3 planeNormal = sliceDirection.normalized;

        // 2. ДОБАВИЛ АЛЬТЕРНАТИВНУЮ ТОЧКУ ПЛОСКОСТИ (главное улучшение!)
        Vector3 planePoint = transform.position;

        // Если разрез не работает через центр, пробуем через точку удара
        var plane = new EzySlice.Plane(planePoint, planeNormal);
        SlicedHull hull = gameObject.Slice(plane, cubeMaterial);

        if (hull == null)
        {
            Debug.Log("Пробуем альтернативную точку плоскости через точку удара...");
            planePoint = slicePosition; // Используем точку контакта вместо центра
            plane = new EzySlice.Plane(planePoint, planeNormal);
            hull = gameObject.Slice(plane, cubeMaterial);
        }

        // 3. ДОБАВИЛ ЕЩЕ ОДНУ АЛЬТЕРНАТИВУ - смещенную точку
        if (hull == null)
        {
            Debug.Log("Пробуем смещенную точку от центра...");
            planePoint = transform.position + planeNormal * 0.1f; // Смещаем немного по нормали
            plane = new EzySlice.Plane(planePoint, planeNormal);
            hull = gameObject.Slice(plane, cubeMaterial);
        }

        if (hull == null)
        {
            Debug.LogWarning("Разрез не сработал!");
            return;
        }

        // 4. ДОБАВИЛ ПРОВЕРКУ РЕЗУЛЬТАТОВ
        GameObject upperHull = hull.CreateUpperHull(gameObject, cubeMaterial);
        GameObject lowerHull = hull.CreateLowerHull(gameObject, cubeMaterial);

        if (upperHull == null || lowerHull == null)
        {
            Debug.LogWarning("Одна из частей не создалась!");
            if (upperHull != null) Destroy(upperHull);
            if (lowerHull != null) Destroy(lowerHull);
            return;
        }

        SetupHull(upperHull, planeNormal);
        SetupHull(lowerHull, -planeNormal);

        Destroy(gameObject);
    }

    private void SetupHull(GameObject hullObj, Vector3 impulseDir)
    {
        if (hullObj == null)
        {
            Debug.LogWarning("SetupHull: передан null объект!");
            return;
        }

        // Проверяем и корректируем направление импульса
        if (impulseDir.magnitude < 0.01f)
        {
            Debug.LogWarning($"Направление импульса слишком мало: {impulseDir}. Использую направление вверх.");
            impulseDir = Vector3.up;
        }

        // Устанавливаем позицию и вращение
        hullObj.transform.position = transform.position;
        hullObj.transform.rotation = transform.rotation;
        hullObj.transform.localScale = transform.localScale;

        // Добавляем коллайдер
        MeshCollider col = hullObj.AddComponent<MeshCollider>();
        col.convex = true;

        // Добавляем физику
        Rigidbody rb = hullObj.AddComponent<Rigidbody>();
        rb.mass = 1.2f;

        // Улучшенный расчет силы
        float forceMultiplier = transform.localScale.magnitude * 0.1f;

        // Нормализуем направление и добавляем случайность
        Vector3 forceDirection = impulseDir.normalized;
        Vector3 randomVariation = Random.insideUnitSphere * 0.3f;

        // Комбинируем основное направление со случайной вариацией
        Vector3 finalForceDirection = (forceDirection + randomVariation).normalized;

        // Применяем силу
        float forceAmount = 3f * forceMultiplier;
        rb.AddForce(finalForceDirection * forceAmount, ForceMode.Impulse);

        // Добавляем вращение (более контролируемое)
        Vector3 torque = Random.insideUnitSphere * 3f;
        rb.AddTorque(torque, ForceMode.Impulse);

        // Логируем для отладки
        Debug.Log($"SetupHull: {hullObj.name}, сила: {finalForceDirection * forceAmount}, вращение: {torque}");

        // Уничтожаем через 5 секунд
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
