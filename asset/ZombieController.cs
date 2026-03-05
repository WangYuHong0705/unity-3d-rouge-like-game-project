using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ZombieController : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public Transform player;

    [Header("Settings")]
    public float detectionRange = 5f;      // 偵測距離
    public float loseInterestRange = 10f;   // 離開追擊範圍
    public float moveSpeed = 2f;           // 移動速度

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip screamClip;

    public float attackRange = 1.2f;       // 攻擊距離
    public float attackInterval = 2.5f;    // 攻擊冷卻
    public int attackDamage = 10;

    private float attackTimer = 0f;
    private bool isDead = false;
    private bool isDying = false;
    private bool isScreaming = false;
    private bool isChasing = false;
    private bool isAttacking = false;

    public float gravity = -20f;
    private float velocityY = 0f;
    private PlayerState playerState;
    private CharacterController controller;

    private InvisibilitySkill invis;


    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator.SetBool("run", false);
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerState = playerObj.GetComponent<PlayerState>();
            invis = playerObj.GetComponent<InvisibilitySkill>();
        }
        else
        {
            Debug.LogError("ZombieController: Player with tag 'Player' not found!");
        }
    }

    void Update()
    {
        if (isDead || isDying || player == null)
            return;

        bool playerInvisible = (invis != null && invis.IsInvisible());
        float distance = Vector3.Distance(transform.position, player.position);
        attackTimer += Time.deltaTime;

        if (controller.isGrounded && velocityY < 0)
            velocityY = -2f;
        velocityY += gravity * Time.deltaTime;

        if (playerInvisible)
        {
            animator.SetBool("run", false);
            isChasing = false;
            return;
        }


        //偵測進入範圍 → 尖叫一次
        if (!isScreaming && !isChasing && distance <= detectionRange)
        {
            //Debug.Log($"Zombie Detected Player | distance = {distance:F2}");
            StartCoroutine(ScreamThenChase());
            return;
        }

        //玩家離太遠 → 停止追擊回 idle
        if (isChasing && distance > loseInterestRange)
        {
            //Debug.Log("Player escaped! Zombie back to idle.");
            isChasing = false;
            animator.SetBool("run", false);
            return;
        }

        //尖叫或攻擊中 → 不移動
        if (isScreaming || isAttacking)
        {
            controller.Move(Vector3.up * velocityY * Time.deltaTime);
            return;
        }

        Vector3 moveDir = Vector3.zero;

        //追擊階段
        if (isChasing && distance > attackRange)
        {
            FacePlayer();
            animator.SetBool("run", true);
            moveDir = (player.position - transform.position).normalized;
            moveDir.y = 0;
        }
        else
        {
            animator.SetBool("run", false);
        }

        //攻擊階段
        if (isChasing && distance <= attackRange)
        {
            //Debug.Log("Zombie try Attack!");
            FacePlayer();
            if (!isAttacking && attackTimer >= attackInterval)
            {
                Debug.Log("Zombie Attack!");
                isAttacking = true;
                attackTimer = 0f;
                animator.SetTrigger("attack");
                Invoke(nameof(ResetAttack), 1.0f);
            }
        }

        Vector3 finalMove = moveDir * moveSpeed;
        finalMove.y = velocityY;
        controller.Move(finalMove * Time.deltaTime);
    }

    void FacePlayer()
    {
        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 10f);
        }
    }

    System.Collections.IEnumerator ScreamThenChase()
    {
        isScreaming = true;
        animator.SetTrigger("scream");
        if (audioSource && screamClip)
            audioSource.PlayOneShot(screamClip);
        animator.SetBool("run", true);

        Debug.Log("Zombie Scream!");

        float screamTime = 2f;
        float timer = 0f;
        while (timer < screamTime)
        {
            FacePlayer(); // 持續面向玩家
            timer += Time.deltaTime;
            yield return null;
        }

        Debug.Log("Zombie Start Chasing!");

        isChasing = true;
        isScreaming = false;
    }

    void ResetAttack() => isAttacking = false;

    void TryAttackPlayer()
    {
        if (player == null || playerState == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= attackRange + 0.2f) // 稍微放寬容差
        {
            Debug.Log("Zombie hit player!");
            playerState.TakeDamage(attackDamage);
        }
        else
        {
            Debug.Log("Zombie attack missed.");
        }
    }

    public void OnDeath()
    {
        enabled = false;

        isChasing = false;
        isAttacking = false;
        isScreaming = false;

        StopAllCoroutines();
        CancelInvoke();

        // 停止移動
        if (controller != null)
            controller.enabled = false;
    }
}
