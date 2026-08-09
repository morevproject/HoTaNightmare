using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
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

    [Header("Монетизация и Валюта")]
    public int hardCurrency = 100;
    public TextMeshProUGUI currencyText;
    public Button cheapHintBtn;
    public Button expHintBtn;
    public Button reviveBtn;
    public int reviveCost = 10;

    [Header("Кнопки вращения (для подсветки)")]
    public Image btnXPos;
    public Image btnXNeg;
    public Image btnYPos;
    public Image btnYNeg;
    public Image btnZPos;
    public Image btnZNeg;
    public Color lockedBtnColor = new Color(0.4f, 0.8f, 1f, 1f); // Голубоватый цвет
    private Color normalBtnColor = Color.white;

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
    public float buttonRotationSpeed = 1.5f; 
    public Rect swipeZone = new Rect(0f, 0.2f, 1f, 0.8f);

    // --- ПЕРЕМЕННЫЕ ДЛЯ КНОПОК И ОСЕЙ ---
    private float inputRotX = 0f;
    private float inputRotY = 0f;
    private float inputRotZ = 0f;

    private bool lockX = false;
    private bool lockY = false;
    private bool lockZ = false;

    // --- ЧИТЫ ---
    public GameObject cheatPanel; 
    private float lastTimerClickTime = 0f; 
    private bool isTimerCheatDisabled = false;

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

        UpdateCurrencyUI();
        currentPlayerLevel = 1; 
        GenerateLevel(currentPlayerLevel);
    }

    void Update()
    {
        bool isOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (isLevelWon) 
        {
            if (Input.GetMouseButtonDown(0) && !isOverUI)
            {
                currentPlayerLevel++;
                if (mainCamera != null) mainCamera.fieldOfView = normalFOV;
                GenerateLevel(currentPlayerLevel);
            }
            return; 
        }

        if (isLevelLost)
        {
            if (Input.GetMouseButtonDown(0) && !isOverUI)
            {
                currentPlayerLevel = Mathf.Max(1, currentPlayerLevel - 1);
                if (mainCamera != null) mainCamera.fieldOfView = normalFOV;
                GenerateLevel(currentPlayerLevel);
            }
            return;
        }

        if (isTimerRunning && !isTimerCheatDisabled)
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
        else if (isTimerCheatDisabled)
        {
            UpdateTimerUI(); 
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
                Vector2 viewportPos = mainCamera.ScreenToViewportPoint(Input.mousePosition);
                bool inSwipeZone = swipeZone.Contains(viewportPos);

                if (!isOverUI && inSwipeZone)
                {
                    float rotX = Input.GetAxis("Mouse X") * swipeRotationSpeed;
                    float rotY = Input.GetAxis("Mouse Y") * swipeRotationSpeed;

                    if (lockY) rotX = 0f; 
                    if (lockX) rotY = 0f; 

                    if (Mathf.Abs(rotX) > 0.001f || Mathf.Abs(rotY) > 0.001f)
                    {
                        targetToRotate.Rotate(Vector3.up, -rotX, Space.World);
                        targetToRotate.Rotate(Vector3.right, rotY, Space.World);
                        isRotated = true;
                    }
                }
            }

            float curInputX = lockX ? 0f : inputRotX;
            float curInputY = lockY ? 0f : inputRotY;
            float curInputZ = lockZ ? 0f : inputRotZ;

            if (Mathf.Abs(curInputX) > 0.001f || Mathf.Abs(curInputY) > 0.001f || Mathf.Abs(curInputZ) > 0.001f)
            {
                float btnRotSpeed = buttonRotationSpeed * 30f * Time.deltaTime;
                targetToRotate.Rotate(Vector3.right, curInputX * btnRotSpeed, Space.World);
                targetToRotate.Rotate(Vector3.up, curInputY * btnRotSpeed, Space.World);
                targetToRotate.Rotate(Vector3.forward, curInputZ * btnRotSpeed, Space.World);
                isRotated = true;
            }

            if (isRotated) CheckWinCondition();
        }
    }

    // --- ЖЕСТКАЯ ФИКСАЦИЯ ОСЕЙ КАЖДЫЙ КАДР ---
    void LateUpdate()
    {
        if (lockX || lockY || lockZ)
        {
            EnforceLockedAxes(playerOuterShape, targetOuterShape);
            EnforceLockedAxes(playerInnerShape, targetInnerShape);
        }
    }

    private void EnforceLockedAxes(Transform pShape, Transform tShape)
    {
        if (pShape == null || tShape == null) return;
        
        Vector3 pEuler = pShape.eulerAngles;
        Vector3 tEuler = tShape.eulerAngles;
        
        pShape.eulerAngles = new Vector3(
            lockX ? tEuler.x : pEuler.x,
            lockY ? tEuler.y : pEuler.y,
            lockZ ? tEuler.z : pEuler.z
        );
    }

    public void SetInputX(float val) { inputRotX = val; }
    public void SetInputY(float val) { inputRotY = val; }
    public void SetInputZ(float val) { inputRotZ = val; }

    public void BuyCheapHint()
    {
        if (hardCurrency >= 5 && GetLockedAxesCount() < 2)
        {
            hardCurrency -= 5;
            LockRandomAxis();
            UpdateCurrencyUI();
            CheckWinCondition(); 
        }
    }

    public void BuyExpensiveHint()
    {
        if (hardCurrency >= 9 && GetLockedAxesCount() == 0) 
        {
            hardCurrency -= 9;
            LockRandomAxis();
            if (GetLockedAxesCount() < 2) LockRandomAxis(); 
            UpdateCurrencyUI();
            CheckWinCondition();
        }
    }

    public void BuyExtraTime()
    {
        if (hardCurrency >= reviveCost)
        {
            hardCurrency -= reviveCost;
            UpdateCurrencyUI();
            
            currentTime += 60f; 
            isLevelLost = false;
            isTimerRunning = true;
            
            if (defeatWindow != null) defeatWindow.SetActive(false);
        }
    }

    private void LockRandomAxis()
    {
        List<char> available = new List<char>();
        if (!lockX) available.Add('X');
        if (!lockY) available.Add('Y');
        if (!lockZ) available.Add('Z');

        if (available.Count == 0) return;

        char chosen = available[Random.Range(0, available.Count)];
        
        if (chosen == 'X') lockX = true;
        if (chosen == 'Y') lockY = true;
        if (chosen == 'Z') lockZ = true;

        // Немедленно применяем фиксацию
        EnforceLockedAxes(playerOuterShape, targetOuterShape);
        EnforceLockedAxes(playerInnerShape, targetInnerShape);
        
        UpdateLockedButtonsVisuals();
    }

    private void UpdateLockedButtonsVisuals()
    {
        if (btnXPos != null) btnXPos.color = lockX ? lockedBtnColor : normalBtnColor;
        if (btnXNeg != null) btnXNeg.color = lockX ? lockedBtnColor : normalBtnColor;
        
        if (btnYPos != null) btnYPos.color = lockY ? lockedBtnColor : normalBtnColor;
        if (btnYNeg != null) btnYNeg.color = lockY ? lockedBtnColor : normalBtnColor;
        
        if (btnZPos != null) btnZPos.color = lockZ ? lockedBtnColor : normalBtnColor;
        if (btnZNeg != null) btnZNeg.color = lockZ ? lockedBtnColor : normalBtnColor;
    }

    private int GetLockedAxesCount()
    {
        return (lockX ? 1 : 0) + (lockY ? 1 : 0) + (lockZ ? 1 : 0);
    }

    public void UpdateCurrencyUI()
    {
        if (currencyText != null) currencyText.text = hardCurrency.ToString();
        
        int lockedCount = GetLockedAxesCount();
        if (cheapHintBtn != null) cheapHintBtn.interactable = (lockedCount < 2 && hardCurrency >= 5);
        if (expHintBtn != null) expHintBtn.interactable = (lockedCount == 0 && hardCurrency >= 9);
        if (reviveBtn != null) reviveBtn.interactable = (hardCurrency >= reviveCost);
    }

    public void OnTimerClicked()
    {
        if (Time.unscaledTime - lastTimerClickTime < 0.5f)
        {
            if (cheatPanel != null) 
            {
                cheatPanel.SetActive(!cheatPanel.activeSelf); 
            }
        }
        lastTimerClickTime = Time.unscaledTime;
    }

    public void CheatAddCurrency()
    {
        hardCurrency += 100;
        UpdateCurrencyUI();
    }

    public void CheatToggleTimer()
    {
        isTimerCheatDisabled = !isTimerCheatDisabled;
        if (isTimerCheatDisabled && timerText != null) timerText.color = Color.green; 
    }

    void UpdateTimerUI()
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(currentTime / 60F);
        int seconds = Mathf.FloorToInt(currentTime % 60F);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        if (isTimerCheatDisabled) return; 

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
        UpdateCurrencyUI(); 
    }

    public void ToggleVision()
    {
        isVisionActive = !isVisionActive;
        UpdateMaterials();
    }

    void GenerateLevel(int playerLevel)
    {
        lockX = lockY = lockZ = false; 
        UpdateCurrencyUI();
        UpdateLockedButtonsVisuals(); 

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

        List<(GameObject, GameObject)> bestPairs = new List<(GameObject, GameObject)>();
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

            int colorDiff = Mathf.Max(
                Mathf.Abs(cP.r - cT.r),
                Mathf.Abs(cP.g - cT.g),
                Mathf.Abs(cP.b - cT.b)
            );

            if (colorDiff > 10)
            {
                if (colorDiff > 100) wrongPixels += 2; 
                else wrongPixels++;
            }
        }

        if (checkedPixels == 0) return 0f;
        return (wrongPixels * 100f) / checkedPixels;
    }
}