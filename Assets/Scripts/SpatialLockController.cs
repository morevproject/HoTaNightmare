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
    [Range(0.1f, 10f)] public float maxErrorPercentage = 2.0f; 
    public int pixelSkip = 4;        

    [Header("Настройки камеры (Зум)")]
    public Camera mainCamera;
    public float normalFOV = 60f;
    public float visionFOV = 40f;
    public float zoomSpeed = 8f;

    [Header("Настройки экрана")]
    [Range(0.1f, 1f)] public float outerScreenCoverage = 0.6f;
    [Range(0.1f, 1f)] public float innerScaleMultiplier = 0.55f; 

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

    // --- НОВЫЕ ПЕРЕМЕННЫЕ ДЛЯ ЦВЕТА И РЕНДЕРЕРОВ ---
    private Renderer pOuterRend, pInnerRend, tOuterRend, tInnerRend;
    private Color currentOuterColor;
    private Color currentInnerColor;

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
        if (isLevelWon) 
        {
            if (Input.GetMouseButtonDown(0))
            {
                currentPlayerLevel++;
                if (mainCamera != null) mainCamera.fieldOfView = normalFOV;
                GenerateLevel(currentPlayerLevel);
            }
            return; 
        }

        if (Input.GetKeyDown(KeyCode.N)) ToggleVision();

        if (mainCamera != null)
        {
            float targetFOV = isVisionActive ? visionFOV : normalFOV;
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
        }

        Transform targetToRotate = isVisionActive ? playerInnerShape : playerOuterShape;

        if (targetToRotate != null)
        {
            bool isRotated = false;

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

            float keyRotSpeed = rotationSpeed * 30f * Time.deltaTime;
            if (Input.GetKey(KeyCode.W)) { targetToRotate.Rotate(Vector3.right, keyRotSpeed, Space.World); isRotated = true; }
            if (Input.GetKey(KeyCode.S)) { targetToRotate.Rotate(Vector3.right, -keyRotSpeed, Space.World); isRotated = true; }
            if (Input.GetKey(KeyCode.A)) { targetToRotate.Rotate(Vector3.up, keyRotSpeed, Space.World); isRotated = true; }
            if (Input.GetKey(KeyCode.D)) { targetToRotate.Rotate(Vector3.up, -keyRotSpeed, Space.World); isRotated = true; }
            if (Input.GetKey(KeyCode.Q)) { targetToRotate.Rotate(Vector3.forward, keyRotSpeed, Space.World); isRotated = true; }
            if (Input.GetKey(KeyCode.E)) { targetToRotate.Rotate(Vector3.forward, -keyRotSpeed, Space.World); isRotated = true; }

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

            if (isRotated) CheckWinCondition();
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
        if (allAvailableShapes.Length < 1) return; 

        int minDifference = int.MaxValue;

        for (int i = 0; i < allAvailableShapes.Length; i++)
        {
            for (int j = 0; j < allAvailableShapes.Length; j++)
            {
                int combinedComplexity = allAvailableShapes[i].GetComponent<ShapeData>().complexityWeight + 
                                         allAvailableShapes[j].GetComponent<ShapeData>().complexityWeight;
                int diff = Mathf.Abs(combinedComplexity - playerLevel);

                if (diff < minDifference) minDifference = diff;
            }
        }

        System.Collections.Generic.List<(GameObject, GameObject)> bestPairs = new System.Collections.Generic.List<(GameObject, GameObject)>();

        for (int i = 0; i < allAvailableShapes.Length; i++)
        {
            for (int j = 0; j < allAvailableShapes.Length; j++)
            {
                int combinedComplexity = allAvailableShapes[i].GetComponent<ShapeData>().complexityWeight + 
                                         allAvailableShapes[j].GetComponent<ShapeData>().complexityWeight;
                int diff = Mathf.Abs(combinedComplexity - playerLevel);

                if (diff == minDifference) bestPairs.Add((allAvailableShapes[i], allAvailableShapes[j]));
            }
        }

        var chosenPair = bestPairs[Random.Range(0, bestPairs.Count)];

        SpawnAndSetupShapes(chosenPair.Item1, chosenPair.Item2);
        if (levelText != null) levelText.text = $"Уровень {currentPlayerLevel}";
    }

    void SpawnAndSetupShapes(GameObject outerPrefab, GameObject innerPrefab)
    {
        foreach (Transform child in transform) { if (child.name.Contains("Clone")) Destroy(child.gameObject); }
        foreach (Transform child in targetAnchor) { if (child.name.Contains("Clone")) Destroy(child.gameObject); }

        // Инстанцируем объекты
        GameObject pOuterObj = Instantiate(outerPrefab, transform);
        playerOuterShape = pOuterObj.transform;
        GameObject pInnerObj = Instantiate(innerPrefab, playerOuterShape);
        playerInnerShape = pInnerObj.transform;

        GameObject tOuterObj = Instantiate(outerPrefab, targetAnchor);
        targetOuterShape = tOuterObj.transform;
        GameObject tInnerObj = Instantiate(innerPrefab, targetOuterShape);
        targetInnerShape = tInnerObj.transform;

        // --- НОВЫЙ БЛОК: КЭШИРУЕМ РЕНДЕРЕРЫ И НАЗНАЧАЕМ МАТЕРИАЛЫ ---
        pOuterRend = playerOuterShape.GetComponentInChildren<Renderer>();
        pInnerRend = playerInnerShape.GetComponentInChildren<Renderer>();
        tOuterRend = targetOuterShape.GetComponentInChildren<Renderer>();
        tInnerRend = targetInnerShape.GetComponentInChildren<Renderer>();

        pOuterRend.material = outerMaterial;
        tOuterRend.material = outerMaterial;
        pInnerRend.material = innerMaterial;
        tInnerRend.material = innerMaterial;

        // Чиним очереди рендера (чтобы Внутренняя всегда рисовалась поверх Внешней)
        pOuterRend.material.renderQueue = 3000;
        tOuterRend.material.renderQueue = 3000;
        pInnerRend.material.renderQueue = 3001;
        tInnerRend.material.renderQueue = 3001;

        // --- НОВЫЙ БЛОК: ГЕНЕРАЦИЯ ЦВЕТА ---
        float hue1 = Random.Range(0f, 1f);
        // Сдвигаем цвет внутренней фигуры минимум на 30% по цветовому кругу, чтобы они никогда не сливались
        float hue2 = (hue1 + Random.Range(0.3f, 0.7f)) % 1f;

        // Насыщенность (0.8) и Яркость (0.9) фиксируем, чтобы цвета были сочными, но не "вырвиглазными"
        currentOuterColor = Color.HSVToRGB(hue1, 0.8f, 0.9f);
        currentInnerColor = Color.HSVToRGB(hue2, 0.8f, 0.9f);

        // Настраиваем масштаб
        SetupVisuals(playerOuterShape, playerInnerShape);
        SetupVisuals(targetOuterShape, targetInnerShape);

        // Вращения
        targetOuterShape.rotation = Quaternion.Euler(Random.Range(-180f, 180f), Random.Range(-180f, 180f), 0);
        targetInnerShape.rotation = Quaternion.Euler(Random.Range(-180f, 180f), Random.Range(-180f, 180f), 0);
        playerOuterShape.rotation = Quaternion.Euler(Random.Range(-180f, 180f), Random.Range(-180f, 180f), 0);
        playerInnerShape.rotation = Quaternion.Euler(Random.Range(-180f, 180f), Random.Range(-180f, 180f), 0);

        isVisionActive = false;
        
        if (visionController != null) visionController.ResetToNormal();
        
        // Эта функция теперь покрасит объекты и задаст правильную альфу!
        UpdateMaterials();

        if (victoryWindow != null) victoryWindow.SetActive(false);
        isLevelWon = false;
    }

    void SetupVisuals(Transform outer, Transform inner)
    {
        outer.localPosition = Vector3.zero;
        inner.localPosition = Vector3.zero;

        if (mainCamera == null) return;

        Mesh outerMesh = outer.GetComponentInChildren<MeshFilter>().sharedMesh;
        Mesh innerMesh = inner.GetComponentInChildren<MeshFilter>().sharedMesh;

        float distance = Vector3.Distance(mainCamera.transform.position, transform.position);
        float frustumHeightNormal = 2.0f * distance * Mathf.Tan(normalFOV * 0.5f * Mathf.Deg2Rad);
        float frustumWidthNormal = frustumHeightNormal * mainCamera.aspect;
        float safeNormalSize = Mathf.Min(frustumHeightNormal, frustumWidthNormal);

        float outerModelSize = outerMesh.bounds.extents.magnitude * 2f;
        if (outerModelSize < 0.001f) outerModelSize = 1f; 

        float maxOuterScale = (safeNormalSize * outerScreenCoverage) / outerModelSize;
        outer.localScale = Vector3.one * maxOuterScale;

        float minOuterExtent = Mathf.Min(outerMesh.bounds.extents.x, outerMesh.bounds.extents.y, outerMesh.bounds.extents.z);
        float maxInnerExtent = innerMesh.bounds.extents.magnitude;
        float safeInnerScale = (minOuterExtent / maxInnerExtent) * innerScaleMultiplier; 
        
        inner.localScale = Vector3.one * safeInnerScale;
    }
    
    void UpdateMaterials()
    {
        // --- ОБНОВЛЕННАЯ ЛОГИКА АЛЬФЫ И ЦВЕТА ---
        float oAlpha = isVisionActive ? visionOuterAlpha : normalOuterAlpha;
        float iAlpha = isVisionActive ? visionInnerAlpha : normalInnerAlpha;

        ApplyColorAndAlpha(pOuterRend, currentOuterColor, oAlpha);
        ApplyColorAndAlpha(tOuterRend, currentOuterColor, oAlpha);

        ApplyColorAndAlpha(pInnerRend, currentInnerColor, iAlpha);
        ApplyColorAndAlpha(tInnerRend, currentInnerColor, iAlpha);
    }

    void ApplyColorAndAlpha(Renderer rend, Color baseColor, float alpha)
    {
        if (rend != null)
        {
            Color c = baseColor;
            c.a = alpha;
            // .material автоматически обращается к созданной копии материала на объекте
            rend.material.color = c; 
        }
    }

    void CheckWinCondition()
    {
        if (playerOuterShape == null || playerInnerShape == null) return;

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
            checkedPixels++; 
            
            if (Mathf.Abs(pPlayer[i].r - pTarget[i].r) > 10 ||
                Mathf.Abs(pPlayer[i].g - pTarget[i].g) > 10 ||
                Mathf.Abs(pPlayer[i].b - pTarget[i].b) > 10)
            {
                wrongPixels++;
            }
        }

        return (wrongPixels * 100f) / checkedPixels;
    }
}