using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// Gameplay options controller - matches Start Menu options functionality
/// Provides: Audio (Master/Music/SFX), Graphics (Resolution/Quality/Fullscreen/VSync), Controls (MouseSens/InvertY)
/// </summary>
public class UIControllerOptions : MonoBehaviour
{
    [Header("UI Prefabs")]
    public GameObject optionsCanvasPrefab;

    [Header("Audio (Optional)")]
    [Tooltip("AudioMixer with exposed parameters: MasterVolume, MusicVolume, SFXVolume")]
    public AudioMixer masterMixer;

    [Header("Debug / State (Read Only)")]
    [SerializeField] private int stagedResolutionIndex = 0;
    [SerializeField] private int stagedQualityLevel = 0;
    [SerializeField] private bool stagedFullscreen = true;
    [SerializeField] private bool stagedVSync = true;
    [SerializeField] private float stagedMasterVolume = 0.75f;
    [SerializeField] private float stagedMusicVolume = 0.7f;
    [SerializeField] private float stagedSFXVolume = 0.8f;
    [SerializeField] private float stagedMouseSensitivity = 2f;
    [SerializeField] private bool stagedInvertY = false;

    // Runtime reference to the instantiated canvas
    public GameObject CanvasInstance { get; private set; }

    // Constants for PlayerPrefs
    private const string KEY_RESOLUTION = "ResolutionIndex";
    private const string KEY_QUALITY = "QualityLevel";
    private const string KEY_FULLSCREEN = "Fullscreen";
    private const string KEY_VSYNC = "VSync";
    private const string KEY_MASTER_VOLUME = "MasterVolume";
    private const string KEY_MUSIC_VOLUME = "MusicVolume";
    private const string KEY_SFX_VOLUME = "SFXVolume";
    private const string KEY_MOUSE_SENSITIVITY = "MouseSensitivity";
    private const string KEY_INVERT_Y = "InvertY";

    // Defaults
    private const float DEFAULT_MASTER_VOLUME = 0.75f;
    private const float DEFAULT_MUSIC_VOLUME = 0.7f;
    private const float DEFAULT_SFX_VOLUME = 0.8f;
    private const float DEFAULT_MOUSE_SENSITIVITY = 2.0f;
    private const bool DEFAULT_INVERT_Y = false;

    // Cache for available resolutions
    private Resolution[] availableRes;

    // Original values for Cancel functionality
    private int originalResolutionIndex;
    private int originalQualityLevel;
    private bool originalFullscreen;
    private bool originalVSync;
    private float originalMasterVolume;
    private float originalMusicVolume;
    private float originalSFXVolume;
    private float originalMouseSensitivity;
    private bool originalInvertY;

    #region Unity Lifecycle

    private void Awake()
    {
        // Instantiate the UI, but keep it hidden by default
        if (optionsCanvasPrefab != null)
        {
            CanvasInstance = Instantiate(optionsCanvasPrefab);
            CanvasInstance.transform.SetParent(this.transform, false);
            CanvasInstance.SetActive(false);
        }
        else
        {
            Debug.LogError("UIControllerOptions: OptionsCanvasPrefab is missing!");
        }

        // 檢查可用解析度
        availableRes = Screen.resolutions;
        Debug.Log($"[UIControllerOptions] Found {availableRes.Length} available resolutions:");
        for (int i = 0; i < availableRes.Length; i++)
        {
            Resolution r = availableRes[i];
            Debug.Log($"  [{i}] {r.width}x{r.height} @ {r.refreshRateRatio.value:F2}Hz");
        }
    }

    private void Start()
    {
        LoadSettings();
    }

    #endregion

    #region Load & Save Logic

