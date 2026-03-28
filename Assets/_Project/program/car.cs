using UnityEngine;
using UnityEngine.InputSystem;

public class car : MonoBehaviour
{
    [SerializeField] private WheelCollider frontLeftWheel;
    [SerializeField] private WheelCollider frontRightWheel;
    [SerializeField] private WheelCollider rearLeftWheel;
    [SerializeField] private WheelCollider rearRightWheel;

    [SerializeField] private Transform frontLeftWheelTransform;
    [SerializeField] private Transform frontRightWheelTransform;
    [SerializeField] private Transform rearLeftWheelTransform;
    [SerializeField] private Transform rearRightWheelTransform;

    [SerializeField] private float motorForce = 100f;
    [SerializeField] private float brakingForce = 3000f;
    [SerializeField] private float maxSteerAngle = 30f;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 2f, 5f);
    [SerializeField] private float cameraFollowSmoothing = 0.1f;
    [SerializeField] private float groundDrag = 0.05f;
    [SerializeField] private float airDrag = 2f;

    private Rigidbody rb;
    private float currentMotorForce = 0f;
    private float currentBrakeForce = 0f;
    private float currentSteerAngle = 0f;
    private bool isGrounded = false;
    private float currentVelocity = 0f; // 前後方向の速度

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            // ドラッグ設定のみ適用
            rb.linearDamping = groundDrag;
            rb.angularDamping = 0.05f;
        }

        // カメラターゲットがない場合はメインカメラを使用
        if (cameraTarget == null && Camera.main != null)
        {
            cameraTarget = Camera.main.transform;
        }

        // WheelColliderを初期化
        InitializeWheels();
    }

    void InitializeWheels()
    {
        WheelCollider[] wheels = new WheelCollider[] { frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel };
        foreach (WheelCollider wheel in wheels)
        {
            if (wheel != null)
            {
                wheel.mass = 20f;
                wheel.wheelDampingRate = 0.25f;
                wheel.forceAppPointDistance = 0f;
                
                WheelFrictionCurve frictionCurve = wheel.sidewaysFriction;
                frictionCurve.extremumValue = 1f;
                frictionCurve.asymptoteValue = 0.5f;
                frictionCurve.extremumSlip = 0.2f;
                frictionCurve.asymptoteSlip = 0.5f;
                wheel.sidewaysFriction = frictionCurve;

                frictionCurve = wheel.forwardFriction;
                frictionCurve.extremumValue = 1f;
                frictionCurve.asymptoteValue = 0.5f;
                frictionCurve.extremumSlip = 0.2f;
                frictionCurve.asymptoteSlip = 0.5f;
                wheel.forwardFriction = frictionCurve;
            }
        }
    }

    void Update()
    {
        HandleInput();
        UpdateCamera();
    }

    void FixedUpdate()
    {
        // 接地判定
        isGrounded = CheckIfGrounded();
        
        // ドラッグを切り替え
        if (rb != null)
        {
            rb.linearDamping = isGrounded ? groundDrag : airDrag;
        }

        ApplyWheelForces();
        UpdateWheelVisuals();
    }

    bool CheckIfGrounded()
    {
        WheelCollider[] wheels = new WheelCollider[] { frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel };
        int groundedCount = 0;

        foreach (WheelCollider wheel in wheels)
        {
            if (wheel != null && wheel.isGrounded)
                groundedCount++;
        }

        return groundedCount >= 2; // 2つ以上のタイヤが接地している
    }

    void HandleInput()
    {
        // 現在の前後方向速度を計算（車のローカル座標で）
        if (rb != null)
        {
            Vector3 localVelocity = transform.GetComponent<Rigidbody>().linearVelocity;
            currentVelocity = Vector3.Dot(localVelocity, transform.forward);
        }

        // アクセル・ブレーキ制御
        currentMotorForce = 0f;
        currentBrakeForce = 0f;

        bool wPressed = Keyboard.current.wKey.isPressed;
        bool sPressed = Keyboard.current.sKey.isPressed;

        if (wPressed && !sPressed)
        {
            // 前進キー
            if (currentVelocity < -1f) // バック中
            {
                // バック状態なのでブレーキ
                currentBrakeForce = brakingForce;
            }
            else
            {
                // 停止中か前進中なので加速
                currentMotorForce = -motorForce;
            }
        }
        else if (sPressed && !wPressed)
        {
            // バックキー
            if (currentVelocity > 1f) // 前進中
            {
                // 前進状態なのでブレーキ
                currentBrakeForce = brakingForce;
            }
            else
            {
                // 停止中かバック中なので加速（逆方向）
                currentMotorForce = motorForce;
            }
        }

        // ステアリング制御
        currentSteerAngle = 0f;
        if (Keyboard.current.aKey.isPressed)
            currentSteerAngle = -maxSteerAngle;
        else if (Keyboard.current.dKey.isPressed)
            currentSteerAngle = maxSteerAngle;
    }

    void ApplyWheelForces()
    {
        // 前輪のステアリング + モーター力
        frontLeftWheel.steerAngle = currentSteerAngle;
        frontRightWheel.steerAngle = currentSteerAngle;

        // 後輪のモーター力
        rearLeftWheel.motorTorque = currentMotorForce;
        rearRightWheel.motorTorque = currentMotorForce;

        // ブレーキ力（全輪）
        frontLeftWheel.brakeTorque = currentBrakeForce;
        frontRightWheel.brakeTorque = currentBrakeForce;
        rearLeftWheel.brakeTorque = currentBrakeForce;
        rearRightWheel.brakeTorque = currentBrakeForce;
    }

    void UpdateWheelVisuals()
    {
        // 各タイヤの回転と位置をビジュアル用メッシュに適用
        UpdateWheelTransform(frontLeftWheel, frontLeftWheelTransform);
        UpdateWheelTransform(frontRightWheel, frontRightWheelTransform);
        UpdateWheelTransform(rearLeftWheel, rearLeftWheelTransform);
        UpdateWheelTransform(rearRightWheel, rearRightWheelTransform);
    }

    void UpdateWheelTransform(WheelCollider wheelCollider, Transform wheelTransform)
    {
        if (wheelTransform == null)
            return;

        // WheelColliderの位置と回転を取得
        Vector3 position;
        Quaternion rotation;
        wheelCollider.GetWorldPose(out position, out rotation);

        // ビジュアル用メッシュの位置と回転を更新
        wheelTransform.position = position;
        wheelTransform.rotation = rotation;
    }

    void UpdateCamera()
    {
        if (cameraTarget == null)
            return;

        // カメラの目標位置を計算
        Vector3 targetPosition = transform.position + transform.TransformDirection(cameraOffset);

        // スムーズに移動
        cameraTarget.position = Vector3.Lerp(cameraTarget.position, targetPosition, cameraFollowSmoothing);

        // 車を見つめるように設定
        cameraTarget.LookAt(transform.position + Vector3.up);
    }
}
