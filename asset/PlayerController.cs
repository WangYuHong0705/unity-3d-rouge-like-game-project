using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 4f;
    public float rotateSpeed = 10f;

    [Header("Roll Settings")]
    public float rollSpeed = 8f;
    public float rollDuration = 0.6f;
    public float rollCooldown = 1.0f;

    [Header("Attack Settings")]
    public float attackCooldown = 1.0f;
    public float attackDuration = 0.8f;

    [Header("Fireball Settings")]
    public float fireballCooldown = 1.0f;  // 冷卻時間
    public float fireballDuration = 1.0f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip swordSlashClip;
    public AudioClip rollClip;

    [Header("References")]
    public Animator animator;
    public Transform cameraTransform;

    private CharacterController controller;
    private PlayerAttack playerAttack;
    private PlayerState playerState;
    private FireballSkill fireballSkill;
    private ShieldSkill shieldSkill;
    private InvisibilitySkill invisSkill;

    private bool isRolling = false;
    private bool isAttacking = false;
    private bool isCastingFireball = false;
    private bool isAimingFireball = false;

    private float rollTimer = 0f;
    private float rollCooldownTimer = 0f;
    private float attackTimer = 0f;
    private float attackCooldownTimer = 0f;
    private float fireballTimer = 0f;
    private float fireballCooldownTimer = 0f;

    private Vector3 rollDirection = Vector3.zero;

    private float gravity = -20f;   // 模擬地心引力
    private float velocityY = 0f;   // 垂直速度

    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerAttack = GetComponent<PlayerAttack>();
        playerState = GetComponent<PlayerState>();
        fireballSkill = GetComponent<FireballSkill>();
        shieldSkill = GetComponent<ShieldSkill>();
        invisSkill = GetComponent<InvisibilitySkill>();
    }

    void Update()
    {
        // --- NEW CODE: Check for Falling ---
        
        if (transform.position.y < -300f)
        {
            // Reset velocity so we don't continue falling fast
            velocityY = 0f;

            // CharacterController requires being disabled briefly to teleport reliably
            controller.enabled = false;

            // Keep X and Z, set Y to 5
            transform.position = new Vector3(0, 5f, 0);

            controller.enabled = true;
        }
        
        // -----------------------------------

        HandleTimers();

        if (controller.isGrounded && velocityY < 0)
            velocityY = -2f; // 讓角色貼地
        velocityY += gravity * Time.deltaTime;

        //翻滾中時，完全禁止動作
        if (isRolling)
        {
            controller.Move(rollDirection * rollSpeed * Time.deltaTime);
            return;
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            KillAllEnemies();
        }

        HandleRoll();
        HandleAttack();
        HandleFireball();
        HandleShield();
        HandleInvisibility();
        HandleMovement();
    }

    void HandleTimers()
    {
        // 翻滾時間計時
        if (isRolling)
        {
            rollTimer -= Time.deltaTime;
            if (rollTimer <= 0)
                isRolling = false;
        }

        // 翻滾冷卻時間
        if (rollCooldownTimer > 0)
            rollCooldownTimer -= Time.deltaTime;

        // 攻擊時間
        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
                isAttacking = false;
        }

        if (attackCooldownTimer > 0)
            attackCooldownTimer -= Time.deltaTime;

        if (isCastingFireball)
        {
            fireballTimer -= Time.deltaTime;
            if (fireballTimer <= 0)
            {
                isCastingFireball = false; // 火球結束，回到可動狀態
            }
        }

        if (fireballCooldownTimer > 0)
            fireballCooldownTimer -= Time.deltaTime;
    }

    void HandleMovement()
    {
        if (isAttacking || isCastingFireball || isAimingFireball)
        {
            animator.SetFloat("Speed", 0);
            return;
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 inputDir = new Vector3(h, 0, v).normalized;

        animator.SetFloat("Speed", inputDir.magnitude);

        Vector3 moveDir = Vector3.zero;

        if (inputDir.magnitude > 0)
        {
            // 把相機的 forward & right 取出，只保留水平分量
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            // 將輸入方向轉換為相機方向座標系
            moveDir = camForward * v + camRight * h;
            moveDir.Normalize();

            // 角色朝向移動方向
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, Time.deltaTime * rotateSpeed);

            // 實際移動
            controller.Move(moveDir * moveSpeed * Time.deltaTime);
        }

        Vector3 move = moveDir * moveSpeed;
        move.y = velocityY;
        controller.Move(move * Time.deltaTime);
    }

    void HandleRoll()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isRolling && !isCastingFireball && rollCooldownTimer <= 0)
        {
            isAttacking = false;
            isCastingFireball = false;

            animator.ResetTrigger("Attack");
            animator.ResetTrigger("Fireball");
            StartRoll();
        }
    }

    void HandleAttack()
    {
        // 攻擊中可被翻滾打斷
        if (Input.GetKeyDown(KeyCode.Space) && isAttacking && rollCooldownTimer <= 0)
        {
            // 取消攻擊
            isAttacking = false;
            animator.ResetTrigger("Attack");

            // 立即翻滾
            StartRoll();
            return;
        }


        // 正常攻擊觸發（不能在翻滾時）
        if (Input.GetMouseButtonDown(0) && !isRolling && !isAttacking && attackCooldownTimer <= 0)
        {
            isAttacking = true;
            attackTimer = attackDuration;
            attackCooldownTimer = attackCooldown;
            animator.SetTrigger("Attack");
            animator.SetFloat("Speed", 0);

            if (audioSource != null && swordSlashClip != null)
                audioSource.PlayOneShot(swordSlashClip);

        }
    }

    void HandleFireball()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isCastingFireball && rollCooldownTimer <= 0)
        {
            isCastingFireball = false;
            animator.ResetTrigger("Fireball");
            StartRoll();
            return;
        }

        if (Input.GetMouseButtonDown(1) && !isRolling)
        {
            // 冷卻未結束 → 不允許瞄準（不扣能量！）
            if (!fireballSkill.IsReady())
            {
                Debug.Log("Fireball still cooling down.");
                return;
            }

            // 能量不足 → 不允許瞄準（不扣能量！）
            if (playerState.currentEnergy < fireballSkill.energyCost)
            {
                playerState.ShowFloatText("Not enough energy!", Color.red);
                return;
            }

            // 可以開始瞄準
            isAimingFireball = true;
            animator.SetBool("Aiming", true);
            fireballSkill.BeginAiming();
            return;
        }

        if (Input.GetMouseButton(1) && isAimingFireball)
        {
            Vector3 lookDir = cameraTransform.forward;
            lookDir.y = 0f;

            if (lookDir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(lookDir);

            fireballSkill.UpdateAiming(fireballSkill.GetMouseWorldPoint());
            return;
        }

        if (Input.GetMouseButtonUp(1) && isAimingFireball)
        {
            isAimingFireball = false;
            animator.SetBool("Aiming", false);
            fireballSkill.EndAiming();

            if (!fireballSkill.TryCast())
            {
                Debug.Log("Fireball cast failed!");
                return;
            }

            animator.SetTrigger("Fireball");
            Debug.Log("Fireball cast succeeded!");
            animator.SetFloat("Speed", 0);

            isCastingFireball = true;
            fireballTimer = fireballDuration;
        }
    }


    void HandleShield()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (shieldSkill != null && shieldSkill.IsReady())
            {
                if (shieldSkill.TryCast()) // 會自動檢查能量 + 冷卻
                {
                    Debug.Log("Shield Activated!");
                }
            }
            else
            {
                Debug.Log("Shield not ready!");
            }
        }
    }

    void HandleInvisibility()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            // 冷卻中
            if (!invisSkill.IsReady())
            {
                playerState.ShowFloatText("Invisibility cooling!", Color.yellow);
                return;
            }

            // 能量不足
            if (playerState.currentEnergy < invisSkill.energyCost)
            {
                playerState.ShowFloatText("Not enough energy!", Color.red);
                return;
            }

            // 技能啟動
            if (invisSkill.TryCast())
            {
                Debug.Log("Invisibility Activated!");
            }
        }
    }

    void StartRoll()
    {
        animator.SetTrigger("Roll");

        if (audioSource != null && rollClip != null)
            audioSource.PlayOneShot(rollClip);

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 inputDir = new Vector3(h, 0, v).normalized;

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = camForward * v + camRight * h;
        moveDir.Normalize();

        if (moveDir.magnitude > 0.1f)
            rollDirection = moveDir;
        else
            rollDirection = transform.forward;

        if (playerAttack.isAttacking)
        {
            playerAttack.EndAttackHitbox();
        }

        isRolling = true;
        rollTimer = rollDuration;
        rollCooldownTimer = rollCooldown;
    }

    public bool IsRolling() => isRolling;

    void KillAllEnemies()
    {
        EnemyState[] enemies = FindObjectsOfType<EnemyState>();

        foreach (var enemy in enemies)
        {
            if (!enemy.IsDead)
            {
                enemy.TakeDamage(enemy.maxHealth);
            }
        }

        Debug.Log($"[G] 已擊殺 {enemies.Length} 名敵人。");
    }
}