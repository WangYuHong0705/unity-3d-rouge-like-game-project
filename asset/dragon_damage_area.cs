using UnityEngine;
using System.Collections;

public class dragon_damage_area : MonoBehaviour
{
    [Header("References")]
    public DragonFollowPlayer dragon;
    public PlayerController player;

    // Logic 1: Find by tag "damage_area1"
    private Transform damageCylinder;

    [Header("Damage Settings")]
    public float dragon_damage = 1f;
    public float knockbackDistance = 5f;
    public float knockbackDuration = 0.3f;
    public float player_radius = 0.5f;

    // Radius of the cylinder collider (adjust manually or read from collider if exists)
    public float cylinderHitRadius = 1.5f;

    void Start()
    {
        // 1. Find logic References
        if (dragon == null) dragon = FindObjectOfType<DragonFollowPlayer>();
        if (player == null) player = FindObjectOfType<PlayerController>();

        // 2. Find Cylinder by Tag "damage_area1"
        GameObject foundCylinder = GameObject.FindGameObjectWithTag("damage_area1");
        if (foundCylinder != null)
        {
            damageCylinder = foundCylinder.transform;
        }
        else
        {
            Debug.LogError("[DragonDamage] Could not find object with tag 'damage_area1'. Make sure the collider on the head is tagged!");
        }
    }

    void Update()
    {
        if (dragon == null || damageCylinder == null || player == null) return;

        // 3. Logic: Only perform damage/knockback on Basic (2) or Claw (3)
        int current_area = dragon.current_attack_area;

        if (current_area == 2 || current_area == 3)
        {
            CheckPlayerHit();
        }

        // Removed all scale, movement, and timing logic. 
        // The cylinder now simply follows the dragon head bone (setup in Hierarchy).
    }

    void CheckPlayerHit()
    {
        // Get positions
        Vector3 damagePos = damageCylinder.position;
        Vector3 playerPos = player.transform.position;

        // Flat distance check (Treat as cylinder)
        Vector3 damagePosFlat = new Vector3(damagePos.x, 0, damagePos.z);
        Vector3 playerPosFlat = new Vector3(playerPos.x, 0, playerPos.z);

        float dist = Vector3.Distance(damagePosFlat, playerPosFlat);
        float combinedRadius = cylinderHitRadius + player_radius;

        // Hit Detection
        if (dist < combinedRadius)
        {
            OnPlayerHit(player, cylinderHitRadius);
        }
    }

    public void OnPlayerHit(PlayerController pc, float cylinderRadius)
    {
        if (pc == null) return;

        // Prevent spamming damage every frame? 
        // Usually, you might want an invincibility frame check here, 
        // but based on your request, I will apply damage directly.

        PlayerState ps = pc.GetComponent<PlayerState>();
        if (ps != null)
        {
            // Apply Damage
            ps.TakeDamage(Mathf.RoundToInt(dragon_damage));
        }

        // Stop player movement
        Rigidbody rb = pc.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;

        // --- KNOCKBACK LOGIC ---
        Vector3 startPos = pc.transform.position;
        Vector3 damagePosXZ = new Vector3(damageCylinder.position.x, startPos.y, damageCylinder.position.z);

        // Calculate direction AWAY from the cylinder (head) center
        Vector3 knockDir = (startPos - damagePosXZ).normalized;
        if (knockDir == Vector3.zero) knockDir = -dragon.transform.forward; // Fallback

        // Prevent Stuck Logic: Ensure player is pushed at least outside the radius
        float safeZoneRadius = cylinderRadius + player_radius + 1.0f;
        float currentDist = Vector3.Distance(startPos, damagePosXZ);

        float targetDistFromCenter = currentDist + knockbackDistance;
        if (targetDistFromCenter < safeZoneRadius) targetDistFromCenter = safeZoneRadius;

        Vector3 targetPos = damagePosXZ + (knockDir * targetDistFromCenter);
        targetPos.y = startPos.y;

        StartCoroutine(KnockbackCoroutine(pc, startPos, targetPos, knockbackDuration, pc.transform.rotation));
    }

    IEnumerator KnockbackCoroutine(PlayerController pc, Vector3 startPos, Vector3 targetPos, float duration, Quaternion fixedRot)
    {
        float fixedY = startPos.y;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // SmoothStep

            Vector3 next = Vector3.Lerp(startPos, targetPos, t);
            next.y = fixedY;

            pc.transform.position = next;
            // Optionally keep rotation fixed or let them spin
            pc.transform.rotation = fixedRot;

            elapsed += Time.deltaTime;
            yield return null;
        }

        Vector3 final = targetPos;
        final.y = fixedY;
        pc.transform.position = final;
    }
}