    /// <summary>
    /// Loads from PlayerPrefs. If no save data, uses defaults.
    /// </summary>
    public void LoadSettings()
    {
        // Audio
        stagedMasterVolume = PlayerPrefs.GetFloat(KEY_MASTER_VOLUME, DEFAULT_MASTER_VOLUME);
        stagedMusicVolume = PlayerPrefs.GetFloat(KEY_MUSIC_VOLUME, DEFAULT_MUSIC_VOLUME);
        stagedSFXVolume = PlayerPrefs.GetFloat(KEY_SFX_VOLUME, DEFAULT_SFX_VOLUME);

        // Graphics
        stagedQualityLevel = PlayerPrefs.GetInt(KEY_QUALITY, QualitySettings.GetQualityLevel());
        stagedFullscreen = PlayerPrefs.GetInt(KEY_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
        stagedVSync = PlayerPrefs.GetInt(KEY_VSYNC, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;

        // Resolution
        if (PlayerPrefs.HasKey(KEY_RESOLUTION))
        {
            stagedResolutionIndex = PlayerPrefs.GetInt(KEY_RESOLUTION);
            if (stagedResolutionIndex < 0 || stagedResolutionIndex >= availableRes.Length)
            {
                stagedResolutionIndex = GetCurrentResolutionIndex();
            }
        }
        else
        {
            stagedResolutionIndex = GetCurrentResolutionIndex();
        }

        // Controls
        stagedMouseSensitivity = PlayerPrefs.GetFloat(KEY_MOUSE_SENSITIVITY, DEFAULT_MOUSE_SENSITIVITY);
        stagedInvertY = PlayerPrefs.GetInt(KEY_INVERT_Y, DEFAULT_INVERT_Y ? 1 : 0) == 1;

        // Apply loaded settings (including resolution on first load)
        ApplyAllSettings(true);

        SyncOriginalsToStaged();

        Debug.Log("[UIControllerOptions] Settings loaded");
    }

    /// <summary>
    /// Commits the Staged values to PlayerPrefs and updates the "Original" restore point.
    /// </summary>
    public void ApplySettings()
    {
        ApplyAllSettings(true);

        PlayerPrefs.SetFloat(KEY_MASTER_VOLUME, stagedMasterVolume);
        PlayerPrefs.SetFloat(KEY_MUSIC_VOLUME, stagedMusicVolume);
        PlayerPrefs.SetFloat(KEY_SFX_VOLUME, stagedSFXVolume);
        PlayerPrefs.SetInt(KEY_RESOLUTION, stagedResolutionIndex);
        PlayerPrefs.SetInt(KEY_QUALITY, stagedQualityLevel);
        PlayerPrefs.SetInt(KEY_FULLSCREEN, stagedFullscreen ? 1 : 0);
        PlayerPrefs.SetInt(KEY_VSYNC, stagedVSync ? 1 : 0);
        PlayerPrefs.SetFloat(KEY_MOUSE_SENSITIVITY, stagedMouseSensitivity);
        PlayerPrefs.SetInt(KEY_INVERT_Y, stagedInvertY ? 1 : 0);
        PlayerPrefs.Save();

        SyncOriginalsToStaged();

        Debug.Log("[UIControllerOptions] Settings saved");
    }

    /// <summary>
    /// Reverts settings to the values held before the menu was opened (or last save).
    /// </summary>
    public void CancelChanges()
    {
        // Revert staged values
        stagedMasterVolume = originalMasterVolume;
        stagedMusicVolume = originalMusicVolume;
        stagedSFXVolume = originalSFXVolume;
        stagedResolutionIndex = originalResolutionIndex;
        stagedQualityLevel = originalQualityLevel;
        stagedFullscreen = originalFullscreen;
        stagedVSync = originalVSync;
        stagedMouseSensitivity = originalMouseSensitivity;
        stagedInvertY = originalInvertY;

        // Apply reversion
        ApplyAllSettings(true);

        Debug.Log("[UIControllerOptions] Changes cancelled");
    }

    /// <summary>
    /// Resets all settings to defaults
    /// </summary>
    public void ResetToDefaults()
    {
        stagedMasterVolume = DEFAULT_MASTER_VOLUME;
        stagedMusicVolume = DEFAULT_MUSIC_VOLUME;
        stagedSFXVolume = DEFAULT_SFX_VOLUME;
        stagedResolutionIndex = GetCurrentResolutionIndex();
        stagedQualityLevel = QualitySettings.names.Length - 1; // Highest quality
        stagedFullscreen = true;
        stagedVSync = true;
        stagedMouseSensitivity = DEFAULT_MOUSE_SENSITIVITY;
        stagedInvertY = DEFAULT_INVERT_Y;

        ApplyAllSettings(true);

        Debug.Log("[UIControllerOptions] Reset to defaults");
    }

    #endregion

    #region Internal Helpers

    private void ApplyAllSettings(bool applyResolution)
    {
        // Audio
        ApplyAudioSettings();

        // Graphics
        ApplyGraphicsSettings(applyResolution);

        // Controls
        ApplyControlSettings();
    }

    private void ApplyAudioSettings()
    {
        if (masterMixer != null)
        {
            // Convert 0-1 slider to -80 to 0 dB
            SetMixerVolume("MasterVolume", stagedMasterVolume);
            SetMixerVolume("MusicVolume", stagedMusicVolume);
            SetMixerVolume("SFXVolume", stagedSFXVolume);
            Debug.Log("Set Volume");
        }
        else
        {
            // Fallback to AudioListener
            AudioListener.volume = stagedMasterVolume;
        }
    }

    private void SetMixerVolume(string parameterName, float sliderValue)
    {
        if (masterMixer == null) return;

        // Convert 0-1 to decibels: -80dB (silent) to 0dB (full)
        float volumeDB = sliderValue > 0.0001f ? Mathf.Log10(sliderValue) * 20f : -80f;
        
        try
        {
            masterMixer.SetFloat(parameterName, volumeDB);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[UIControllerOptions] Failed to set {parameterName}: {e.Message}");
        }
    }

    private void ApplyGraphicsSettings(bool applyRes)
    {
        // Quality
        if (stagedQualityLevel >= 0 && stagedQualityLevel < QualitySettings.names.Length)
        {
            QualitySettings.SetQualityLevel(stagedQualityLevel);
        }

        // VSync
        QualitySettings.vSyncCount = stagedVSync ? 1 : 0;

        // Resolution & Fullscreen
        if (applyRes && availableRes != null && stagedResolutionIndex >= 0 && stagedResolutionIndex < availableRes.Length)
        {
            Resolution r = availableRes[stagedResolutionIndex];
            FullScreenMode mode = stagedFullscreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
            Screen.SetResolution(r.width, r.height, mode, r.refreshRateRatio);
        }
        else if (applyRes)
        {
            // Just apply fullscreen mode
            Screen.fullScreen = stagedFullscreen;
        }
    }

    private void ApplyControlSettings()
    {
        // Apply mouse sensitivity to Cinemachine
        var axisController = FindAnyObjectByType<CinemachineInputAxisController>();
        if (axisController != null)
        {
            foreach (var ctrl in axisController.Controllers)
            {
                //Debug.Log($"[UIControllerOptions] Applying mouse sensitivity to axis: {ctrl.Name}");
                if (ctrl.Name == "Look Orbit X")
                    ctrl.Input.Gain = stagedMouseSensitivity * 3.0f;

                if (ctrl.Name == "Look Orbit Y")
                {
                    // Apply invert Y here
                    float yGain = stagedMouseSensitivity * 3.0f;
                    ctrl.Input.Gain = stagedInvertY ? -yGain : yGain;
                }
            }
        }
        else
        {
            // Fallback to legacy camera controller
            var cam = Camera.main;
            if (cam != null)
            {
                var camCtrl = cam.GetComponent(typeof(TMPro.Examples.CameraController)) as TMPro.Examples.CameraController;
                if (camCtrl != null)
                {
                    camCtrl.MoveSensitivity = stagedMouseSensitivity;
                }
            }
        }
    }

    private void SyncOriginalsToStaged()
    {
        originalMasterVolume = stagedMasterVolume;
        originalMusicVolume = stagedMusicVolume;
        originalSFXVolume = stagedSFXVolume;
        originalResolutionIndex = stagedResolutionIndex;
        originalQualityLevel = stagedQualityLevel;
        originalFullscreen = stagedFullscreen;
        originalVSync = stagedVSync;
        originalMouseSensitivity = stagedMouseSensitivity;
        originalInvertY = stagedInvertY;
    }

    private int GetCurrentResolutionIndex()
    {
        if (availableRes == null || availableRes.Length == 0) return 0;

        int index = System.Array.FindIndex(availableRes, r =>
            r.width == Screen.width &&
            r.height == Screen.height &&
            r.refreshRateRatio.value == Screen.currentResolution.refreshRateRatio.value
        );

        return index >= 0 ? index : 0;
    }

    #endregion

    #region Public API

    // Audio Getters
    public float GetStagedMasterVolume() => stagedMasterVolume;
    public float GetStagedMusicVolume() => stagedMusicVolume;
    public float GetStagedSFXVolume() => stagedSFXVolume;

    // Graphics Getters
    public Resolution[] GetAvailableResolutions() => availableRes;
    public int GetStagedResolutionIndex() => stagedResolutionIndex;
    public int GetStagedQualityLevel() => stagedQualityLevel;
    public bool GetStagedFullscreen() => stagedFullscreen;
    public bool GetStagedVSync() => stagedVSync;

    // Control Getters
    public float GetStagedMouseSensitivity() => stagedMouseSensitivity;
    public bool GetStagedInvertY() => stagedInvertY;

    // Audio Setters
    public void StageMasterVolume(float v)
    {
        stagedMasterVolume = Mathf.Clamp01(v);
        ApplyAudioSettings(); // Apply immediately for preview
    }

    public void StageMusicVolume(float v)
    {
        stagedMusicVolume = Mathf.Clamp01(v);
        ApplyAudioSettings();
    }

    public void StageSFXVolume(float v)
    {
        stagedSFXVolume = Mathf.Clamp01(v);
        ApplyAudioSettings();
    }

    // Graphics Setters
    public void StageResolution(int index)
    {
        if (index >= 0 && index < availableRes.Length)
        {
            stagedResolutionIndex = index;
        }
    }

    public void StageQualityLevel(int level)
    {
        if (level >= 0 && level < QualitySettings.names.Length)
        {
            stagedQualityLevel = level;
            QualitySettings.SetQualityLevel(level); // Apply immediately
        }
    }

    public void StageFullscreen(bool enabled)
    {
        stagedFullscreen = enabled;
    }

    public void StageVSync(bool enabled)
    {
        stagedVSync = enabled;
        QualitySettings.vSyncCount = enabled ? 1 : 0; // Apply immediately
    }

    // Control Setters
    public void StageMouseSensitivity(float v)
    {
        stagedMouseSensitivity = Mathf.Clamp(v, 0.5f, 5f);
        ApplyControlSettings(); // Apply immediately
    }

    public void StageInvertY(bool enabled)
    {
        stagedInvertY = enabled;
        ApplyControlSettings(); // Apply immediately
    }

    #endregion
}