using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class StartMenuController : MonoBehaviour
{
    public GameObject uiRoot;
    public Transform playerTransform;
    public Transform playerTarget;
    public Transform cameraTransform;
    public Transform cameraLookTarget;
    public Image fadeImage;
    public string gameplaySceneName = "Gameplay";
    public float playerMoveTime = 20f;
    public float cameraRotateTime = 3f;
    public float fadeDuration = 10f;

    [Header("Cutscene")]
    [Tooltip("Inline cutscene component (for playing Timeline in same scene)")]
    public InlineTimelineCutscene inlineCutscene;
    
    [Tooltip("OR: Separate cutscene scene name (leave empty if using inline)")]
    public string cutsceneSceneName = "";
    
    [Tooltip("Play cutscene on New Game")]
    public bool playCutsceneOnNewGame = true;
    
    [Tooltip("Skip cutscene on Load Game")]
    public bool skipCutsceneOnLoad = true;
    
    [Tooltip("Skip start animation when using inline cutscene (Timeline plays immediately)")]
    public bool skipStartAnimForInlineCutscene = true;

    [Header("Options")]
    public StartMenuOptionsController optionsController;

    private Canvas runtimeFadeCanvas;
    private Coroutine fadeCoroutine;
    private EventSystem eventSystem;

    void Awake()
    {
        // 1. Ensure EventSystem exists immediately
        eventSystem = FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject go = new GameObject("EventSystem");
            eventSystem = go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        // 2. Setup the UI Root and Raycaster
        if (uiRoot != null)
        {
            Canvas canvas = uiRoot.GetComponent<Canvas>();
            if (canvas != null)
            {
                // Ensure Raycaster exists BEFORE Start
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
                }
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }

        // 3. Setup Fade Image (The most likely culprit for blocking input)
        if (fadeImage != null)
        {
            // Force raycastTarget to false so it's "invisible" to clicks
            fadeImage.raycastTarget = false;

            var c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;

            SetupFadeCanvas();
        }

        // Find references
        if (inlineCutscene == null) inlineCutscene = GetComponent<InlineTimelineCutscene>();
        if (optionsController == null) optionsController = GetComponent<StartMenuOptionsController>();
    }

    void Start()
    {
        if (uiRoot != null)
        {
            // FORCE REFRESH: This "wakes up" the GraphicRaycaster and registers it with the EventSystem
            uiRoot.SetActive(false);
            uiRoot.SetActive(true);

            Canvas canvas = uiRoot.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = false;
                canvas.enabled = true;
            }

            CanvasGroup canvasGroup = uiRoot.GetComponent<CanvasGroup>() ?? uiRoot.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        // Ensure cursor is free
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Helper to keep Awake clean
    private void SetupFadeCanvas()
    {
        Canvas parentCanvas = fadeImage.GetComponentInParent<Canvas>();
        if (parentCanvas == null || (uiRoot != null && parentCanvas.transform.IsChildOf(uiRoot.transform)))
        {
            var go = new GameObject("StartMenu_FadeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            runtimeFadeCanvas = go.GetComponent<Canvas>();
            runtimeFadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            runtimeFadeCanvas.overrideSorting = true;
            runtimeFadeCanvas.sortingOrder = 10000;

            fadeImage.transform.SetParent(runtimeFadeCanvas.transform, false);
        }

        // Ensure the image fills the screen
        RectTransform rt = fadeImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // 1. Updated Button Handlers with specific timings
    public void OnStartPressed()
    {
        Debug.Log("[StartMenu] New Game - Playing Timeline then 3s Fade");
        GameStartParameters.shouldLoadSavedData = false;
        StartCoroutine(NewGameSequence());
    }

    public void OnLoadPressed()
    {
        Debug.Log("[StartMenu] Load Game - 3s Slow Fade");
        GameStartParameters.shouldLoadSavedData = true;
        StartCoroutine(LoadGameSequence());
    }

    IEnumerator NewGameSequence()
    {
        // 1. Clear the Menu UI
        if (uiRoot != null) uiRoot.SetActive(false);

        // 2. Trigger the Cutscene
        if (inlineCutscene != null)
        {
            inlineCutscene.PlayCutscene();

            // Give it a moment to initialize
            yield return new WaitForSeconds(0.1f);

            // 3. Wait here until the cutscene script marks itself as completed
            while (inlineCutscene.IsPlaying())
            {
                yield return null;
            }
        }

        // 4. NOW do the fast fade (3 seconds)
        Debug.Log("[StartMenu] Cutscene over. Starting 3s Fade.");
        yield return StartCoroutine(FadeToBlackRealtime(3f));

        // 5. Load the game
        yield return StartCoroutine(LoadGameplayScene());
    }

    IEnumerator LoadGameSequence()
    {
        // 1. Hide UI
        if (uiRoot != null) uiRoot.SetActive(false);

        // 2. Slow Fade (3s)
        Debug.Log("[StartMenu] Load Game: Fading out slowly (30s)...");
        yield return StartCoroutine(FadeToBlackRealtime(3f));

        // 3. Load Gameplay
        yield return StartCoroutine(LoadGameplayScene());
    }

    public void OnOptionsPressed()
    {
        Debug.Log("[StartMenu] Options button pressed");
        if (optionsController != null)
        {
            optionsController.OpenOptions();
        }
        else
        {
            Debug.LogWarning("[StartMenu] Options controller not found!");
        }
    }

    public void OnQuitPressed()
    {
        Debug.Log("[StartMenu] Quit button pressed");
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    IEnumerator PlayInlineCutsceneSequence()
    {
        if (!skipStartAnimForInlineCutscene)
        {
            Debug.Log("[StartMenu] Playing start animation before Timeline...");
            yield return StartCoroutine(PlayStartSequenceAnimation());
        }
        else
        {
            Debug.Log("[StartMenu] Skipping start animation, playing Timeline immediately...");
            if (uiRoot != null)
                uiRoot.SetActive(false);
        }

        if (inlineCutscene != null)
        {
            Debug.Log("[StartMenu] Starting inline cutscene Timeline...");
            inlineCutscene.PlayCutscene();
        }
        else
        {
            Debug.LogError("[StartMenu] InlineTimelineCutscene component is missing!");
            yield return StartCoroutine(LoadGameplayScene());
        }
    }

    IEnumerator PlayStartSequenceAndLoadCutscene()
    {
        yield return StartCoroutine(PlayStartSequenceAnimation());
        Debug.Log($"[StartMenu] Loading cutscene scene: {cutsceneSceneName}");
        var asyncOp = SceneManager.LoadSceneAsync(cutsceneSceneName);
        while (!asyncOp.isDone) yield return null;
    }

    IEnumerator PlayStartSequenceAndLoad()
    {
        yield return StartCoroutine(PlayStartSequenceAnimation());
        yield return StartCoroutine(LoadGameplayScene());
    }

    IEnumerator LoadGameplayScene()
    {
        Debug.Log($"[StartMenu] Loading gameplay scene: {gameplaySceneName}");
        var asyncOp = SceneManager.LoadSceneAsync(gameplaySceneName);
        while (!asyncOp.isDone) yield return null;
    }

    IEnumerator PlayStartSequenceAnimation()
    {
        if (fadeImage != null) fadeImage.raycastTarget = true;

        if (uiRoot != null)
            uiRoot.SetActive(false);

        if (fadeImage != null && !fadeImage.gameObject.activeInHierarchy)
            fadeImage.gameObject.SetActive(true);

        Animator playerAnimator = null;
        if (playerTransform != null)
        {
            playerAnimator = playerTransform.GetComponent<Animator>();

            foreach (var mb in playerTransform.GetComponents<MonoBehaviour>())
            {
                mb.enabled = false;
            }

            var cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            var rb = playerTransform.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }

        Coroutine loopCoroutine = null;
        if (playerAnimator != null)
        {
            string stateName = "Slow Run";
            int stateHash = Animator.StringToHash(stateName);
            bool played = false;

            int layerCount = Mathf.Max(1, playerAnimator.layerCount);
            for (int layer = 0; layer < layerCount; layer++)
            {
                try
                {
                    if (playerAnimator.HasState(layer, stateHash))
                    {
                        playerAnimator.CrossFadeInFixedTime(stateHash, 0.12f, layer, 0f);
                        played = true;
                        break;
                    }
                }
                catch { }
            }

            if (!played)
            {
                try
                {
                    playerAnimator.Play(stateName, -1, 0f);
                    played = true;
                }
                catch { }
            }

            if (!played)
            {
                Debug.LogWarning($"[StartMenu] Animator doesn't contain state '{stateName}'");
            }
            else
            {
                loopCoroutine = StartCoroutine(EnsureAnimatorStateLoops(playerAnimator, stateHash));
            }
        }

        if (fadeImage != null && fadeCoroutine == null)
        {
            fadeCoroutine = StartCoroutine(FadeToBlackRealtime(fadeDuration));
        }

        float elapsed = 0f;
        Vector3 startPos = playerTransform != null ? playerTransform.position : Vector3.zero;
        Quaternion camStartRot = cameraTransform != null ? cameraTransform.rotation : Quaternion.identity;
        Quaternion camEndRot = camStartRot;
        if (cameraTransform != null && cameraLookTarget != null)
            camEndRot = Quaternion.LookRotation((cameraLookTarget.position - cameraTransform.position).normalized, Vector3.up);

        float duration = Mathf.Max(playerMoveTime, cameraRotateTime);
        while (elapsed < duration)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            if (playerTransform != null && playerTarget != null)
            {
                float t = Mathf.Clamp01(elapsed / playerMoveTime);
                playerTransform.position = Vector3.Lerp(startPos, playerTarget.position, EaseOutCubic(t));
                Vector3 forward = (playerTarget.position - playerTransform.position);
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.001f)
                    playerTransform.rotation = Quaternion.Slerp(playerTransform.rotation, Quaternion.LookRotation(forward), t);
            }

            if (cameraTransform != null)
            {
                float ct = Mathf.Clamp01(elapsed / cameraRotateTime);
                cameraTransform.rotation = Quaternion.Slerp(camStartRot, camEndRot, EaseInOutQuad(ct));
            }

            yield return null;
        }

        Debug.Log("[StartMenu] Start sequence animation complete");

        if (fadeCoroutine != null)
        {
            yield return fadeCoroutine;
            fadeCoroutine = null;
        }

        if (loopCoroutine != null)
            StopCoroutine(loopCoroutine);

        if (playerAnimator != null)
        {
            string[] fallbackStates = { "Idle", "Stand" };
            bool returned = false;
            foreach (var st in fallbackStates)
            {
                int h = Animator.StringToHash(st);
                for (int layer = 0; layer < playerAnimator.layerCount; layer++)
                {
                    if (playerAnimator.HasState(layer, h))
                    {
                        try { playerAnimator.CrossFadeInFixedTime(h, 0.12f, layer, 0f); returned = true; break; } catch { }
                    }
                }
                if (returned) break;
            }
        }
    }

    IEnumerator FadeToBlackRealtime(float duration)
    {
        if (fadeImage == null) yield break;

        // Ensure the fade image can now block raycasts to prevent accidental clicks
        fadeImage.raycastTarget = true;

        float elapsed = 0f;
        Color c = fadeImage.color;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Clamp01(elapsed / duration);
            fadeImage.color = c;
            yield return null;
        }
        c.a = 1f;
        fadeImage.color = c;
    }

    private IEnumerator EnsureAnimatorStateLoops(Animator animator, int stateHash)
    {
        int layerIndex = -1;
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            try
            {
                if (animator.HasState(layer, stateHash)) { layerIndex = layer; break; }
            }
            catch { }
        }
        if (layerIndex < 0) yield break;

        while (true)
        {
            yield return null;
            var info = animator.GetCurrentAnimatorStateInfo(layerIndex);
            if (info.shortNameHash == stateHash)
            {
                if (info.normalizedTime >= 0.99f)
                {
                    try { animator.CrossFadeInFixedTime(stateHash, 0.05f, layerIndex, 0f); }
                    catch { try { animator.Play(stateHash, layerIndex, 0f); } catch { } }
                }
            }
            else
            {
                try { animator.CrossFadeInFixedTime(stateHash, 0.05f, layerIndex, 0f); }
                catch { }
            }
        }
    }

    static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
}
