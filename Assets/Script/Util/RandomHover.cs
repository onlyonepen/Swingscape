using DG.Tweening;
using UnityEngine;

public class RandomHover : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float radius = 0.5f;
    [SerializeField] private float minMoveDuration = 1f;
    [SerializeField] private float maxMoveDuration = 2f;
    [SerializeField] private Ease ease = Ease.InOutSine;

    [Header("Pause Between Moves")]
    [SerializeField] private float minPause = 0f;
    [SerializeField] private float maxPause = 0.5f;

    [Header("2D Mode")]
    [Tooltip("Restrict wandering to the XY plane instead of a full 3D sphere. Ignored for UI (RectTransform), which always wanders in 2D.")]
    [SerializeField] private bool restrictToPlane;

    [Header("Seed")]
    [Tooltip("Objects sharing the same seed move through the same sequence of offsets/durations, so they wander in sync with each other.")]
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int seed;

    private RectTransform rectTransform;
    private Vector3 origin;
    private Tween activeTween;
    private System.Random rng;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        rng = new System.Random(useRandomSeed ? System.Guid.NewGuid().GetHashCode() : seed);
        origin = rectTransform ? (Vector3)rectTransform.anchoredPosition : transform.localPosition;
        MoveToNewPoint();
    }

    private void OnDisable()
    {
        activeTween?.Kill();
        if (rectTransform) rectTransform.anchoredPosition = origin;
        else transform.localPosition = origin;
    }

    private void MoveToNewPoint()
    {
        Vector3 offset = (rectTransform || restrictToPlane)
            ? (Vector3)(NextInsideUnitCircle() * radius)
            : NextInsideUnitSphere() * radius;

        Vector3 target = origin + offset;
        float duration = NextFloat(minMoveDuration, maxMoveDuration);

        activeTween = rectTransform
            ? rectTransform.DOAnchorPos(target, duration).SetEase(ease)
            : transform.DOLocalMove(target, duration).SetEase(ease);

        activeTween.OnComplete(() =>
        {
            float pause = NextFloat(minPause, maxPause);
            activeTween = pause > 0f ? DOVirtual.DelayedCall(pause, MoveToNewPoint) : null;
            if (activeTween == null) MoveToNewPoint();
        });
    }

    private float NextFloat(float min, float max)
    {
        return (float)(min + rng.NextDouble() * (max - min));
    }

    private Vector2 NextInsideUnitCircle()
    {
        float angle = NextFloat(0f, Mathf.PI * 2f);
        float distance = Mathf.Sqrt((float)rng.NextDouble());
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    }

    private Vector3 NextInsideUnitSphere()
    {
        Vector3 point;
        do
        {
            point = new Vector3(NextFloat(-1f, 1f), NextFloat(-1f, 1f), NextFloat(-1f, 1f));
        } while (point.sqrMagnitude > 1f);
        return point;
    }
}
