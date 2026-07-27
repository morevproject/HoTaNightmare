using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SpatialLockController : MonoBehaviour
{
    [Header("Настройки генерации")]
    public int currentPlayerLevel = 1;
    public Transform targetAnchor;

    [Header("Интерфейс (UI)")]
    public TextMeshProUGUI levelText;
    public GameObject victoryWindow;  

    [Header("Связь с окружением")]
    public VisionController visionController;

    [Header("Пиксельная проверка (Render Textures)")]
    public RenderTexture playerRT;    
    public RenderTexture targetRT;    
    [Range(0.1f, 10f)] public float maxErrorPercentage = 2.0f;  // Теперь мы задаем % ошибки!
    public int pixelSkip = 4;        

    [Header("Настройки камеры (Зум)")]
    public Camera mainCamera;
    public float normalFOV = 60f;
    public float visionFOV = 40f;
    public float zoomSpeed = 8f;

    [Header("Настройки экрана")]
    [Range(0.1f, 1f)] public float outerScreenCoverage = 0.6f;

    [Header("Материалы (Transparent)")]
    public Material outerMaterial;
    public Material innerMaterial;

    [Header("Настройки прозрачности")]
    [Range(0, 1)] public float normalOuterAlpha = 0.6f;
    [Range(0, 1)] public float normalInnerAlpha = 0.2f;
    [Range(0, 1)] public float visionOuterAlpha = 0.1f;
    [Range(0, 1)] public float visionInnerAlpha = 0.8f;

    [Header("Управление")]
    public float rotationSpeed = 5f;

    [Header("Мобильный UI (Ползунки)")]
    public Slider sliderX;
    public Slider sliderY;
    public Slider sliderZ;

    private float lastSliderX;
    private float lastSliderY;
    private float lastSliderZ;

    private Transform playerOuterShape;
    private Transform playerInnerShape;
    private Transform targetOuterShape;
    private Transform targetInnerShape;

    private bool isVisionActive = false;
    private bool isLevelWon = false;

    private Texture2D texPlayer;
    private Texture2D texTarget;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (sliderX != null) lastSliderX = sliderX.value;
        if (sliderY != null) lastSliderY = sliderY.value;
        if (sliderZ != null) lastSliderZ = sliderZ.value;

        currentPlayerLevel = 1; 
        GenerateLevel(currentPlayerLevel);
    }

    void Update()
    {
        // 1. Ожидание тапа при победе
        if (isLevelWon) 
        {
            if (Input.GetMouseButtonDown(0))
            {
                currentPlayerLevel++;
                
                // Жестко сбрасываем камеру на нормальный вид при переходе
                if (mainCamera != null) mainCamera.fieldOfView = normalFOV;
                
                GenerateLevel(currentPlayerLevel);
            }
            return; // Блокируем всё остальное управление, пока висит окно победы
        }

        // 2. Смена вижна (Снаружи/Внутри)
        if (Input.GetKeyDown(KeyCode.N)) ToggleVision();

        // 3. Плавный зум камеры
        if (mainCamera != null)
        {
            float targetFOV = isVisionActive ? visionFOV : normalFOV;
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
        }

        Transform targetToRotate = isVisionActive ? playerInnerShape : playerOuterShape;

        if (targetToRotate != null)
        {
            bool isRotated = false;

            // 4. Управление свайпами (с проверкой на UI)
            if (Input.GetMouseButton(0))
            {
                bool isOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (!isOverUI)
                {
                    float rotX = Input.GetAxis("Mouse X") * rotationSpeed;
                    float rotY = Input.GetAxis("Mouse Y") * rotationSpeed;

                    if (Mathf.Abs(rotX) > 0.001f || Mathf.Abs(rotY) > 0.001f)
                    {
                        targetToRotate.Rotate(Vector3.up, -rotX, Space.World);
                        targetToRotate.Rotate(Vector3.right, rotY, Space.World);
                        isRotated = true;
                    }
                }
            }

            // 5. Управление клавиатурой (для ПК-тестов)
            float keyRotSpeed = rotationSpeed * 30f * Time.deltaTime;
            if (Input.GetKey(KeyCode.W)) { targetToRotate.Rotate(Vector3.right, keyRotSpeed, Space.World); isRotated = true; }
            if (Input.GetKey(KeyCode.S)) { targetToRotate.Rotate(Vector3.right, -keyRotSpeed, Space.World); isRotated = true; }
            if (Input.GetKey(KeyCode.A)) { targetToRotate.Rotate(Vector3.up, keyRotSpeed, Space.World); isRotated = true; }
            if (Input.GetKey(KeyCode.D)) { targetToRotate.Rotate(Vector3.up, -keyRotSpeed, Space.World); isRotated = true; }
            if (Input.GetKey(KeyCode.Q)) { targetToRotate.Rotate(Vector3.forward, keyRotSpeed, Space.World); isRotated = true; }
            if (Input.GetKey(KeyCode.E)) { targetToRotate.Rotate(Vector3.forward, -keyRotSpeed, Space.World); isRotated = true; }

            // 6. Управление ползунками (Мобилки)
            if (sliderX != null && sliderY != null && sliderZ != null)
            {
                float deltaX = sliderX.value - lastSliderX;
                float deltaY = sliderY.value - lastSliderY;
                float deltaZ = sliderZ.value - lastSliderZ;

                if (Mathf.Abs(deltaX) > 0.001f || Mathf.Abs(deltaY) > 0.001f || Mathf.Abs(deltaZ) > 0.001f)
                {
                    targetToRotate.Rotate(Vector3.right, deltaX, Space.World);
                    targetToRotate.Rotate(Vector3.up, deltaY, Space.World);
                    targetToRotate.Rotate(Vector3.forward, deltaZ, Space.World);
                    isRotated = true;
                }

                lastSliderX = sliderX.value;
                lastSliderY = sliderY.value;
                lastSliderZ = sliderZ.value;
            }

            // 7. Проверка победы (только если фигура сдвинулась)
            if (isRotated) CheckWinCondition();
        }

        // ОТЛАДКА ПИКСЕЛЕЙ
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log($"Ошибка совпадения: {GetErrorPercentage():F2}%");
        }
    }

    public void ToggleVision()
    {
        isVisionActive = !isVisionActive;
        UpdateMaterials();
    }

    void GenerateLevel(int playerLevel)
    {
        GameObject[] allAvailableShapes = Resources.LoadAll<GameObject>("Shapes");
        if (allAvailableShapes.Length < 2) return;

        GameObject bestOuter = null;
        GameObject bestInner = null;
        int minDifference = int.MaxValue;

        for (int i = 0; i < allAvailableShapes.Length; i++)
        {
            for (int j = 0; j < allAvailableShapes.Length; j++)
            {
                if (i == j) continue; 

                int combinedComplexity = allAvailableShapes[i].GetComponent<ShapeData>().complexityWeight + 
                                         allAvailableShapes[j].GetComponent<ShapeData>().complexityWeight;
                int diff = Mathf.Abs(combinedComplexity - playerLevel);

                if (diff < minDifference)
                {
                    minDifference = diff;
                    bestOuter = allAvailableShapes[i];
                    bestInner = allAvailableShapes[j];
                }
            }
        }

        SpawnAndSetupShapes(bestOuter, bestInner);
        if (levelText != null) levelText.text = $"Уровень {currentPlayerLevel}";
    }

    void SpawnAndSetupShapes(GameObject outerPrefab, GameObject innerPrefab)
    {
        foreach (Transform child in transform) { if (child.name.Contains("Clone")) Destroy(child.gameObject); }
        foreach (Transform child in targetAnchor) { if (child.name.Contains("Clone")) Destroy(child.gameObject); }

        GameObject pOuterObj = Instantiate(outerPrefab, transform);
        playerOuterShape = pOuterObj.transform;
        GameObject pInnerObj = Instantiate(innerPrefab, playerOuterShape);
        playerInnerShape = pInnerObj.transform;

        GameObject tOuterObj = Instantiate(outerPrefab, targetAnchor);
        targetOuterShape = tOuterObj.transform;
        GameObject tInnerObj = Instantiate(innerPrefab, targetOuterShape);
        targetInnerShape = tInnerObj.transform;

        SetupVisuals(playerOuterShape, playerInnerShape);
        SetupVisuals(targetOuterShape, targetInnerShape);

        targetOuterShape.rotation = Quaternion.Euler(Random.Range(-180f, 180f), Random.Range(-180f, 180f), 0);
        targetInnerShape.rotation = Quaternion.Euler(Random.Range(-180f, 180f), Random.Range(-180f, 180f), 0);

        playerOuterShape.rotation = Quaternion.Euler(Random.Range(-180f, 180f), Random.Range(-180f, 180f), 0);
        playerInnerShape.rotation = Quaternion.Euler(Random.Range(-180f, 180f), Random.Range(-180f, 180f), 0);

        // ВОТ ОН - ГАРАНТИРОВАННЫЙ ВОЗВРАТ В НАРУЖНЫЙ МИР
        isVisionActive = false;
        
        // Синхронизируем состояние с VisionController!
        if (visionController != null) 
        {
            visionController.ResetToNormal();
        }
        
        UpdateMaterials();

        // Скрываем окно победы при запуске нового уровня
        if (victoryWindow != null) victoryWindow.SetActive(false);
        isLevelWon = false;
    }

    void SetupVisuals(Transform outer, Transform inner)
    {
        outer.localPosition = Vector3.zero;
        inner.localPosition = Vector3.zero;

        outer.GetComponentInChildren<Renderer>().material = outerMaterial;
        inner.GetComponentInChildren<Renderer>().material = innerMaterial;

        outerMaterial.renderQueue = 3000;
        innerMaterial.renderQueue = 3001;

        if (mainCamera == null) return;

        Mesh outerMesh = outer.GetComponentInChildren<MeshFilter>().sharedMesh;
        Mesh innerMesh = inner.GetComponentInChildren<MeshFilter>().sharedMesh;

        float distance = Vector3.Distance(mainCamera.transform.position, transform.position);
        float frustumHeightNormal = 2.0f * distance * Mathf.Tan(normalFOV * 0.5f * Mathf.Deg2Rad);
        float frustumWidthNormal = frustumHeightNormal * mainCamera.aspect;
        float safeNormalSize = Mathf.Min(frustumHeightNormal, frustumWidthNormal);

        float maxOuterScale = (safeNormalSize * outerScreenCoverage) / 1.73f;
        outer.localScale = Vector3.one * maxOuterScale;

        float minOuterExtent = Mathf.Min(outerMesh.bounds.extents.x, outerMesh.bounds.extents.y, outerMesh.bounds.extents.z);
        float maxInnerExtent = innerMesh.bounds.extents.magnitude;

        float safeInnerScale = (minOuterExtent / maxInnerExtent) * 0.95f; 
        
        inner.localScale = Vector3.one * safeInnerScale;
    }

    void UpdateMaterials()
    {
        SetAlpha(outerMaterial, isVisionActive ? visionOuterAlpha : normalOuterAlpha);
        SetAlpha(innerMaterial, isVisionActive ? visionInnerAlpha : normalInnerAlpha);
    }

    void SetAlpha(Material mat, float alpha)
    {
        if (mat != null)
        {
            Color c = mat.color;
            c.a = alpha;
            mat.color = c;
        }
    }

    void CheckWinCondition()
    {
        if (playerOuterShape == null || playerInnerShape == null) return;

        // Теперь мы получаем не штуки, а процент!
        float errorPercent = GetErrorPercentage();

        if (errorPercent <= maxErrorPercentage)
        {
            isLevelWon = true;
            if (victoryWindow != null) victoryWindow.SetActive(true);
        }
    }

    float GetErrorPercentage()
    {
        if (playerRT == null || targetRT == null) return 100f;

        if (texPlayer == null || texPlayer.width != playerRT.width || texPlayer.height != playerRT.height)
        {
            texPlayer = new Texture2D(playerRT.width, playerRT.height, TextureFormat.RGB24, false);
            texTarget = new Texture2D(targetRT.width, targetRT.height, TextureFormat.RGB24, false);
        }

        RenderTexture.active = playerRT;
        texPlayer.ReadPixels(new Rect(0, 0, playerRT.width, playerRT.height), 0, 0);
        texPlayer.Apply();

        RenderTexture.active = targetRT;
        texTarget.ReadPixels(new Rect(0, 0, targetRT.width, targetRT.height), 0, 0);
        texTarget.Apply();

        RenderTexture.active = null;

        Color32[] pPlayer = texPlayer.GetPixels32();
        Color32[] pTarget = texTarget.GetPixels32();

        int wrongPixels = 0;
        int checkedPixels = 0;

        for (int i = 0; i < pPlayer.Length; i += pixelSkip)
        {
            checkedPixels++; // Считаем, сколько пикселей мы реально проверили
            
            if (Mathf.Abs(pPlayer[i].r - pTarget[i].r) > 10 ||
                Mathf.Abs(pPlayer[i].g - pTarget[i].g) > 10 ||
                Mathf.Abs(pPlayer[i].b - pTarget[i].b) > 10)
            {
                wrongPixels++;
            }
        }

        // Возвращаем процент несовпадающих пикселей от проверенных
        return (wrongPixels * 100f) / checkedPixels;
    }
}