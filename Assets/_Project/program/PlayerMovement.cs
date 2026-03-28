using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("移動設定")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float groundDrag = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float walkJumpForce = 3f; // walk_jump用のジャンプ力（jumpForceより小さい値を推奨）
    [SerializeField] private float jumpCooldown = 0.25f;
    [SerializeField] private float jumpDelay = 0f; // ジャンプディレイ秒数
    
    [Header("地面判定")]
    [SerializeField] private float groundDragAmount = 1f;
    [SerializeField] private float airDragAmount = 0.5f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundDist = 0.5f;

    [Header("デバッグ設定")]
    [SerializeField] private bool debugIsGrounded = true;
    [SerializeField] private bool debugHitInfo = true;
    [SerializeField] private bool debugAnimationMovement = true;

    [Header("頭部IK設定")]
    [SerializeField] private Transform headBone;
    [SerializeField] private bool enableHeadLookAt = true;
    [SerializeField] private float headLookAtSmoothness = 10f;
    
    [Header("UI/オブジェクト非表示設定")]
    [SerializeField] private Transform cameraPositionObject;
    [SerializeField] private Transform gunMuzzlePositionObject;
    [SerializeField] private GameObject gunObject;
    
    [Header("足音設定")]
    [SerializeField] private AudioClip[] footstepClips = new AudioClip[3]; // 3つの足音
    [SerializeField] private float footstepVolume = 1f;
    [SerializeField] private float footstepInterval = 0.2f; // 足音の間隔（runは2歩なので0.4fの半分）
    
    private Camera mainCamera;
    private AudioSource audioSource;
    private float footstepTimer = 0f;
    private bool wasRunningLastFrame = false;
    private bool wasGroundedLastFrame = false;
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private Animator animator;
    private CameraController cameraController;
    private bool isGrounded;
    private bool readyToJump = true;
    private float horizontalInput;
    private float verticalInput;
    private bool jumpPressed = false;
    
    // walk/idle状態でのジャンプ追跡用
    private bool wasRunningBeforeJump = false; // ジャンプ前がrun状態だったか
    private bool wasInWalkBeforeAirbornestate = false; // 空中に落ちる直前がwalk状態だったか
    
    // ジャンプディレイ用
    private float jumpDelayTimer = 0f;
    private bool jumpDelayActive = false;
    private float currentJumpForce = 0f; // 現在のジャンプで使用するジャンプ力

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        
        capsuleCollider = GetComponent<CapsuleCollider>();
        animator = GetComponent<Animator>();
        cameraController = GetComponent<CameraController>();
        
        // AudioSourceを取得
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        
        // カメラを取得
        mainCamera = Camera.main;
        
        // UI/オブジェクトを非表示化
        if (cameraPositionObject != null)
            cameraPositionObject.gameObject.SetActive(false);
        if (gunMuzzlePositionObject != null)
            gunMuzzlePositionObject.gameObject.SetActive(false);
        
        // headBone が指定されていない場合は自動取得
        if (headBone == null && animator != null)
        {
            Transform headTransform = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headTransform != null)
            {
                headBone = headTransform;
                if (debugAnimationMovement)
                    Debug.Log("頭のボーンを自動取得しました: " + headBone.name);
            }
        }
    }

    private void Update()
    {
        // 地面判定（改善版）
        CheckGrounded();

        // 入力取得
        HandleInput();

        // ドラッグ調整
        ApplyDrag();

        // スピードリミット
        SpeedControl();

        // ジャンプディレイ更新
        if (jumpDelayActive)
        {
            jumpDelayTimer -= Time.deltaTime;
            if (jumpDelayTimer <= 0)
            {
                ExecuteJump();
                jumpDelayActive = false;
            }
        }

        // ジャンプ状態の設定
        if (animator != null)
        {
            AnimatorStateInfo animState = animator.GetCurrentAnimatorStateInfo(0);
            bool isInJumpState = animState.IsName("jump");
            bool isInWalkJumpState = animState.IsName("walk_jump");
            
            // 現在がrun状態か、walk/idle状態かを判定
            bool isRunningFlag = animator.GetBool("isRunning");
            bool isWalkingFlag = animator.GetBool("isWalking");
            bool isIdleFlag = !isRunningFlag && !isWalkingFlag && isGrounded; // その他はidle
            
            // run状態でのジャンプ処理（従来と同じ）
            // run → jump（isJump=true）
            if (jumpPressed && isRunningFlag)
            {
                wasRunningBeforeJump = true;
                animator.SetBool("isJump", true);
            }
            else if (isInJumpState)
            {
                animator.SetBool("isJump", true);
            }
            else
            {
                animator.SetBool("isJump", false);
            }
            
            // walk/idle状態でのジャンプ処理（新規追加）
            // walk/idle → walk_jump（isWalkjump=true）
            if (jumpPressed && (isWalkingFlag || isIdleFlag))
            {
                wasRunningBeforeJump = false;
                animator.SetBool("isWalkjump", true);
            }
            else if (isInWalkJumpState)
            {
                animator.SetBool("isWalkjump", true);
            }
            else
            {
                animator.SetBool("isWalkjump", false);
            }
            
            // 空中判定と状態遷移
            // run状態から落ちた場合：jumping（isJumping=true, isWalkjumping=false）
            // walk/idle状態から落ちた場合：walk_jumping（isWalkjumping=true, isJumping=false）
            if (!isGrounded)
            {
                // 現在空中
                if (!wasRunningBeforeJump && (isWalkingFlag || isIdleFlag || isInWalkJumpState))
                {
                    // walk/idle状態から落ちた場合
                    wasInWalkBeforeAirbornestate = true;
                    animator.SetBool("isWalkjumping", true);
                    animator.SetBool("isJumping", false);
                }
                else
                {
                    // run状態から落ちた場合
                    wasInWalkBeforeAirbornestate = false;
                    animator.SetBool("isJumping", true);
                    animator.SetBool("isWalkjumping", false);
                }
            }
            else
            {
                // 着地時
                animator.SetBool("isJumping", false);
                animator.SetBool("isWalkjumping", false);
                wasRunningBeforeJump = false;
                wasInWalkBeforeAirbornestate = false;
            }
            
            jumpPressed = false;
            
            // 銃オブジェクトの表示制御
            if (gunObject != null)
            {
                bool isRunningFlag_Gun = animator.GetBool("isRunning");
                bool isJumpFlag = animator.GetBool("isJump");
                bool isJumpingFlag = animator.GetBool("isJumping");
                bool isWalkjumpingFlag = animator.GetBool("isWalkjumping");
                
                // run、jump、jumpingのいずれかが true なら銃を非表示、walk_jumpingの場合は表示
                gunObject.SetActive(!isRunningFlag_Gun && !isJumpFlag && (!isJumpingFlag || isWalkjumpingFlag));
            }
        }
    }

    private void CheckGrounded()
    {
        Vector3 rayStartPos = transform.position + Vector3.down * (capsuleCollider != null ? capsuleCollider.height / 4f : 0f);
        
        RaycastHit hitAllLayers;
        bool rayAllLayers = Physics.Raycast(rayStartPos, Vector3.down, out hitAllLayers, groundDist);
        
        // jump状態中のみY速度で判定、それ以外はraycast結果を信用
        AnimatorStateInfo animState = animator != null ? animator.GetCurrentAnimatorStateInfo(0) : default;
        bool isInJumpState = animator != null && animState.IsName("jump");
        bool isInWalkJumpState = animator != null && animState.IsName("walk_jump");
        
        if (isInJumpState || isInWalkJumpState)
        {
            // ジャンプアニメーション中は、Y速度が下向きになるまで着地と判定しない
            isGrounded = rayAllLayers && rb.linearVelocity.y <= 0.01f;
        }
        else
        {
            // 通常と jumping/walk_jumping/run 状態は raycast 結果をそのまま使用
            isGrounded = rayAllLayers;
        }
        
        // 着地判定：前フレームで空中、今フレームで着地したら足音を再生
        if (!wasGroundedLastFrame && isGrounded)
        {
            PlayRandomFootstep();
        }
        
        // 詳細なデバッグ情報
        if (debugIsGrounded)
            Debug.Log($"IsGrounded: {isGrounded}, Y速度: {rb.linearVelocity.y}, State: {animState.shortNameHash}");
        if (rayAllLayers && debugHitInfo)
            Debug.Log($"Hit: {hitAllLayers.collider.gameObject.name} (Distance: {hitAllLayers.distance})");
        
        // 前フレームの状態を保存
        wasGroundedLastFrame = isGrounded;
        
        Debug.DrawRay(rayStartPos, Vector3.down * groundDist, Color.red);
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    // アニメーター更新後に呼ばれるコールバック、IKで頭ボーンを制御
    private void OnAnimatorIK(int layerIndex)
    {
        if (enableHeadLookAt && headBone != null && cameraController != null && animator != null)
        {
            // CameraControllerからマウスの上下回転を取得
            float xRotation = cameraController.GetXRotation();
            
            // 現在の頭ボーンの親の回転を基準に、マウスの上下回転を加える
            Quaternion targetRotation = Quaternion.Euler(xRotation, 0, 0);
            
            // 相対的な回転として適用
            headBone.localRotation = Quaternion.Lerp(headBone.localRotation, targetRotation, Time.deltaTime * headLookAtSmoothness);
        }
    }

    private void LateUpdate()
    {
        // LateUpdateで頭ボーンを強制更新（アニメーション再生中の上書き対策）
        if (enableHeadLookAt && headBone != null && cameraController != null && animator != null)
        {
            float xRotation = cameraController.GetXRotation();
            Quaternion targetRotation = Quaternion.Euler(xRotation, 0, 0);
            headBone.localRotation = Quaternion.Lerp(headBone.localRotation, targetRotation, Time.deltaTime * headLookAtSmoothness);
        }
    }

    private void HandleInput()
    {
        // WASD入力
        horizontalInput = 0f;
        verticalInput = 0f;

        if (Keyboard.current?.wKey.isPressed ?? false)
            verticalInput += 1f;
        if (Keyboard.current?.sKey.isPressed ?? false)
            verticalInput -= 1f;
        if (Keyboard.current?.dKey.isPressed ?? false)
            horizontalInput += 1f;
        if (Keyboard.current?.aKey.isPressed ?? false)
            horizontalInput -= 1f;

        // スペースキーでジャンプ
        if ((Keyboard.current?.spaceKey.wasPressedThisFrame ?? false) && isGrounded && readyToJump)
        {
            Jump();
        }
    }

    private void MovePlayer()
    {
        // 移動入力をワールド空間に変換
        Vector3 moveDirection = transform.forward * verticalInput + transform.right * horizontalInput;

        // 走る判定
        float currentSpeed = (Keyboard.current?.leftShiftKey.isPressed ?? false) ? sprintSpeed : moveSpeed;
        bool isRunning = (Keyboard.current?.leftShiftKey.isPressed ?? false);

        // 移動を適用（力の方式から直接速度操作に変更）
        if (moveDirection.magnitude > 0)
        {
            Vector3 targetVelocity = moveDirection.normalized * currentSpeed;
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }
        else if (isGrounded)
        {
            // 入力がないときは速度を0にする
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }

        // アニメーション制御
        if (animator != null)
        {
            // 歩く判定：移動入力がある かつ Shiftを押していない
            bool isWalking = moveDirection.magnitude > 0 && !isRunning;
            // 走る判定：移動入力がある かつ Shiftを押している
            bool shouldRun = isRunning && moveDirection.magnitude > 0;
            
            animator.SetBool("isWalking", isWalking);
            animator.SetBool("isRunning", shouldRun);
            
            // デバッグログ
            if (debugAnimationMovement)
                Debug.Log($"isWalking: {isWalking}, isRunning: {shouldRun}, moveDirection: {moveDirection.magnitude}, isShiftPressed: {isRunning}");
        }

        // 足音の再生処理
        bool isRunningNow = isRunning && (transform.forward * verticalInput + transform.right * horizontalInput).magnitude > 0;
        UpdateFootsteps(isRunningNow);

        // 地面にいるときのみドラッグを減らす
        if (isGrounded)
            rb.linearDamping = 0.1f;
        else
            rb.linearDamping = 0.05f;
    }

    private void UpdateFootsteps(bool isRunningNow)
    {
        if (isRunningNow && isGrounded)
        {
            footstepTimer += Time.deltaTime;
            
            if (footstepTimer >= footstepInterval)
            {
                PlayRandomFootstep();
                footstepTimer = 0f;
            }
        }
        else
        {
            // 走っていない場合、タイマーをリセット
            footstepTimer = 0f;
        }
        
        wasRunningLastFrame = isRunningNow;
    }

    private void PlayRandomFootstep()
    {
        if (footstepClips.Length == 0 || audioSource == null)
            return;
        
        // 有効なクリップをフィルタリング
        System.Collections.Generic.List<AudioClip> validClips = new System.Collections.Generic.List<AudioClip>();
        foreach (AudioClip clip in footstepClips)
        {
            if (clip != null)
                validClips.Add(clip);
        }
        
        if (validClips.Count == 0)
            return;
        
        // ランダムに選択して再生
        AudioClip selectedClip = validClips[Random.Range(0, validClips.Count)];
        audioSource.PlayOneShot(selectedClip, footstepVolume);
    }

    private void ApplyDrag()
    {
        // 地形上でのスライドを減らす
        if (isGrounded && new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude > 0.01f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.95f, rb.linearVelocity.y, rb.linearVelocity.z * 0.95f);
        }
    }

    private void SpeedControl()
    {
        // このメソッドは不要になったので、処理を簡略化
        // スプリント時は速度制限を調整
        if (Keyboard.current?.leftShiftKey.isPressed ?? false)
        {
            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (flatVel.magnitude > sprintSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * sprintSpeed;
                rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
            }
        }
    }

    private void Jump()
    {
        // ジャンプ前の状態を記録
        bool isRunningFlag = animator != null && animator.GetBool("isRunning");
        bool isWalkingFlag = animator != null && animator.GetBool("isWalking");
        bool isIdleFlag = animator != null && !isRunningFlag && !isWalkingFlag && isGrounded;
        
        if (isRunningFlag)
        {
            // run状態でのジャンプ
            wasRunningBeforeJump = true;
            currentJumpForce = jumpForce;
        }
        else if (isWalkingFlag || isIdleFlag)
        {
            // walk/idle状態でのジャンプ
            wasRunningBeforeJump = false;
            currentJumpForce = walkJumpForce;
        }

        // ジャンプアニメーション開始フラグをセット（モーション移行）
        jumpPressed = true;

        // ジャンプクールダウン開始
        readyToJump = false;
        Invoke(nameof(ResetJump), jumpCooldown);

        // ジャンプディレイ設定（実際の飛び上がり）
        if (jumpDelay > 0f)
        {
            jumpDelayTimer = jumpDelay;
            jumpDelayActive = true;
        }
        else
        {
            // ディレイなしの場合は即座にジャンプ実行
            ExecuteJump();
        }
    }

    private void ExecuteJump()
    {
        // 保存されたジャンプ力を使用してジャンプ
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, currentJumpForce, rb.linearVelocity.z);
    }

    private void ResetJump()
    {
        readyToJump = true;
    }

    // 外部から速度を設定するメソッド
    public void SetVelocity(Vector3 velocity)
    {
        rb.linearVelocity = velocity;
    }

    // 現在の速度を取得
    public Vector3 GetVelocity()
    {
        return rb.linearVelocity;
    }
}
