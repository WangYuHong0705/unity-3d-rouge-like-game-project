using UnityEngine;
using System.Collections.Generic;

public class Fireball : MonoBehaviour
{
    public float explosionRadius = 2.5f;   
    
    [Header("Damage Calculation")]
    [Tooltip("Base damage (before magic scaling)")]
    public int baseDamage = 40;
    
    [Tooltip("Magic stat from player (set automatically)")]
    public float magic = 0f;
    
    // Calculated damage: baseDamage + magic
    private int explosionDamage => Mathf.RoundToInt(baseDamage + magic);
    
    public LayerMask enemyLayer;           

    private Vector3 startPos;
    private Vector3 targetPos;
    private float speed;
    private float gravity;

    private float time;

    private Vector3 velocity;
    private bool hasExploded = false;
    private HashSet<EnemyState> damagedEnemies = new HashSet<EnemyState>();


    public void Initialize(Vector3 start, Vector3 target, float speed, float gravity, Vector3 launchVelocity)
    {
        this.startPos = start;
        this.targetPos = target;
        this.speed = speed;
        this.gravity = gravity;

        velocity = launchVelocity;
        
        // Get magic stat from player when fireball is created
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // Try to get magic from various possible components/fields
            var playerState = player.GetComponent<PlayerState>();
            if (playerState != null)
            {
                // Try to find magic field via reflection
                var magicField = playerState.GetType().GetField("magic", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (magicField != null && magicField.FieldType == typeof(float))
                {
                    magic = (float)magicField.GetValue(playerState);
                    Debug.Log($"[Fireball] Magic stat loaded: {magic}, Total damage: {explosionDamage}");
                }
            }
        }
    }

    void Update()
    {
        time += Time.deltaTime;

        Vector3 pos = startPos + velocity * time;
        pos.y += 0.5f * gravity * time * time;

        transform.position = pos;

        Vector3 currentVelocity = velocity + Vector3.up * (gravity * time);
        if (currentVelocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(currentVelocity);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Explode();
    }

    private Vector3 GetHitPoint(Collider enemy)
    {
        Vector3 dir = (enemy.bounds.center - transform.position).normalized;
        Ray ray = new Ray(transform.position, dir);

        if (enemy.Raycast(ray, out RaycastHit hitInfo, 10f))
            return hitInfo.point;

        // fallback：沒有 hit，就使用敵人的 bounds 中心
        return enemy.bounds.center;
    }

    private void ShowFloatingDamageAt(Vector3 hitPoint, int damage, Color color)
    {
        FloatingTextManager ftm = GetComponentInChildren<FloatingTextManager>();
        if (ftm == null)
        {
            Debug.LogWarning("No FloatingTextManager found on player!");
            return;
        }
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

    private void Explode()
    {
        if (hasExploded) return;   // 防止重複傷害
        hasExploded = true;
        
        Debug.Log($"[Fireball] Exploding! Base damage: {baseDamage}, Magic: {magic}, Total: {explosionDamage}");
        
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, enemyLayer);

        foreach (Collider hit in hits)
        {
            EnemyState enemyState = hit.GetComponent<EnemyState>();
            if (enemyState != null && !damagedEnemies.Contains(enemyState))
            {
                damagedEnemies.Add(enemyState);
                enemyState.TakeDamage(explosionDamage);
            }
            Vector3 hitPoint = GetHitPoint(hit);
            ShowFloatingDamageAt(hitPoint, explosionDamage, Color.red);
        }

        Destroy(gameObject);
    }
}
