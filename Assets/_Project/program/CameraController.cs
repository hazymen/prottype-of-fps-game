using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("カメラ設定")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform cameraPosition;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 0.6f, 0);
    [SerializeField] private Vector3 sprintCameraOffset = new Vector3(0, 0.5f, 0);
    [SerializeField] private bool followPlayer = true;
    
    [Header("カメラ動き")]
    [SerializeField] private float smoothness = 0.1f;
    [SerializeField] private float sprintSmoothness = 0.15f;
    
    [Header("マウス操作")]
    [SerializeField] private float mouseSensitivity = 1f;
    [SerializeField] private float maxLookAngle = 90f;
    
    private Transform playerTransform;
    private Vector3 targetPosition;
    private float xRotation = 0f;

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        playerTransform = GetComponent<Transform>();
        
        // カーソルをロック
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        HandleMouseLook();
    }

    private void LateUpdate()
    {
        if (followPlayer && mainCamera != null && playerTransform != null)
        {
            UpdateCameraPosition();
        }
    }

    private void HandleMouseLook()
    {
        // マウス移動を取得
        Vector2 mouseDelta = Mouse.current?.delta.ReadValue() ?? Vector2.zero;
        
        // マウス感度を適用
        float mouseX = mouseDelta.x * mouseSensitivity * 0.01f;
        float mouseY = mouseDelta.y * mouseSensitivity * 0.01f;

        // Y軸（左右）回転 - プレイヤーを回転
        playerTransform.Rotate(Vector3.up * mouseX);

        // X軸（上下）回転 - カメラを回転
        xRotation = Mathf.Clamp(xRotation - mouseY, -maxLookAngle, maxLookAngle);
    }

    private void UpdateCameraPosition()
    {
        // 走行中かどうかを判定
        bool isSprinting = (Keyboard.current?.leftShiftKey.isPressed ?? false);
        float currentSmoothness = isSprinting ? sprintSmoothness : smoothness;
        
        // カメラの目標位置を計算
        if (cameraPosition != null)
        {
            // カメラ用の位置オブジェクトが指定されている場合は即座に設定
            mainCamera.transform.position = cameraPosition.position;
        }
        else
        {
            // 従来のオフセット方式を使用
            Vector3 currentOffset = isSprinting ? sprintCameraOffset : cameraOffset;
            targetPosition = playerTransform.position + playerTransform.TransformDirection(currentOffset);
            
            // 滑らかにカメラを移動
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPosition, currentSmoothness);
        }
        
        // カメラの回転をプレイヤーの回転 + マウスの上下回転に同期
        Quaternion targetRotation = playerTransform.rotation * Quaternion.Euler(xRotation, 0, 0);
        mainCamera.transform.rotation = targetRotation;
    }

    // 外部からカメラオフセットを変更
    public void SetCameraOffset(Vector3 offset)
    {
        cameraOffset = offset;
    }

    // マウスの上下回転量を取得
    public float GetXRotation()
    {
        return xRotation;
    }

    // 走行時カメラオフセットを変更
    public void SetSprintCameraOffset(Vector3 offset)
    {
        sprintCameraOffset = offset;
    }

    // 外部からカメラ追従をオン/オフ
    public void SetFollowPlayer(bool follow)
    {
        followPlayer = follow;
    }

    // カメラの滑らかさを変更
    public void SetSmoothness(float value)
    {
        smoothness = Mathf.Clamp01(value);
    }
    
    // マウス感度を変更
    public void SetMouseSensitivity(float value)
    {
        mouseSensitivity = Mathf.Clamp(value, 0.1f, 10f);
    }

    // カメラ参照を取得
    public Camera GetMainCamera()
    {
        return mainCamera;
    }
}
