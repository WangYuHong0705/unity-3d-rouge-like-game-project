using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class PlayerStatsChangedEvent1 : UnityEvent { }

public class PlayerState1 : MonoBehaviour
{
    [Header("Basic Stats")]
    public int maxHP = 100;
    public int currentHP = 100;
    public int level = 1;
    public int xpToNextLevel = 100;
    public int currentXP = 0;
    public int coins = 0;

    [Header("Combat Stats")]
    [Tooltip("Magic power (adds to spell damage like fireball)")]
    public float magic = 0f;

    [Header("Regeneration")]
    [Tooltip("HP regeneration per second (set by powerups)")]
    public float healthRegenRate = 0f;
    
    private float regenAccumulator = 0f; // Accumulates fractional HP regen

    [Header("Inventory (Future)")]
    public int maxInventorySize = 10;
    public string[] items;

    [Header("Energy System")]
    public int maxEnergy = 100;
    public int currentEnergy = 0;
    public int perfectDodgeGain = 25;   // 極限閃避獲得能量

    [Header("Events")]
    public PlayerStatsChangedEvent onStatsChanged;
    public UnityEvent onPlayerDeath;

    private FloatingTextManager textManager;
    private bool isDead = false;

    void Start()
    {
        textManager = GetComponent<FloatingTextManager>();
    }

    void Awake()
    {
        if (onStatsChanged == null)
            onStatsChanged = new PlayerStatsChangedEvent();
        
        if (onPlayerDeath == null)
            onPlayerDeath = new UnityEvent();
    }

    void Update()
    {
        // Handle HP regeneration
        if (healthRegenRate > 0f && currentHP > 0 && currentHP < maxHP && !isDead)
        {
            // Calculate regen amount for this frame
            float regenThisFrame = healthRegenRate * Time.deltaTime;
            regenAccumulator += regenThisFrame;

            // Only heal when we've accumulated at least 1 HP
            if (regenAccumulator >= 1f)
            {
                int hpToHeal = Mathf.FloorToInt(regenAccumulator);
                regenAccumulator -= hpToHeal;

                // Heal without showing text (to avoid spam)
                int oldHP = currentHP;
                currentHP = Mathf.Min(currentHP + hpToHeal, maxHP);
                
                if (currentHP > oldHP)
                {
                    onStatsChanged.Invoke();
                    // Optionally show floating text every few seconds
                    // textManager?.ShowText($"+{currentHP - oldHP} HP", new Color(0.2f, 1f, 0.2f));
                }
            }
        }
    }

    public void TakeDamage(int damage, bool canBeDodged = true)
    {
        if (isDead) return;

        ShieldSkill shield = GetComponent<ShieldSkill>();
        if (shield != null && shield.shieldActive)
        {
            shield.absorbedDamage += damage;
            textManager?.ShowText("Shield!", Color.cyan, true);
            return;
        }

        PlayerController controller = GetComponent<PlayerController>();
        if (canBeDodged && controller != null && controller.IsRolling())
        {
            textManager?.ShowText("DODGE!", Color.green, true);
            AddEnergy(perfectDodgeGain);
            Debug.Log("Damage avoided!");
            return;
        }

        currentHP = Mathf.Max(currentHP - damage, 0);
        onStatsChanged.Invoke();
        
        if (currentHP == 0)
        {
            OnPlayerDeath();
        }
    }

    private void OnPlayerDeath()
    {
        if (isDead) return;
        
        isDead = true;
        Debug.Log("[PlayerState] Player has died!");
        
        onPlayerDeath.Invoke();
        
        GameEndTrigger.TriggerGameOverStatic();
    }

    public void AddEnergy(int amount)
    {
        if (amount <= 0) return;

        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0, maxEnergy);
        onStatsChanged.Invoke();

        textManager?.ShowText($"+{amount} Energy", new Color(0.4f, 0.8f, 1f)); // 青藍色
        Debug.Log($"Energy +{amount} ({currentEnergy}/{maxEnergy})");
    }

    public bool UseEnergy(int amount)
    {
        if (currentEnergy < amount)
        {
            textManager?.ShowText("Not enough energy!", Color.red);
            Debug.Log("Energy不足，無法施放技能。");
            return false;
        }

        currentEnergy -= amount;
        onStatsChanged.Invoke();
        Debug.Log($"Energy used: {amount}. ({currentEnergy}/{maxEnergy})");
        return true;
    }

    public void Heal(int amount)
    {
        currentHP = Mathf.Min(currentHP + amount, maxHP);
        onStatsChanged.Invoke();
        Debug.Log($"Healed {amount} HP.");
    }

    public void AddCoins(int amount)
    {
        coins += amount;
        onStatsChanged.Invoke();
        Debug.Log($"Got {amount} coins. Total: {coins}");
        textManager?.ShowText($"+{coins}", new Color(0.9f, 0.8f, 0.2f));
    }

    public void AddExperience(int exp)
    {
        if (exp <= 0) return;
        currentXP += exp;
        Debug.Log($"Gained {exp} XP. XP: {currentXP}/{xpToNextLevel}");
        textManager?.ShowText($"+{exp} XP", Color.cyan); // 黃金色

        // 檢查升級（支援多次升級）
        while (currentXP >= xpToNextLevel)
        {
            currentXP -= xpToNextLevel;
            LevelUp();
        }
        onStatsChanged.Invoke();
    }

    private void LevelUp()
    {
        level++;
        xpToNextLevel += 50;
        maxHP += 10;
        currentHP += 10;
        Heal(10);
        Debug.Log($"Level up! New level: {level}");
        textManager?.ShowText($"LEVEL UP!", Color.cyan);
        onStatsChanged.Invoke();
    }
}
