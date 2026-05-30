using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class MainScenePauseMenu : MonoBehaviour
{
    private const string MainSceneName = "Main VR Scene";
    private const string StartSceneName = "Start Scene";
    private const float MenuDistance = 1.2f;
    private const float MenuVerticalOffset = -0.02f;
    private const float MenuScale = 0.00145f;
    private const float VolumeStep = 0.05f;
    private const string DefaultClickAudioName = "uiclick";
    private const string DefaultHoverEnterAudioName = "uihoverenter";
    private const float ReportNearSpeedMargin = 0.8f;
    private const float ReportNearWorkAngleMargin = 5f;
    private const float ReportNearTravelAngleMargin = 3f;
    private const float ReportNearCtwdMargin = 2f;

    private enum ReportParameterStatus
    {
        Great,
        Ok,
        Bad
    }

    private readonly Dictionary<Behaviour, bool> storedLocomotionStates = new Dictionary<Behaviour, bool>();

    private Transform headTransform;
    private Canvas pauseCanvas;
    private GameObject canvasRoot;
    private GameObject mainPanel;
    private GameObject optionsPanel;
    private GameObject reportPanel;
    private GameObject reviewPanel;
    private GameObject confirmationPopup;
    private Button resumeButton;
    private Button optionsButton;
    private Button finishWeldButton;
    private Button mainMenuButton;
    private Button restartSceneButton;
    private Button reportBackButton;
    private Button reportRestartButton;
    private Button optionsConfirmButton;
    private Button volumeDecreaseButton;
    private Button volumeIncreaseButton;
    private Button tableHeightDecreaseButton;
    private Button tableHeightIncreaseButton;
    private Button turnModeButton;
    private Button popupConfirmButton;
    private Button popupCancelButton;
    private TMP_Text volumeValueText;
    private TMP_Text tableHeightValueText;
    private TMP_Text turnValueText;
    private TMP_Text popupTitleText;
    private TMP_Text popupDescriptionText;
    private TMP_Text popupConfirmButtonText;
    private TMP_Text reportHeadlineText;
    private TMP_Text reportSummaryText;
    private TMP_Text reportJointValueText;
    private TMP_Text reportTimeValueText;
    private TMP_Text reportMainDefectValueText;
    private TMP_Text reportNormalWeldValueText;
    private TMP_Text reviewHeadlineText;
    private TMP_Text reviewSummaryText;
    private RawImage reviewImage;
    private readonly TMP_Text[] reportParameterResultTexts = new TMP_Text[4];
    private readonly TMP_Text[] reportParameterTargetTexts = new TMP_Text[4];
    private readonly TMP_Text[] reportParameterStatusTexts = new TMP_Text[4];
    private readonly GameObject[] reportIssueRows = new GameObject[3];
    private readonly TMP_Text[] reportIssueLabelTexts = new TMP_Text[3];
    private readonly TMP_Text[] reportIssuePercentTexts = new TMP_Text[3];
    private readonly Image[] reportIssueFillImages = new Image[3];
    private readonly TMP_Text[] reportQualityPercentTexts = new TMP_Text[5];
    private readonly Image[] reportQualityFillImages = new Image[5];
    private readonly Button[] reportQualityButtons = new Button[5];
    private Sprite panelSprite;
    private Texture2D generatedSpriteTexture;
    private Material noDepthUiMaterial;
    private bool isMenuOpen;
    private bool lastPauseButtonState;
    private bool hasSeenPauseButtonRelease;
    private float previousTimeScale = 1f;
    private Action pendingConfirmationAction;
    private GameObject panelVisibleBeforeConfirmation;
    private WeldReportTracker reportTracker;
    private WeldingHudPanel weldingHudPanel;
    private WeldBead weldBeadReviewSource;
    private bool hudWasActiveBeforeMenu;
    private Camera reviewCamera;
    private RenderTexture reviewRenderTexture;

    public bool IsMenuOpen => isMenuOpen;

    private readonly Color panelColor = new Color32(0, 0, 0, 235);
    private readonly Color buttonColor = new Color32(0, 0, 0, 255);
    private readonly Color overlayColor = new Color32(0, 0, 0, 180);
    private readonly Color whiteText = new Color32(255, 255, 255, 255);
    private readonly Color outlineColor = new Color32(255, 255, 255, 255);
    private readonly Color valueTint = new Color32(220, 220, 220, 255);
    private readonly Color reportPanelColor = new Color32(7, 16, 27, 242);
    private readonly Color reportCardColor = new Color32(10, 22, 36, 235);
    private readonly Color reportCardOutlineColor = new Color32(45, 74, 99, 255);
    private readonly Color reportDividerColor = new Color32(35, 58, 79, 255);
    private readonly Color reportAccentBlue = new Color32(104, 180, 255, 255);
    private readonly Color reportMutedText = new Color32(188, 201, 215, 255);
    private readonly Color reportWarningOrange = new Color32(255, 177, 92, 255);
    private readonly Color reportPassGreen = new Color32(91, 211, 119, 255);
    private readonly Color reportFailRed = new Color32(237, 93, 83, 255);
    private readonly Color reportTrackColor = new Color32(22, 35, 49, 255);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MainSceneName)
            return;

        if (UnityEngine.Object.FindFirstObjectByType<MainScenePauseMenu>() != null)
            return;

        GameObject pauseMenuObject = new GameObject("Main Scene Pause Menu");
        pauseMenuObject.AddComponent<MainScenePauseMenu>();
    }

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != MainSceneName)
        {
            enabled = false;
            return;
        }

        panelSprite = LoadDefaultSprite();
        FindHeadTransform();
        lastPauseButtonState = IsPauseButtonPressed();
        hasSeenPauseButtonRelease = !lastPauseButtonState;
    }

    private void OnDestroy()
    {
        if (isMenuOpen)
        {
            Time.timeScale = 1f;
            RestoreLocomotionProviders();
        }

        if (generatedSpriteTexture != null)
            Destroy(generatedSpriteTexture);

        if (noDepthUiMaterial != null)
            Destroy(noDepthUiMaterial);

        if (reviewRenderTexture != null)
        {
            reviewRenderTexture.Release();
            Destroy(reviewRenderTexture);
        }

        if (reviewCamera != null)
            Destroy(reviewCamera.gameObject);
    }

    private void Update()
    {
        if (!enabled)
            return;

        if (headTransform == null)
            FindHeadTransform();

        RefreshCanvasCamera();

        bool pausePressed = IsPauseButtonPressed();
        if (!hasSeenPauseButtonRelease)
        {
            if (!pausePressed)
                hasSeenPauseButtonRelease = true;
        }
        else if (pausePressed && !lastPauseButtonState)
            TogglePauseMenu();

        lastPauseButtonState = pausePressed;

        if (isMenuOpen)
            UpdateMenuAnchor();
    }

    private void TogglePauseMenu()
    {
        SetMenuOpen(!isMenuOpen, true);
    }

    private void SetMenuOpen(bool shouldOpen, bool updateTimeScale)
    {
        isMenuOpen = shouldOpen;

        if (!shouldOpen)
        {
            if (canvasRoot != null)
                canvasRoot.SetActive(false);

            if (updateTimeScale)
                Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;

            RestoreLocomotionProviders();
            RestoreWeldingHud();
            RestoreAllBeadVisibility();
            ShowMainPanel();
            return;
        }

        if (headTransform == null)
            FindHeadTransform();

        if (canvasRoot == null)
            BuildMenu();

        RefreshCanvasCamera();
        if (headTransform != null)
            UpdateMenuAnchor();

        if (canvasRoot != null)
            canvasRoot.SetActive(true);

        previousTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
        if (updateTimeScale)
            Time.timeScale = 0f;

        StoreAndDisableLocomotionProviders();
        HideWeldingHud();
        ShowMainPanel();
        RefreshOptionsDisplay();
        if (headTransform != null)
            UpdateMenuAnchor();
    }

    private void ShowMainPanel()
    {
        if (mainPanel != null)
            mainPanel.SetActive(true);

        if (optionsPanel != null)
            optionsPanel.SetActive(false);

        if (reportPanel != null)
            reportPanel.SetActive(false);

        if (reviewPanel != null)
            reviewPanel.SetActive(false);

        if (confirmationPopup != null)
            confirmationPopup.SetActive(false);

        RestoreAllBeadVisibility();
    }

    private void ShowOptionsPanel()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);

        if (optionsPanel != null)
            optionsPanel.SetActive(true);

        if (reportPanel != null)
            reportPanel.SetActive(false);

        if (reviewPanel != null)
            reviewPanel.SetActive(false);

        if (confirmationPopup != null)
            confirmationPopup.SetActive(false);

        RestoreAllBeadVisibility();
        RefreshOptionsDisplay();
    }

    private void ShowMainMenuPopup()
    {
        ShowConfirmationPopup(
            "Return To Main Menu?",
            "If you hit Confirm, you will return to the Main Menu and lose all progress made in this session.",
            "Confirm",
            ReturnToMainMenu);
    }

    private void ShowRestartScenePopup()
    {
        ShowConfirmationPopup(
            "Restart Welding Session?",
            "Restarting will clear the current welding progress and reload this session, allowing you to start over.",
            "Restart",
            RestartCurrentScene);
    }

    private void ShowReportPanel()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);

        if (optionsPanel != null)
            optionsPanel.SetActive(false);

        if (confirmationPopup != null)
            confirmationPopup.SetActive(false);

        if (reviewPanel != null)
            reviewPanel.SetActive(false);

        if (reportPanel != null)
            reportPanel.SetActive(true);

        RestoreAllBeadVisibility();
    }

    private void FinishWeld()
    {
        ResolveReportTracker();
        PopulateReport();

        ShowReportPanel();
    }

    private void HideConfirmationPopup()
    {
        pendingConfirmationAction = null;

        if (confirmationPopup != null)
            confirmationPopup.SetActive(false);

        if (panelVisibleBeforeConfirmation != null)
        {
            panelVisibleBeforeConfirmation.SetActive(true);
            panelVisibleBeforeConfirmation = null;
        }
    }

    private void ConfirmPopupAction()
    {
        Action action = pendingConfirmationAction;
        HideConfirmationPopup();
        action?.Invoke();
    }

    private void ResumeScene()
    {
        SetMenuOpen(false, true);
    }

    private void ConfirmOptions()
    {
        ShowMainPanel();
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        RestoreLocomotionProviders();
        isMenuOpen = false;
        SceneTransitionManager.GoToSceneAsync(StartSceneName);
    }

    private void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        RestoreLocomotionProviders();
        isMenuOpen = false;
        SceneTransitionManager.GoToSceneAsync(MainSceneName);
    }

    private void AdjustVolume(float delta)
    {
        SimulatorSettings.SetVolume(SimulatorSettings.GetVolume() + delta);
        RefreshOptionsDisplay();
    }

    private void AdjustTableHeight(float delta)
    {
        SimulatorSettings.SetTableHeight(SimulatorSettings.GetTableHeight() + delta);
        TableHeightManager.ApplyCurrentTableHeight();
        RefreshOptionsDisplay();
    }

    private void ToggleTurnMode()
    {
        int nextTurnMode = SimulatorSettings.GetTurnModeIndex() == SimulatorSettings.ContinuousTurnIndex
            ? SimulatorSettings.SnapTurnIndex
            : SimulatorSettings.ContinuousTurnIndex;

        SimulatorSettings.SetTurnModeIndex(nextTurnMode);
        RefreshOptionsDisplay();
    }

    private void RefreshOptionsDisplay()
    {
        if (volumeValueText != null)
            volumeValueText.text = Mathf.RoundToInt(SimulatorSettings.GetVolume() * 100f) + "%";

        if (tableHeightValueText != null)
            tableHeightValueText.text = SimulatorSettings.GetTableHeight().ToString("0.00") + " m";

        if (turnValueText != null)
            turnValueText.text = SimulatorSettings.GetTurnModeIndex() == SimulatorSettings.ContinuousTurnIndex
                ? "Continuous"
                : "Snap";
    }

    private void BuildMenu()
    {
        canvasRoot = CreateUIObject("Pause Menu Canvas", transform, 5);
        RectTransform canvasRect = canvasRoot.AddComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1200f, 900f);
        canvasRoot.transform.localScale = Vector3.one * MenuScale;

        pauseCanvas = canvasRoot.AddComponent<Canvas>();
        pauseCanvas.renderMode = RenderMode.WorldSpace;
        pauseCanvas.worldCamera = Camera.main;
        pauseCanvas.overrideSorting = true;
        pauseCanvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        canvasRoot.AddComponent<TrackedDeviceGraphicRaycaster>();

        mainPanel = CreatePanel("Main Panel", canvasRoot.transform, new Vector2(760f, 820f));
        CreateAnchoredText(mainPanel.transform, "Paused Title", "Paused", new Vector2(640f, 70f), new Vector2(0f, 320f), 54f, FontStyles.Bold, whiteText);

        resumeButton = CreateAnchoredButton(mainPanel.transform, "Resume", new Vector2(580f, 78f), new Vector2(0f, 180f));
        resumeButton.onClick.AddListener(ResumeScene);

        optionsButton = CreateAnchoredButton(mainPanel.transform, "Options", new Vector2(580f, 78f), new Vector2(0f, 80f));
        optionsButton.onClick.AddListener(ShowOptionsPanel);

        finishWeldButton = CreateAnchoredButton(mainPanel.transform, "Finish Weld", new Vector2(580f, 78f), new Vector2(0f, -20f));
        finishWeldButton.onClick.AddListener(FinishWeld);

        restartSceneButton = CreateAnchoredButton(mainPanel.transform, "Restart", new Vector2(580f, 78f), new Vector2(0f, -120f));
        restartSceneButton.onClick.AddListener(ShowRestartScenePopup);

        mainMenuButton = CreateAnchoredButton(mainPanel.transform, "Main Menu", new Vector2(580f, 78f), new Vector2(0f, -230f));
        mainMenuButton.onClick.AddListener(ShowMainMenuPopup);

        optionsPanel = CreatePanel("Options Panel", canvasRoot.transform, new Vector2(840f, 920f));
        CreateAnchoredText(optionsPanel.transform, "Options Title", "Options", new Vector2(660f, 64f), new Vector2(0f, 365f), 48f, FontStyles.Bold, whiteText);
        CreateAnchoredText(optionsPanel.transform, "Options Description", "Optimize your experience.", new Vector2(720f, 56f), new Vector2(0f, 310f), 24f, FontStyles.Normal, valueTint);
        CreateAnchoredText(optionsPanel.transform, "Volume Label", "Volume", new Vector2(280f, 44f), new Vector2(0f, 205f), 28f, FontStyles.Bold, whiteText);

        volumeDecreaseButton = CreateAnchoredButton(optionsPanel.transform, "-", new Vector2(110f, 78f), new Vector2(-170f, 125f));
        volumeDecreaseButton.onClick.AddListener(() => AdjustVolume(-VolumeStep));

        CreateValueDisplay(optionsPanel.transform, "Volume Value", new Vector2(220f, 78f), new Vector2(0f, 125f), out volumeValueText);

        volumeIncreaseButton = CreateAnchoredButton(optionsPanel.transform, "+", new Vector2(110f, 78f), new Vector2(170f, 125f));
        volumeIncreaseButton.onClick.AddListener(() => AdjustVolume(VolumeStep));

        CreateAnchoredText(optionsPanel.transform, "Table Height Label", "Table Height", new Vector2(320f, 44f), new Vector2(0f, 20f), 28f, FontStyles.Bold, whiteText);

        tableHeightDecreaseButton = CreateAnchoredButton(optionsPanel.transform, "-", new Vector2(110f, 78f), new Vector2(-170f, -60f));
        tableHeightDecreaseButton.onClick.AddListener(() => AdjustTableHeight(-SimulatorSettings.TableHeightStep));

        CreateValueDisplay(optionsPanel.transform, "Table Height Value", new Vector2(220f, 78f), new Vector2(0f, -60f), out tableHeightValueText);

        tableHeightIncreaseButton = CreateAnchoredButton(optionsPanel.transform, "+", new Vector2(110f, 78f), new Vector2(170f, -60f));
        tableHeightIncreaseButton.onClick.AddListener(() => AdjustTableHeight(SimulatorSettings.TableHeightStep));

        CreateAnchoredText(optionsPanel.transform, "Turning Label", "Stick Turning Behavior", new Vector2(420f, 44f), new Vector2(0f, -165f), 28f, FontStyles.Bold, whiteText);

        turnModeButton = CreateAnchoredButton(optionsPanel.transform, "Continuous", new Vector2(460f, 78f), new Vector2(0f, -245f));
        turnModeButton.onClick.AddListener(ToggleTurnMode);
        turnValueText = turnModeButton.GetComponentInChildren<TMP_Text>(true);

        optionsConfirmButton = CreateAnchoredButton(optionsPanel.transform, "Confirm", new Vector2(580f, 82f), new Vector2(0f, -380f));
        optionsConfirmButton.onClick.AddListener(ConfirmOptions);

        reportPanel = CreatePanel("Final Weld Report Panel", canvasRoot.transform, new Vector2(1140f, 900f));
        StyleReportPanel(reportPanel);
        CreateAnchoredText(reportPanel.transform, "Report Title", "FINAL WELD REPORT", new Vector2(980f, 58f), new Vector2(0f, 388f), 40f, FontStyles.Bold, whiteText);

        reportHeadlineText = CreateAnchoredText(reportPanel.transform, "Report Headline", "No weld data recorded", new Vector2(980f, 42f), new Vector2(0f, 335f), 28f, FontStyles.Bold, reportWarningOrange);
        reportSummaryText = CreateAnchoredText(reportPanel.transform, "Report Summary", "Record a weld before generating a report.", new Vector2(980f, 38f), new Vector2(0f, 296f), 20f, FontStyles.Normal, reportMutedText);

        GameObject sessionCard = CreateReportCard("Session Summary Card", reportPanel.transform, new Vector2(390f, 225f), new Vector2(-345f, 145f));
        CreateReportSectionHeader(sessionCard.transform, "SESSION SUMMARY", new Vector2(0f, 82f), reportAccentBlue);
        reportJointValueText = CreateReportMetricRow(sessionCard.transform, "Joint", "--", 36f);
        reportTimeValueText = CreateReportMetricRow(sessionCard.transform, "Time", "--", -6f);
        reportMainDefectValueText = CreateReportMetricRow(sessionCard.transform, "Main defect", "--", -48f);
        reportNormalWeldValueText = CreateReportMetricRow(sessionCard.transform, "Normal weld", "--", -90f);

        GameObject parameterCard = CreateReportCard("Measured Parameters Card", reportPanel.transform, new Vector2(620f, 225f), new Vector2(185f, 145f));
        CreateReportSectionHeader(parameterCard.transform, "MEASURED PARAMETERS", new Vector2(0f, 82f), reportAccentBlue);
        CreateParameterTable(parameterCard.transform);

        GameObject issuesCard = CreateReportCard("Priority Corrections Card", reportPanel.transform, new Vector2(430f, 250f), new Vector2(-300f, -130f));
        CreateReportSectionHeader(issuesCard.transform, "PRIORITY CORRECTIONS", new Vector2(0f, 90f), reportWarningOrange);
        for (int i = 0; i < reportIssueRows.Length; i++)
            CreateIssueRow(issuesCard.transform, i, 46f - i * 62f);

        GameObject qualityCard = CreateReportCard("Weld Quality Breakdown Card", reportPanel.transform, new Vector2(550f, 250f), new Vector2(250f, -130f));
        CreateReportSectionHeader(qualityCard.transform, "WELD QUALITY BREAKDOWN", new Vector2(0f, 90f), reportAccentBlue);
        CreateQualityRows(qualityCard.transform);

        reportBackButton = CreateAnchoredButton(reportPanel.transform, "Back", new Vector2(320f, 72f), new Vector2(-190f, -350f));
        reportBackButton.onClick.AddListener(ShowMainPanel);

        reportRestartButton = CreateAnchoredButton(reportPanel.transform, "Restart", new Vector2(320f, 72f), new Vector2(190f, -350f));
        reportRestartButton.onClick.AddListener(ShowRestartScenePopup);

        reviewPanel = CreatePanel("Defect Review Panel", canvasRoot.transform, new Vector2(1140f, 900f));
        StyleReportPanel(reviewPanel);
        CreateAnchoredText(reviewPanel.transform, "Review Title", "DEFECT REVIEW", new Vector2(980f, 58f), new Vector2(0f, 388f), 40f, FontStyles.Bold, whiteText);
        reviewHeadlineText = CreateAnchoredText(reviewPanel.transform, "Review Headline", "Selected defect", new Vector2(980f, 42f), new Vector2(0f, 335f), 28f, FontStyles.Bold, reportWarningOrange);
        reviewSummaryText = CreateAnchoredText(reviewPanel.transform, "Review Summary", "Inspect the isolated bead section below.", new Vector2(980f, 38f), new Vector2(0f, 296f), 20f, FontStyles.Normal, reportMutedText);
        CreateReviewImageFrame(reviewPanel.transform);

        Button reviewBackButton = CreateAnchoredButton(reviewPanel.transform, "Back to Report", new Vector2(360f, 72f), new Vector2(0f, -350f));
        reviewBackButton.onClick.AddListener(ShowReportPanel);

        confirmationPopup = CreateUIObject("Confirmation Popup", canvasRoot.transform, 5);
        RectTransform popupRootRect = confirmationPopup.AddComponent<RectTransform>();
        StretchToParent(popupRootRect);

        Image popupOverlay = confirmationPopup.AddComponent<Image>();
        popupOverlay.sprite = panelSprite;
        popupOverlay.type = Image.Type.Sliced;
        popupOverlay.color = overlayColor;

        GameObject popupPanel = CreatePanel("Popup Panel", confirmationPopup.transform, new Vector2(760f, 400f));
        popupTitleText = CreateAnchoredText(popupPanel.transform, "Confirmation Title", "Confirm Action", new Vector2(620f, 60f), new Vector2(0f, 120f), 42f, FontStyles.Bold, whiteText);
        popupDescriptionText = CreateAnchoredText(popupPanel.transform, "Confirmation Description", "Please confirm this action.", new Vector2(680f, 110f), new Vector2(0f, 30f), 24f, FontStyles.Normal, valueTint);

        popupCancelButton = CreateAnchoredButton(popupPanel.transform, "Cancel", new Vector2(250f, 78f), new Vector2(-145f, -115f));
        popupCancelButton.onClick.AddListener(HideConfirmationPopup);

        popupConfirmButton = CreateAnchoredButton(popupPanel.transform, "Confirm", new Vector2(250f, 78f), new Vector2(145f, -115f));
        popupConfirmButton.onClick.AddListener(ConfirmPopupAction);
        popupConfirmButtonText = popupConfirmButton.GetComponentInChildren<TMP_Text>(true);

        ApplyNoDepthMaterials();
        RebuildLayouts();
        canvasRoot.SetActive(false);
    }

    private void ShowConfirmationPopup(string title, string description, string confirmLabel, Action confirmAction)
    {
        pendingConfirmationAction = confirmAction;
        panelVisibleBeforeConfirmation = GetCurrentlyVisiblePrimaryPanel();

        if (panelVisibleBeforeConfirmation != null)
            panelVisibleBeforeConfirmation.SetActive(false);

        if (popupTitleText != null)
            popupTitleText.text = title;

        if (popupDescriptionText != null)
            popupDescriptionText.text = description;

        if (popupConfirmButtonText != null)
            popupConfirmButtonText.text = confirmLabel;

        if (confirmationPopup != null)
            confirmationPopup.SetActive(true);
    }

    private void ShowDefectReview(TubeMeshBuilder.WeldDefect defect)
    {
        ResolveWeldBeadReviewSource();
        if (weldBeadReviewSource == null)
            return;

        weldBeadReviewSource.ShowOnlyDefect(defect);
        bool hasBounds = weldBeadReviewSource.TryGetDefectBounds(defect, out Bounds defectBounds);
        bool hasPathBounds = TryGetReviewPathBounds(out Bounds pathBounds);

        if (reportPanel != null)
            reportPanel.SetActive(false);

        if (reviewPanel != null)
            reviewPanel.SetActive(true);

        SetText(reviewHeadlineText, GetDefectLabel(defect), defect == TubeMeshBuilder.WeldDefect.Normal ? reportPassGreen : reportWarningOrange);
        SetText(
            reviewSummaryText,
            hasBounds
                ? "Only this weld category is shown across the full weld path."
                : "No recorded sections of this category were found in the weld.",
            reportMutedText);

        Bounds reviewBounds = hasPathBounds
            ? pathBounds
            : hasBounds
                ? defectBounds
                : GetFallbackReviewBounds();

        ConfigureReviewCamera(reviewBounds);
    }

    private GameObject GetCurrentlyVisiblePrimaryPanel()
    {
        if (reviewPanel != null && reviewPanel.activeSelf)
            return reviewPanel;

        if (reportPanel != null && reportPanel.activeSelf)
            return reportPanel;

        if (optionsPanel != null && optionsPanel.activeSelf)
            return optionsPanel;

        if (mainPanel != null && mainPanel.activeSelf)
            return mainPanel;

        return null;
    }

    private void CreateVolumeRow(Transform parent)
    {
        GameObject row = CreateSettingRow(parent, "Volume");
        volumeDecreaseButton = CreateFooterButton(row.transform, "-", 110f, buttonColor, whiteText);
        volumeDecreaseButton.onClick.AddListener(() => AdjustVolume(-VolumeStep));

        GameObject valueLabel = CreateUIObject("Volume Value", row.transform, 5);
        RectTransform valueRect = valueLabel.AddComponent<RectTransform>();
        valueRect.sizeDelta = new Vector2(220f, 72f);

        LayoutElement valueLayout = valueLabel.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 220f;
        valueLayout.preferredHeight = 72f;

        Image valueBackground = valueLabel.AddComponent<Image>();
        valueBackground.sprite = panelSprite;
        valueBackground.type = Image.Type.Sliced;
        valueBackground.color = buttonColor;

        Outline valueOutline = valueLabel.AddComponent<Outline>();
        valueOutline.effectColor = outlineColor;
        valueOutline.effectDistance = new Vector2(2f, -2f);
        valueOutline.useGraphicAlpha = false;

        volumeValueText = CreateStretchText(valueLabel, "100%", 30f, FontStyles.Bold);
        volumeValueText.color = valueTint;

        volumeIncreaseButton = CreateFooterButton(row.transform, "+", 110f, buttonColor, whiteText);
        volumeIncreaseButton.onClick.AddListener(() => AdjustVolume(VolumeStep));
    }

    private void CreateTurnRow(Transform parent)
    {
        GameObject row = CreateSettingRow(parent, "Turning");
        turnModeButton = CreateFooterButton(row.transform, "Continuous", 460f, buttonColor, whiteText);
        turnModeButton.onClick.AddListener(ToggleTurnMode);

        turnValueText = turnModeButton.GetComponentInChildren<TMP_Text>(true);
    }

    private GameObject CreateSettingRow(Transform parent, string label)
    {
        GameObject rowContainer = CreateUIObject(label + " Row", parent, 5);
        RectTransform rowRect = rowContainer.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(720f, 154f);

        VerticalLayoutGroup rowLayout = rowContainer.AddComponent<VerticalLayoutGroup>();
        rowLayout.spacing = 14f;
        rowLayout.childAlignment = TextAnchor.UpperCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;

        TMP_Text labelText = CreateText(CreateUIObject(label + " Label", rowContainer.transform, 5), label, 28f, FontStyles.Bold, TextAlignmentOptions.Center);
        labelText.color = whiteText;

        GameObject buttonRow = CreateUIObject(label + " Controls", rowContainer.transform, 5);
        RectTransform buttonRowRect = buttonRow.AddComponent<RectTransform>();
        buttonRowRect.sizeDelta = new Vector2(720f, 82f);

        HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 18f;
        buttonLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonLayout.childControlWidth = false;
        buttonLayout.childControlHeight = false;
        buttonLayout.childForceExpandWidth = false;
        buttonLayout.childForceExpandHeight = false;

        return buttonRow;
    }

    private GameObject CreatePanel(string name, Transform parent, Vector2 size)
    {
        GameObject panel = CreateUIObject(name, parent, 5);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        Image image = panel.AddComponent<Image>();
        image.sprite = panelSprite;
        image.type = Image.Type.Sliced;
        image.color = panelColor;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = false;

        return panel;
    }

    private void CreateTitle(Transform parent, string text, float fontSize)
    {
        GameObject titleObject = CreateUIObject(text + " Title", parent, 5);
        RectTransform rect = titleObject.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(640f, 70f);

        TMP_Text title = CreateText(titleObject, text, fontSize, FontStyles.Bold, TextAlignmentOptions.Center);
        title.color = whiteText;
    }

    private void CreateDescription(Transform parent, string text, float fontSize)
    {
        GameObject descriptionObject = CreateUIObject("Description", parent, 5);
        RectTransform rect = descriptionObject.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(680f, 68f);

        TMP_Text description = CreateText(descriptionObject, text, fontSize, FontStyles.Normal, TextAlignmentOptions.Center);
        description.color = valueTint;
    }

    private void CreateSpacer(Transform parent, float height)
    {
        GameObject spacer = CreateUIObject("Spacer", parent, 5);
        LayoutElement layoutElement = spacer.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
    }

    private Button CreateWideButton(Transform parent, string label, float height)
    {
        return CreateFooterButton(parent, label, 580f, buttonColor, whiteText, height);
    }

    private Button CreateAnchoredButton(Transform parent, string label, Vector2 size, Vector2 anchoredPosition)
    {
        Button button = CreateFooterButton(parent, label, size.x, buttonColor, whiteText, size.y);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return button;
    }

    private Button CreateFooterButton(Transform parent, string label, float width, Color backgroundColor, Color textColor)
    {
        return CreateFooterButton(parent, label, width, backgroundColor, textColor, 78f);
    }

    private Button CreateFooterButton(Transform parent, string label, float width, Color backgroundColor, Color textColor, float height)
    {
        GameObject buttonObject = CreateUIObject(label + " Button", parent, 5);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = height;

        Image background = buttonObject.AddComponent<Image>();
        background.sprite = panelSprite;
        background.type = Image.Type.Sliced;
        background.color = backgroundColor;

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = false;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color32(190, 190, 190, 255);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color32(255, 255, 255, 180);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        GameObject labelObject = CreateUIObject("Label", buttonObject.transform, 5);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        StretchToParent(labelRect);

        TMP_Text text = CreateText(labelObject, label, 30f, FontStyles.Bold, TextAlignmentOptions.Center);
        text.color = textColor;

        AttachOptionalUIAudio(buttonObject);
        return button;
    }

    private TMP_Text CreateAnchoredText(Transform parent, string objectName, string value, Vector2 size, Vector2 anchoredPosition, float fontSize, FontStyles style, Color color)
    {
        GameObject textObject = CreateUIObject(objectName, parent, 5);
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

    private void CreateReportSectionHeader(Transform parent, string label, Vector2 anchoredPosition, Color color)
    {
        TMP_Text header = CreateAnchoredText(
            parent,
            label + " Header",
            label,
            new Vector2(320f, 32f),
            anchoredPosition,
            22f,
            FontStyles.Bold,
            color);
        header.alignment = TextAlignmentOptions.Left;
    }

    private void StyleReportPanel(GameObject panel)
    {
        if (panel == null)
            return;

        Image image = panel.GetComponent<Image>();
        if (image != null)
            image.color = reportPanelColor;

        Outline outline = panel.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = new Color32(102, 184, 255, 255);
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }

    private GameObject CreateReportCard(string name, Transform parent, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject card = CreateUIObject(name, parent, 5);
        RectTransform rect = card.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = card.AddComponent<Image>();
        image.sprite = panelSprite;
        image.type = Image.Type.Sliced;
        image.color = reportCardColor;

        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = reportCardOutlineColor;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = false;

        return card;
    }

    private TMP_Text CreateReportMetricRow(Transform parent, string label, string value, float y)
    {
        TMP_Text labelText = CreateAnchoredText(
            parent,
            label + " Label",
            label,
            new Vector2(170f, 28f),
            new Vector2(-100f, y),
            20f,
            FontStyles.Normal,
            whiteText);
        labelText.alignment = TextAlignmentOptions.Left;

        TMP_Text valueText = CreateAnchoredText(
            parent,
            label + " Value",
            value,
            new Vector2(170f, 28f),
            new Vector2(100f, y),
            20f,
            FontStyles.Bold,
            whiteText);
        valueText.alignment = TextAlignmentOptions.Right;

        CreateReportDivider(parent, y - 21f, 350f);
        return valueText;
    }

    private void CreateParameterTable(Transform parent)
    {
        CreateParameterHeaderCell(parent, "PARAMETER", new Vector2(-205f, 40f), 170f, TextAlignmentOptions.Left);
        CreateParameterHeaderCell(parent, "RESULT", new Vector2(-28f, 40f), 120f, TextAlignmentOptions.Center);
        CreateParameterHeaderCell(parent, "TARGET", new Vector2(130f, 40f), 155f, TextAlignmentOptions.Center);
        CreateParameterHeaderCell(parent, "STATUS", new Vector2(250f, 40f), 90f, TextAlignmentOptions.Center);
        CreateReportDivider(parent, 22f, 580f);

        string[] labels = { "Travel speed", "Work angle", "Travel angle", "CTWD" };
        for (int i = 0; i < labels.Length; i++)
        {
            float y = 2f - i * 32f;
            TMP_Text label = CreateAnchoredText(parent, labels[i] + " Parameter Label", labels[i], new Vector2(170f, 28f), new Vector2(-205f, y), 19f, FontStyles.Normal, whiteText);
            label.alignment = TextAlignmentOptions.Left;

            reportParameterResultTexts[i] = CreateAnchoredText(parent, labels[i] + " Result", "--", new Vector2(120f, 28f), new Vector2(-28f, y), 19f, FontStyles.Normal, whiteText);
            reportParameterTargetTexts[i] = CreateAnchoredText(parent, labels[i] + " Target", "--", new Vector2(155f, 28f), new Vector2(130f, y), 19f, FontStyles.Normal, whiteText);
            reportParameterStatusTexts[i] = CreateAnchoredText(parent, labels[i] + " Status", "--", new Vector2(90f, 28f), new Vector2(250f, y), 19f, FontStyles.Bold, reportMutedText);

            if (i < labels.Length - 1)
                CreateReportDivider(parent, y - 17f, 580f);
        }
    }

    private void CreateParameterHeaderCell(Transform parent, string text, Vector2 position, float width, TextAlignmentOptions alignment)
    {
        TMP_Text header = CreateAnchoredText(parent, text + " Header", text, new Vector2(width, 26f), position, 17f, FontStyles.Bold, reportAccentBlue);
        header.alignment = alignment;
    }

    private void CreateIssueRow(Transform parent, int index, float y)
    {
        GameObject row = CreateUIObject("Issue Row " + (index + 1), parent, 5);
        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(390f, 54f);
        rowRect.anchoredPosition = new Vector2(0f, y);
        reportIssueRows[index] = row;

        TMP_Text rank = CreateAnchoredText(row.transform, "Rank", (index + 1).ToString(), new Vector2(34f, 34f), new Vector2(-174f, 10f), 20f, FontStyles.Bold, reportWarningOrange);
        rank.alignment = TextAlignmentOptions.Center;

        reportIssueLabelTexts[index] = CreateAnchoredText(row.transform, "Issue Label", "--", new Vector2(230f, 24f), new Vector2(-38f, 10f), 18f, FontStyles.Normal, whiteText);
        reportIssueLabelTexts[index].alignment = TextAlignmentOptions.Left;
        reportIssuePercentTexts[index] = CreateAnchoredText(row.transform, "Issue Percent", "--", new Vector2(72f, 24f), new Vector2(154f, 10f), 18f, FontStyles.Bold, reportWarningOrange);
        reportIssuePercentTexts[index].alignment = TextAlignmentOptions.Right;
        reportIssueFillImages[index] = CreateProgressBar(row.transform, "Issue Bar", new Vector2(320f, 8f), new Vector2(18f, -16f), reportWarningOrange);
    }

    private void CreateQualityRows(Transform parent)
    {
        string[] labels = { "Normal", "Overlap", "Lack of Fusion", "Porosity", "Spatter" };
        Color[] fills = { reportAccentBlue, reportWarningOrange, new Color32(247, 187, 52, 255), new Color32(122, 175, 214, 255), new Color32(160, 76, 220, 255) };
        TubeMeshBuilder.WeldDefect[] defects =
        {
            TubeMeshBuilder.WeldDefect.Normal,
            TubeMeshBuilder.WeldDefect.Overlap,
            TubeMeshBuilder.WeldDefect.LackOfFusion,
            TubeMeshBuilder.WeldDefect.Porosity,
            TubeMeshBuilder.WeldDefect.Spatter
        };

        for (int i = 0; i < labels.Length; i++)
        {
            float y = 50f - i * 38f;
            GameObject row = CreateUIObject(labels[i] + " Quality Row", parent, 5);
            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(500f, 30f);
            rowRect.anchoredPosition = new Vector2(0f, y);

            Image rowImage = row.AddComponent<Image>();
            rowImage.color = new Color(0f, 0f, 0f, 0f);

            Button rowButton = row.AddComponent<Button>();
            rowButton.targetGraphic = rowImage;
            ColorBlock rowColors = rowButton.colors;
            rowColors.normalColor = Color.white;
            rowColors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            rowColors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            rowColors.selectedColor = Color.white;
            rowButton.colors = rowColors;
            AttachOptionalUIAudio(row);
            TubeMeshBuilder.WeldDefect capturedDefect = defects[i];
            rowButton.onClick.AddListener(() => ShowDefectReview(capturedDefect));
            reportQualityButtons[i] = rowButton;

            TMP_Text label = CreateAnchoredText(row.transform, labels[i] + " Quality Label", labels[i], new Vector2(160f, 24f), new Vector2(-178f, 0f), 18f, FontStyles.Normal, whiteText);
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;
            reportQualityFillImages[i] = CreateProgressBar(row.transform, labels[i] + " Quality Bar", new Vector2(260f, 16f), new Vector2(25f, 0f), fills[i]);
            reportQualityFillImages[i].raycastTarget = false;
            reportQualityPercentTexts[i] = CreateAnchoredText(row.transform, labels[i] + " Quality Percent", "--", new Vector2(70f, 24f), new Vector2(220f, 0f), 18f, FontStyles.Bold, whiteText);
            reportQualityPercentTexts[i].alignment = TextAlignmentOptions.Right;
            reportQualityPercentTexts[i].raycastTarget = false;
        }
    }

    private void CreateReviewImageFrame(Transform parent)
    {
        GameObject frame = CreateReportCard("Review Camera Card", parent, new Vector2(930f, 520f), new Vector2(0f, -10f));
        GameObject imageObject = CreateUIObject("Review Image", frame.transform, 5);
        RectTransform imageRect = imageObject.AddComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.sizeDelta = new Vector2(900f, 490f);
        reviewImage = imageObject.AddComponent<RawImage>();
        reviewImage.color = Color.white;
    }

    private Image CreateProgressBar(Transform parent, string name, Vector2 size, Vector2 anchoredPosition, Color fillColor)
    {
        GameObject track = CreateUIObject(name + " Track", parent, 5);
        RectTransform trackRect = track.AddComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0.5f, 0.5f);
        trackRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackRect.pivot = new Vector2(0.5f, 0.5f);
        trackRect.sizeDelta = size;
        trackRect.anchoredPosition = anchoredPosition;

        Image trackImage = track.AddComponent<Image>();
        trackImage.sprite = panelSprite;
        trackImage.type = Image.Type.Sliced;
        trackImage.color = reportTrackColor;
        trackImage.raycastTarget = false;

        GameObject fill = CreateUIObject(name + " Fill", track.transform, 5);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.sizeDelta = new Vector2(size.x, 0f);

        Image fillImage = fill.AddComponent<Image>();
        fillImage.sprite = panelSprite;
        fillImage.type = Image.Type.Sliced;
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;
        return fillImage;
    }

    private void SetProgressBar(Image fillImage, float percent)
    {
        if (fillImage == null)
            return;

        RectTransform rect = fillImage.rectTransform;
        Vector2 size = rect.sizeDelta;
        size.x = Mathf.Clamp01(percent / 100f) * ((RectTransform)rect.parent).sizeDelta.x;
        rect.sizeDelta = size;
    }

    private void CreateReportDivider(Transform parent, float y, float width)
    {
        GameObject divider = CreateUIObject("Divider", parent, 5);
        RectTransform rect = divider.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, 2f);
        rect.anchoredPosition = new Vector2(0f, y);

        Image image = divider.AddComponent<Image>();
        image.color = reportDividerColor;
    }

    private void CreateValueDisplay(Transform parent, string name, Vector2 size, Vector2 anchoredPosition, out TMP_Text text)
    {
        GameObject valueObject = CreateUIObject(name, parent, 5);
        RectTransform rect = valueObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image background = valueObject.AddComponent<Image>();
        background.sprite = panelSprite;
        background.type = Image.Type.Sliced;
        background.color = buttonColor;

        Outline outline = valueObject.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = false;

        GameObject labelObject = CreateUIObject("Label", valueObject.transform, 5);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        StretchToParent(labelRect);

        text = CreateText(labelObject, "100%", 30f, FontStyles.Bold, TextAlignmentOptions.Center);
        text.color = valueTint;
    }

    private TMP_Text CreateStretchText(GameObject owner, string value, float fontSize, FontStyles style)
    {
        RectTransform rect = owner.GetComponent<RectTransform>();
        if (rect == null)
            rect = owner.AddComponent<RectTransform>();

        TMP_Text text = CreateText(owner, value, fontSize, style, TextAlignmentOptions.Center);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return text;
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

    private void AttachOptionalUIAudio(GameObject target)
    {
        if (target == null || target.GetComponent<UIAudio>() != null)
            return;

        UIAudio uiAudio = target.AddComponent<UIAudio>();
        uiAudio.clickAudioName = DefaultClickAudioName;
        uiAudio.hoverEnterAudioName = DefaultHoverEnterAudioName;
        uiAudio.hoverExitAudioName = string.Empty;
    }

    private void UpdateMenuAnchor()
    {
        if (canvasRoot == null || headTransform == null)
            return;

        Transform canvasTransform = canvasRoot.transform;
        if (canvasTransform.parent != headTransform)
            canvasTransform.SetParent(headTransform, false);

        canvasTransform.localPosition = new Vector3(0f, MenuVerticalOffset, MenuDistance);
        canvasTransform.localRotation = Quaternion.identity;
    }

    private void FindHeadTransform()
    {
        if (Camera.main != null)
        {
            headTransform = Camera.main.transform;
            return;
        }

        Camera anyCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (anyCamera != null)
            headTransform = anyCamera.transform;
    }

    private void RefreshCanvasCamera()
    {
        if (pauseCanvas == null)
            return;

        if (pauseCanvas.worldCamera == null && Camera.main != null)
            pauseCanvas.worldCamera = Camera.main;
    }

    private bool IsPauseButtonPressed()
    {
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHand.isValid && leftHand.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondaryPressed))
            return secondaryPressed;

        List<InputDevice> leftHandDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand,
            leftHandDevices);

        for (int i = 0; i < leftHandDevices.Count; i++)
        {
            if (leftHandDevices[i].TryGetFeatureValue(CommonUsages.secondaryButton, out bool pressed))
                return pressed;
        }

        return false;
    }

    private void StoreAndDisableLocomotionProviders()
    {
        storedLocomotionStates.Clear();

        foreach (LocomotionProvider provider in FindSceneComponents<LocomotionProvider>())
        {
            if (provider == null)
                continue;

            storedLocomotionStates[provider] = provider.enabled;
            provider.enabled = false;
        }
    }

    private void RestoreLocomotionProviders()
    {
        foreach (KeyValuePair<Behaviour, bool> pair in storedLocomotionStates)
        {
            if (pair.Key != null)
                pair.Key.enabled = pair.Value;
        }

        storedLocomotionStates.Clear();
        SetTurnTypeFromPlayerPref.ApplyPlayerPrefToCurrentScene();
        SimulatorSettings.ApplyVolume();
    }

    private static T[] FindSceneComponents<T>() where T : Component
    {
        T[] allComponents = Resources.FindObjectsOfTypeAll<T>();
        int validCount = 0;

        for (int i = 0; i < allComponents.Length; i++)
        {
            T component = allComponents[i];
            if (component == null || !component.gameObject.scene.IsValid())
                continue;

            validCount++;
        }

        if (validCount == allComponents.Length)
            return allComponents;

        T[] validComponents = new T[validCount];
        int index = 0;

        for (int i = 0; i < allComponents.Length; i++)
        {
            T component = allComponents[i];
            if (component == null || !component.gameObject.scene.IsValid())
                continue;

            validComponents[index] = component;
            index++;
        }

        return validComponents;
    }

    private GameObject CreateUIObject(string name, Transform parent, int layer)
    {
        GameObject go = new GameObject(name);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    private void RebuildLayouts()
    {
        Canvas.ForceUpdateCanvases();

        if (mainPanel != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(mainPanel.GetComponent<RectTransform>());

        if (optionsPanel != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(optionsPanel.GetComponent<RectTransform>());

        if (reportPanel != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(reportPanel.GetComponent<RectTransform>());

        if (confirmationPopup != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(confirmationPopup.GetComponent<RectTransform>());
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
            name = "Runtime Pause Menu UI No Depth"
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
            name = sourceMaterial.name + " Pause Menu No Depth"
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

    private void ResolveReportTracker()
    {
        if (reportTracker != null)
            return;

        reportTracker = UnityEngine.Object.FindFirstObjectByType<WeldReportTracker>();
    }

    private void ResolveWeldBeadReviewSource()
    {
        if (weldBeadReviewSource != null)
            return;

        weldBeadReviewSource = UnityEngine.Object.FindFirstObjectByType<WeldBead>();
    }

    private void ResolveWeldingHud()
    {
        if (weldingHudPanel != null)
            return;

        weldingHudPanel = UnityEngine.Object.FindFirstObjectByType<WeldingHudPanel>(FindObjectsInactive.Include);
    }

    private void HideWeldingHud()
    {
        ResolveWeldingHud();
        if (weldingHudPanel == null)
            return;

        hudWasActiveBeforeMenu = weldingHudPanel.gameObject.activeSelf;
        weldingHudPanel.gameObject.SetActive(false);
    }

    private void RestoreWeldingHud()
    {
        if (weldingHudPanel != null)
            weldingHudPanel.gameObject.SetActive(hudWasActiveBeforeMenu);
    }

    private void RestoreAllBeadVisibility()
    {
        ResolveWeldBeadReviewSource();
        weldBeadReviewSource?.ShowAllDefects();
    }

    private void ConfigureReviewCamera(Bounds targetBounds)
    {
        EnsureReviewCamera();
        if (reviewCamera == null)
            return;

        Vector3 center = targetBounds.center;
        Vector3 viewDirection = GetReviewViewDirection();
        float radius = Mathf.Max(targetBounds.extents.magnitude, 0.08f);
        Vector3 cameraPosition = center + viewDirection * Mathf.Max(radius * 4f, 0.28f);

        reviewCamera.transform.position = cameraPosition;
        reviewCamera.transform.rotation = Quaternion.LookRotation(center - cameraPosition, Vector3.up);
        reviewCamera.orthographic = true;
        reviewCamera.orthographicSize = CalculateReviewOrthographicSize(targetBounds);
        reviewCamera.nearClipPlane = 0.01f;
        reviewCamera.farClipPlane = 10f;
        reviewCamera.Render();
    }

    private bool TryGetReviewPathBounds(out Bounds bounds)
    {
        ResolveWeldBeadReviewSource();
        WeldPath weldPath = weldBeadReviewSource != null ? weldBeadReviewSource.weldPath : null;
        if (weldPath == null || weldPath.waypoints == null || weldPath.waypoints.Count == 0)
        {
            bounds = default;
            return false;
        }

        bool hasBounds = false;
        bounds = default;
        for (int i = 0; i < weldPath.waypoints.Count; i++)
        {
            Transform waypoint = weldPath.waypoints[i];
            if (waypoint == null)
                continue;

            if (!hasBounds)
            {
                bounds = new Bounds(waypoint.position, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(waypoint.position);
            }
        }

        if (!hasBounds)
            return false;

        // Keep the entire seam visible, but avoid wasting most of the frame on empty plate area.
        bounds.Expand(0.06f);
        return true;
    }

    private Vector3 GetReviewViewDirection()
    {
        ResolveWeldBeadReviewSource();
        WeldPath weldPath = weldBeadReviewSource != null ? weldBeadReviewSource.weldPath : null;
        WeldableJoint activeJoint = weldPath != null ? weldPath.GetComponentInParent<WeldableJoint>() : null;
        if (activeJoint != null)
        {
            activeJoint.ResolveWorkAngleReferences();

            Vector3 viewDirection = Vector3.zero;
            if (activeJoint.workAnglePlateA != null)
                viewDirection += activeJoint.workAnglePlateA.forward;
            if (activeJoint.workAnglePlateB != null)
                viewDirection += activeJoint.workAnglePlateB.forward;

            if (viewDirection.sqrMagnitude > 1e-8f)
                return viewDirection.normalized;
        }

        Vector3 fallback = headTransform != null
            ? -Vector3.ProjectOnPlane(headTransform.forward, Vector3.up)
            : Vector3.back;
        if (fallback.sqrMagnitude < 1e-8f)
            fallback = Vector3.back;
        return fallback.normalized;
    }

    private float CalculateReviewOrthographicSize(Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        Vector3[] corners =
        {
            center + new Vector3(-extents.x, -extents.y, -extents.z),
            center + new Vector3(-extents.x, -extents.y,  extents.z),
            center + new Vector3(-extents.x,  extents.y, -extents.z),
            center + new Vector3(-extents.x,  extents.y,  extents.z),
            center + new Vector3( extents.x, -extents.y, -extents.z),
            center + new Vector3( extents.x, -extents.y,  extents.z),
            center + new Vector3( extents.x,  extents.y, -extents.z),
            center + new Vector3( extents.x,  extents.y,  extents.z)
        };

        float maxAbsX = 0f;
        float maxAbsY = 0f;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 local = reviewCamera.transform.InverseTransformPoint(corners[i]);
            maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(local.x));
            maxAbsY = Mathf.Max(maxAbsY, Mathf.Abs(local.y));
        }

        float aspect = reviewRenderTexture != null && reviewRenderTexture.height > 0
            ? (float)reviewRenderTexture.width / reviewRenderTexture.height
            : 2f;

        return Mathf.Max(Mathf.Max(maxAbsY, maxAbsX / Mathf.Max(aspect, 0.01f)) * 1.12f, 0.08f);
    }

    private void EnsureReviewCamera()
    {
        if (reviewCamera == null)
        {
            GameObject cameraObject = new GameObject("Weld Review Camera");
            reviewCamera = cameraObject.AddComponent<Camera>();
            reviewCamera.enabled = false;
            reviewCamera.clearFlags = CameraClearFlags.Skybox;
            reviewCamera.cullingMask = ~(1 << 5);
        }

        if (reviewRenderTexture == null)
        {
            reviewRenderTexture = new RenderTexture(2048, 1024, 16)
            {
                name = "Weld Review Render Texture"
            };
        }

        reviewCamera.targetTexture = reviewRenderTexture;
        if (reviewImage != null)
            reviewImage.texture = reviewRenderTexture;
    }

    private Bounds GetFallbackReviewBounds()
    {
        ResolveWeldBeadReviewSource();

        if (weldBeadReviewSource != null && weldBeadReviewSource.weldPath != null &&
            weldBeadReviewSource.weldPath.waypoints != null &&
            weldBeadReviewSource.weldPath.waypoints.Count > 0)
        {
            bool hasBounds = false;
            Bounds bounds = default;
            for (int i = 0; i < weldBeadReviewSource.weldPath.waypoints.Count; i++)
            {
                Transform waypoint = weldBeadReviewSource.weldPath.waypoints[i];
                if (waypoint == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = new Bounds(waypoint.position, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(waypoint.position);
                }
            }

            if (hasBounds)
            {
                bounds.Expand(0.08f);
                return bounds;
            }
        }

        Vector3 fallbackCenter = headTransform != null
            ? headTransform.position + headTransform.forward * 0.6f
            : Vector3.zero;
        return new Bounds(fallbackCenter, Vector3.one * 0.12f);
    }

    private static string GetDefectLabel(TubeMeshBuilder.WeldDefect defect)
    {
        return defect switch
        {
            TubeMeshBuilder.WeldDefect.Normal => "Normal Weld",
            TubeMeshBuilder.WeldDefect.Overlap => "Overlap",
            TubeMeshBuilder.WeldDefect.LackOfFusion => "Lack of Fusion",
            TubeMeshBuilder.WeldDefect.Porosity => "Porosity",
            TubeMeshBuilder.WeldDefect.Spatter => "Spatter",
            _ => defect.ToString()
        };
    }

    private void PopulateReport()
    {
        if (reportTracker == null)
        {
            if (reportHeadlineText != null)
                reportHeadlineText.text = "No report tracker found";
            if (reportSummaryText != null)
                reportSummaryText.text = "The scene does not currently contain a weld report tracker.";
            PopulateEmptyReport();
            return;
        }

        if (reportHeadlineText != null)
            reportHeadlineText.text = reportTracker.BuildHeadlineText();
        if (reportSummaryText != null)
            reportSummaryText.text = reportTracker.BuildSummaryText();

        if (!reportTracker.HasSamples || reportTracker.TotalWeldTime <= 0f)
        {
            PopulateEmptyReport();
            return;
        }

        PopulateSessionSummary();
        PopulateParameterRows();
        PopulatePriorityCorrections();
        PopulateQualityBreakdown();
    }

    private void PopulateEmptyReport()
    {
        SetText(reportJointValueText, "--", whiteText);
        SetText(reportTimeValueText, "--", whiteText);
        SetText(reportMainDefectValueText, "--", reportMutedText);
        SetText(reportNormalWeldValueText, "--", whiteText);

        for (int i = 0; i < reportParameterResultTexts.Length; i++)
        {
            SetText(reportParameterResultTexts[i], "--", whiteText);
            SetText(reportParameterTargetTexts[i], "--", whiteText);
            SetText(reportParameterStatusTexts[i], "--", reportMutedText);
        }

        for (int i = 0; i < reportIssueRows.Length; i++)
        {
            if (reportIssueRows[i] != null)
                reportIssueRows[i].SetActive(i == 0);

            SetText(reportIssueLabelTexts[i], i == 0 ? "No issue data available" : string.Empty, whiteText);
            SetText(reportIssuePercentTexts[i], i == 0 ? "--" : string.Empty, reportWarningOrange);
            SetProgressBar(reportIssueFillImages[i], 0f);
        }

        for (int i = 0; i < reportQualityPercentTexts.Length; i++)
        {
            SetText(reportQualityPercentTexts[i], "--", whiteText);
            SetProgressBar(reportQualityFillImages[i], 0f);
        }
    }

    private void PopulateSessionSummary()
    {
        SetText(reportJointValueText, WeldJointCatalog.GetLabel(reportTracker.TrackedJointType), whiteText);
        SetText(reportTimeValueText, reportTracker.TotalWeldTime.ToString("0.0") + " s", whiteText);

        TubeMeshBuilder.WeldDefect defect = reportTracker.DominantDefect;
        Color defectColor = defect == TubeMeshBuilder.WeldDefect.Normal
            ? reportPassGreen
            : reportWarningOrange;
        SetText(reportMainDefectValueText, reportTracker.GetDominantDefectLabel(), defectColor);
        SetText(
            reportNormalWeldValueText,
            reportTracker.GetDefectPercent(TubeMeshBuilder.WeldDefect.Normal).ToString("0") + "%",
            whiteText);
    }

    private void PopulateParameterRows()
    {
        WeldQualityProfile profile = reportTracker.TrackedProfile;

        SetParameterRow(
            0,
            reportTracker.AverageSpeedMmPerSec.ToString("0.0") + " mm/s",
            FormatRange(profile.NormalSpeedMin, profile.NormalSpeedMax) + " mm/s",
            GetReportParameterStatus(
                reportTracker.AverageSpeedMmPerSec,
                profile.NormalSpeedMin,
                profile.NormalSpeedMax,
                ReportNearSpeedMargin));
        SetParameterRow(
            1,
            reportTracker.AverageWorkAngleDeg.ToString("0.0") + " deg",
            FormatRange(profile.NormalWorkAngleMin, profile.NormalWorkAngleMax) + " deg",
            GetReportParameterStatus(
                reportTracker.AverageWorkAngleDeg,
                profile.NormalWorkAngleMin,
                profile.NormalWorkAngleMax,
                ReportNearWorkAngleMargin));
        SetParameterRow(
            2,
            reportTracker.AverageTravelAngleDeg.ToString("0.0") + " deg",
            FormatRange(profile.NormalTravelAngleMin, profile.NormalTravelAngleMax) + " deg",
            GetReportParameterStatus(
                reportTracker.AverageTravelAngleDeg,
                profile.NormalTravelAngleMin,
                profile.NormalTravelAngleMax,
                ReportNearTravelAngleMargin));
        SetParameterRow(
            3,
            reportTracker.AverageCtwdMm.ToString("0.0") + " mm",
            FormatRange(profile.NormalCtwdMin, profile.NormalCtwdMax) + " mm",
            GetReportParameterStatus(
                reportTracker.AverageCtwdMm,
                profile.NormalCtwdMin,
                profile.NormalCtwdMax,
                ReportNearCtwdMargin));
    }

    private void PopulatePriorityCorrections()
    {
        List<WeldReportTracker.ReportIssueSummary> issues = reportTracker.GetPriorityIssues(reportIssueRows.Length);
        if (issues.Count == 0)
        {
            if (reportIssueRows[0] != null)
                reportIssueRows[0].SetActive(true);

            SetText(reportIssueLabelTexts[0], "All tracked parameters in range", reportPassGreen);
            SetText(reportIssuePercentTexts[0], "OK", reportPassGreen);
            SetProgressBar(reportIssueFillImages[0], 100f);

            for (int i = 1; i < reportIssueRows.Length; i++)
            {
                if (reportIssueRows[i] != null)
                    reportIssueRows[i].SetActive(false);
            }

            return;
        }

        for (int i = 0; i < reportIssueRows.Length; i++)
        {
            bool hasIssue = i < issues.Count;
            if (reportIssueRows[i] != null)
                reportIssueRows[i].SetActive(hasIssue);

            if (!hasIssue)
                continue;

            SetText(reportIssueLabelTexts[i], issues[i].Label, whiteText);
            SetText(reportIssuePercentTexts[i], issues[i].Percent.ToString("0") + "%", reportWarningOrange);
            SetProgressBar(reportIssueFillImages[i], issues[i].Percent);
        }
    }

    private void PopulateQualityBreakdown()
    {
        TubeMeshBuilder.WeldDefect[] defects =
        {
            TubeMeshBuilder.WeldDefect.Normal,
            TubeMeshBuilder.WeldDefect.Overlap,
            TubeMeshBuilder.WeldDefect.LackOfFusion,
            TubeMeshBuilder.WeldDefect.Porosity,
            TubeMeshBuilder.WeldDefect.Spatter
        };

        for (int i = 0; i < defects.Length; i++)
        {
            float percent = reportTracker.GetDefectPercent(defects[i]);
            SetText(reportQualityPercentTexts[i], percent.ToString("0") + "%", whiteText);
            SetProgressBar(reportQualityFillImages[i], percent);
        }
    }

    private void SetParameterRow(int index, string result, string target, ReportParameterStatus status)
    {
        if (index < 0 || index >= reportParameterResultTexts.Length)
            return;

        SetText(reportParameterResultTexts[index], result, whiteText);
        SetText(reportParameterTargetTexts[index], target, whiteText);
        switch (status)
        {
            case ReportParameterStatus.Great:
                SetText(reportParameterStatusTexts[index], "GREAT", reportPassGreen);
                break;
            case ReportParameterStatus.Ok:
                SetText(reportParameterStatusTexts[index], "OK", reportWarningOrange);
                break;
            default:
                SetText(reportParameterStatusTexts[index], "BAD", reportFailRed);
                break;
        }
    }

    private static string FormatRange(float min, float max)
    {
        return min.ToString("0.0") + "-" + max.ToString("0.0");
    }

    private static ReportParameterStatus GetReportParameterStatus(
        float value,
        float strictMin,
        float strictMax,
        float nearMargin)
    {
        if (value >= strictMin && value <= strictMax)
            return ReportParameterStatus.Great;

        if (value >= strictMin - nearMargin && value <= strictMax + nearMargin)
            return ReportParameterStatus.Ok;

        return ReportParameterStatus.Bad;
    }

    private static void SetText(TMP_Text target, string text, Color color)
    {
        if (target == null)
            return;

        target.text = text;
        target.color = color;
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
}
