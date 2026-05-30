using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionManager : MonoBehaviour
{
    private const float DefaultFadeDuration = 0.45f;
    private const float FadeCanvasDistance = 0.32f;
    private const float FadeCanvasScale = 0.0005f;

    public FadeScreen fadeScreen;
    public static SceneTransitionManager singleton;

    private Canvas fadeCanvas;
    private CanvasGroup fadeCanvasGroup;
    private Image fadeImage;
    private Texture2D generatedSpriteTexture;
    private Coroutine fadeRoutine;
    private bool hasCompletedInitialFade;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHook()
    {
        EnsureInstance();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();
        singleton.HandleSceneLoaded();
    }

    private void Awake()
    {
        if (singleton != null && singleton != this)
        {
            Destroy(gameObject);
            return;
        }

        singleton = this;
        DontDestroyOnLoad(gameObject);
        EnsureFadeOverlay();
    }

    private void OnDestroy()
    {
        if (singleton == this)
            singleton = null;

        if (generatedSpriteTexture != null)
            Destroy(generatedSpriteTexture);
    }

    public static void GoToScene(int sceneIndex)
    {
        EnsureInstance();
        singleton.StartCoroutine(singleton.GoToSceneRoutine(sceneIndex));
    }

    public static void GoToSceneAsync(int sceneIndex)
    {
        EnsureInstance();
        singleton.StartCoroutine(singleton.GoToSceneAsyncRoutine(sceneIndex));
    }

    public static void GoToSceneAsync(string sceneName)
    {
        EnsureInstance();
        singleton.StartCoroutine(singleton.GoToSceneAsyncRoutine(sceneName));
    }

    private static void EnsureInstance()
    {
        if (singleton != null)
            return;

        SceneTransitionManager existing = FindFirstObjectByType<SceneTransitionManager>();
        if (existing != null)
        {
            singleton = existing;
            return;
        }

        GameObject managerObject = new GameObject("Scene Transition Manager");
        singleton = managerObject.AddComponent<SceneTransitionManager>();
    }

    private void HandleSceneLoaded()
    {
        if (fadeScreen != null)
        {
            fadeScreen.FadeIn();
            return;
        }

        EnsureFadeOverlay();
        AttachFadeOverlayToCamera();

        if (!hasCompletedInitialFade)
        {
            fadeCanvasGroup.alpha = 1f;
            hasCompletedInitialFade = true;
        }

        FadeTo(0f, DefaultFadeDuration);
    }

    private IEnumerator GoToSceneRoutine(int sceneIndex)
    {
        yield return FadeOutRoutine();
        SceneManager.LoadScene(sceneIndex);
    }

    private IEnumerator GoToSceneAsyncRoutine(int sceneIndex)
    {
        yield return FadeOutRoutine();
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex);
        while (operation != null && !operation.isDone)
            yield return null;
    }

    private IEnumerator GoToSceneAsyncRoutine(string sceneName)
    {
        yield return FadeOutRoutine();
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        while (operation != null && !operation.isDone)
            yield return null;
    }

    private IEnumerator FadeOutRoutine()
    {
        if (fadeScreen != null)
        {
            fadeScreen.FadeOut();
            float timer = 0f;
            float duration = Mathf.Max(0f, fadeScreen.fadeDuration);
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            yield break;
        }

        EnsureFadeOverlay();
        AttachFadeOverlayToCamera();
        yield return FadeToRoutine(1f, DefaultFadeDuration);
    }

    private void FadeTo(float targetAlpha, float duration)
    {
        if (fadeScreen != null)
            return;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeToRoutine(targetAlpha, duration));
    }

    private IEnumerator FadeToRoutine(float targetAlpha, float duration)
    {
        EnsureFadeOverlay();

        float startAlpha = fadeCanvasGroup.alpha;
        if (Mathf.Approximately(startAlpha, targetAlpha))
        {
            fadeCanvas.enabled = targetAlpha > 0.001f;
            fadeRoutine = null;
            yield break;
        }

        fadeCanvas.enabled = true;

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = duration <= 0f ? 1f : Mathf.Clamp01(timer / duration);
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
        fadeCanvas.enabled = targetAlpha > 0.001f;
        fadeRoutine = null;
    }

    private void EnsureFadeOverlay()
    {
        if (fadeScreen != null)
            return;

        if (fadeCanvas != null && fadeCanvasGroup != null && fadeImage != null)
            return;

        GameObject canvasObject = new GameObject("Runtime Fade Canvas");
        canvasObject.transform.SetParent(transform, false);

        RectTransform canvasRect = canvasObject.AddComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(2400f, 1600f);
        canvasObject.transform.localScale = Vector3.one * FadeCanvasScale;

        fadeCanvas = canvasObject.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.WorldSpace;
        fadeCanvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        fadeCanvasGroup = canvasObject.AddComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 1f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.interactable = false;

        GameObject imageObject = new GameObject("Fade Image");
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform imageRect = imageObject.AddComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        fadeImage = imageObject.AddComponent<Image>();
        fadeImage.sprite = LoadDefaultSprite();
        fadeImage.type = Image.Type.Sliced;
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;
    }

    private void AttachFadeOverlayToCamera()
    {
        if (fadeScreen != null)
            return;

        EnsureFadeOverlay();

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindFirstObjectByType<Camera>();

        if (targetCamera == null)
            return;

        Transform canvasTransform = fadeCanvas.transform;
        if (canvasTransform.parent != targetCamera.transform)
            canvasTransform.SetParent(targetCamera.transform, false);

        fadeCanvas.worldCamera = targetCamera;
        canvasTransform.localPosition = new Vector3(0f, 0f, FadeCanvasDistance);
        canvasTransform.localRotation = Quaternion.identity;
        canvasTransform.localScale = Vector3.one * FadeCanvasScale;
    }

    private Sprite LoadDefaultSprite()
    {
        if (generatedSpriteTexture == null)
        {
            generatedSpriteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            generatedSpriteTexture.SetPixel(0, 0, Color.white);
            generatedSpriteTexture.Apply();
        }

        return Sprite.Create(generatedSpriteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
    }
}
