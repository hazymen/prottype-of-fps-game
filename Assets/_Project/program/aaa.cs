using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class aaa : MonoBehaviour
{
    [Header("銃の設定")]
    [SerializeField] private float shootForce = 100f;
    [SerializeField] private float shootCooldown = 0.1f;
    [SerializeField] private float shootDistance = 1000f;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform muzzlePosition;
    
    [Header("サウンド・エフェクト（オプション)")]
    [SerializeField] private AudioClip[] shootSoundClips = new AudioClip[3];
    [SerializeField] private float shootVolume = 1f;
    [SerializeField] private ParticleSystem muzzleFlash;
    
    private AudioSource shootAudioSource;

    [Header("弾道表示の設定")]
    [SerializeField] private bool showTrajectory = true;
    [SerializeField] private float trajectoryDisplayTime = 0.1f;
    [SerializeField] private Color trajectoryColor = Color.yellow;
    [SerializeField] private float trajectoryWidth = 0.05f;

    [Header("射撃遅延設定")]
    [SerializeField] private float shootDelayOnWalk = 0.3f; // walk中の射撃遅延時間

    [Header("デバッグ設定")]
    [SerializeField] private bool debugHitLog = true;

    [Header("アニメーション遷移ラグ")]
    [SerializeField] private float shootToIdleDelay = 0.3f; // shoot→idle へのラグ時間
    [SerializeField] private float walkToIdleTransitionTime = 0.3f; // walk→idle移行時の銃を構える時間

    private float lastShootTime = 0f;
    private Animator animator;
    private PlayerMovement playerMovement;
    private bool isShooting = false;
    private Coroutine shootDelayCoroutine;
    private bool wasWalkingLastFrame = false;
    private float walkToIdleTransitionTimer = 0f;
    private bool isWalkToIdleTransitioning = false;

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        
        // AudioSourceを取得、なければ追加
        shootAudioSource = GetComponent<AudioSource>();
        if (shootAudioSource == null)
            shootAudioSource = gameObject.AddComponent<AudioSource>();
        
        // Animatorを取得
        animator = GetComponent<Animator>();
        
        // PlayerMovementを取得
        playerMovement = GetComponent<PlayerMovement>();
        
        // マズル位置オブジェクトを非表示にする
        if (muzzlePosition != null)
            muzzlePosition.gameObject.SetActive(false);
    }

    private void Update()
    {
        // walk→idle遷移の検出
        if (animator != null)
        {
            bool isWalkingNow = animator.GetBool("isWalking");
            bool isIdleNow = !animator.GetBool("isRunning") && !isWalkingNow;
            
            // walk状態から他の状態への遷移を検出（主にidle）
            if (wasWalkingLastFrame && !isWalkingNow && isIdleNow)
            {
                isWalkToIdleTransitioning = true;
                walkToIdleTransitionTimer = walkToIdleTransitionTime;
            }
            
            wasWalkingLastFrame = isWalkingNow;
        }
        
        // walk→idle遷移中のタイマー更新
        if (isWalkToIdleTransitioning)
        {
            walkToIdleTransitionTimer -= Time.deltaTime;
            if (walkToIdleTransitionTimer <= 0)
            {
                isWalkToIdleTransitioning = false;
            }
        }
        
        HandleShooting();
        
        // 視点の高さに合わせてaimPitchを常にリアルタイム更新
        if (animator != null && mainCamera != null)
        {
            // カメラのX軸回転（上下の角度）を取得
            float cameraPitch = mainCamera.transform.localEulerAngles.x;
            
            // 180度以上の場合は負の値に変換（-180～0）
            if (cameraPitch > 180f)
                cameraPitch -= 360f;
            
            // ノーマライズ：-90～90を0～1に変換して1.3倍率を適用
            float normalizedPitch = Mathf.Clamp01((cameraPitch + 90) / 180f * 1.0f);
            
            animator.SetFloat("aimPitch", normalizedPitch);
        }
    }

    private void HandleShooting()
    {
        // マウス左クリックで射撃
        if (Mouse.current?.leftButton.wasPressedThisFrame ?? false)
        {
            // クールダウン中はスキップ
            if (Time.time - lastShootTime < shootCooldown)
                return;
            
            // 走行中は射撃できない
            if (Keyboard.current?.leftShiftKey.isPressed ?? false)
                return;

            // 空中にいる場合は射撃できない
            if (playerMovement != null && !playerMovement.IsGrounded())
                return;

            // walk状態かを確認
            bool isWalking = animator != null && animator.GetBool("isWalking");
            
            // walk中は発砲できない
            if (isWalking)
                return;
            
            // walk→idle遷移中（銃を構える時間中）は発砲できない
            if (isWalkToIdleTransitioning)
                return;
            
            // それ以外は即座に射撃
            Shoot();
            
            lastShootTime = Time.time;
        }
    }

    private void Shoot()
    {
        // 射撃アニメーション開始（idle状態などからの即座射撃用）
        if (animator != null)
        {
            animator.SetBool("isShooting", true);
            isShooting = true;
        }

        // 即座に弾を発射
        FireBullet();
        
        // 射撃アニメーション終了（shoot→idleラグ付き）
        Invoke(nameof(EndShooting), shootToIdleDelay);
    }

    
    private void EndShooting()
    {
        if (animator != null)
        {
            animator.SetBool("isShooting", false);
            isShooting = false;
        }
    }
    
    private IEnumerator ShootWithDelay(float delay)
    {
        // 射撃アニメーション開始（walk→shoot遷移）
        if (animator != null)
        {
            animator.SetBool("isShooting", true);
            isShooting = true;
        }
        
        // 指定時間待機（walk→shoot遷移完了まで待つ）
        yield return new WaitForSeconds(delay);
        
        // 実際に弾を発射
        FireBullet();
        
        // 射撃アニメーション終了（shoot→idleラグ付き）
        Invoke(nameof(EndShooting), shootToIdleDelay);
    }
    
    private void FireBullet()
    {
        // 銃口のエフェクト
        if (muzzleFlash != null)
            muzzleFlash.Play();

        // 発砲音（ランダムに選択）
        if (shootSoundClips.Length > 0 && shootAudioSource != null)
        {
            int randomIndex = Random.Range(0, shootSoundClips.Length);
            if (shootSoundClips[randomIndex] != null)
            {
                shootAudioSource.PlayOneShot(shootSoundClips[randomIndex], shootVolume);
            }
        }

        // 銃口の位置を取得
        Vector3 shootOrigin = muzzlePosition != null ? muzzlePosition.position : mainCamera.transform.position;
        
        // レイキャストで先端から弾を発射
        Ray shootRay = new Ray(shootOrigin, mainCamera.transform.forward);
        RaycastHit hit;
        Vector3 hitPoint = shootOrigin + shootRay.direction * shootDistance;

        if (Physics.Raycast(shootRay, out hit, shootDistance))
        {
            hitPoint = hit.point;
            
            // 敵に命中した場合
            Rigidbody targetRigidbody = hit.collider.GetComponent<Rigidbody>();
            if (targetRigidbody != null)
            {
                // 敵をノックバック
                targetRigidbody.AddForce(shootRay.direction * shootForce, ForceMode.Impulse);
            }

            // 弾痕エフェクト（オプション）
            if (debugHitLog)
                Debug.Log($"命中: {hit.collider.gameObject.name}");
        }

        // 弾道を表示
        if (showTrajectory)
        {
            CreateTrajectoryLine(shootOrigin, hitPoint);
        }
    }

    private void CreateTrajectoryLine(Vector3 startPoint, Vector3 endPoint)
    {
        // 独立したGameObjectを作成
        GameObject trajectoryObject = new GameObject("TrajectoryLine");
        trajectoryObject.transform.position = Vector3.zero;  // ワールド座標の原点に配置

        // LineRendererを追加
        LineRenderer lineRenderer = trajectoryObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = trajectoryColor;
        lineRenderer.endColor = trajectoryColor;
        lineRenderer.startWidth = trajectoryWidth;
        lineRenderer.endWidth = trajectoryWidth;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);

        // 指定時間後に削除
        Destroy(trajectoryObject, trajectoryDisplayTime);
    }
}
