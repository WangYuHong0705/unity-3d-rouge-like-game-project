using UnityEngine;

public abstract class PlayerSkill : MonoBehaviour
{
    [Header("Skill Info")]
    public string skillName = "New Skill";
    public int energyCost = 20;
    public float cooldown = 3f;

    [Header("UI")]
    public SkillCooldownUI cooldownUI;

    protected float cooldownTimer = 0f;
    protected PlayerState playerState;
    protected Animator animator;

    protected virtual void Awake()
    {
        playerState = GetComponent<PlayerState>();
        animator = GetComponent<Animator>();
    }

    protected virtual void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    public bool IsReady()
    {
        return cooldownTimer <= 0f;
    }

    public bool TryCast()
    {
        // Check energy
        if (!playerState.UseEnergy(energyCost))
            return false;

        // Check cooldown
        if (!IsReady())
        {
            Debug.Log($"{skillName} still cooling down.");
            return false;
        }

        // Perform skill
        ActivateSkill();

        // Apply cooldown
        cooldownTimer = cooldown;

        if (cooldownUI != null)
            cooldownUI.StartCooldown(cooldown);

        return true;
    }

    // Each skill implements their own action here
    protected abstract void ActivateSkill();
}
