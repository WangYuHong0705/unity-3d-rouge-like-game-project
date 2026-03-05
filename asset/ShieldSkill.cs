using UnityEngine;
using System.Collections;

public class ShieldSkill : PlayerSkill
{
    [Header("Shield Settings")]
    public float shieldDuration = 2f;
    public float explosionRadius = 3f;
    public LayerMask enemyLayer;

    [Header("VFX Prefabs")]
    public ParticleSystem shieldVFXPrefab;     // 啟動時護盾特效
    public ParticleSystem explosionVFXPrefab;  // 結束爆炸特效

    [Header("Bubble Settings")]
    public GameObject shieldBubblePrefab;      // 護盾泡泡
    public Transform bubbleAnchor;             // 所有特效的基準點

    private GameObject activeBubble;
    private ParticleSystem activeShieldVFX;

    [HideInInspector] public float absorbedDamage = 0f;
    [HideInInspector] public bool shieldActive = false;

    protected override void ActivateSkill()
    {
        StartCoroutine(ShieldRoutine());
    }

    private IEnumerator ShieldRoutine()
    {
        shieldActive = true;
        absorbedDamage = 0f;

        SpawnBubble();
        SpawnShieldVFX();

        yield return new WaitForSeconds(shieldDuration);

        shieldActive = false;

        DestroyBubble();
        DestroyShieldVFX();   // 自動等待特效播完

        Explode();            // 爆炸也會自動播放完才消失
    }

    // ---------------------------------------------------
    // 工具：取得 Anchor 位置
    // ---------------------------------------------------
    private Vector3 AnchorPos()
    {
        return bubbleAnchor != null ? bubbleAnchor.position : transform.position;
    }

    // ---------------------------------------------------
    // 泡泡
    // ---------------------------------------------------
    private void SpawnBubble()
    {
        if (shieldBubblePrefab == null) return;

        activeBubble = Instantiate(shieldBubblePrefab, AnchorPos(), Quaternion.identity);
        activeBubble.transform.SetParent(transform, true);
    }

    private void DestroyBubble()
    {
        if (activeBubble != null)
            Destroy(activeBubble);
    }

    // ---------------------------------------------------
    // 護盾特效
    // ---------------------------------------------------
    private void SpawnShieldVFX()
    {
        if (shieldVFXPrefab == null) return;

        activeShieldVFX = Instantiate(shieldVFXPrefab, AnchorPos(), Quaternion.identity);
        activeShieldVFX.transform.SetParent(transform, true);
        activeShieldVFX.Play();
    }

    private void DestroyShieldVFX()
    {
        if (activeShieldVFX != null)
            StartCoroutine(WaitAndDestroy(activeShieldVFX));
    }

    // ---------------------------------------------------
    // 爆炸特效 + 範圍傷害
    // ---------------------------------------------------
    private void Explode()
    {
        if (explosionVFXPrefab != null)
        {
            ParticleSystem ps = Instantiate(explosionVFXPrefab, AnchorPos(), Quaternion.identity);
            StartCoroutine(WaitAndDestroy(ps));
        }

        Collider[] hits = Physics.OverlapSphere(AnchorPos(), explosionRadius, enemyLayer);

        foreach (var hit in hits)
        {
            EnemyState enemy = hit.GetComponent<EnemyState>();
            if (enemy != null)
                enemy.TakeDamage(Mathf.RoundToInt(absorbedDamage));
        }

        Debug.Log($"Shield Burst Damage = {absorbedDamage}");
    }

    // ---------------------------------------------------
    // 通用特效回收：等特效播完才 Destroy
    // ---------------------------------------------------
    private IEnumerator WaitAndDestroy(ParticleSystem ps)
    {
        if (ps == null) yield break;

        while (ps.IsAlive(true))
            yield return null;

        Destroy(ps.gameObject);
    }
}
