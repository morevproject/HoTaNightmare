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
    public GameObject defeatWindow;   

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

    [Header("Управление (Гибридное)")]
    public float swipeRotationSpeed = 5f;
    public float buttonRotationSpeed = 1.5f; // Замедленная скорость для кнопок

    [Header("Зона свайпа (от 0 до 1)")]
    [Tooltip("X, Y - отступ слева и снизу. Width, Height - ширина и высота. (0, 0.2, 1, 0.8) = верхние 80% экрана")]
    public Rect swipeZone = new Rect(0f, 0.2f, 1f, 0.8f);

    // --- ПЕРЕМЕННЫЕ ДЛЯ КНОПОК ---
    private float inputRotX = 0f;
    private float inputRotY = 0f;
    private float inputRotZ = 0f;

    private Transform playerOuterShape;
    private Transform playerInnerShape;
    private Transform targetOuterShape;
    private Transform targetInnerShape;

    private Renderer pOuterRend, pInnerRend, tOuterRend, tInnerRend;
    private Color currentOuterColor;
    private Color currentInnerColor;

    private bool isVisionActive = false;
    private bool isLevelWon = false;
    private bool isLevelLost = false; 

    private Texture2D texPlayer;
    private Texture2D texTarget;

    void Start()
    {
        QualitySettings.antiAliasing = 0;

        if (mainCamera == null) mainCamera = Camera.main;

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

        if (isLevelLost)
        {
            if (Input.GetMouseButtonDown(0))
            {
                currentPlayerLevel = Mathf.Max(1, currentPlayerLevel - 1);
                if (mainCamera != null) mainCamera.fieldOfView = normalFOV;
                GenerateLevel(currentPlayerLevel);
            }
            return;
        }

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

            // --- 1. УПРАВЛЕНИЕ СВАЙПАМИ (Грубая наводка) ---
            if (Input.GetMouseButton(0))
            {
                bool isOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                
                // Переводим позицию мыши/пальца в координаты от 0 до 1
                Vector2 viewportPos = mainCamera.ScreenToViewportPoint(Input.mousePosition);
                
                // Проверяем, находится ли палец в разрешенной зоне (например, выше кнопок)
                bool inSwipeZone = swipeZone.Contains(viewportPos);

                if (!isOverUI && inSwipeZone)
                {
                    float rotX = Input.GetAxis("Mouse X") * swipeRotationSpeed;
                    float rotY = Input.GetAxis("Mouse Y") * swipeRotationSpeed;

                    if (Mathf.Abs(rotX) > 0.001f || Mathf.Abs(rotY) > 0.001f)
                    {
                        targetToRotate.Rotate(Vector3.up, -rotX, Space.World);
                        targetToRotate.Rotate(Vector3.right, rotY, Space.World);
                        isRotated = true;
                    }
                }
            }

            // --- 2. УПРАВЛЕНИЕ С UI КНОПОК (Точная доводка) ---
            if (Mathf.Abs(inputRotX) > 0.001f || Mathf.Abs(inputRotY) > 0.001f || Mathf.Abs(inputRotZ) > 0.001f)
            {
                float btnRotSpeed = buttonRotationSpeed * 30f * Time.deltaTime;
                targetToRotate.Rotate(Vector3.right, inputRotX * btnRotSpeed, Space.World);
                targetToRotate.Rotate(Vector3.up, inputRotY * btnRotSpeed, Space.World);
                targetToRotate.Rotate(Vector3.forward, inputRotZ * btnRotSpeed, Space.World);
                isRotated = true;
            }

            // Управление клавиатурой (для ПК)
            float keyRotSpeed = swipeRotationSpeed * 30f * Time.deltaTime;
            if (Input.GetKey(KeyCode.W)) { targetToRotate.Rotate(Vector3.right, keyRotSpeed, Space.World); isRotated = true; }
            if (Input.GetKey(KeyCode.S)) { targetToRotate.Rotate(Vector3.right, -keyRotSpeed, Space.World); isRotated = true; }
            if (Input.GetKey(KeyCode.A)) { targetToRotate.Rotate(Vector3.up, keyRotSpeed, Space.World); isRotated = true; }
            if (Input.GetKey(KeyCode.D)) { targetToRotate.Rotate(Vector3.up, -keyRotSpeed, Space.World); isRotated = true; }
            if (Input.GetKey(KeyCode.Q)) { targetToRotate.Rotate(Vector3.forward, keyRotSpeed, Space.World); isRotated = true; }
            if (Input.GetKey(KeyCode.E)) { targetToRotate.Rotate(Vector3.forward, -keyRotSpeed, Space.World); isRotated = true; }

            if (isRotated) CheckWinCondition();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log($"Честная ошибка совпадения (без фона): {GetErrorPercentage():F2}%");
        }
    }

    // --- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ КНОПОК UI ---
    public void SetInputX(float val) { inputRotX = val; }
    public void SetInputY(float val) { inputRotY = val; }
    public void SetInputZ(float val) { inputRotZ = val; }

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
        isLevelLost = true; 
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

        if (victoryWindow != null) victoryWindow.SetActive(false);
        if (defeatWindow != null) defeatWindow.SetActive(false); 
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