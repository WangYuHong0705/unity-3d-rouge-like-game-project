using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SkeletonController : MonoBehaviour
{
    [Header("Shared Enemy State")]
    public EnemyState enemyState;

    // target will be set when player enters vision
    public GameObject target;

    // prefab for patrol flag
    public bool TogglePatrol;
    public GameObject patrolFlagPrefab;
    public float patrolFlagSpawnOuterRadius = 7f;
    public float patrolFlagSpawnInnerRadius = 4f;
    public float idleTimeMean = 5f;
    public float idleTimeVariance = 2f;
    private float _idleTimer = 0f;

    [Header("References")]
    public NavMeshAgent agent;
    public Animator animator;
    public Collider attackBox;

    [Tooltip("AnimatorControllers")]
    public RuntimeAnimatorController[] AnimatorControllers;

    public enum EnemyBehaviorState
    {
        Idle = 0,
        Moving = 1,
        Attacking = 2,
        Hurt = 3,
        Dead = 4
    }

    public EnemyBehaviorState currentBehaviorState = EnemyBehaviorState.Idle;

    // distance tolerance used to decide when player reached destination
    [SerializeField] public float stoppingDistance = 1.5f;

    // destination tracked inside enemy controller
    private Vector3 _destination;

    // keep last applied controller index to avoid redundant assignment
    private int _currentControllerIndex = 0;

    private float _prev_normalizedTime = 0f;

    [Header("Vision / Targeting")]
    public string playerTag = "Player";

    // New: detection ranges for targeting player
    [Header("Aggro Settings")]
    public float detectionRange = 8f;
    public float loseInterestRange = 12f;

    [Header("Stats / Settings (controller-specific)")]
    public float attackDamage = 10f;
    public float moveSpeed = 3.5f;

    // Internal for attack-box/player detection
    private bool _playerInAttackBox = false;
    private PlayerState _playerStateInBox = null;
    private bool _attackAppliedThisSwing = false;

    void Start()
    {
        // ensure references
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.stoppingDistance = stoppingDistance;
        }

        if (animator == null) animator = GetComponent<Animator>();

        if (AnimatorControllers == null)
            Debug.LogWarning("SkeletonController: AnimatorControllers not assigned.");

        if (attackBox == null) Debug.LogWarning("SkeletonController: AttackBox collider not assigned.");

        if (patrolFlagPrefab == null)
            Debug.LogWarning("SkeletonController: PatrolFlagPrefab not assigned.");

        // ensure we follow project hierarchy: use EnemyState for health / shared info
        if (enemyState == null)
        {
            enemyState = GetComponent<EnemyState>();
            if (enemyState == null)
            {
                Debug.LogWarning("SkeletonController: EnemyState not found on GameObject. Add EnemyState to follow shared enemy structure.");
            }
        }

        // wire controller -> enemyState and push animator / player references
        if (enemyState != null)
        {
            enemyState.controller = this;
            if (animator != null) enemyState.animator = animator;

            var playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null) enemyState.player = playerObj.transform;
        }

        // attach relay to attackBox so we can detect player collisions even if attackBox is a child object
        if (attackBox != null)
        {
            var relay = attackBox.gameObject.GetComponent<AttackBoxRelay>();
            if (relay == null) relay = attackBox.gameObject.AddComponent<AttackBoxRelay>();
            relay.owner = this;
        }

        ApplyState(currentBehaviorState, true);
    }

    void Update()
    {
        // update player reference (keeps in sync if EnemyState has it)
        Transform playerTransform = null;
        if (enemyState != null && enemyState.player != null) playerTransform = enemyState.player;
        else
        {
            var p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) playerTransform = p.transform;
        }

        // Vision / Targeting logic (new): set/clear player target based on distance
        UpdateVision(playerTransform);

        // if enemyState marks enemy dead, ensure controller transitions
        if (enemyState != null)
        {
            // if EnemyState was killed externally, sync state
            if (enemyState.IsDead && currentBehaviorState != EnemyBehaviorState.Dead)
            {
                ApplyState(EnemyBehaviorState.Dead);
            }
        }

        // Update Behavior
        UpdateBehavior();

        // Update Animator
        UpdateAnimation();
    }

    private void UpdateVision(Transform playerTransform)
    {
        if (playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        if (target == null || !target.CompareTag(playerTag))
        {
            if (dist <= detectionRange)
            {
                target = playerTransform.gameObject;
            }
        }
        else
        {
            // currently targeting player, drop if too far
            if (dist > loseInterestRange)
            {
                // only drop player target if not currently attacking
                if (currentBehaviorState != EnemyBehaviorState.Attacking)
                {
                    target = null;
                }
            }
        }
    }

    private void UpdateBehavior()
    {
        switch (currentBehaviorState)
        {
            case EnemyBehaviorState.Idle:
                UpdateIdle();
                break;
            case EnemyBehaviorState.Moving:
                UpdateMoving();
                break;
            case EnemyBehaviorState.Attacking:
                break;
            case EnemyBehaviorState.Hurt:
                break;
            case EnemyBehaviorState.Dead:
                UpdateDead(); // Check for animation finish and destroy
                break;
            default:
                break;
        }
        // Update Attack Box (this now also applies damage to player when in box)
        UpdateAttackBox();
    }

    private void UpdateIdle()
    {
        // if not patrolling, skip patrol logic
        if (!TogglePatrol) return;

        // if target is set, if reached target, destroy it else do nothing
        if (target != null)
        {
            if (target.CompareTag("PatrolFlag") &&
               Vector3.Distance(transform.position, target.transform.position) <= stoppingDistance)
            {
                Debug.Log("Reach PatrolFlag");
                // reached patrol flag -> destroy it
                Destroy(target);
            }
            else
            {
                return;
            }
        }
        // has no target -> countdown idle timer
        if (_idleTimer > 0f)
        {
            _idleTimer -= Time.deltaTime;
            return;
        }
        // spawn patrol flag
        SpawnFlag();
        // reset idle timer
        _idleTimer = idleTimeMean + Random.Range(-idleTimeVariance, idleTimeVariance);
    }

    private void UpdateMoving()
    {
        // if we have a target, keep destination updated
        if (target != null && agent.isStopped == false)
        {
            _destination = target.transform.position;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.SetDestination(_destination);
            }
            agent.speed = moveSpeed;
            //agent.angularSpeed = 270f; // Optional rotation speed
        }
    }

    private void UpdateDead()
    {
        // Wait for the Death animation to complete before destroying
        // Ensure your Death Animation Clip has "Loop Time" UNCHECKED in the Inspector
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        // Check if normalizedTime is >= 1 (animation finished) and not transitioning
        if (stateInfo.normalizedTime >= 1.0f && !animator.IsInTransition(0))
        {
            Debug.Log("SkeletonController: Death animation finished. Destroying.");

            // Clean up patrol flag if it exists
            if (target != null && target.CompareTag("PatrolFlag"))
            {
                Destroy(target);
            }

            Destroy(gameObject);
        }
    }

    private void UpdateAttackBox()
    {
        if (animator == null || attackBox == null) return;

        // enable attack box collider during attack animation at specific interval
        float normTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
        bool shouldEnable = (currentBehaviorState == EnemyBehaviorState.Attacking && normTime >= 0.2f && normTime <= 0.5f);

        if (shouldEnable)
        {
            // when enabling for a new swing, reset flag so damage can be applied once per swing
            if (!attackBox.enabled)
            {
                _attackAppliedThisSwing = false;
            }

            attackBox.enabled = true;

            // Apply damage once per swing while player is inside the attack box
            if (!_attackAppliedThisSwing && _playerInAttackBox && _playerStateInBox != null)
            {
                // forward damage to player
                _playerStateInBox.TakeDamage((int)attackDamage);
                _attackAppliedThisSwing = true;
            }
        }
        else
        {
            attackBox.enabled = false;
            // reset for next swing
            _attackAppliedThisSwing = false;
        }
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;

        switch (currentBehaviorState)
        {
            case EnemyBehaviorState.Idle:
                UpdateIdleAnimation();
                break;
            case EnemyBehaviorState.Moving:
                UpdateMovingAnimation();
                break;
            case EnemyBehaviorState.Attacking:
                UpdateAttackingAnimation();
                break;
            case EnemyBehaviorState.Hurt:
                UpdateHurtAnimation();
                break;
            case EnemyBehaviorState.Dead:
                // Dead animation is triggered by state, logic handled in UpdateDead
                break;
        }

        // apply correct animator controller
        UpdateAnimationController();
        _prev_normalizedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
    }

    private void UpdateIdleAnimation()
    {
        if (target == null) return;

        // see if in stopping distance
        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist <= stoppingDistance)
        {
            // in range -> check if target is tagged as player
            if (target.CompareTag(playerTag))
            {
                ApplyState(EnemyBehaviorState.Attacking);
            }
            else
            {
                ApplyState(EnemyBehaviorState.Idle);
            }
        }
        else
        {
            ApplyState(EnemyBehaviorState.Moving);
        }
    }

    private void UpdateMovingAnimation()
    {
        if (target == null)
        {
            ApplyState(EnemyBehaviorState.Idle);
            return;
        }

        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist <= stoppingDistance)
        {
            ApplyState(EnemyBehaviorState.Idle);
        }
        else
        {
            ApplyState(EnemyBehaviorState.Moving);
        }
    }

    private void UpdateAttackingAnimation()
    {
        // check if attack animation finished (using loop check or normalized time > 1)
        if (_prev_normalizedTime > animator.GetCurrentAnimatorStateInfo(0).normalizedTime)
        {
            ApplyState(EnemyBehaviorState.Idle);
        }
        else
        {
            ApplyState(EnemyBehaviorState.Attacking);
        }
    }

    private void UpdateHurtAnimation()
    {
        // check if hurt animation finished
        if (_prev_normalizedTime > animator.GetCurrentAnimatorStateInfo(0).normalizedTime)
        {
            ApplyState(EnemyBehaviorState.Idle);
        }
        else
        {
            ApplyState(EnemyBehaviorState.Hurt);
        }
    }

    private void UpdateDeadAnimation()
    {
        // This is handled by the state transition and UpdateDead()
    }

    private void UpdateAnimationController()
    {
        int idx = (int)currentBehaviorState;
        if (AnimatorControllers != null && idx >= 0 && idx < AnimatorControllers.Length)
        {
            if (_currentControllerIndex != idx)
            {
                var ctrl = AnimatorControllers[idx];
                if (ctrl != null)
                {
                    animator.runtimeAnimatorController = ctrl;
                    _currentControllerIndex = idx;
                }
            }
        }
    }

    private void ApplyState(EnemyBehaviorState newState, bool forceControllerApply = false)
    {
        if (currentBehaviorState == newState && !forceControllerApply) return;

        currentBehaviorState = newState;

        switch (newState)
        {
            case EnemyBehaviorState.Idle:
                if (agent != null)
                {
                    agent.isStopped = true;
                    _idleTimer = idleTimeMean + Random.Range(-idleTimeVariance, idleTimeVariance);
                }
                break;
            case EnemyBehaviorState.Moving:
                if (agent != null) agent.isStopped = false;
                break;
            case EnemyBehaviorState.Attacking:
                if (agent != null) agent.isStopped = true;
                break;
            case EnemyBehaviorState.Hurt:
                if (agent != null) agent.isStopped = true;
                break;
            case EnemyBehaviorState.Dead:
                if (agent != null) agent.isStopped = true;
                // Important: Ensure we don't switch back to other states
                break;
        }
    }

    // -------------------------------------------------------------
    // NEW FUNCTION: Handles Damage and triggers Death if Health <= 0
    // -------------------------------------------------------------
    public void TakeDamage(int damage)
    {
        // 1. If already dead, do nothing
        if (currentBehaviorState == EnemyBehaviorState.Dead) return;

        // 2. Forward logic to shared EnemyState
        if (enemyState != null)
        {
            enemyState.TakeDamage(damage);

            // 3. Check if dead
            if (enemyState.IsDead)
            {
                Debug.Log("SkeletonController: Health <= 0. Dying...");
                ApplyState(EnemyBehaviorState.Dead);
            }
            else
            {
                // If not dead, play Hurt animation
                ApplyState(EnemyBehaviorState.Hurt);
            }
        }
        else
        {
            // Fallback: If no EnemyState is attached, destroy immediately
            Debug.LogWarning("SkeletonController: No EnemyState found. Destroying immediately.");
            Destroy(gameObject);
        }
    }

    internal void OnAttackBoxTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (other.CompareTag(playerTag))
        {
            _playerInAttackBox = true;
            _playerStateInBox = other.GetComponent<PlayerState>();
        }
    }

    internal void OnAttackBoxTriggerExit(Collider other)
    {
        if (other == null) return;
        if (other.CompareTag(playerTag))
        {
            _playerInAttackBox = false;
            _playerStateInBox = null;
        }
    }

    private void SpawnFlag()
    {
        int max_attempts = 10;
        for (int attempt = 0; attempt < max_attempts; attempt++)
        {
            Vector3 randomSpawnOffset = Random.insideUnitSphere;
            Vector3 randomPos = transform.position
                + randomSpawnOffset.normalized * patrolFlagSpawnInnerRadius
                + randomSpawnOffset * (patrolFlagSpawnOuterRadius - patrolFlagSpawnInnerRadius);

            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                target = Instantiate(patrolFlagPrefab, hit.position, Quaternion.identity);
                //Debug.Log("SkeletonController: Spawned patrol flag at " + hit.position);
                return;
            }
        }
        Debug.Log("SkeletonController: Failed to spawn patrol flag on NavMesh after " + max_attempts + " attempts.");
    }
}

// Relay component added at runtime to the attackBox GameObject to forward trigger events
public class AttackBoxRelay : MonoBehaviour
{
    public SkeletonController owner;

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null) owner.OnAttackBoxTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (owner != null) owner.OnAttackBoxTriggerExit(other);
    }
}