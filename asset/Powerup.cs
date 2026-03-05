using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Powerup : MonoBehaviour
{
    public enum PowerupType
    {
        maxHealth,
        damage,
        speed,
        magic,
        HealthRegen,
        maxEnergy,
        EnergyGain
    }

    public Image iconImage;

    public Sprite iconMaxHealth;
    public Sprite iconDamage;
    public Sprite iconSpeed;
    public Sprite iconMagic;
    public Sprite iconHealthRegen;
    public Sprite iconMaxEnergy;
    public Sprite iconEnergyGain;

    public PowerupType type;

    private UIControllerPowerup controller;

    private Vector3 originScale;

    // Exposed tuning for the appear animation
    public float appearDuration = 0.35f;
    [Tooltip("Overshoot multiplier for the spline (1 = no overshoot)")]
    public float overshootMultiplier = 1.18f;

    // Called by UIControllerPowerup to initialize this UI choice
    public void Initialize(PowerupType t, UIControllerPowerup c)
    {
        type = t;
        controller = c;

        // find image if not assigned
        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>();

        UpdateIcon();

        // wire button
        var btn = GetComponentInChildren<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => { controller.OnPowerupSelected(type); });
        }

        // Capture the target/origin scale now (UIControllerPowerup should set target scale before Initialize).
        originScale = transform.localScale;

        // Start from zero and animate to originScale using a spline (use unscaled time so animation continues when Time.timeScale == 0)
        transform.localScale = Vector3.zero;
        StopAllCoroutines();
        StartCoroutine(AppearAnimation());
    }

    private void UpdateIcon()
    {
        if (iconImage == null) return;
        switch (type)
        {
            case PowerupType.maxHealth: iconImage.sprite = iconMaxHealth; break;
            case PowerupType.damage: iconImage.sprite = iconDamage; break;
            case PowerupType.speed: iconImage.sprite = iconSpeed; break;
            case PowerupType.magic: iconImage.sprite = iconMagic; break;
            case PowerupType.HealthRegen: iconImage.sprite = iconHealthRegen; break;
            case PowerupType.maxEnergy: iconImage.sprite = iconMaxEnergy; break;
            case PowerupType.EnergyGain: iconImage.sprite = iconEnergyGain; break;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // if initialized in editor, ensure visual is correct
        UpdateIcon();
        // originScale is captured in Initialize to avoid timing issues with Instantiate order.
    }

    public void OnButtonClicked()
    {
        Debug.Log("Powerup: OnButtonClicked for type " + type);
        if (controller != null)
        {
            controller.OnPowerupSelected(type);
        }
    }

    // Appear animation using a cubic Bezier spline (runs on unscaled time so it plays while Time.timeScale == 0)
    IEnumerator AppearAnimation()
    {
        float duration = Mathf.Max(0.01f, appearDuration);
        float elapsed = 0f;

        Vector3 p0 = Vector3.zero;
        Vector3 p3 = originScale;

        // Overshoot target for a nicer feel, then settle to p3
        Vector3 overshoot = p3 * overshootMultiplier;

        // Control points produce a smooth curve from 0 -> overshoot -> target
        Vector3 p1 = Vector3.Lerp(p0, overshoot, 0.55f);
        Vector3 p2 = Vector3.Lerp(overshoot, p3, 0.55f);

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);

            // Evaluate cubic Bezier at t
            transform.localScale = CubicBezier(p0, p1, p2, p3, t);

            // Use unscaled delta so animation keeps playing when the game is paused (Time.timeScale == 0)
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        transform.localScale = p3;
    }

    private Vector3 CubicBezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        return uuu * a + 3f * uu * t * b + 3f * u * tt * c + ttt * d;
    }
}
