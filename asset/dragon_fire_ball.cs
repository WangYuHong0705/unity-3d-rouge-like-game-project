using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DragonFireballAttack : MonoBehaviour
{
    [Header("References")]
    public DragonFollowPlayer dragon;
    public GameObject fireballPrefab; // The "Body" (本體)
    public PlayerController player;

    [Header("Settings")]
    public int fireballCount = 30;
    public float scatterRadius = 20f;
    public float frontDistance = 25f;
    public Vector2 spawnHeightRange = new Vector2(30f, 50f);
    public float groundY = 0f;
    public float groundStayTime = 15f;
    public Vector3 groundScale = new Vector3(10f, 2f, 10f);
    public float fallSpeed = 40f;
    public float gravity = 40f;
    public float fireballSpawnDelay = 0.2f;
    public float fireballDamage = 1f;

    // Internal State
    private List<FireballBehavior> activeFireballs = new List<FireballBehavior>();
    private Coroutine meteorCoroutine = null;
    private bool isMeteorActive = false;
    private float[] fireballHeights = new float[10000];
    private int fireballCounter = 0;

    // Track if we have already cleaned up to prevent spamming Destroy
    private bool isCleanedUp = false;

    void Start()
    {
        if (dragon == null)
        {
            GameObject d = GameObject.FindGameObjectWithTag("dragon");
            if (d != null)
                dragon = d.GetComponent<DragonFollowPlayer>();
            else
                Debug.LogError("[FireballAttack] No object with tag 'dragon' found!");
        }

        if (player == null)
            player = FindObjectOfType<PlayerController>();
    }

    void Update()
    {
        // 1. Safety Check: If dragon is null, it means it was Destroyed.
        if (dragon == null && !isCleanedUp)
        {
            Debug.Log("[Meteor] ☠️ Dragon object missing/destroyed. Cleanup.");
            CleanupAllFireballs();
            return;
        }

        if (player == null || isCleanedUp) return;

        // 2. Normal Attack Logic
        if ((dragon.current_attack_area == 5) && !dragon.lock_meteor)
        {
            dragon.lock_meteor = true;
            meteorCoroutine = StartCoroutine(MeteorWave());
        }
    }

    // --- TRIGGER ON DESTRUCTION ---
    // This runs immediately if the Dragon (assuming this script is on the dragon) is Destroyed.
    private void OnDestroy()
    {
        CleanupAllFireballs();
    }

    private void OnDisable()
    {
        CleanupAllFireballs();
    }

    // --- Cleanup Function ---
    private void CleanupAllFireballs()
    {
        if (isCleanedUp) return;
        isCleanedUp = true;

        // 1. Stop spawning new fireballs
        if (meteorCoroutine != null)
        {
            StopCoroutine(meteorCoroutine);
            meteorCoroutine = null;
        }
        isMeteorActive = false;

        // 2. Destroy all CLONED fireballs
        for (int i = activeFireballs.Count - 1; i >= 0; i--)
        {
            if (activeFireballs[i] != null)
            {
                if (activeFireballs[i].gameObject != null)
                {
                    Destroy(activeFireballs[i].gameObject);
                }
            }
        }
        activeFireballs.Clear();

        // 3. NEW: Destroy the FIREBALL PREFAB (The Body/本體)
        // Only do this if fireballPrefab is a Scene Object. 
        // If it is an Asset in your project folder, Destroy won't effect the file (which is good).
        if (fireballPrefab != null)
        {
            Debug.Log("[Meteor] Destroying Fireball Template (本體)");
            Destroy(fireballPrefab);
        }

        // 4. OPTIONAL: If this script is NOT on the dragon (e.g. it's on a separate "AttackManager"),
        // we should destroy this manager object too.
        if (dragon == null && this.gameObject != null)
        {
            // Only destroy self if we are not already being destroyed by the parent dragon
            // If this script is ON the dragon, the dragon's destruction handles this naturally.
            // If this script is separate, we destroy it here.
            Destroy(this.gameObject, 0.1f);
        }
    }

    IEnumerator MeteorWave()
    {
        isMeteorActive = true;
        isCleanedUp = false;

        Debug.Log("[Meteor] ☄️ Meteor wave started");

        yield return StartCoroutine(SpawnMeteorFireballs());

        Debug.Log("[Meteor] 🌧️ Meteor wave finished");

        isMeteorActive = false;
        meteorCoroutine = null;
    }

    IEnumerator SpawnMeteorFireballs()
    {
        for (int i = 0; i < fireballCount; i++)
        {
            // Only check if dragon is null (Destroyed)
            if (dragon == null)
            {
                CleanupAllFireballs();
                yield break;
            }

            Vector2 randomCircle = Random.insideUnitCircle * scatterRadius;
            Vector3 targetPos = player.transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
            targetPos.y = groundY;

            Vector3 forwardOffset = player.transform.forward * frontDistance;
            float height = Random.Range(spawnHeightRange.x, spawnHeightRange.y);
            Vector3 spawnPos = player.transform.position + forwardOffset + new Vector3(randomCircle.x, height, randomCircle.y);

            // Important: We instantiate copies. The original 'fireballPrefab' remains unless destroyed in cleanup.
            GameObject fb = Instantiate(fireballPrefab, spawnPos, Quaternion.identity);
            fb.transform.localScale = Vector3.one;

            int id = fireballCounter++;
            fireballHeights[id] = height;

            FireballBehavior behavior = fb.GetComponent<FireballBehavior>();
            if (behavior == null)
                behavior = fb.AddComponent<FireballBehavior>();

            behavior.Init(this, player, id, spawnPos, targetPos,
                          fallSpeed, gravity, groundY, groundScale, groundStayTime);

            activeFireballs.Add(behavior);

            yield return new WaitForSeconds(fireballSpawnDelay);
        }
    }

    public void UpdateFireballHeight(int id, float height)
    {
        if (id >= 0 && id < fireballHeights.Length)
            fireballHeights[id] = height;
    }

    public void NotifyFireballDestroyed(FireballBehavior fb)
    {
        if (activeFireballs.Contains(fb))
            activeFireballs.Remove(fb);
    }
}

