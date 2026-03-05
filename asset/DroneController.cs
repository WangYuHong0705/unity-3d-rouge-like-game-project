using UnityEngine;

public class DroneController : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    [Header("Settings")]
    public float detectionRange = 5f;
    public float loseInterestRange = 10f;
    public float attackRange = 1.0f;
    public float rotationSmooth = 5f;

    // [CHANGED] Added a fixed height setting
    public float fixedFlyHeight = 3f;

    public float followRadius = 10f;
    public float moveSpeed = 3f;
    public float moveCooldown = 1f;

    [Header("Attack Settings")]
    public GameObject missilePrefab;
    public float MissilSpeed = 20f;
    public int MissilDamage = 20;

    public float AttackCooldown = 3f;

    [Header("Death Settings")]
    public float fallSpeed = 5f;
    public float flipSpeed = 180f;

    private Vector3 spawnPosition;
    private Vector3 targetPosition;
    private Quaternion targetRotation;

    private float MoveTimer = 0f;
    private float AttackTimer = 0f;

    private bool isDead = false;
    private bool isFalling = false;
    private bool isChasing = false;
    private bool isAttacking = false;

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                player = p.transform;
            else
                Debug.LogWarning("[Drone] No player found with tag 'Player'!");
        }

        spawnPosition = transform.position;
        AttackTimer = Time.time + AttackCooldown;

        // Initialize target with the correct height immediately
        PickNewTarget();
    }

    void Update()
    {
        if (isFalling)
        {
            HandleFalling();
            return;
        }

        if (isDead || player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (!isChasing && distanceToPlayer <= detectionRange)
        {
            isChasing = true;
            Debug.Log("[Drone] Detected player (within patrol range).");
        }
        else if (isChasing && distanceToPlayer > loseInterestRange)
        {
            isChasing = false;
            Debug.Log("[Drone] Player left patrol area, back to patrol.");
            // Pick a new patrol target immediately to avoid getting stuck
            PickNewTarget();
            return;
        }

        if (isChasing)
        {
            FacePlayer();
            if (distanceToPlayer > attackRange)
            {
                MoveToPlayer();
                isAttacking = false;
            }
            else
            {
                HandleAttack();
                isAttacking = true;
            }
        }
        else
        {
            Patrol();
        }
    }

    void Patrol()
    {
        // Ignore Y axis when calculating distance for reaching destination
        Vector3 flatPos = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 flatTarget = new Vector3(targetPosition.x, 0, targetPosition.z);

        if (Vector3.Distance(flatPos, flatTarget) < 0.5f && MoveTimer <= 0f)
        {
            PickNewTarget();
            MoveTimer = moveCooldown;
        }

        // Move towards target (which now has fixed Y=3)
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        Vector3 moveDir = (targetPosition - transform.position).normalized;
        moveDir.y = 0;
        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * rotationSmooth);
        }

        MoveTimer -= Time.deltaTime;
    }

    void PickNewTarget()
    {
        Vector2 offset = Random.insideUnitCircle * followRadius;

        // [CHANGED] Force Y to fixedFlyHeight (3.0f)
        targetPosition = new Vector3(
            spawnPosition.x + offset.x,
            fixedFlyHeight,
            spawnPosition.z + offset.y
        );
    }

    void MoveToPlayer()
    {
        // [CHANGED] Force Y to fixedFlyHeight (3.0f), ignore player's Y height
        Vector3 desiredPos = new Vector3(
            player.position.x,
            fixedFlyHeight,
            player.position.z
        );

        transform.position = Vector3.MoveTowards(transform.position, desiredPos, moveSpeed * Time.deltaTime);
    }

    void FacePlayer()
    {
        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0; // Keep looking horizontally
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 10f);
        }
    }

    void HandleAttack()
    {
        if (Time.time < AttackTimer) return;
        AttackTimer = Time.time + AttackCooldown;

        if (missilePrefab == null) return;

        Vector3 spawnPos = transform.position;
        Vector3 aimPoint = player.position;
        Collider playerCollider = player.GetComponent<Collider>();
        if (playerCollider != null)
        {
            aimPoint = playerCollider.bounds.center;
        }

        Vector3 dirToPlayer = (aimPoint - spawnPos).normalized;

        // Spawn missile looking at player
        Quaternion spawnRot = Quaternion.LookRotation(dirToPlayer);
        GameObject missile = Instantiate(missilePrefab, spawnPos, spawnRot);
        Rigidbody rb = missile.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = dirToPlayer * MissilSpeed;
        }

        Missile missileComp = missile.GetComponent<Missile>();
        if (missileComp != null)
        {
            missileComp.owner = gameObject;
            missileComp.damage = MissilDamage;
        }
    }

    public void OnDeath()
    {
        if (isDead) return;
        isDead = true;
        isFalling = true;

        targetRotation = Quaternion.Euler(180f, transform.rotation.eulerAngles.y, 0f);
        Debug.Log("[Drone] Dying: flipping and falling...");
    }

    void HandleFalling()
    {
        // Only here do we allow the Y to change downwards
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            flipSpeed * Time.deltaTime
        );

        transform.position += Vector3.down * fallSpeed * Time.deltaTime;

        // Destroy if it falls too low
        if (transform.position.y < -10f)
            Destroy(gameObject);
    }
}