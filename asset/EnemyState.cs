using System.Threading.Tasks;
using UnityEngine;

public class EnemyState : MonoBehaviour
{
    [Header("Enemy Health Settings")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("References")]
    public Animator animator;
    public Transform player;
    public MonoBehaviour controller;

    private PlayerState playerState;


    [Header("Rewards")]
    public int rewardXP = 20;
    public int rewardCoins = 5;

    private bool isDead = false;

    void Start()
    {
        currentHealth = maxHealth;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerState = playerObj.GetComponent<PlayerState>();
        }
        else
        {
            Debug.LogError("EnemyState: Player with tag 'Player' not found!");
        }
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        Debug.Log($"{gameObject.name} 受到 {amount} 傷害！剩餘血量：{currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }else
        {
            if (animator != null)
                animator.SetTrigger("injure");
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"{gameObject.name} 被擊敗！");
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

    private System.Collections.IEnumerator WaitAndDestroy()
    {
        if (animator == null)
        {
            yield return new WaitForSeconds(3f);
            Destroy(gameObject);
            yield break;
        }
        // 等到死亡動畫開始播放
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        while (!stateInfo.IsName("dead")) //這裡的名稱要跟 Animator Clip 名稱一致
        {
            yield return null;
            stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        }

        // 播放完動畫再等 0.3 秒
        yield return new WaitForSeconds(stateInfo.length + 0.3f);

        //若你有死亡特效，確保特效是子物件，或先分離再摧毀本體
        Destroy(gameObject);
    }

    public bool IsDead => isDead;
}
