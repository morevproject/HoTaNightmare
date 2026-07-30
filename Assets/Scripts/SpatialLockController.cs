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
    public GameObject defeatWindow;   // --- НОВОЕ: Окно поражения ---

    [Header("Таймер")]
    public TimerSettings timerSettings; 
    public TextMeshProUGUI timerText;   
    private float currentTime;
    private bool isTimerRunning = false;

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

    private Renderer pOuterRend, pInnerRend, tOuterRend, tInnerRend;
    private Color currentOuterColor;
    private Color currentInnerColor;

    private bool isVisionActive = false;
    private bool isLevelWon = false;
    private bool isLevelLost = false; // --- НОВОЕ: Состояние поражения ---

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
                if (mainCamera != null) mainCamera.fieldOfView = normalFOV;
                GenerateLevel(currentPlayerLevel);
            }
            return; 
        }

        // 2. Ожидание тапа при поражении (НОВОЕ)
        if (isLevelLost)
        {
            if (Input.GetMouseButtonDown(0))
            {
                // Защита от падения ниже 1 уровня уже тут!
                currentPlayerLevel = Mathf.Max(1, currentPlayerLevel - 1);
                if (mainCamera != null) mainCamera.fieldOfView = normalFOV;
                GenerateLevel(currentPlayerLevel);
            }
            return;
        }

        // 3. Логика таймера
        if (isTimerRunning)
        {
            currentTime -= Time.deltaTime;

            if (currentTime <= 0f)
            {
                currentTime = 0f;
                HandleLoss();
            }
            else
            {
                UpdateTimerUI();
            }
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

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log($"Честная ошибка совпадения (без фона): {GetErrorPercentage():F2}%");
        }
    }

    void UpdateTimerUI()
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(currentTime / 60F);
        int seconds = Mathf.FloorToInt(currentTime % 60F);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        if (currentTime <= 10f)
        {
            timerText.color = Color.red;
            float scale = 1f + Mathf.PingPong(Time.time * 0.6f, 0.3f);
            timerText.transform.localScale = new Vector3(scale, scale, 1f);
        }
        else
        {
            timerText.color = Color.white;
            timerText.transform.localScale = Vector3.one;
        }
    }

    void HandleLoss()
    {
        isTimerRunning = false;
        isLevelLost = true; // Включаем режим ожидания тапа
        
        // Показываем плашку поражения
        if (defeatWindow != null) defeatWindow.SetActive(true);
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

        GameObject pOuterObj = Instantiate(outerPrefab, transform);
        playerOuterShape = pOuterObj.transform;
        GameObject pInnerObj = Instantiate(innerPrefab, playerOuterShape);
        playerInnerShape = pInnerObj.transform;

        GameObject tOuterObj = Instantiate(outerPrefab, targetAnchor);
        targetOuterShape = tOuterObj.transform;
        GameObject tInnerObj = Instantiate(innerPrefab, targetOuterShape);
        targetInnerShape = tInnerObj.transform;

        pOuterRend = playerOuterShape.GetComponentInChildren<Renderer>();
        pInnerRend = playerInnerShape.GetComponentInChildren<Renderer>();
        tOuterRend = targetOuterShape.GetComponentInChildren<Renderer>();
        tInnerRend = targetInnerShape.GetComponentInChildren<Renderer>();

        pOuterRend.material = outerMaterial;
        tOuterRend.material = outerMaterial;
        pInnerRend.material = innerMaterial;
        tInnerRend.material = innerMaterial;

        pOuterRend.material.renderQueue = 3000;
        tOuterRend.material.renderQueue = 3000;
        pInnerRend.material.renderQueue = 3001;
        tInnerRend.material.renderQueue = 3001;

        float hue1 = Random.Range(0f, 1f);
        float hue2 = (hue1 + Random.Range(0.3f, 0.7f)) % 1f;

        currentOuterColor = Color.HSVToRGB(hue1, 0.8f, 0.9f);
        currentInnerColor = Color.HSVToRGB(hue2, 0.8f, 0.9f);

        SetupVisuals(playerOuterShape, playerInnerShape);
        SetupVisuals(targetOuterShape, targetInnerShape);

        targetOuterShape.rotation = Quaternion.Euler(Random.Range(-180f, 180f), Random.Range(-180f, 180f), 0);
        targetInnerShape.rotation = Quaternion.Euler(Random.Range(-180f, 180f), Random.Range(-180f, 180f), 0);
        playerOuterShape.rotation = Quaternion.Euler(Random.Range(-180f, 180f), Random.Range(-180f, 180f), 0);
        playerInnerShape.rotation = Quaternion.Euler(Random.Range(-180f, 180f), Random.Range(-180f, 180f), 0);

        isVisionActive = false;
        
        if (visionController != null) visionController.ResetToNormal();
        
        UpdateMaterials();

        // --- Сброс состояний окон и победы/поражения ---
        if (victoryWindow != null) victoryWindow.SetActive(false);
        if (defeatWindow != null) defeatWindow.SetActive(false); // Прячем окно поражения при старте уровня
        isLevelWon = false;
        isLevelLost = false;

        if (timerSettings != null)
        {
            currentTime = timerSettings.GetTimeForLevel(currentPlayerLevel);
            isTimerRunning = true;
            UpdateTimerUI(); 
        }
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
            isTimerRunning = false; 
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
            Color32 cP = pPlayer[i];
            Color32 cT = pTarget[i];

            bool isPlayerBg = cP.r > 250 && cP.g > 250 && cP.b > 250;
            bool isTargetBg = cT.r > 250 && cT.g > 250 && cT.b > 250;

            if (isPlayerBg && isTargetBg) continue;

            checkedPixels++;

            if (Mathf.Abs(cP.r - cT.r) > 10 ||
                Mathf.Abs(cP.g - cT.g) > 10 ||
                Mathf.Abs(cP.b - cT.b) > 10)
            {
                wrongPixels++;
            }
        }

        if (checkedPixels == 0) return 0f;

        return (wrongPixels * 100f) / checkedPixels;
    }
}