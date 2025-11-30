using UnityEngine;

public class SaberController : MonoBehaviour
{
    [Header("Saber Settings")]
    public SaberType saberType;
    public float saberLength = 1f;
    public float saberWidth = 0.1f;
    public Color saberColor = Color.white;

    [Header("Slicing Settings")]
    public float sliceForce = 10f;
    public LayerMask cubeLayerMask;
    public Material saberLitTemplateMaterial;

    private Vector3 previousPosition;
    private Vector3 currentVelocity;
    private LineRenderer saberTrail;
    private AudioSource sliceSound;

    void Start()
    {
        previousPosition = transform.position;

        // Настраиваем визуал сабера
        SetupSaberVisual();

        sliceSound = GetComponent<AudioSource>();
    }

    void Update()
    {
        UpdateVelocity();
        CheckForSlices();

        // Обновляем трейл сабера
        if (saberTrail != null)
        {
            saberTrail.SetPosition(0, transform.position);
            saberTrail.SetPosition(1, transform.position + transform.forward * saberLength);
        }
    }

    private void SetupSaberVisual()
    {
        // Создаем LineRenderer для визуализации сабера
        saberTrail = gameObject.AddComponent<LineRenderer>();
        saberTrail.positionCount = 2;
        saberTrail.startWidth = saberWidth;
        saberTrail.endWidth = saberWidth;

        // !!! ИСПРАВЛЕННАЯ СТРОКА: Используем готовый шаблон !!!
        // Создаем новый экземпляр материала на основе ассета, который вы создали
        saberTrail.material = new Material(saberLitTemplateMaterial);

        saberTrail.material.color = saberColor;

        // Эти строки теперь должны работать корректно, так как материал действителен:
        saberTrail.material.EnableKeyword("_EMISSION");
        saberTrail.material.SetColor("_EmissionColor", saberColor * 2f);
    }

    private void UpdateVelocity()
    {
        currentVelocity = (transform.position - previousPosition) / Time.deltaTime;
        previousPosition = transform.position;
    }

    private void CheckForSlices()
    {
        // Если меч почти не двигался — ничего не делаем
        if (currentVelocity.magnitude < 0.5f)
            return;

        // SphereCast вдоль движения сабера
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            saberWidth,
            currentVelocity.normalized,
            currentVelocity.magnitude * Time.deltaTime,
            cubeLayerMask
        );

        foreach (RaycastHit hit in hits)
        {
            CubeController cube = hit.collider.GetComponent<CubeController>();
            if (cube != null)
            {
                if (cube.TrySlice(hit.point, currentVelocity.normalized, saberType))
                {
                    OnSuccessfulSlice();
                }
            }
        }
    }

    private void OnSuccessfulSlice()
    {
        // Воспроизводим звук
        if (sliceSound != null)
        {
            sliceSound.Play();
        }

        // Визуальные эффекты
        CreateSliceSpark();
    }

    private void CreateSliceSpark()
    {
        // Создаем эффект искр при разрубании
        GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spark.transform.position = transform.position;
        spark.transform.localScale = Vector3.one * 0.1f;

        Renderer renderer = spark.GetComponent<Renderer>();
        renderer.material.color = saberColor;

        // Добавляем физику для эффекта
        Rigidbody rb = spark.AddComponent<Rigidbody>();
        rb.AddForce(Random.insideUnitSphere * 5f, ForceMode.Impulse);

        // Уничтожаем через время
        Destroy(spark, 1f);
    }

    // Для VR-контроллеров
    public void SetSaberPosition(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;
    }
}