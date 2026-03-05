using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public class EnemyState_angel : MonoBehaviour
{
    [Header("Enemy Health Settings")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Revive Settings")]
    [Tooltip("The tag of the OTHER boss (e.g., if this is angel_boss1, put angel_boss2 here)")]
    public string partnerTag = "angel_boss1";
    [Tooltip("Time in seconds to wait before reviving")]
    public float reviveTime = 10f;

    // Internal state to track if we are currently down waiting to revive
    private bool isReviving = false;

    [Header("References")]
    public Animator animator;
    public Transform player;
    public MonoBehaviour controller;

    private PlayerState playerState;

    [Header("Rewards")]
    public int rewardXP = 20;
    public int rewardCoins = 5;

    // isDead represents the "True Death" where the object is destroyed
    private bool isDead = false;

    void Start()
    {
        currentHealth = maxHealth;
        if (player != null)
            playerState = player.GetComponent<PlayerState>();
    }

    public void TakeDamage(int amount)
    {
        // If already dead or currently in the process of reviving (downed), ignore damage
        if (isDead || isReviving) return;

        currentHealth -= amount;
        Debug.Log($"(new){gameObject.name} 受到 {amount} 傷害！剩餘血量：{currentHealth}");

        if (currentHealth <= 0)
        {
            // Instead of calling Die() directly, we check logic
            AttemptReviveOrDie();
        }
        else
        {
            if (animator != null)
                animator.SetTrigger("injure");
        }
    }

    private void AttemptReviveOrDie()
    {
        // Try to find the partner
        GameObject partner = GameObject.FindGameObjectWithTag(partnerTag);
        bool isPartnerAlive = false;

        if (partner != null)
        {
            EnemyState_angel partnerState = partner.GetComponent<EnemyState_angel>();
            // Partner is alive if they are not Dead AND not currently Reviving (downed)
            if (partnerState != null && !partnerState.IsDead && !partnerState.IsReviving)
            {
                isPartnerAlive = true;
            }
        }

        if (isPartnerAlive)
        {
            // Partner is alive, so we start the revive process instead of dying
            StartCoroutine(ReviveProcess(partner));
        }
        else
        {
            // Partner is dead or doesn't exist, so we die for real
            PerformTrueDeath();
        }
    }

    private IEnumerator ReviveProcess(GameObject partner)
    {
        isReviving = true;
        currentHealth = 0; // Ensure health is displayed as 0

        Debug.Log($"{gameObject.name} 被擊倒！正在等待復活...");

        // Play death animation (falling down)
        if (animator != null)
            animator.SetTrigger("dead");

        // Wait for the revive duration
        float timer = 0f;
        while (timer < reviveTime)
        {
            timer += Time.deltaTime;

            // CRITICAL CHECK:
            // While waiting, we must check if the partner has died.
            // If the partner dies/disappears while we are waiting, we must die too.
            if (partner == null || partner.GetComponent<EnemyState_angel>().IsDead)
            {
                Debug.Log($"{gameObject.name} 復活失敗！夥伴已死亡。");
                isReviving = false; // Stop reviving flag
                PerformTrueDeath(); // Die for real
                yield break; // Exit coroutine
            }

            yield return null;
        }

        // If we survived the wait time, we revive
        Revive();
    }

    private void Revive()
    {
        isReviving = false;
        // Revive with 30% Max Health
        currentHealth = Mathf.RoundToInt(maxHealth * 0.3f);

        Debug.Log($"{gameObject.name} 復活了！恢復血量至：{currentHealth}");

        // Reset Animation - You need a trigger "revive" in your Animator, 
        // or transition from Dead -> Idle based on a bool
        if (animator != null)
        {
            animator.SetTrigger("revive");
            // If you don't have a "revive" trigger, you might need:
            // animator.Play("Idle"); 
        }
    }

    private void PerformTrueDeath()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"{gameObject.name} 徹底被擊敗！");

        // Only trigger "dead" animation if we aren't already lying on the ground from the revive wait
        // If isReviving was true (but failed), we likely already played the "dead" anim.
        // However, to be safe and ensure logic flow, we trigger it again or let the state machine handle it.
        if (animator != null)
            animator.SetTrigger("dead");

        if (controller != null)
        {
            var method = controller.GetType().GetMethod("OnDeath");
            if (method != null)
                method.Invoke(controller, null);
        }

        if (playerState != null)
        {
            playerState.AddExperience(rewardXP);
            playerState.AddCoins(rewardCoins);
        }

        StartCoroutine(WaitAndDestroy());
    }

    private IEnumerator WaitAndDestroy()
    {
        if (animator == null)
        {
            yield return new WaitForSeconds(3f);
            Destroy(gameObject);
            yield break;
        }

        // Wait for animation state to verify we are in "dead" state
        // Note: If we were already lying on the ground waiting to revive, this might need adjustment
        // depending on your specific animation transition setup.
        yield return null;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        // Wait until we are actually playing the death clip
        while (!stateInfo.IsName("dead") && !stateInfo.IsName("Death"))
        {
            yield return null;
            stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        }

        // Wait for the animation length + buffer
        yield return new WaitForSeconds(stateInfo.length + 0.3f);

        Destroy(gameObject);
    }

    public bool IsDead => isDead;
    public bool IsReviving => isReviving;
}