using UnityEngine;

/// <summary>
/// Osu-style timing telegraph: two sprites slide in from either side of a world-space
/// target and meet when <see cref="SetProgress"/> reaches 1. Purely visual and purely
/// reactive — the caller (e.g. GrapplePullState) computes progress from whatever actually
/// decides when the pull ends (live distance, not a precomputed ETA) and pushes it in every
/// frame via SetProgress, so the sprites always reflect the real state of the pull instead
/// of a schedule that can drift out of sync with it.
/// </summary>
public class StaggerQTEIndicator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform leftSprite;
    [SerializeField] private RectTransform rightSprite;
    [Tooltip("The HUD canvas these sprites live on (same canvas GrappleTargeting's reticle uses).")]
    [SerializeField] private Canvas targetCanvas;

    [Header("Layout")]
    [Tooltip("How far apart (in canvas pixels) the sprites start, left/right of the target.")]
    [SerializeField] private float startOffset = 400f;

    public static StaggerQTEIndicator Instance { get; private set; }

    private Camera cam;
    private Transform target;
    private float progress;
    private bool active;

    private void Awake()
    {
        Instance = this;
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Start the telegraph: sprites appear fully apart. Call SetProgress every frame
    /// afterwards to drive them toward the meeting point.</summary>
    public void Begin(Transform worldTarget, Camera viewCamera)
    {
        target = worldTarget;
        cam = viewCamera;
        progress = 0f;
        active = true;
        leftSprite.gameObject.SetActive(true);
        rightSprite.gameObject.SetActive(true);
    }

    /// <summary>0 = sprites fully apart, 1 = sprites meeting. Call every frame while active.</summary>
    public void SetProgress(float percent01) => progress = Mathf.Clamp01(percent01);

    /// <summary>Hide immediately — call when the pull ends (success, early abort, or state exit).</summary>
    public void Cancel() => Hide();

    private void Hide()
    {
        active = false;
        if (leftSprite != null) leftSprite.gameObject.SetActive(false);
        if (rightSprite != null) rightSprite.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!active) return;

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Hide();
            return;
        }

        Vector3 screenPoint = cam.WorldToScreenPoint(target.position);
        if (screenPoint.z <= 0f)
        {
            Hide();
            return;
        }

        Camera canvasCam = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)targetCanvas.transform, screenPoint, canvasCam, out Vector2 center))
        {
            Hide();
            return;
        }

        leftSprite.anchoredPosition = center + Vector2.left * startOffset * (1f - progress);
        rightSprite.anchoredPosition = center + Vector2.right * startOffset * (1f - progress);
    }
}
