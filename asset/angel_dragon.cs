using UnityEngine;
using System.Collections;

public class DragonDamageCollider : MonoBehaviour
{
    [Header("References")]
    public string playerTag = "Player";

    [Header("Combat Settings")]
    public float damageAmount = 25f;       // Damage per hit
    public float damageCooldown = 1.5f;    // Time before Dragon can damage player again

    [Header("Knockback Settings")]
    public float knockbackHorizontal = 15f; // How far back the player gets pushed
    public float knockbackVertical = 5f;    // How high the player gets lifted

    public float knockbackDuration = 0.4f;

    private bool canDamage = true;

    private void OnEnable()
    {
        // Reset state when Dragon appears
        canDamage = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only process if we can damage and the object is the player
        if (!canDamage) return;

        if (other.CompareTag(playerTag))
        {
            // Try to get the PlayerController component
            PlayerController targetPlayer = other.GetComponent<PlayerController>();

            // Fallback: search in parent if collider is on a child part
            if (targetPlayer == null) targetPlayer = other.GetComponentInParent<PlayerController>();

            if (targetPlayer != null)
            {
                HitPlayer(targetPlayer);
            }
        }
    }

    private void HitPlayer(PlayerController pc)
    {
        // 1. Deal Damage
        PlayerState ps = pc.GetComponent<PlayerState>();
        if (ps != null)
        {
            ps.TakeDamage(Mathf.RoundToInt(damageAmount));
            Debug.Log("Dragon hit player! Dealt " + damageAmount);
        }

        // 2. Stop Player Momentum (Reset velocity to avoid physics conflicts)
        Rigidbody rb = pc.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;

        // 3. Calculate Knockback Direction (Horizontal Only)
        Vector3 rawDirection = pc.transform.position - transform.position;
        Vector3 horizontalDir = new Vector3(rawDirection.x, 0, rawDirection.z).normalized;

        // Safety: If player is exactly inside the dragon, push them forward
        if (horizontalDir == Vector3.zero) horizontalDir = transform.forward;

        // 4. Calculate Target Position
        Vector3 startPos = pc.transform.position;

        // Combine Horizontal Push + Vertical Lift
        Vector3 knockbackVector = (horizontalDir * knockbackHorizontal) + (Vector3.up * knockbackVertical);
        Vector3 targetPos = startPos + knockbackVector;

        // 5. Apply Knockback
        StartCoroutine(KnockbackCoroutine(pc, startPos, targetPos, knockbackDuration));

        // 6. Start Cooldown
        StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        canDamage = false;
        yield return new WaitForSeconds(damageCooldown);
        canDamage = true;
    }

    private IEnumerator KnockbackCoroutine(PlayerController pc, Vector3 startPos, Vector3 targetPos, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Safety check in case player is destroyed
            if (pc == null) yield break;

            float t = elapsed / duration;

            // Ease-out effect: t * (2 - t) (Fast start, slows down at the end)
            t = t * (2 - t);

            pc.transform.position = Vector3.Lerp(startPos, targetPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (pc != null) pc.transform.position = targetPos;
    }
}