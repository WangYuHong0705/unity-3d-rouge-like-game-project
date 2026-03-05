using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class PlayerStatsChangedEvent : UnityEvent { }

[System.Serializable]
public class PlayerSaveData
{
    public int maxHP;
    public int currentHP;
    public int level;
    public int xpToNextLevel;
    public int currentXP;
    public int coins;
    public int maxEnergy;
    public int currentEnergy;
    public float magic;
    public string[] items;
}

public class PlayerState : MonoBehaviour
{
    [Header("Basic Stats")]
    public int maxHP = 100;
    public int currentHP = 100;
    public int level = 1;
    public int xpToNextLevel = 100;
    public int currentXP = 0;
    public int coins = 0;

    [Header("Inventory (Future)")]
    public int maxInventorySize = 10;
    public string[] items;

    [Header("Combat Stats")]
    [Tooltip("Magic power (adds to spell damage like fireball)")]
    public float magic = 0f;

    [Header("Regeneration")]
    [Tooltip("HP regeneration per second (set by powerups)")]
    public float healthRegenRate = 0f;

    private float regenAccumulator = 0f; // Accumulates fractional HP regen

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

        if (GameStartParameters.shouldLoadSavedData && PlayerPrefs.HasKey(saveKey))
        {
            LoadFromPlayerPrefs();
        }
    }

    private void Update()
    {
        if (healthRegenRate > 0f && currentHP > 0 && currentHP < maxHP && !isDead)
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
    }

    public void TakeDamage(int damage, bool canBeDodged = true)
    {
        ShieldSkill shield = GetComponent<ShieldSkill>();

        if (shield != null && shield.shieldActive)
        {
            shield.absorbedDamage += damage;
            textManager?.ShowText("Shield!", Color.cyan, true);
            return;  // 完全免傷
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

        FindAnyObjectByType<DamageVignette>()?.Flash();

        if (currentHP == 0) OnPlayerDeath();
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

    public void ShowFloatText(string msg, Color color, bool stay = false)
    {
        textManager?.ShowText(msg, color, stay);
    }

    [Header("Save Settings")]
    [SerializeField] private string saveKey = "PlayerProfile_1";

    /// <summary>
    /// API Call: Exports current stats to PlayerPrefs
    /// </summary>
    public void SaveToPlayerPrefs()
    {
        PlayerSaveData data = new PlayerSaveData
        {
            maxHP = this.maxHP,
            currentHP = this.currentHP,
            level = this.level,
            xpToNextLevel = this.xpToNextLevel,
            currentXP = this.currentXP,
            coins = this.coins,
            maxEnergy = this.maxEnergy,
            currentEnergy = this.currentEnergy,
            magic = this.magic,
            items = this.items
        };

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(saveKey, json);
        PlayerPrefs.Save();

        Debug.Log($"[PlayerState] Data saved to {saveKey}");
    }

    /// <summary>
    /// API Call: Imports stats from PlayerPrefs and refreshes UI
    /// </summary>
    public void LoadFromPlayerPrefs()
    {
        if (!PlayerPrefs.HasKey(saveKey))
        {
            Debug.LogWarning("No save data found for key: " + saveKey);
            return;
        }

        string json = PlayerPrefs.GetString(saveKey);
        PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(json);

        // Map data back to MonoBehaviour fields
        this.maxHP = data.maxHP;
        this.currentHP = data.currentHP;
        this.level = data.level;
        this.xpToNextLevel = data.xpToNextLevel;
        this.currentXP = data.currentXP;
        this.coins = data.coins;
        this.maxEnergy = data.maxEnergy;
        this.currentEnergy = data.currentEnergy;
        this.magic = data.magic;
        this.items = data.items;

        // Reset state flags
        this.isDead = (currentHP <= 0);

        // Notify UI/Listeners that stats have changed after loading
        onStatsChanged?.Invoke();

        Debug.Log("[PlayerState] Data loaded successfully.");
    }

}
