using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class TutorialDirector : MonoBehaviour
{
    private const string StartSceneName = "Start Scene";
    private const string MainSceneName = "Main VR Scene";
    private const string StartBasicsKey = "tutorial.start-basics";
    private const string MovementKey = "tutorial.movement";
    private const string TableHeightKey = "tutorial.table-height";
    private const string PreWeldDetectionKey = "tutorial.pre-weld-detection";
    private const float CanvasDistance = 1.18f;
    private const float CanvasScale = 0.0014f;
    private const float TablePromptEdgeDistance = 0.85f;
    private const float StartSceneOpeningDelay = 6f;
    private const float MainSceneOpeningDelay = 1f;
    private const float ShowDuration = 0.22f;
    private const float HideDuration = 0.16f;

    private sealed class TutorialPage
    {
        public string Title;
        public string Eyebrow;
        public string Description;
        public string ImageResourcePath;
        public string VideoResourcePath;
    }

    private sealed class TutorialSequence
    {
        public string CompletionKey;
        public List<TutorialPage> Pages;
    }

    private static TutorialDirector activeInstance;
    private static readonly HashSet<string> completedTutorialsThisSession = new HashSet<string>();

    private readonly Queue<TutorialSequence> queuedSequences = new Queue<TutorialSequence>();
    private readonly Color panelColor = new Color32(7, 16, 27, 242);
    private readonly Color cardColor = new Color32(10, 22, 36, 235);
    private readonly Color outlineColor = new Color32(47, 88, 121, 255);
    private readonly Color dividerColor = new Color32(35, 58, 79, 255);
    private readonly Color accentBlue = new Color32(104, 180, 255, 255);
    private readonly Color accentOrange = new Color32(255, 177, 92, 255);
    private readonly Color whiteText = new Color32(255, 255, 255, 255);
    private readonly Color mutedText = new Color32(196, 207, 219, 255);
    private readonly Color dimOverlay = new Color32(0, 0, 0, 165);

    private Transform headTransform;
    private Canvas tutorialCanvas;
    private GameObject canvasRoot;
    private GameObject panelRoot;
    private RectTransform panelRect;
    private CanvasGroup panelCanvasGroup;
    private RawImage stillImage;
    private RawImage videoImage;
    private GameObject stillFrame;
    private GameObject videoFrame;
    private TMP_Text titleText;
    private TMP_Text eyebrowText;
    private TMP_Text descriptionText;
    private TMP_Text pageCounterText;
    private TMP_Text progressDotsText;
    private Button backButton;
    private Button nextButton;
    private TMP_Text nextButtonLabel;
    private Button closeButton;
    private AudioSource audioSource;
    private AudioClip popupClip;
    private AudioClip pageChangeClip;
    private VideoPlayer videoPlayer;
    private RenderTexture videoRenderTexture;
    private Material noDepthUiMaterial;
    private Sprite panelSprite;
    private Texture2D generatedSpriteTexture;
    private TutorialSequence currentSequence;
    private int currentPageIndex;
    private bool isVisible;
    private bool isAnimating;
    private bool tablePromptQueued;
    private bool preWeldPromptQueued;
    private bool mainSceneTutorialsUnlocked;
    private WeldParameterMonitor parameterMonitor;
    private WeldingHudPanel weldingHudPanel;
    private Transform tableTransform;
    private Collider tableCollider;
    private bool hudWasActiveBeforeTutorial;
    private bool hudHiddenByTutorial;
    private Coroutine videoPrepareCoroutine;

    public bool IsTutorialVisible => isVisible || isAnimating || (canvasRoot != null && canvasRoot.activeInHierarchy);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTutorialSessionState()
    {
        completedTutorialsThisSession.Clear();
        activeInstance = null;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != StartSceneName && scene.name != MainSceneName)
            return;

        if (FindFirstObjectByType<TutorialDirector>() != null)
            return;

        GameObject tutorialObject = new GameObject("Tutorial Director");
        tutorialObject.AddComponent<TutorialDirector>();
    }

    private void Awake()
    {
        activeInstance = this;
        panelSprite = LoadDefaultSprite();
        popupClip = Resources.Load<AudioClip>("Tutorial/SFX/BotW - Interact Sound");
        pageChangeClip = Resources.Load<AudioClip>("Tutorial/SFX/Page changes");
        FindHeadTransform();
        BuildUi();
    }

    private void Start()
    {
        if (!SimulatorSettings.GetTutorialsEnabled())
            return;

        string activeScene = SceneManager.GetActiveScene().name;
        if (activeScene == StartSceneName)
            StartCoroutine(QueueStartBasicsAfterSceneSettles());
        else if (activeScene == MainSceneName)
            StartCoroutine(QueueMovementPromptAfterSceneSettles());
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;

        if (generatedSpriteTexture != null)
            Destroy(generatedSpriteTexture);

        if (videoRenderTexture != null)
            videoRenderTexture.Release();

        if (videoPrepareCoroutine != null)
            StopCoroutine(videoPrepareCoroutine);

        if (noDepthUiMaterial != null)
            Destroy(noDepthUiMaterial);

        RestoreWeldingHud();
    }

    private void Update()
    {
        if (!SimulatorSettings.GetTutorialsEnabled())
            return;

        if (headTransform == null)
            FindHeadTransform();

        if (isVisible)
            UpdateCanvasAnchor();

        if (SceneManager.GetActiveScene().name != MainSceneName)
            return;

        ResolveMainSceneDependencies();
        if (!mainSceneTutorialsUnlocked)
            return;

        TryQueueTableHeightPrompt();
        TryQueuePreWeldPrompt();
    }

    private IEnumerator QueueMovementPromptAfterSceneSettles()
    {
        yield return new WaitForSecondsRealtime(MainSceneOpeningDelay);
        mainSceneTutorialsUnlocked = true;

        if (!HasCompleted(MovementKey))
        {
            EnqueueSequence(CreateMovementSequence());
        }
    }

    private IEnumerator QueueStartBasicsAfterSceneSettles()
    {
        yield return new WaitForSecondsRealtime(StartSceneOpeningDelay);
        TryQueueStartBasics();
    }

    [ContextMenu("Reset Tutorial Progress")]
    private void ResetTutorialProgress()
    {
        completedTutorialsThisSession.Clear();
    }

    private void TryQueueStartBasics()
    {
        if (HasCompleted(StartBasicsKey))
            return;

        EnqueueSequence(CreateStartBasicsSequence());
    }

    private void TryQueueTableHeightPrompt()
    {
        if (tablePromptQueued || HasCompleted(TableHeightKey) || tableTransform == null || headTransform == null)
            return;

        if (GetHorizontalDistanceToTable(headTransform.position) > TablePromptEdgeDistance)
            return;

        tablePromptQueued = true;
        EnqueueSequence(CreateTableHeightSequence());
    }

    private void TryQueuePreWeldPrompt()
    {
        if (preWeldPromptQueued || HasCompleted(PreWeldDetectionKey) || parameterMonitor == null)
            return;

        if (!parameterMonitor.HasPreviewMeasurements && !parameterMonitor.HasLiveMeasurements)
            return;

        preWeldPromptQueued = true;
        EnqueueSequence(CreatePreWeldSequence());
    }

    private void EnqueueSequence(TutorialSequence sequence)
    {
        if (!SimulatorSettings.GetTutorialsEnabled())
            return;

        if (sequence == null || sequence.Pages == null || sequence.Pages.Count == 0)
            return;

        queuedSequences.Enqueue(sequence);
        if (!isVisible && !isAnimating)
            ShowNextQueuedSequence();
    }

    private void ShowNextQueuedSequence()
    {
        if (queuedSequences.Count == 0)
            return;

        currentSequence = queuedSequences.Dequeue();
        currentPageIndex = 0;
        if (canvasRoot != null && !canvasRoot.activeSelf)
            canvasRoot.SetActive(true);

        ApplyPage(currentSequence.Pages[currentPageIndex], true);
        StartCoroutine(AnimateVisible(true));
    }

    private void ShowPreviousPage()
    {
        if (currentSequence == null || currentPageIndex <= 0 || isAnimating)
            return;

        currentPageIndex--;
        ApplyPage(currentSequence.Pages[currentPageIndex], false);
        PlayPageChangeSound();
    }

    private void ShowNextPage()
    {
        if (currentSequence == null || isAnimating)
            return;

        if (currentPageIndex < currentSequence.Pages.Count - 1)
        {
            currentPageIndex++;
            ApplyPage(currentSequence.Pages[currentPageIndex], false);
            PlayPageChangeSound();
            return;
        }

        CompleteCurrentSequence();
    }

    private void CloseCurrentSequence()
    {
        if (currentSequence == null || isAnimating)
            return;

        CompleteCurrentSequence();
    }

    private void CompleteCurrentSequence()
    {
        if (currentSequence != null)
            MarkCompleted(currentSequence.CompletionKey);

        StartCoroutine(HideThenAdvance());
    }

    private IEnumerator HideThenAdvance()
    {
        yield return AnimateVisible(false);
        currentSequence = null;
        currentPageIndex = 0;
        if (queuedSequences.Count > 0)
            ShowNextQueuedSequence();
        else
            RestoreWeldingHud();
    }

    private IEnumerator AnimateVisible(bool shouldShow)
    {
        if (canvasRoot == null || panelRect == null || panelCanvasGroup == null)
            yield break;

        isAnimating = true;
        if (shouldShow)
        {
            canvasRoot.SetActive(true);
            UpdateCanvasAnchor();
            HideWeldingHud();
            PlayPopupSound();
        }

        float duration = shouldShow ? ShowDuration : HideDuration;
        float elapsed = 0f;
        float startAlpha = panelCanvasGroup.alpha;
        float targetAlpha = shouldShow ? 1f : 0f;
        Vector3 startScale = panelRect.localScale;
        Vector3 targetScale = shouldShow ? Vector3.one : Vector3.one * 0.96f;
        Vector2 startPosition = panelRect.anchoredPosition;
        Vector2 targetPosition = shouldShow ? Vector2.zero : new Vector2(0f, -24f);

        if (shouldShow)
        {
            panelCanvasGroup.alpha = 0f;
            panelRect.localScale = Vector3.one * 0.94f;
            panelRect.anchoredPosition = new Vector2(0f, -28f);
            startAlpha = 0f;
            startScale = panelRect.localScale;
            startPosition = panelRect.anchoredPosition;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            float eased = shouldShow ? EaseOutBack(t) : EaseInCubic(t);
            panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            panelRect.localScale = Vector3.Lerp(startScale, targetScale, eased);
            panelRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, eased);
            yield return null;
        }

        panelCanvasGroup.alpha = targetAlpha;
        panelRect.localScale = targetScale;
        panelRect.anchoredPosition = targetPosition;
        isVisible = shouldShow;
        isAnimating = false;

        if (!shouldShow)
            canvasRoot.SetActive(false);
    }

    private void ApplyPage(TutorialPage page, bool firstPageOfPopup)
    {
        if (page == null)
            return;

        SetText(titleText, page.Title, whiteText);
        SetText(eyebrowText, page.Eyebrow, accentOrange);
        SetText(descriptionText, page.Description, mutedText);

        Texture2D stillTexture = string.IsNullOrWhiteSpace(page.ImageResourcePath)
            ? null
            : Resources.Load<Texture2D>(page.ImageResourcePath);
        VideoClip videoClip = string.IsNullOrWhiteSpace(page.VideoResourcePath)
            ? null
            : Resources.Load<VideoClip>(page.VideoResourcePath);

        bool hasStill = stillTexture != null;
        bool hasVideo = videoClip != null;

        if (stillFrame != null)
            stillFrame.SetActive(hasStill);
        if (videoFrame != null)
            videoFrame.SetActive(hasVideo);

        if (stillImage != null)
            stillImage.texture = stillTexture;

        ConfigureVideo(videoClip);
        ConfigureMediaLayout(hasStill, hasVideo, stillTexture, videoClip);

        bool hasMultiplePages = currentSequence != null && currentSequence.Pages.Count > 1;
        if (pageCounterText != null)
            pageCounterText.text = hasMultiplePages
                ? (currentPageIndex + 1) + " / " + currentSequence.Pages.Count
                : string.Empty;

        if (progressDotsText != null)
            progressDotsText.text = BuildProgressDots();

        if (backButton != null)
            backButton.gameObject.SetActive(hasMultiplePages);

        if (backButton != null)
            backButton.interactable = currentPageIndex > 0;

        if (nextButtonLabel != null)
            nextButtonLabel.text = hasMultiplePages && currentPageIndex < currentSequence.Pages.Count - 1
                ? "Next"
                : "Got It";

        if (closeButton != null)
            closeButton.gameObject.SetActive(hasMultiplePages);

        if (!firstPageOfPopup && panelRect != null)
            panelRect.localScale = Vector3.one;
    }

    private string BuildProgressDots()
    {
        if (currentSequence == null || currentSequence.Pages.Count <= 1)
            return string.Empty;

        string dots = string.Empty;
        for (int i = 0; i < currentSequence.Pages.Count; i++)
        {
            dots += i == currentPageIndex ? "●" : "○";
            if (i < currentSequence.Pages.Count - 1)
                dots += "  ";
        }

        return dots;
    }

    private void ConfigureVideo(VideoClip clip)
    {
        if (videoPlayer == null || videoImage == null)
            return;

        if (videoPrepareCoroutine != null)
        {
            StopCoroutine(videoPrepareCoroutine);
            videoPrepareCoroutine = null;
        }

        if (clip == null)
        {
            videoPlayer.Stop();
            videoPlayer.clip = null;
            videoImage.texture = null;
            return;
        }

        if (videoRenderTexture == null)
        {
            videoRenderTexture = new RenderTexture(1024, 576, 0, RenderTextureFormat.ARGB32)
            {
                name = "Tutorial Video Render Texture"
            };
        }

        videoPlayer.Stop();
        videoPlayer.clip = clip;
        videoPlayer.targetTexture = videoRenderTexture;
        videoImage.texture = videoRenderTexture;
        videoRenderTexture.DiscardContents();
        videoPrepareCoroutine = StartCoroutine(PrepareAndPlayVideo());
    }

    private IEnumerator PrepareAndPlayVideo()
    {
        if (videoPlayer == null)
            yield break;

        videoPlayer.Prepare();
        while (videoPlayer != null && !videoPlayer.isPrepared)
            yield return null;

        if (videoPlayer == null)
            yield break;

        videoPlayer.Play();
        videoPrepareCoroutine = null;
    }

    private void ConfigureMediaLayout(bool hasStill, bool hasVideo, Texture2D stillTexture, VideoClip videoClip)
    {
        RectTransform stillRect = stillFrame != null ? stillFrame.GetComponent<RectTransform>() : null;
        RectTransform videoRect = videoFrame != null ? videoFrame.GetComponent<RectTransform>() : null;

        if (hasStill && hasVideo)
        {
            SetMediaFrameLayout(stillRect, new Vector2(455f, 300f), new Vector2(-240f, 0f));
            SetMediaFrameLayout(videoRect, new Vector2(455f, 300f), new Vector2(240f, 0f));
        }
        else if (hasStill)
        {
            SetMediaFrameLayout(stillRect, new Vector2(760f, 300f), Vector2.zero);
        }
        else if (hasVideo)
        {
            SetMediaFrameLayout(videoRect, new Vector2(760f, 300f), Vector2.zero);
        }

        ConfigureRawImageAspect(stillImage, stillTexture != null ? stillTexture.width : 0, stillTexture != null ? stillTexture.height : 0);
        ConfigureRawImageAspect(videoImage, videoClip != null ? (int)videoClip.width : 0, videoClip != null ? (int)videoClip.height : 0);
    }

    private static void SetMediaFrameLayout(RectTransform rect, Vector2 size, Vector2 position)
    {
        if (rect == null)
            return;

        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void ConfigureRawImageAspect(RawImage image, int width, int height)
    {
        if (image == null)
            return;

        AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>();
        if (fitter == null)
            fitter = image.gameObject.AddComponent<AspectRatioFitter>();

        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = width > 0 && height > 0 ? (float)width / height : 16f / 9f;
    }

    private void BuildUi()
    {
        canvasRoot = CreateUiObject("Tutorial Canvas", transform, 5);
        RectTransform canvasRect = canvasRoot.AddComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1280f, 820f);
        canvasRoot.transform.localScale = Vector3.one * CanvasScale;

        tutorialCanvas = canvasRoot.AddComponent<Canvas>();
        tutorialCanvas.renderMode = RenderMode.WorldSpace;
        tutorialCanvas.worldCamera = Camera.main;
        tutorialCanvas.overrideSorting = true;
        tutorialCanvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        canvasRoot.AddComponent<TrackedDeviceGraphicRaycaster>();

        Image dimmer = canvasRoot.AddComponent<Image>();
        dimmer.sprite = panelSprite;
        dimmer.color = dimOverlay;
        dimmer.raycastTarget = true;

        panelRoot = CreateUiObject("Tutorial Panel", canvasRoot.transform, 5);
        panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1120f, 700f);

        panelCanvasGroup = panelRoot.AddComponent<CanvasGroup>();
        panelCanvasGroup.alpha = 0f;

        Image panelImage = panelRoot.AddComponent<Image>();
        panelImage.sprite = panelSprite;
        panelImage.type = Image.Type.Sliced;
        panelImage.color = panelColor;

        Outline panelOutline = panelRoot.AddComponent<Outline>();
        panelOutline.effectColor = accentBlue;
        panelOutline.effectDistance = new Vector2(3f, -3f);
        panelOutline.useGraphicAlpha = false;

        CreateDivider(panelRoot.transform, new Vector2(0f, 252f), 1000f);
        CreateDivider(panelRoot.transform, new Vector2(0f, -246f), 1000f);

        eyebrowText = CreateAnchoredText(panelRoot.transform, "Eyebrow", "VR TUTORIAL", new Vector2(960f, 36f), new Vector2(0f, 306f), 24f, FontStyles.Bold, accentOrange);
        titleText = CreateAnchoredText(panelRoot.transform, "Title", "Tutorial Title", new Vector2(960f, 60f), new Vector2(0f, 268f), 42f, FontStyles.Bold, whiteText);
        descriptionText = CreateAnchoredText(panelRoot.transform, "Description", "Tutorial description", new Vector2(920f, 72f), new Vector2(0f, -174f), 22f, FontStyles.Normal, mutedText);
        descriptionText.textWrappingMode = TextWrappingModes.Normal;

        GameObject mediaRoot = CreateUiObject("Media Root", panelRoot.transform, 5);
        RectTransform mediaRootRect = mediaRoot.AddComponent<RectTransform>();
        mediaRootRect.anchorMin = new Vector2(0.5f, 0.5f);
        mediaRootRect.anchorMax = new Vector2(0.5f, 0.5f);
        mediaRootRect.pivot = new Vector2(0.5f, 0.5f);
        mediaRootRect.sizeDelta = new Vector2(960f, 330f);
        mediaRootRect.anchoredPosition = new Vector2(0f, 36f);

        stillFrame = CreateMediaFrame(mediaRoot.transform, "Still Frame", new Vector2(455f, 300f), new Vector2(-240f, 0f), out stillImage);
        videoFrame = CreateMediaFrame(mediaRoot.transform, "Video Frame", new Vector2(455f, 300f), new Vector2(240f, 0f), out videoImage);

        videoPlayer = panelRoot.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;

        pageCounterText = CreateAnchoredText(panelRoot.transform, "Page Counter", string.Empty, new Vector2(180f, 30f), new Vector2(0f, -214f), 20f, FontStyles.Bold, mutedText);
        progressDotsText = CreateAnchoredText(panelRoot.transform, "Progress Dots", string.Empty, new Vector2(220f, 26f), new Vector2(0f, -238f), 18f, FontStyles.Bold, accentBlue);

        backButton = CreateButton(panelRoot.transform, "Back", new Vector2(220f, 64f), new Vector2(-300f, -314f), accentBlue, out _);
        backButton.onClick.AddListener(ShowPreviousPage);

        nextButton = CreateButton(panelRoot.transform, "Next", new Vector2(280f, 64f), new Vector2(0f, -314f), accentOrange, out nextButtonLabel);
        nextButton.onClick.AddListener(ShowNextPage);

        closeButton = CreateButton(panelRoot.transform, "Skip", new Vector2(220f, 64f), new Vector2(300f, -314f), accentBlue, out _);
        closeButton.onClick.AddListener(CloseCurrentSequence);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.8f;

        ApplyNoDepthMaterials();
        canvasRoot.SetActive(false);
    }

    private GameObject CreateMediaFrame(Transform parent, string name, Vector2 size, Vector2 position, out RawImage rawImage)
    {
        GameObject frame = CreateUiObject(name, parent, 5);
        RectTransform frameRect = frame.AddComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = size;
        frameRect.anchoredPosition = position;

        Image background = frame.AddComponent<Image>();
        background.sprite = panelSprite;
        background.type = Image.Type.Sliced;
        background.color = cardColor;

        Outline outline = frame.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = false;

        GameObject media = CreateUiObject("Media", frame.transform, 5);
        RectTransform mediaRect = media.AddComponent<RectTransform>();
        mediaRect.anchorMin = Vector2.zero;
        mediaRect.anchorMax = Vector2.one;
        mediaRect.offsetMin = new Vector2(10f, 10f);
        mediaRect.offsetMax = new Vector2(-10f, -10f);

        rawImage = media.AddComponent<RawImage>();
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;
        return frame;
    }

    private void ResolveMainSceneDependencies()
    {
        if (parameterMonitor == null)
            parameterMonitor = FindFirstObjectByType<WeldParameterMonitor>();

        if (weldingHudPanel == null)
            weldingHudPanel = FindFirstObjectByType<WeldingHudPanel>(FindObjectsInactive.Include);

        if (tableTransform == null)
        {
            GameObject table = GameObject.Find("welding table");
            if (table == null)
                table = GameObject.Find("Welding Table");
            if (table == null)
                table = GameObject.Find("Table");

            if (table != null)
            {
                tableTransform = table.transform;
                tableCollider = table.GetComponent<Collider>();
                if (tableCollider == null)
                    tableCollider = table.GetComponentInChildren<Collider>();
            }
        }
    }

    private float GetHorizontalDistanceToTable(Vector3 worldPosition)
    {
        if (tableCollider == null)
            return tableTransform != null
                ? HorizontalDistance(worldPosition, tableTransform.position)
                : float.PositiveInfinity;

        Bounds bounds = tableCollider.bounds;
        float closestX = Mathf.Clamp(worldPosition.x, bounds.min.x, bounds.max.x);
        float closestZ = Mathf.Clamp(worldPosition.z, bounds.min.z, bounds.max.z);
        return HorizontalDistance(worldPosition, new Vector3(closestX, worldPosition.y, closestZ));
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    private void UpdateCanvasAnchor()
    {
        if (canvasRoot == null || headTransform == null)
            return;

        Transform canvasTransform = canvasRoot.transform;
        if (canvasTransform.parent != headTransform)
            canvasTransform.SetParent(headTransform, false);

        canvasTransform.localPosition = new Vector3(0f, -0.02f, CanvasDistance);
        canvasTransform.localRotation = Quaternion.identity;

        if (tutorialCanvas != null && tutorialCanvas.worldCamera == null && Camera.main != null)
            tutorialCanvas.worldCamera = Camera.main;
    }

    private void FindHeadTransform()
    {
        if (Camera.main != null)
        {
            headTransform = Camera.main.transform;
            return;
        }

        Camera anyCamera = FindFirstObjectByType<Camera>();
        if (anyCamera != null)
            headTransform = anyCamera.transform;
    }

    private TutorialSequence CreateStartBasicsSequence()
    {
        return new TutorialSequence
        {
            CompletionKey = StartBasicsKey,
            Pages = new List<TutorialPage>
            {
                new TutorialPage
                {
                    Eyebrow = "MENU CONTROL",
                    Title = "Aim With The Ray",
                    Description = "Point the controller ray at menu buttons, then press the trigger to confirm your selection.",
                    ImageResourcePath = "Tutorial/Images/Ray cast menue"
                },
                new TutorialPage
                {
                    Eyebrow = "COMFORT TURNING",
                    Title = "Snap Turn",
                    Description = "Snap turn rotates the view in fixed steps. It is the gentler option if smooth turning feels uncomfortable.",
                    ImageResourcePath = "Tutorial/Images/Snap pause",
                    VideoResourcePath = "Tutorial/Videos/Snap spin"
                },
                new TutorialPage
                {
                    Eyebrow = "COMFORT TURNING",
                    Title = "Continuous Turn",
                    Description = "Continuous turn rotates smoothly while the stick is held. Choose the style that feels best before welding.",
                    ImageResourcePath = "Tutorial/Images/continuous pause",
                    VideoResourcePath = "Tutorial/Videos/continous spin"
                }
            }
        };
    }

    private TutorialSequence CreateMovementSequence()
    {
        return new TutorialSequence
        {
            CompletionKey = MovementKey,
            Pages = new List<TutorialPage>
            {
                new TutorialPage
                {
                    Eyebrow = "MOVEMENT",
                    Title = "Move Around The Workshop",
                    Description = "Use the thumbstick to move through the workshop and position yourself comfortably before starting the weld.",
                    VideoResourcePath = "Tutorial/Videos/movment"
                },
                new TutorialPage
                {
                    Eyebrow = "PAUSE MENU",
                    Title = "Open The Pause Screen",
                    Description = "Press the menu button on the left controller at any time to open the pause screen, where you can resume, adjust options, finish the weld, restart, or return to the main menu.",
                    ImageResourcePath = "Tutorial/Images/pause screen"
                }
            }
        };
    }

    private TutorialSequence CreateTableHeightSequence()
    {
        return CreateSinglePageSequence(
            TableHeightKey,
            "WORKSTATION SETUP",
            "Adjust Table Height",
            "Use the table-height control to bring the joint into a comfortable working position before welding.",
            null,
            "Tutorial/Videos/Adjust table height");
    }

    private TutorialSequence CreatePreWeldSequence()
    {
        return new TutorialSequence
        {
            CompletionKey = PreWeldDetectionKey,
            Pages = new List<TutorialPage>
            {
                new TutorialPage
                {
                    Eyebrow = "PRE-WELD CHECK",
                    Title = "Use The Live Parameters",
                    Description = "Before pulling the trigger, line up the torch until the HUD shows sensible angle and CTWD values. This helps you begin the weld from a better pose.",
                    VideoResourcePath = "Tutorial/Videos/Dedction before welding"
                },
                new TutorialPage
                {
                    Eyebrow = "POST-WELD REVIEW",
                    Title = "Read The Final Report",
                    Description = "When the weld is complete, use Finish Weld from the pause menu to review the dominant defect, measured parameters, and the main corrections to make on the next attempt.",
                    ImageResourcePath = "Tutorial/Images/finish weld report"
                }
            }
        };
    }

    private static TutorialSequence CreateSinglePageSequence(
        string completionKey,
        string eyebrow,
        string title,
        string description,
        string imageResourcePath,
        string videoResourcePath)
    {
        return new TutorialSequence
        {
            CompletionKey = completionKey,
            Pages = new List<TutorialPage>
            {
                new TutorialPage
                {
                    Eyebrow = eyebrow,
                    Title = title,
                    Description = description,
                    ImageResourcePath = imageResourcePath,
                    VideoResourcePath = videoResourcePath
                }
            }
        };
    }

    private void PlayPopupSound()
    {
        if (audioSource != null && popupClip != null)
            audioSource.PlayOneShot(popupClip);
    }

    private void PlayPageChangeSound()
    {
        if (audioSource != null && pageChangeClip != null)
            audioSource.PlayOneShot(pageChangeClip);
    }

    private static bool HasCompleted(string key)
    {
        return completedTutorialsThisSession.Contains(key);
    }

    private static void MarkCompleted(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        completedTutorialsThisSession.Add(key);
    }

    private GameObject CreateUiObject(string name, Transform parent, int layer)
    {
        GameObject go = new GameObject(name);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    private TMP_Text CreateAnchoredText(
        Transform parent,
        string objectName,
        string value,
        Vector2 size,
        Vector2 anchoredPosition,
        float fontSize,
        FontStyles style,
        Color color)
    {
        GameObject textObject = CreateUiObject(objectName, parent, 5);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        TMP_Text text = CreateText(textObject, value, fontSize, style, TextAlignmentOptions.Center);
        text.color = color;
        return text;
    }

    private Button CreateButton(
        Transform parent,
        string label,
        Vector2 size,
        Vector2 anchoredPosition,
        Color accentColor,
        out TMP_Text labelText)
    {
        GameObject buttonObject = CreateUiObject(label + " Button", parent, 5);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = buttonObject.AddComponent<Image>();
        image.sprite = panelSprite;
        image.type = Image.Type.Sliced;
        image.color = cardColor;

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = false;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.38f);
        button.colors = colors;

        GameObject labelObject = CreateUiObject("Label", buttonObject.transform, 5);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        StretchToParent(labelRect);
        labelText = CreateText(labelObject, label, 26f, FontStyles.Bold, TextAlignmentOptions.Center);
        labelText.color = accentColor;
        return button;
    }

    private void HideWeldingHud()
    {
        ResolveMainSceneDependencies();
        if (weldingHudPanel == null || hudHiddenByTutorial)
            return;

        hudWasActiveBeforeTutorial = weldingHudPanel.gameObject.activeSelf;
        weldingHudPanel.gameObject.SetActive(false);
        hudHiddenByTutorial = true;
    }

    private void RestoreWeldingHud()
    {
        if (!hudHiddenByTutorial)
            return;

        if (weldingHudPanel != null)
            weldingHudPanel.gameObject.SetActive(hudWasActiveBeforeTutorial);

        hudHiddenByTutorial = false;
    }

    private TMP_Text CreateText(GameObject owner, string value, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = owner.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.margin = Vector4.zero;
        text.raycastTarget = false;
        return text;
    }

    private void CreateDivider(Transform parent, Vector2 anchoredPosition, float width)
    {
        GameObject divider = CreateUiObject("Divider", parent, 5);
        RectTransform rect = divider.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, 2f);
        rect.anchoredPosition = anchoredPosition;

        Image image = divider.AddComponent<Image>();
        image.color = dividerColor;
    }

    private void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
    }

    private void ApplyNoDepthMaterials()
    {
        if (canvasRoot == null)
            return;

        Graphic[] graphics = canvasRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            TextMeshProUGUI text = graphic as TextMeshProUGUI;
            if (text != null)
            {
                ApplyNoDepthTextMaterial(text);
                continue;
            }

            Material material = GetNoDepthUiMaterial();
            if (material != null)
                graphic.material = material;
        }
    }

    private Material GetNoDepthUiMaterial()
    {
        if (noDepthUiMaterial != null)
            return noDepthUiMaterial;

        Shader noDepthShader = Shader.Find("UI/NoZTest");
        if (noDepthShader == null)
            return null;

        noDepthUiMaterial = new Material(noDepthShader)
        {
            name = "Runtime Tutorial UI No Depth"
        };

        return noDepthUiMaterial;
    }

    private static void ApplyNoDepthTextMaterial(TextMeshProUGUI text)
    {
        Material sourceMaterial = text.fontSharedMaterial;
        if (sourceMaterial == null)
            return;

        Shader overlayShader = Shader.Find("TextMeshPro/Distance Field Overlay");
        if (overlayShader == null)
            overlayShader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");

        if (overlayShader == null)
            return;

        Material overlayMaterial = new Material(sourceMaterial)
        {
            shader = overlayShader,
            name = sourceMaterial.name + " Tutorial No Depth"
        };

        text.fontSharedMaterial = overlayMaterial;
    }

    private Sprite LoadDefaultSprite()
    {
        if (generatedSpriteTexture == null)
        {
            const int textureSize = 64;
            const int cornerRadius = 14;
            generatedSpriteTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            generatedSpriteTexture.wrapMode = TextureWrapMode.Clamp;
            generatedSpriteTexture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float alpha = SampleRoundedRectAlpha(x, y, textureSize, cornerRadius);
                    generatedSpriteTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            generatedSpriteTexture.Apply();
        }

        return Sprite.Create(
            generatedSpriteTexture,
            new Rect(0f, 0f, generatedSpriteTexture.width, generatedSpriteTexture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(18f, 18f, 18f, 18f));
    }

    private static float SampleRoundedRectAlpha(int pixelX, int pixelY, int textureSize, int radius)
    {
        float sampleX = pixelX + 0.5f;
        float sampleY = pixelY + 0.5f;
        float min = radius;
        float max = textureSize - radius;

        if ((sampleX >= min && sampleX <= max) || (sampleY >= min && sampleY <= max))
            return 1f;

        float cornerX = sampleX < min ? min : max;
        float cornerY = sampleY < min ? min : max;
        float distance = Vector2.Distance(new Vector2(sampleX, sampleY), new Vector2(cornerX, cornerY));
        return Mathf.Clamp01(radius + 0.5f - distance);
    }

    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.70158f;
        float shifted = t - 1f;
        return 1f + shifted * shifted * ((overshoot + 1f) * shifted + overshoot);
    }

    private static float EaseInCubic(float t)
    {
        return t * t * t;
    }

    private static void SetText(TMP_Text target, string value, Color color)
    {
        if (target == null)
            return;

        target.text = value;
        target.color = color;
    }
}
