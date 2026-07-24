using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Ссылки")]
    public Camera playerCamera;

    [Header("Параметры движения")]
    public float walkSpeed = 4.0f;
    public float mouseSensitivity = 2.0f;

    private CharacterController controller;
    private float cameraPitch = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        
        // Если камера не назначена в инспекторе, цепляем ту, что внутри объекта
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        // Блокируем курсор в центре экрана для удобного обзора
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // 1. Вращение камеры и персонажа мышкой
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -89f, 89f); // Ограничиваем угол обзора по вертикали

        playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);

        // 2. Передвижение с клавиатуры (WASD)
        float moveZ = Input.GetAxis("Vertical");
        float moveX = Input.GetAxis("Horizontal");

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        controller.Move(move * walkSpeed * Time.deltaTime);

        // Базовая гравитация, чтобы персонаж не висел в воздухе
        if (!controller.isGrounded)
        {
            controller.Move(Vector3.down * 9.81f * Time.deltaTime);
        }
    }
}