using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AngelKnightBoss : MonoBehaviour
{
    // ... [保留原本的 Follow, Movement, Idle Settings] ...
    [Header("Follow Settings")]
    public Transform player;
    public float rotationSpeed = 5f;
    public float attack_rotate_speed = 2f;

    [Header("Movement Settings")]
    public float run_speed = 6f;

    [Header("Idle Settings (Global)")]
    public float idle_after_attack = 2f;

    [Header("Idle Before Specific Attacks")]
    public float idle_before_combo1 = 1.0f;
    public float idle_before_dash = 1.5f;
    public float idle_before_spell = 2.0f;
    public float idle_before_dragon = 3.0f;

    [Header("Boss Stats")]
    public float max_blood = 10000;
    public float current_blood = 10000;

    [Header("Animation Setup")]
    public Animator animator;
    public string runStateName = "Fly_2";      // 俯衝/飛行時的動畫
    public string idleStateName = "Float_Idle_3";
    public string ascendStateName = "Fly_Up";  // [NEW] 飛回高空時的動畫名稱

    // ... [保留原本 Attack Durations, Ranges, Damage Areas] ...
    [Header("Attack Durations")]
    public float time_attack1 = 3.0f;
    public float time_attack2 = 3.0f;
    public float time_spell = 4.0f;
    public float time_dragon = 30.0f;

    [Header("Attack Ranges")]
    public float dist_attack1 = 3.0f;
    public float dist_attack2 = 8.0f;
    public float dist_spell = 10.0f;

    [Header("Damage Areas")]
    public Collider damage_area1;
    public float dash_damage_amount = 200f;

    [Header("Dragon Attack Settings")]
    public string dragonTag = "angel_dragon_attack1";

    // [NEW] 這是你要控制的「起始俯衝 Y 高度」
    [Tooltip("The fixed height the dragon will fly up to before diving.")]
    public float swoop_start_height = 15f;

    [Tooltip("How fast the dragon flies back up to the start height.")]
    public float ascend_speed = 10f; // [NEW] 升空速度

    public float dragon_fly_speed = 25f;
    public float arena_radius = 25f;
    public float parabola_curvature = 0.08f;
    public float angel_land_y = 0.2f;


    private GameObject dragonObject;

    // ... [保留原本的 Sequence List, Private Variables] ...
    [Header("Linear Attack Sequence")]
    public List<string> attackOrder = new List<string>() { "Combo", "Dash", "Spell", "Dragon" };
    private int currentOrderIndex = 0;
    [HideInInspector] public string currentAttackAnimName = "";
    private bool isAttacking = false;
    private bool lock_rotation = false;
    private float currentMoveSpeed = 0f;
    private bool isDashing = false;
    private string nextAttackAnim;
    private float nextAttackRange;
    private float nextAttackDuration;
    private float nextIdleBeforeTime;
    private string currentPlanType = "Normal";
    private Coroutine behaviorCoroutine;

    // ... [Start, AngelBehaviorLoop, SetNextAttackPlan, PerformSpecificAttack 保持不變] ...
    // ... (為了節省篇幅，這裡省略中間未修改的函式，直接進入重點 PerformDragonSequence) ...

    private IEnumerator Start()
    {
        // (保持原本的 Start 內容)
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (damage_area1 != null) damage_area1.enabled = false;
        if (dragonObject == null && !string.IsNullOrEmpty(dragonTag))
        {
            GameObject foundDragon = GameObject.FindGameObjectWithTag(dragonTag);
            if (foundDragon != null)
            {
                dragonObject = foundDragon;
                dragonObject.SetActive(false);
            }
        }
        currentOrderIndex = 0;
        if (attackOrder.Count > 0) SetNextAttackPlan(attackOrder[0]);
        else { attackOrder.Add("Combo"); SetNextAttackPlan("Combo"); }
        currentMoveSpeed = 0;
        animator.Play(idleStateName);
        yield return new WaitForSeconds(1f);
        if (behaviorCoroutine != null) StopCoroutine(behaviorCoroutine);
        behaviorCoroutine = StartCoroutine(AngelBehaviorLoop());
    }

    private IEnumerator AngelBehaviorLoop()
    {
        // (保持原本的 Loop 內容)
        while (true)
        {
            if (player == null) yield break;
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer > nextAttackRange && !isAttacking)
            {
                currentMoveSpeed = run_speed;
                if (!animator.GetCurrentAnimatorStateInfo(0).IsName(runStateName))
                {
                    animator.speed = 1f; animator.Play(runStateName);
                }
                lock_rotation = false;
                yield return null;
            }
            else if (!isAttacking)
            {
                isAttacking = true;
                currentMoveSpeed = 0f; lock_rotation = false;
                animator.speed = 1f; animator.Play(idleStateName, 0, 0f);
                yield return new WaitForSeconds(nextIdleBeforeTime);

                if (currentPlanType == "Dragon") yield return StartCoroutine(PerformDragonSequence());
                else yield return StartCoroutine(PerformSpecificAttack(nextAttackAnim, nextAttackDuration));

                currentMoveSpeed = 0f; lock_rotation = false; animator.speed = 1f;
                animator.Play(idleStateName, 0, 0f);
                yield return new WaitForSeconds(idle_after_attack);

                currentOrderIndex++;
                if (currentOrderIndex >= attackOrder.Count) currentOrderIndex = 0;
                SetNextAttackPlan(attackOrder[currentOrderIndex]);
                isAttacking = false;
            }
            else { yield return null; }
        }
    }

    private void SetNextAttackPlan(string attackName)
    {
        // (保持原本的 SetNextAttackPlan 內容)
        currentPlanType = "Normal";
        switch (attackName)
        {
            case "Combo": nextAttackAnim = "Float_Combo_1"; nextAttackRange = dist_attack1; nextAttackDuration = time_attack1; nextIdleBeforeTime = idle_before_combo1; break;
            case "Dash": nextAttackAnim = "Float_Attack_4_3_To_Idle_1"; nextAttackRange = dist_attack2; nextAttackDuration = time_attack2; nextIdleBeforeTime = idle_before_dash; break;
            case "Spell": nextAttackAnim = "Float_Spell"; nextAttackRange = dist_spell; nextAttackDuration = time_spell; nextIdleBeforeTime = idle_before_spell; break;
            case "Dragon":
                nextAttackAnim = runStateName;
                nextAttackRange = 60f;
                nextAttackDuration = time_dragon;
                nextIdleBeforeTime = idle_before_dragon;
                currentPlanType = "Dragon";
                break;
            default: nextAttackAnim = "Float_Combo_1"; nextAttackRange = dist_attack1; nextAttackDuration = time_attack1; nextIdleBeforeTime = idle_before_combo1; break;
        }
    }

    private IEnumerator PerformSpecificAttack(string animName, float duration)
    {
        // (保持原本的 PerformSpecificAttack 內容)
        lock_rotation = true;
        currentAttackAnimName = animName;
        float clipLength = GetAnimationClipLength(animName);
        if (clipLength > 0 && duration > 0) animator.speed = clipLength / duration;
        else animator.speed = 1f;
        animator.Play(animName, 0, 0f);
        yield return new WaitForSeconds(duration);
        currentAttackAnimName = "";
        animator.speed = 1f;
        lock_rotation = false;
    }

    // ------------------------------------------------------------------------
    // --- [UPDATED] DRAGON SEQUENCE (Ascend -> Lock -> Dive) ---
    // ------------------------------------------------------------------------

    // ------------------------------------------------------------------------
    // --- ADVANCED DRAGON SEQUENCE (Ascend -> Dive Past Player -> Arena Edge) ---
    // ------------------------------------------------------------------------
    private IEnumerator PerformDragonSequence()
    {
        lock_rotation = true;
        currentMoveSpeed = 0f;

        // 1. 啟動龍模型
        if (dragonObject != null) dragonObject.SetActive(true);

        // 第一次瞬移：為了安全，第一次攻擊前直接將龍設定在空中的起始位置
        // 這樣可以避免第一次升空動畫在地面穿模
        Vector3 initialPos = transform.position;
        initialPos.y = swoop_start_height;

        // 確保在半徑內
        Vector3 flatInit = new Vector3(initialPos.x, 0, initialPos.z);
        if (flatInit.magnitude > arena_radius)
            initialPos = flatInit.normalized * (arena_radius - 1f) + new Vector3(0, swoop_start_height, 0);

        transform.position = initialPos;

        float endTime = Time.time + time_dragon;

        // --- 攻擊循環 ---
        while (Time.time < endTime)
        {
            // =========================================================
            // PHASE 1: ASCEND & LOCK (升空並鎖定)
            // =========================================================

            animator.speed = 1f;
            animator.Play(ascendStateName); // 播放升空動畫

            // 檢查是否還未達到高度
            while (transform.position.y < swoop_start_height - 0.5f)
            {
                if (Time.time >= endTime) break;

                // 1. 垂直升空
                Vector3 targetHeightPos = new Vector3(transform.position.x, swoop_start_height, transform.position.z);
                transform.position = Vector3.MoveTowards(transform.position, targetHeightPos, ascend_speed * Time.deltaTime);

                // 2. 始終面向玩家 (追蹤凝視)
                Vector3 dirToPlayer = (player.position - transform.position).normalized;
                dirToPlayer.y = 0;
                if (dirToPlayer != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dirToPlayer);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }

                yield return null;
            }

            // 強制校正高度
            Vector3 hoverPos = transform.position;
            hoverPos.y = swoop_start_height;
            transform.position = hoverPos;

            // 短暫懸停，讓玩家反應即將到來的俯衝
            yield return new WaitForSeconds(0.3f);


            // =========================================================
            // PHASE 2: CALCULATE DIVE VECTOR (計算穿透路徑)
            // =========================================================

            animator.Play(runStateName); // 切換俯衝動畫

            // 鎖定「這一瞬間」的玩家位置作為最低點
            Vector3 targetSnapPos = player.position;

            Vector3 startDivePos = transform.position;
            Vector3 flatStart = new Vector3(startDivePos.x, 0, startDivePos.z);
            Vector3 flatTarget = new Vector3(targetSnapPos.x, 0, targetSnapPos.z);

            // 計算方向向量：從 龍 -> 玩家 (並將延伸到身後)
            Vector3 diveDirection = (flatTarget - flatStart).normalized;
            if (diveDirection == Vector3.zero) diveDirection = transform.forward;

            // 開啟傷害
            isDashing = true;
            if (damage_area1 != null) damage_area1.enabled = true;

            // =========================================================
            // PHASE 3: DIVE THROUGH (俯衝並飛向對面)
            // =========================================================

            while (true)
            {
                if (Time.time >= endTime) break;

                // 1. 水平移動 (沿著固定方向一直飛)
                Vector3 currentPos = transform.position;
                currentPos += diveDirection * dragon_fly_speed * Time.deltaTime;

                // 2. 計算拋物線高度
                // 公式：Height = PlayerY + Curvature * (DistToPlayer)^2
                // 當龍接近玩家，Dist 變小，高度降低。
                // 當龍 *穿過* 玩家繼續飛，Dist 變大，高度會自動升高 (Swoop Up Effect)
                Vector3 currentFlat = new Vector3(currentPos.x, 0, currentPos.z);
                float distToTargetSnap = Vector3.Distance(currentFlat, flatTarget);

                float calculatedY = targetSnapPos.y + (parabola_curvature * (distToTargetSnap * distToTargetSnap));

                // 限制高度不超過起始高度 (避免飛太遠時衝上外太空)
                if (calculatedY > swoop_start_height) calculatedY = swoop_start_height;

                currentPos.y = calculatedY;

                // 3. 更新旋轉
                Vector3 moveVec = currentPos - transform.position;
                if (moveVec.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(moveVec);
                }

                transform.position = currentPos;

                // 4. 檢查邊界 (Stop Condition)
                // 我們只檢查是否飛出了 Arena Radius
                float distFromCenter = Vector3.Distance(currentFlat, Vector3.zero);

                // 必須滿足兩個條件才停止：
                // A. 距離中心超過半徑 (撞牆)
                // B. 已經飛過了玩家 (利用 Dot Product 判斷，避免龍一開始就在邊緣導致直接判定停止)
                Vector3 toTarget = flatTarget - currentFlat;
                bool passedPlayer = Vector3.Dot(diveDirection, toTarget) < 0;

                if (distFromCenter > arena_radius && passedPlayer)
                {
                    // 修正位置到邊界上，避免穿幫
                    Vector3 clampedPos = currentFlat.normalized * arena_radius;
                    clampedPos.y = currentPos.y;
                    transform.position = clampedPos;

                    // 撞到對面牆壁，結束這次俯衝，準備下一次升空
                    break;
                }

                yield return null;
            }

            // 關閉傷害
            if (damage_area1 != null) damage_area1.enabled = false;
            isDashing = false;

            // 在牆邊稍作停留，準備轉身升空
            yield return new WaitForSeconds(0.2f);
        }

        // --- CLEANUP (結束 Dragon Phase) ---
        if (dragonObject != null) dragonObject.SetActive(false);
        if (damage_area1 != null) damage_area1.enabled = false;
        isDashing = false;
        currentAttackAnimName = "";
        animator.speed = 1f;
        lock_rotation = false;

        // 最後降落邏輯 (確保回到地面)
        Vector3 landPos = transform.position;
        landPos.y = angel_land_y;
        Vector3 flatLandFinal = new Vector3(landPos.x, 0, landPos.z);
        if (Vector3.Distance(flatLandFinal, Vector3.zero) > arena_radius)
        {
            landPos = flatLandFinal.normalized * (arena_radius - 1f);
            landPos.y = angel_land_y;
        }
        transform.position = landPos;
    }

    // (保持原本的 OnTriggerEnter, GetAnimationClipLength, LateUpdate, TakeDamage)
    private void OnTriggerEnter(Collider other)
    {
        if (isDashing && other.CompareTag("Player"))
        {
            other.SendMessage("TakeDamage", dash_damage_amount, SendMessageOptions.DontRequireReceiver);
        }
    }
    private float GetAnimationClipLength(string clipName)
    {
        if (animator.runtimeAnimatorController == null) return 0f;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName) return clip.length;
        }
        return 0f;
    }
    private void LateUpdate()
    {
        if (player == null) return;
        if (currentPlanType != "Dragon")
        {
            Vector3 targetPos = player.position; targetPos.y = transform.position.y;
            if (currentMoveSpeed > 0 && player.position.y > 0) transform.position = Vector3.MoveTowards(transform.position, targetPos, currentMoveSpeed * Time.deltaTime);
            if (!lock_rotation)
            {
                Vector3 lookDir = player.position - transform.position; lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    float currentRotSpeed = isAttacking ? attack_rotate_speed : rotationSpeed;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), currentRotSpeed * Time.deltaTime);
                }
            }
        }
    }
    public void TakeDamage(float amount)
    {
        current_blood -= amount;
        if (current_blood <= 0)
        {
            current_blood = 0; animator.Play("Death"); StopAllCoroutines();
            if (dragonObject != null) dragonObject.SetActive(false);
            if (damage_area1 != null) damage_area1.enabled = false;
            this.enabled = false;
        }
    }
}