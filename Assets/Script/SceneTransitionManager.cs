using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private Color fadeColor = Color.black;

    private CanvasGroup canvasGroup;
    private bool isTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        //DontDestroyOnLoad(gameObject);

        BuildFadeCanvas();
    }

    private void Start()
    {
        StartCoroutine(FadeInRoutine());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void BuildFadeCanvas()
    {
        var canvasGO = new GameObject("FadeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var imageGO = new GameObject("FadeImage", typeof(Image));
        imageGO.transform.SetParent(canvasGO.transform, false);

        var image = imageGO.GetComponent<Image>();
        image.color = fadeColor;

        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        canvasGroup = imageGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;
    }

    private IEnumerator FadeInRoutine()
    {
        isTransitioning = true;

        bool fadeInDone = false;
        canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() => fadeInDone = true);
        yield return new WaitUntil(() => fadeInDone);

        canvasGroup.blocksRaycasts = false;
        isTransitioning = false;
    }

    public void LoadScene(string sceneName)
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionRoutine(sceneName, -1));
    }

    public void LoadScene(int buildIndex)
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionRoutine(null, buildIndex));
    }

    private IEnumerator TransitionRoutine(string sceneName, int buildIndex)
    {
        isTransitioning = true;
        canvasGroup.blocksRaycasts = true;

        bool fadeOutDone = false;
        canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true).OnComplete(() => fadeOutDone = true);
        yield return new WaitUntil(() => fadeOutDone);

        AsyncOperation op = sceneName != null
            ? SceneManager.LoadSceneAsync(sceneName)
            : SceneManager.LoadSceneAsync(buildIndex);

        while (op != null && !op.isDone)
        {
            yield return null;
        }

        bool fadeInDone = false;
        canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() => fadeInDone = true);
        yield return new WaitUntil(() => fadeInDone);

        canvasGroup.blocksRaycasts = false;
        isTransitioning = false;
    }
}
