using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameStartMenu : MonoBehaviour
{
    private const string MainSceneName = "Main VR Scene";
    private const float VolumeStep = 0.05f;

    [Header("UI Pages")]
    public GameObject mainMenu;
    public GameObject options;
    public GameObject about;

    [Header("Main Menu Buttons")]
    public Button startButton;
    public Button optionButton;
    public Button aboutButton;
    public Button quitButton;

    public List<Button> returnButtons;

    private readonly Dictionary<WeldJointType, Button> jointButtons = new Dictionary<WeldJointType, Button>();
    private readonly Dictionary<WeldJointType, Image> jointButtonBackgrounds = new Dictionary<WeldJointType, Image>();
    private readonly Dictionary<WeldJointType, Outline> jointButtonOutlines = new Dictionary<WeldJointType, Outline>();
    private readonly Dictionary<WeldJointType, RawImage> jointPreviewImages = new Dictionary<WeldJointType, RawImage>();
    private readonly Dictionary<WeldJointType, TMP_Text> jointButtonLabels = new Dictionary<WeldJointType, TMP_Text>();

    private GameObject jointSelectionMenu;
    private GameObject customOptionsRoot;
    private GameObject customAboutRoot;
    private GameObject startMenuPanel;
    private GameObject quitConfirmationPopup;
    private GameObject pageVisibleBeforeQuitConfirmation;
    private Button confirmSelectionButton;
    private Button startOptionsVolumeDecreaseButton;
    private Button startOptionsVolumeIncreaseButton;
    private Button startOptionsTableHeightDecreaseButton;
    private Button startOptionsTableHeightIncreaseButton;
    private Button startOptionsTurnModeButton;
    private Button startOptionsConfirmButton;
    private Toggle mainMenuDisableTutorialToggle;
    private Toggle startOptionsDisableTutorialToggle;
    private TMP_Text startOptionsVolumeValueText;
    private TMP_Text startOptionsTableHeightValueText;
    private TMP_Text startOptionsTurnValueText;
    private WeldJointType? selectedJointType;
    private Sprite uiSprite;

    private readonly Color defaultCardColor = new Color32(0, 0, 0, 255);
    private readonly Color highlightedCardColor = new Color32(0, 0, 0, 255);
    private readonly Color defaultOutlineColor = new Color32(110, 110, 110, 255);
    private readonly Color selectedOutlineColor = new Color32(255, 255, 255, 255);
    private readonly Color labelColor = new Color32(255, 255, 255, 255);
    private readonly Color confirmEnabledColor = new Color32(0, 0, 0, 255);
    private readonly Color confirmDisabledColor = new Color32(0, 0, 0, 210);
    private const string DefaultClickAudioName = "uiclick";
    private const string DefaultHoverEnterAudioName = "uihoverenter";
    private const float DefaultOutlineThickness = 2f;
    private const float SelectedJointOutlineThickness = 3f;
    private Texture2D generatedSpriteTexture;

    private void Start()
    {
        uiSprite = LoadDefaultSprite();
        CreateJointSelectionMenu();
        BuildRuntimeOptionsPage();
        BuildRuntimeAboutPage();
        EnsureMainMenuPanel();
        BuildQuitConfirmationPopup();
        SimulatorSettings.ApplyVolume();
        SetTurnTypeFromPlayerPref.ApplyPlayerPrefToCurrentScene();
        ApplyExistingMenuTheme();
        UpdateExistingMenuLabels();

        if (startButton != null)
            startButton.onClick.AddListener(OpenJointSelectionMenu);

        if (optionButton != null)
            optionButton.onClick.AddListener(EnableOption);

        if (aboutButton != null)
            aboutButton.onClick.AddListener(EnableAbout);

        if (quitButton != null)
            quitButton.onClick.AddListener(ShowQuitConfirmationPopup);

        if (returnButtons != null)
        {
            foreach (Button item in returnButtons)
            {
                if (item != null)
                    item.onClick.AddListener(EnableMainMenu);
            }
        }

        EnableMainMenu();
    }

    private void OnDestroy()
    {
        if (generatedSpriteTexture != null)
            Destroy(generatedSpriteTexture);
    }

    private void ApplyExistingMenuTheme()
    {
        EnsureMainMenuPanel();
        ApplyThemeToPageButtons(mainMenu);
        ApplyThemeToPageButtons(options);
        ApplyThemeToPageButtons(about);
    }

    private void UpdateExistingMenuLabels()
    {
        SetPrimaryButtonLabel(options, "Confirm");
        SetPrimaryButtonLabel(about, "Back");
    }

    private void ApplyThemeToPageButtons(GameObject page)
    {
        if (page == null)
            return;

        foreach (Button button in page.GetComponentsInChildren<Button>(true))
            ApplyBlackAndWhiteTheme(button);
    }

    private void ApplyBlackAndWhiteTheme(Button button)
    {
        if (button == null)
            return;

        Image background = button.targetGraphic as Image;
        if (background == null)
            background = button.GetComponent<Image>();

        if (background != null)
        {
            background.sprite = uiSprite;
            background.type = Image.Type.Sliced;
            background.color = defaultCardColor;
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();

        outline.effectColor = selectedOutlineColor;
        outline.effectDistance = new Vector2(DefaultOutlineThickness, -DefaultOutlineThickness);
        outline.useGraphicAlpha = false;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color32(210, 210, 210, 255);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color32(255, 255, 255, 180);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
            text.color = labelColor;

        foreach (Text text in button.GetComponentsInChildren<Text>(true))
            text.color = Color.white;
    }

    private void SetPrimaryButtonLabel(GameObject page, string label)
    {
        if (page == null)
            return;

        Button[] buttons = page.GetComponentsInChildren<Button>(true);
        if (buttons.Length == 0)
            return;

        SetButtonLabel(buttons[0], label);
    }

    private void SetButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null)
        {
            tmpText.text = label;
            return;
        }

        Text legacyText = button.GetComponentInChildren<Text>(true);
        if (legacyText != null)
            legacyText.text = label;
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void StartGame()
    {
        OpenJointSelectionMenu();
    }

    public void HideAll()
    {
        SetPageVisible(mainMenu, false);
        SetPageVisible(options, false);
        SetPageVisible(about, false);
        SetPageVisible(jointSelectionMenu, false);
        SetPageVisible(quitConfirmationPopup, false);
    }

    public void EnableMainMenu()
    {
        HideAll();
        SetPageVisible(mainMenu, true);
    }

    public void EnableOption()
    {
        HideAll();
        SetPageVisible(options, true);
        RefreshStartOptionsDisplay();
    }

    public void EnableAbout()
    {
        HideAll();
        SetPageVisible(about, true);
    }

    private void BuildRuntimeAboutPage()
    {
        if (about == null || customAboutRoot != null)
            return;

        for (int i = 0; i < about.transform.childCount; i++)
            about.transform.GetChild(i).gameObject.SetActive(false);

        customAboutRoot = CreateUIObject("Runtime About UI", about.transform, about.layer);
        RectTransform rootRect = customAboutRoot.AddComponent<RectTransform>();
        StretchToParent(rootRect);

        GameObject panel = CreatePanel("About Panel", customAboutRoot.transform, new Vector2(1120f, 980f));
        CreateAnchoredText(
            panel.transform,
            "About Title",
            "NEXT WELDER",
            new Vector2(980f, 112f),
            new Vector2(0f, 370f),
            52f,
            FontStyles.Bold,
            labelColor);

        CreateAnchoredText(
            panel.transform,
            "About Slogan",
            "Train smarter. Weld better.",
            new Vector2(720f, 54f),
            new Vector2(0f, 304f),
            28f,
            FontStyles.Italic,
            new Color32(255, 177, 92, 255));

        CreateAnchoredText(
            panel.transform,
            "About Description",
            "A VR-based welding training simulator that helps users practice in a safe, repeatable environment while tracking key welding parameters in real time. " +
            "As the user welds, the system detects quality defects based on their technique and produces a final report showing what went wrong and where improvement is needed.",
            new Vector2(950f, 190f),
            new Vector2(0f, 135f),
            28f,
            FontStyles.Normal,
            new Color32(226, 226, 226, 255));

        CreateAnchoredText(
            panel.transform,
            "Made By Label",
            "Made by",
            new Vector2(420f, 44f),
            new Vector2(0f, -55f),
            28f,
            FontStyles.Bold,
            labelColor);

        CreateAnchoredText(
            panel.transform,
            "Student Names",
            "Eng. Mohammad Al-Mubarak  |  Eng. Mubarak Alkhaldi  |  Eng. Abdulaziz Al-Enazy",
            new Vector2(960f, 56f),
            new Vector2(0f, -108f),
            26f,
            FontStyles.Normal,
            new Color32(226, 226, 226, 255));

        CreateAnchoredText(
            panel.transform,
            "Supervised By Label",
            "Supervised by",
            new Vector2(320f, 44f),
            new Vector2(0f, -195f),
            28f,
            FontStyles.Bold,
            labelColor);

        CreateAnchoredText(
            panel.transform,
            "Supervisor Names",
            "Dr. Tanvir Sayeed  |  Dr. Mohammed Abdul Hannan",
            new Vector2(900f, 56f),
            new Vector2(0f, -248f),
            26f,
            FontStyles.Normal,
            new Color32(226, 226, 226, 255));

        CreateAnchoredText(
            panel.transform,
            "Special Thanks",
            "Special thanks to, our esteemed Dean, Dr. Fahad Al-Amri.",
            new Vector2(900f, 56f),
            new Vector2(0f, -340f),
            24f,
            FontStyles.Italic,
            new Color32(255, 177, 92, 255));

        Button backButton = CreateAnchoredButton(panel.transform, "Back", new Vector2(460f, 82f), new Vector2(0f, -430f));
        backButton.onClick.AddListener(EnableMainMenu);
    }

    private void OpenJointSelectionMenu()
    {
        HideAll();
        SetPageVisible(jointSelectionMenu, true);
        RefreshJointSelectionVisuals();
    }

    private void ConfirmJointSelection()
    {
        if (!selectedJointType.HasValue)
            return;

        WeldJointSelectionState.SetSelection(selectedJointType.Value);
        SceneTransitionManager.GoToSceneAsync(MainSceneName);
    }

    private void AdjustStartOptionsVolume(float delta)
    {
        SimulatorSettings.SetVolume(SimulatorSettings.GetVolume() + delta);
        RefreshStartOptionsDisplay();
    }

    private void ToggleStartOptionsTurnMode()
    {
        int nextTurnMode = SimulatorSettings.GetTurnModeIndex() == SimulatorSettings.ContinuousTurnIndex
            ? SimulatorSettings.SnapTurnIndex
            : SimulatorSettings.ContinuousTurnIndex;

        SimulatorSettings.SetTurnModeIndex(nextTurnMode);
        SetTurnTypeFromPlayerPref.ApplyPlayerPrefToCurrentScene();
        RefreshStartOptionsDisplay();
    }

    private void AdjustStartOptionsTableHeight(float delta)
    {
        SimulatorSettings.SetTableHeight(SimulatorSettings.GetTableHeight() + delta);
        TableHeightManager.ApplyCurrentTableHeight();
        RefreshStartOptionsDisplay();
    }

    private void RefreshStartOptionsDisplay()
    {
        if (startOptionsVolumeValueText != null)
            startOptionsVolumeValueText.text = Mathf.RoundToInt(SimulatorSettings.GetVolume() * 100f) + "%";

        if (startOptionsTableHeightValueText != null)
            startOptionsTableHeightValueText.text = SimulatorSettings.GetTableHeight().ToString("0.00") + " m";

        if (startOptionsTurnValueText != null)
            startOptionsTurnValueText.text = SimulatorSettings.GetTurnModeIndex() == SimulatorSettings.ContinuousTurnIndex
                ? "Continuous"
                : "Snap";

        if (startOptionsDisableTutorialToggle != null)
            startOptionsDisableTutorialToggle.SetIsOnWithoutNotify(!SimulatorSettings.GetTutorialsEnabled());

        if (mainMenuDisableTutorialToggle != null)
            mainMenuDisableTutorialToggle.SetIsOnWithoutNotify(!SimulatorSettings.GetTutorialsEnabled());
    }

    private void SelectJoint(WeldJointType jointType)
    {
        selectedJointType = jointType;
        RefreshJointSelectionVisuals();
    }

    private void RefreshJointSelectionVisuals()
    {
        foreach (KeyValuePair<WeldJointType, Image> pair in jointButtonBackgrounds)
        {
            bool isSelected = selectedJointType.HasValue && selectedJointType.Value == pair.Key;
            pair.Value.color = isSelected ? highlightedCardColor : defaultCardColor;

            if (jointButtonOutlines.TryGetValue(pair.Key, out Outline outline))
            {
                outline.effectColor = isSelected ? selectedOutlineColor : defaultOutlineColor;
                float outlineThickness = isSelected ? SelectedJointOutlineThickness : DefaultOutlineThickness;
                outline.effectDistance = new Vector2(outlineThickness, -outlineThickness);
            }

            if (jointPreviewImages.TryGetValue(pair.Key, out RawImage previewImage))
                previewImage.color = isSelected ? Color.white : new Color(0.8f, 0.8f, 0.8f, 1f);

            if (jointButtonLabels.TryGetValue(pair.Key, out TMP_Text label))
                label.color = labelColor;
        }

        if (confirmSelectionButton != null)
        {
            bool hasSelection = selectedJointType.HasValue;
            confirmSelectionButton.interactable = hasSelection;

            Image buttonGraphic = confirmSelectionButton.targetGraphic as Image;
            if (buttonGraphic != null)
                buttonGraphic.color = hasSelection ? confirmEnabledColor : confirmDisabledColor;
        }
    }

    private void CreateJointSelectionMenu()
    {
        if (jointSelectionMenu != null)
            return;

        Transform parent = mainMenu != null ? mainMenu.transform.parent : transform;
        jointSelectionMenu = CreateUIObject("Joint Selection", parent, parent.gameObject.layer);
        RectTransform rootRect = jointSelectionMenu.AddComponent<RectTransform>();
        StretchToParent(rootRect);

        GameObject title = CreateUIObject("Title", jointSelectionMenu.transform, jointSelectionMenu.layer);
        RectTransform titleRect = title.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(900f, 80f);
        titleRect.anchoredPosition = new Vector2(0f, -36f);
        TMP_Text titleLabel = CreateText(title, "Choose A Weld Joint", 56f, FontStyles.Bold, TextAlignmentOptions.Center);
        titleLabel.color = labelColor;

        GameObject subtitle = CreateUIObject("Subtitle", jointSelectionMenu.transform, jointSelectionMenu.layer);
        RectTransform subtitleRect = subtitle.AddComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.5f, 1f);
        subtitleRect.anchorMax = new Vector2(0.5f, 1f);
        subtitleRect.pivot = new Vector2(0.5f, 1f);
        subtitleRect.sizeDelta = new Vector2(980f, 50f);
        subtitleRect.anchoredPosition = new Vector2(0f, -104f);
        TMP_Text subtitleLabel = CreateText(subtitle, "Select a joint type, then hit confirm to start welding", 26f, FontStyles.Bold, TextAlignmentOptions.Center);
        subtitleLabel.color = Color.black;

        GameObject gridRoot = CreateUIObject("Joint Cards", jointSelectionMenu.transform, jointSelectionMenu.layer);
        RectTransform gridRect = gridRoot.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.sizeDelta = new Vector2(1100f, 620f);
        gridRect.anchoredPosition = new Vector2(0f, -15f);

        GridLayoutGroup gridLayout = gridRoot.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(510f, 255f);
        gridLayout.spacing = new Vector2(32f, 30f);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 2;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.childAlignment = TextAnchor.UpperCenter;
        gridLayout.padding = new RectOffset(0, 0, 0, 0);

        foreach (WeldJointType jointType in WeldJointCatalog.OrderedTypes)
        {
            CreateJointSelectionButton(gridRoot.transform, jointType);
        }

        GameObject footer = CreateUIObject("Footer", jointSelectionMenu.transform, jointSelectionMenu.layer);
        RectTransform footerRect = footer.AddComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0.5f, 0f);
        footerRect.anchorMax = new Vector2(0.5f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.sizeDelta = new Vector2(820f, 96f);
        footerRect.anchoredPosition = new Vector2(0f, 34f);

        HorizontalLayoutGroup footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
        footerLayout.spacing = 24f;
        footerLayout.childAlignment = TextAnchor.MiddleCenter;
        footerLayout.childControlWidth = false;
        footerLayout.childControlHeight = false;
        footerLayout.childForceExpandWidth = false;
        footerLayout.childForceExpandHeight = false;

        Button backButton = CreateFooterButton(footer.transform, "Back", 220f, defaultCardColor, labelColor);
        backButton.onClick.AddListener(EnableMainMenu);

        confirmSelectionButton = CreateFooterButton(footer.transform, "Confirm", 320f, confirmDisabledColor, Color.white);
        confirmSelectionButton.onClick.AddListener(ConfirmJointSelection);

        RefreshJointSelectionVisuals();
        SetPageVisible(jointSelectionMenu, false);
    }

    private void BuildRuntimeOptionsPage()
    {
        if (options == null || customOptionsRoot != null)
            return;

        for (int i = 0; i < options.transform.childCount; i++)
            options.transform.GetChild(i).gameObject.SetActive(false);

        customOptionsRoot = CreateUIObject("Runtime Options UI", options.transform, options.layer);
        RectTransform rootRect = customOptionsRoot.AddComponent<RectTransform>();
        StretchToParent(rootRect);

        GameObject panel = CreatePanel("Options Panel", customOptionsRoot.transform, new Vector2(840f, 1020f));
        CreateAnchoredText(panel.transform, "Options Title", "Options", new Vector2(660f, 64f), new Vector2(0f, 415f), 48f, FontStyles.Bold, labelColor);
        CreateAnchoredText(panel.transform, "Options Description", "Optimize your experience.", new Vector2(720f, 56f), new Vector2(0f, 360f), 24f, FontStyles.Normal, new Color32(220, 220, 220, 255));
        CreateAnchoredText(panel.transform, "Volume Label", "Volume", new Vector2(280f, 44f), new Vector2(0f, 255f), 28f, FontStyles.Bold, labelColor);

        startOptionsVolumeDecreaseButton = CreateAnchoredButton(panel.transform, "-", new Vector2(110f, 78f), new Vector2(-170f, 175f));
        startOptionsVolumeDecreaseButton.onClick.AddListener(() => AdjustStartOptionsVolume(-VolumeStep));

        CreateValueDisplay(panel.transform, "Volume Value", new Vector2(220f, 78f), new Vector2(0f, 175f), out startOptionsVolumeValueText);

        startOptionsVolumeIncreaseButton = CreateAnchoredButton(panel.transform, "+", new Vector2(110f, 78f), new Vector2(170f, 175f));
        startOptionsVolumeIncreaseButton.onClick.AddListener(() => AdjustStartOptionsVolume(VolumeStep));

        CreateAnchoredText(panel.transform, "Table Height Label", "Table Height", new Vector2(320f, 44f), new Vector2(0f, 70f), 28f, FontStyles.Bold, labelColor);

        startOptionsTableHeightDecreaseButton = CreateAnchoredButton(panel.transform, "-", new Vector2(110f, 78f), new Vector2(-170f, -10f));
        startOptionsTableHeightDecreaseButton.onClick.AddListener(() => AdjustStartOptionsTableHeight(-SimulatorSettings.TableHeightStep));

        CreateValueDisplay(panel.transform, "Table Height Value", new Vector2(220f, 78f), new Vector2(0f, -10f), out startOptionsTableHeightValueText);

        startOptionsTableHeightIncreaseButton = CreateAnchoredButton(panel.transform, "+", new Vector2(110f, 78f), new Vector2(170f, -10f));
        startOptionsTableHeightIncreaseButton.onClick.AddListener(() => AdjustStartOptionsTableHeight(SimulatorSettings.TableHeightStep));

        CreateAnchoredText(panel.transform, "Turning Label", "Stick Turning Behavior", new Vector2(420f, 44f), new Vector2(0f, -115f), 28f, FontStyles.Bold, labelColor);

        startOptionsTurnModeButton = CreateAnchoredButton(panel.transform, "Continuous", new Vector2(460f, 78f), new Vector2(0f, -195f));
        startOptionsTurnModeButton.onClick.AddListener(ToggleStartOptionsTurnMode);
        startOptionsTurnValueText = startOptionsTurnModeButton.GetComponentInChildren<TMP_Text>(true);

        CreateAnchoredText(panel.transform, "Tutorial Label", "Tutorial", new Vector2(240f, 44f), new Vector2(0f, -300f), 28f, FontStyles.Bold, labelColor);
        startOptionsDisableTutorialToggle = CreateTutorialToggle(
            panel.transform,
            "Disable Tutorial Toggle",
            new Vector2(0f, -365f),
            new Vector2(460f, 70f),
            56f,
            28f);

        startOptionsConfirmButton = CreateAnchoredButton(panel.transform, "Confirm", new Vector2(580f, 82f), new Vector2(0f, -440f));
        startOptionsConfirmButton.onClick.AddListener(EnableMainMenu);

        RefreshStartOptionsDisplay();
    }

    private void EnsureMainMenuPanel()
    {
        if (mainMenu == null)
            return;

        if (startMenuPanel == null)
        {
            startMenuPanel = CreatePanel("Main Menu Panel", mainMenu.transform, new Vector2(1080f, 980f));
            RectTransform panelRect = startMenuPanel.GetComponent<RectTransform>();
            panelRect.anchoredPosition = new Vector2(0f, -20f);
        }

        startMenuPanel.transform.SetAsFirstSibling();

        if (mainMenuDisableTutorialToggle == null)
        {
            mainMenuDisableTutorialToggle = CreateTutorialToggle(
                mainMenu.transform,
                "Main Menu Disable Tutorial Toggle",
                new Vector2(0f, -470f),
                new Vector2(700f, 96f),
                72f,
                42f);
            mainMenuDisableTutorialToggle.transform.SetAsLastSibling();
        }
    }

    private void BuildQuitConfirmationPopup()
    {
        if (quitConfirmationPopup != null)
            return;

        Transform parent = mainMenu != null ? mainMenu.transform.parent : transform;
        quitConfirmationPopup = CreateUIObject("Quit Confirmation Popup", parent, parent.gameObject.layer);
        RectTransform popupRootRect = quitConfirmationPopup.AddComponent<RectTransform>();
        StretchToParent(popupRootRect);

        Image popupOverlay = quitConfirmationPopup.AddComponent<Image>();
        popupOverlay.color = new Color32(0, 0, 0, 110);

        GameObject popupPanel = CreatePanel("Quit Popup Panel", quitConfirmationPopup.transform, new Vector2(760f, 400f));
        CreateAnchoredText(
            popupPanel.transform,
            "Quit Confirmation Title",
            "Quit Simulator?",
            new Vector2(620f, 60f),
            new Vector2(0f, 120f),
            42f,
            FontStyles.Bold,
            labelColor);
        CreateAnchoredText(
            popupPanel.transform,
            "Quit Confirmation Description",
            "You are about to close the simulator.",
            new Vector2(680f, 100f),
            new Vector2(0f, 30f),
            24f,
            FontStyles.Normal,
            new Color32(220, 220, 220, 255));

        Button cancelButton = CreateAnchoredButton(popupPanel.transform, "Cancel", new Vector2(250f, 78f), new Vector2(-145f, -115f));
        cancelButton.onClick.AddListener(HideQuitConfirmationPopup);

        Button confirmQuitButton = CreateAnchoredButton(popupPanel.transform, "Quit", new Vector2(250f, 78f), new Vector2(145f, -115f));
        confirmQuitButton.onClick.AddListener(ConfirmQuitGame);

        quitConfirmationPopup.SetActive(false);
    }

    private void ShowQuitConfirmationPopup()
    {
        pageVisibleBeforeQuitConfirmation = GetCurrentlyVisiblePage();
        if (pageVisibleBeforeQuitConfirmation != null)
            pageVisibleBeforeQuitConfirmation.SetActive(false);

        if (quitConfirmationPopup != null)
            quitConfirmationPopup.SetActive(true);
    }

    private void HideQuitConfirmationPopup()
    {
        if (quitConfirmationPopup != null)
            quitConfirmationPopup.SetActive(false);

        if (pageVisibleBeforeQuitConfirmation != null)
        {
            pageVisibleBeforeQuitConfirmation.SetActive(true);
            pageVisibleBeforeQuitConfirmation = null;
        }
    }

    private void ConfirmQuitGame()
    {
        QuitGame();
    }

    private GameObject GetCurrentlyVisiblePage()
    {
        if (mainMenu != null && mainMenu.activeSelf)
            return mainMenu;

        if (options != null && options.activeSelf)
            return options;

        if (about != null && about.activeSelf)
            return about;

        if (jointSelectionMenu != null && jointSelectionMenu.activeSelf)
            return jointSelectionMenu;

        return null;
    }

    private void CreateJointSelectionButton(Transform parent, WeldJointType jointType)
    {
        GameObject buttonObject = CreateUIObject(WeldJointCatalog.GetLabel(jointType), parent, parent.gameObject.layer);
        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(510f, 255f);

        Image background = buttonObject.AddComponent<Image>();
        background.sprite = uiSprite;
        background.color = defaultCardColor;
        background.type = Image.Type.Sliced;

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = defaultOutlineColor;
        outline.effectDistance = new Vector2(DefaultOutlineThickness, -DefaultOutlineThickness);
        outline.useGraphicAlpha = false;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color32(210, 210, 210, 255);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color32(180, 180, 180, 180);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(() => SelectJoint(jointType));
        AttachDefaultUIAudio(buttonObject);

        GameObject imageHolder = CreateUIObject("Preview", buttonObject.transform, buttonObject.layer);
        RectTransform imageRect = imageHolder.AddComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0f, 0.25f);
        imageRect.anchorMax = new Vector2(1f, 1f);
        imageRect.offsetMin = new Vector2(18f, -8f);
        imageRect.offsetMax = new Vector2(-18f, -18f);

        RawImage previewImage = imageHolder.AddComponent<RawImage>();
        previewImage.texture = LoadJointPreviewTexture(jointType);
        previewImage.color = new Color(0.97f, 0.97f, 0.97f, 1f);
        previewImage.raycastTarget = false;

        GameObject labelObject = CreateUIObject("Label", buttonObject.transform, buttonObject.layer);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0.3f);
        labelRect.offsetMin = new Vector2(20f, 16f);
        labelRect.offsetMax = new Vector2(-20f, -8f);

        TMP_Text label = CreateText(labelObject, WeldJointCatalog.GetLabel(jointType), 29f, FontStyles.Bold, TextAlignmentOptions.Center);
        label.color = labelColor;

        jointButtons[jointType] = button;
        jointButtonBackgrounds[jointType] = background;
        jointButtonOutlines[jointType] = outline;
        jointPreviewImages[jointType] = previewImage;
        jointButtonLabels[jointType] = label;
    }

    private Button CreateFooterButton(Transform parent, string label, float width, Color backgroundColor, Color textColor)
    {
        GameObject buttonObject = CreateUIObject(label + " Button", parent, parent.gameObject.layer);
        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(width, 78f);

        LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = 78f;

        Image background = buttonObject.AddComponent<Image>();
        background.sprite = uiSprite;
        background.color = backgroundColor;
        background.type = Image.Type.Sliced;

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = selectedOutlineColor;
        outline.effectDistance = new Vector2(DefaultOutlineThickness, -DefaultOutlineThickness);
        outline.useGraphicAlpha = false;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color32(210, 210, 210, 255);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color32(255, 255, 255, 180);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        AttachDefaultUIAudio(buttonObject);

        GameObject labelObject = CreateUIObject("Label", buttonObject.transform, buttonObject.layer);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        StretchToParent(labelRect);

        TMP_Text text = CreateText(labelObject, label, 30f, FontStyles.Bold, TextAlignmentOptions.Center);
        text.color = textColor;

        return button;
    }

    private GameObject CreatePanel(string name, Transform parent, Vector2 size)
    {
        GameObject panel = CreateUIObject(name, parent, parent.gameObject.layer);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        Image image = panel.AddComponent<Image>();
        image.sprite = uiSprite;
        image.type = Image.Type.Sliced;
        image.color = new Color32(0, 0, 0, 235);

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = selectedOutlineColor;
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = false;

        return panel;
    }

    private Button CreateAnchoredButton(Transform parent, string label, Vector2 size, Vector2 anchoredPosition)
    {
        Button button = CreateFooterButton(parent, label, size.x, defaultCardColor, labelColor);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return button;
    }

    private TMP_Text CreateAnchoredText(Transform parent, string objectName, string value, Vector2 size, Vector2 anchoredPosition, float fontSize, FontStyles style, Color color)
    {
        GameObject textObject = CreateUIObject(objectName, parent, parent.gameObject.layer);
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

    private void CreateValueDisplay(Transform parent, string name, Vector2 size, Vector2 anchoredPosition, out TMP_Text text)
    {
        GameObject valueObject = CreateUIObject(name, parent, parent.gameObject.layer);
        RectTransform rect = valueObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image background = valueObject.AddComponent<Image>();
        background.sprite = uiSprite;
        background.type = Image.Type.Sliced;
        background.color = defaultCardColor;

        Outline outline = valueObject.AddComponent<Outline>();
        outline.effectColor = selectedOutlineColor;
        outline.effectDistance = new Vector2(DefaultOutlineThickness, -DefaultOutlineThickness);
        outline.useGraphicAlpha = false;

        GameObject labelObject = CreateUIObject("Label", valueObject.transform, valueObject.layer);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        StretchToParent(labelRect);

        text = CreateText(labelObject, "50%", 30f, FontStyles.Bold, TextAlignmentOptions.Center);
        text.color = new Color32(220, 220, 220, 255);
    }

    private Toggle CreateTutorialToggle(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 rootSize,
        float checkboxSize,
        float fontSize)
    {
        GameObject toggleRoot = CreateUIObject(objectName, parent, parent.gameObject.layer);
        RectTransform rootRect = toggleRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = rootSize;
        rootRect.anchoredPosition = anchoredPosition;

        Toggle toggle = toggleRoot.AddComponent<Toggle>();

        GameObject backgroundObject = CreateUIObject("Background", toggleRoot.transform, toggleRoot.layer);
        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.pivot = new Vector2(0f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(checkboxSize, checkboxSize);
        backgroundRect.anchoredPosition = Vector2.zero;
        Image background = backgroundObject.AddComponent<Image>();
        background.sprite = uiSprite;
        background.type = Image.Type.Sliced;
        background.color = defaultCardColor;

        Outline backgroundOutline = backgroundObject.AddComponent<Outline>();
        backgroundOutline.effectColor = selectedOutlineColor;
        backgroundOutline.effectDistance = new Vector2(DefaultOutlineThickness, -DefaultOutlineThickness);
        backgroundOutline.useGraphicAlpha = false;

        GameObject checkmarkObject = CreateUIObject("Checkmark", backgroundObject.transform, backgroundObject.layer);
        RectTransform checkmarkRect = checkmarkObject.AddComponent<RectTransform>();
        checkmarkRect.anchorMin = Vector2.zero;
        checkmarkRect.anchorMax = Vector2.one;
        float inset = Mathf.Max(10f, checkboxSize * 0.18f);
        checkmarkRect.offsetMin = new Vector2(inset, inset);
        checkmarkRect.offsetMax = new Vector2(-inset, -inset);
        Image checkmark = checkmarkObject.AddComponent<Image>();
        checkmark.sprite = uiSprite;
        checkmark.type = Image.Type.Sliced;
        checkmark.color = new Color32(255, 177, 92, 255);

        GameObject labelObject = CreateUIObject("Label", toggleRoot.transform, toggleRoot.layer);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.sizeDelta = new Vector2(rootSize.x - checkboxSize - 28f, rootSize.y);
        labelRect.anchoredPosition = new Vector2(checkboxSize + 24f, 0f);
        TMP_Text label = CreateText(labelObject, "Disable Tutorial", fontSize, FontStyles.Bold, TextAlignmentOptions.Left);
        label.color = labelColor;

        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        toggle.isOn = !SimulatorSettings.GetTutorialsEnabled();
        toggle.onValueChanged.AddListener(isOn => SimulatorSettings.SetTutorialsEnabled(!isOn));
        AttachDefaultUIAudio(toggleRoot);
        return toggle;
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

    private Texture2D LoadJointPreviewTexture(WeldJointType jointType)
    {
        return Resources.Load<Texture2D>(GetJointPreviewResourcePath(jointType));
    }

    private void AttachDefaultUIAudio(GameObject target)
    {
        if (target == null || target.GetComponent<UIAudio>() != null)
            return;

        UIAudio uiAudio = target.AddComponent<UIAudio>();
        uiAudio.clickAudioName = DefaultClickAudioName;
        uiAudio.hoverEnterAudioName = DefaultHoverEnterAudioName;
        uiAudio.hoverExitAudioName = string.Empty;
    }

    private string GetJointPreviewResourcePath(WeldJointType jointType)
    {
        switch (jointType)
        {
            case WeldJointType.CornerJoint:
                return "JointPreviews/Corner Joint";
            case WeldJointType.TeeJoint:
                return "JointPreviews/Tee Joint";
            case WeldJointType.ButtJoint:
                return "JointPreviews/Butt Joint";
            case WeldJointType.EdgeJoint:
                return "JointPreviews/Edge Joint";
            default:
                return string.Empty;
        }
    }

    private GameObject CreateUIObject(string name, Transform parent, int layer)
    {
        GameObject go = new GameObject(name);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        return go;
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

    private void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
    }

    private void SetPageVisible(GameObject page, bool visible)
    {
        if (page != null)
            page.SetActive(visible);
    }
}