// -------------------- FireballBehavior --------------------

[RequireComponent(typeof(Collider))]
public class FireballBehavior : MonoBehaviour
{
    private DragonFireballAttack controller;
    private PlayerController player;

    private int fireballID;
    private Vector3 velocity;
    private float gravity;
    private float groundY;
    private Vector3 groundScale;
    private float stayTime;

    private bool hitGround = false;
    private Collider fireballCollider;

    private Coroutine damageCoroutine = null;

    public void Init(DragonFireballAttack ctrl, PlayerController p, int id,
                     Vector3 start, Vector3 target,
                     float initialSpeed, float g, float groundY,
                     Vector3 groundScale, float stayTime)
    {
        controller = ctrl;
        player = p;
        fireballID = id;
        gravity = g;
        this.groundY = groundY;
        this.groundScale = groundScale;
        this.stayTime = stayTime;

        transform.position = start;
        transform.localScale = Vector3.one;

        fireballCollider = GetComponent<Collider>();
        if (fireballCollider == null)
            fireballCollider = gameObject.AddComponent<SphereCollider>();
        fireballCollider.isTrigger = true;

        Vector3 dir = (target - start).normalized;
        velocity = dir * initialSpeed;
    }

    void Update()
    {
        // FAILSAFE: If the main controller script is missing (Dragon Destroyed), destroy self.
        if (controller == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!hitGround)
        {
            velocity += Vector3.down * gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;

            if (controller != null)
                controller.UpdateFireballHeight(fireballID, transform.position.y);

            if (transform.position.y <= groundY)
            {
                transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
                transform.localScale = groundScale;
                hitGround = true;
                StartCoroutine(BurnAndDestroy());
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc != null && damageCoroutine == null)
        {
            damageCoroutine = StartCoroutine(DamagePlayerOverTime(pc));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc != null && damageCoroutine != null)
        {
            StopCoroutine(damageCoroutine);
            damageCoroutine = null;
        }
    }

    private IEnumerator DamagePlayerOverTime(PlayerController pc)
    {
        PlayerState ps = pc.GetComponent<PlayerState>();
        while (ps != null)
        {
            if (controller != null)
            {
                ps.TakeDamage(Mathf.RoundToInt(controller.fireballDamage));
            }
            else
            {
                yield break;
            }
            yield return new WaitForSeconds(0.2f);
        }
    }

    IEnumerator BurnAndDestroy()
    {
        yield return new WaitForSeconds(stayTime);
        if (controller != null)
            controller.NotifyFireballDestroyed(this);
        Destroy(gameObject);
    }
}