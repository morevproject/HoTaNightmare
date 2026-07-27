using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VisionController : MonoBehaviour
{
    [Header("Окружение: Обычный мир")]
    public Material normalSky;
    public GameObject normalLight;
    public GameObject normalDog; 

    [Header("Окружение: Кошмар")]
    public Material nightmareSky;
    public GameObject nightmareLight;
    public GameObject nightmareCerberus; 

    [Header("Интерфейс")]
    public Button switchVisionButton;
    public TMP_Text buttonText; 

    private bool isNightmareActive = false;

    void Start()
    {
        if (switchVisionButton != null)
        {
            // Оставляем возможность кликать, если вдруг курсор разблокируется
            switchVisionButton.onClick.AddListener(ToggleVision);
        }

        SetWorldState(false);
    }

    void Update()
    {
        // Переключаем вижн по нажатию клавиши N
        if (Input.GetKeyDown(KeyCode.N))
        {
            ToggleVision();
        }
    }

    void ToggleVision()
    {
        isNightmareActive = !isNightmareActive;
        SetWorldState(isNightmareActive);
    }

    // Добавляем публичный метод для сброса состояния из других скриптов
    public void ResetToNormal()
    {
        isNightmareActive = false;
        SetWorldState(false);
    }

    void SetWorldState(bool isNightmare)
    {
        // 1. Свет
        if (normalLight != null) normalLight.SetActive(!isNightmare);
        if (nightmareLight != null) nightmareLight.SetActive(isNightmare);

        // 2. Небо
        RenderSettings.skybox = isNightmare ? nightmareSky : normalSky;

        // 3. Объекты (Куб/Сфера)
        if (normalDog != null) normalDog.SetActive(!isNightmare);
        if (nightmareCerberus != null) nightmareCerberus.SetActive(isNightmare);

        // 4. Текст на кнопке-индикаторе
        if (buttonText != null)
        {
            // Теперь логика правильная: если мы в кошмаре - зовём СНАРУЖИ, и наоборот
            buttonText.text = isNightmare ? "СНАРУЖИ" : "ВНУТРИ";
        }
    }

    void OnDestroy()
    {
        if (switchVisionButton != null)
        {
            switchVisionButton.onClick.RemoveListener(ToggleVision);
        }
    }
}