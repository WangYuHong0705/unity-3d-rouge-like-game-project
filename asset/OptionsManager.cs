using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;

public class OptionsManager : MonoBehaviour
{
    [Header("Audio UI")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("Graphics UI")]
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown qualityDropdown;
    public Toggle fullscreenToggle;
    public Toggle vsyncToggle;

    [Header("Controls UI")]
    public Slider mouseSensitivitySlider;
    public Toggle invertYToggle;

    [Header("Buttons")]
    public Button applyButton;
    public Button cancelButton;
    public Button resetButton;

    [Header("Audio Preview")]
    public AudioClip volumeSampleClip;

    // Reference to the options controller
    public UIControllerOptions optionsController;

    private Resolution[] availableRes;
    private AudioSource previewSource;

    // Throttle preview playback
    private float lastPreviewPlayTime = -10f;
    private const float minPreviewInterval = 0.15f;

    void Start()
    {
        // Find Controller
        if (optionsController == null)
            optionsController = FindAnyObjectByType<UIControllerOptions>();

        if (optionsController == null)
        {
            Debug.LogError("[OptionsManager] UIControllerOptions not found in scene");
            return;
        }

        // Setup Audio Source
        previewSource = gameObject.AddComponent<AudioSource>();
        previewSource.playOnAwake = false;
        previewSource.spatialBlend = 0f;
        if (volumeSampleClip != null) previewSource.clip = volumeSampleClip;

        // Initialize UI elements
        InitializeAudioUI();
        InitializeGraphicsUI();
        InitializeControlsUI();
        InitializeButtons();
    }

    #region Initialize UI

    private void InitializeAudioUI()
    {
        // Master Volume
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.SetValueWithoutNotify(optionsController.GetStagedMasterVolume());
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        // Music Volume
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
            musicVolumeSlider.SetValueWithoutNotify(optionsController.GetStagedMusicVolume());
            musicVolumeSlider.onValueChanged.RemoveAllListeners();
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        // SFX Volume
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
            sfxVolumeSlider.SetValueWithoutNotify(optionsController.GetStagedSFXVolume());
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }
    }

    private void InitializeGraphicsUI()
    {
        // Resolution
        if (resolutionDropdown != null)
        {
            availableRes = optionsController.GetAvailableResolutions();
            resolutionDropdown.ClearOptions();

            var options = availableRes.Select(r =>
            {
                double hz = r.refreshRateRatio.value;
                return $"{r.width} x {r.height} @ {System.Math.Round(hz)}Hz";
            }).ToList();

            resolutionDropdown.AddOptions(options);
            resolutionDropdown.SetValueWithoutNotify(optionsController.GetStagedResolutionIndex());
            resolutionDropdown.onValueChanged.RemoveAllListeners();
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }

        // Quality
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(QualitySettings.names.ToList());
            qualityDropdown.SetValueWithoutNotify(optionsController.GetStagedQualityLevel());
            qualityDropdown.onValueChanged.RemoveAllListeners();
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        }

        // Fullscreen
        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(optionsController.GetStagedFullscreen());
            fullscreenToggle.onValueChanged.RemoveAllListeners();
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        }

        // VSync
        if (vsyncToggle != null)
        {
            vsyncToggle.SetIsOnWithoutNotify(optionsController.GetStagedVSync());
            vsyncToggle.onValueChanged.RemoveAllListeners();
            vsyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        }
    }

    private void InitializeControlsUI()
    {
        // Mouse Sensitivity
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.minValue = 0.5f;
            mouseSensitivitySlider.maxValue = 5f;
            mouseSensitivitySlider.SetValueWithoutNotify(optionsController.GetStagedMouseSensitivity());
            mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
            mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
        }

        // Invert Y
        if (invertYToggle != null)
        {
            invertYToggle.SetIsOnWithoutNotify(optionsController.GetStagedInvertY());
            invertYToggle.onValueChanged.RemoveAllListeners();
            invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
        }
    }

    private void InitializeButtons()
    {
        if (applyButton != null)
        {
            applyButton.onClick.RemoveAllListeners();
            applyButton.onClick.AddListener(ApplySettings);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(CancelChanges);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(ResetToDefaults);
        }
    }

    #endregion

    #region Event Handlers - Audio

    public void OnMasterVolumeChanged(float v)
    {
        optionsController.StageMasterVolume(v);
        PlayVolumePreview(v);
    }

    private void OnMusicVolumeChanged(float v)
    {
        optionsController.StageMusicVolume(v);
    }

    private void OnSFXVolumeChanged(float v)
    {
        optionsController.StageSFXVolume(v);
        PlayVolumePreview(v);
    }

    private void PlayVolumePreview(float volume)
    {
        if (previewSource != null && volumeSampleClip != null)
        {
            float now = Time.unscaledTime;
            if (now - lastPreviewPlayTime >= minPreviewInterval)
            {
                previewSource.volume = volume;
                previewSource.PlayOneShot(volumeSampleClip);
                lastPreviewPlayTime = now;
            }
        }
    }

    #endregion

    #region Event Handlers - Graphics

    public void OnResolutionChanged(int index)
    {
        optionsController.StageResolution(index);
    }

    private void OnQualityChanged(int index)
    {
        optionsController.StageQualityLevel(index);
    }

    private void OnFullscreenChanged(bool enabled)
    {
        optionsController.StageFullscreen(enabled);
    }

    private void OnVSyncChanged(bool enabled)
    {
        optionsController.StageVSync(enabled);
    }

    #endregion

    #region Event Handlers - Controls

    private void OnMouseSensitivityChanged(float v)
    {
        optionsController.StageMouseSensitivity(v);
    }

    private void OnInvertYChanged(bool enabled)
    {
        optionsController.StageInvertY(enabled);
    }

    #endregion

    #region Button Actions

    public void ApplySettings()
    {
        optionsController.ApplySettings();
        if (previewSource != null) previewSource.Stop();
        Debug.Log("[OptionsManager] Settings applied");
    }

    public void CancelChanges()
    {
        optionsController.CancelChanges();

        // Update UI to match cancelled values
        RefreshAllUI();

        if (previewSource != null) previewSource.Stop();
        Debug.Log("[OptionsManager] Changes cancelled");
    }

    public void ResetToDefaults()
    {
        optionsController.ResetToDefaults();

        // Update UI to match defaults
        RefreshAllUI();

        Debug.Log("[OptionsManager] Reset to defaults");
    }

    private void RefreshAllUI()
    {
        // Audio
        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(optionsController.GetStagedMasterVolume());
        if (musicVolumeSlider != null)
            musicVolumeSlider.SetValueWithoutNotify(optionsController.GetStagedMusicVolume());
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(optionsController.GetStagedSFXVolume());

        // Graphics
        if (resolutionDropdown != null)
            resolutionDropdown.SetValueWithoutNotify(optionsController.GetStagedResolutionIndex());
        if (qualityDropdown != null)
            qualityDropdown.SetValueWithoutNotify(optionsController.GetStagedQualityLevel());
        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(optionsController.GetStagedFullscreen());
        if (vsyncToggle != null)
            vsyncToggle.SetIsOnWithoutNotify(optionsController.GetStagedVSync());

        // Controls
        if (mouseSensitivitySlider != null)
            mouseSensitivitySlider.SetValueWithoutNotify(optionsController.GetStagedMouseSensitivity());
        if (invertYToggle != null)
            invertYToggle.SetIsOnWithoutNotify(optionsController.GetStagedInvertY());
    }

    #endregion
}