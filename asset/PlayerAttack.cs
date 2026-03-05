using UnityEngine;
using UnityEditor;

public class PlayerAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackRange = 2.5f;      
    public float attackAngle = 90f;
    public int attackDamage = 25;
    public float attackHeight = 2f;

    [Header("References")]
    public Transform attackCenter;        
    public LayerMask enemyLayer;     
    public Transform cameraTransform;

    public bool isAttacking = false;     
    private bool hasHitThisSwing = false; 

    void Update()
    {
        if (isAttacking)
        {
            Collider[] hits = Physics.OverlapSphere(attackCenter.position, attackRange, enemyLayer);

            foreach (Collider hit in hits)
            {
                Vector3 closestPoint = hit.ClosestPoint(attackCenter.position);
                Vector3 dirToTarget = (closestPoint - attackCenter.position).normalized;

                
                Vector3 forward = transform.forward; forward.y = 0;
                dirToTarget.y = 0;

                float angle = Vector3.Angle(forward, dirToTarget);
                float distance = Vector3.Distance(attackCenter.position, closestPoint);

                if (angle <= attackAngle / 2f && distance <= attackRange)
                {
                    EnemyState enemy = hit.GetComponent<EnemyState>();
                    if (enemy != null && !hasHitThisSwing && !enemy.IsDead)
                    {
                        Debug.Log($"cause {hit.name} for {attackDamage} damage!");
                        enemy.TakeDamage(attackDamage);
                        hasHitThisSwing = true;

                        ShowFloatingDamageAtPoint(attackDamage, closestPoint, Color.red);
                    }
                }
            }
        }
    }

    private void ShowFloatingDamageAtPoint(int damage, Vector3 hitPoint, Color color)
    {
        // 嘗試取得玩家自己的 FloatingTextManager
        FloatingTextManager ftm = GetComponentInChildren<FloatingTextManager>();
        if (ftm == null)
        {
            Debug.LogWarning("No FloatingTextManager found on player!");
            return;
        }

        // 從 manager 取得 prefab
        GameObject prefab = ftm.floatingTextPrefab;
        if (prefab != null)
        {
            // 稍微隨機偏移，避免完全重疊
            Vector3 randomOffset = new Vector3(
                Random.Range(-0.15f, 0.15f),
                Random.Range(0.2f, 0.3f),
                Random.Range(-0.15f, 0.15f)
            );

            GameObject ft = Instantiate(prefab, hitPoint + randomOffset, Quaternion.identity);
            ft.GetComponent<FloatingText>().SetText($"-{damage}", color, true);
        }
    }


    public void StartAttackHitbox()
    {
        isAttacking = true;
        hasHitThisSwing = false;
        //Debug.Log("Attack Hitbox start");
    }

    public void EndAttackHitbox()
    {
        isAttacking = false;
        //Debug.Log("Attack Hitbox end");
    }
    
    //void OnDrawGizmos()
    //{
    //    if (attackCenter != null && isAttacking)
    //    {
    //        Handles.color = new Color(1f, 0f, 0f, 0.25f); 
    //        Vector3 forward = attackCenter.forward;
    //        Handles.DrawSolidArc(
    //            attackCenter.position,
    //            Vector3.up,
    //            Quaternion.Euler(0, -attackAngle / 2f, 0) * forward,
    //            attackAngle,
    //            attackRange
    //        );
    //    }
    //}
}