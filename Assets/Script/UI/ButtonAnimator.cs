using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class ButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Hover")]
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float hoverDuration = 0.15f;
    [SerializeField] private Ease hoverEase = Ease.OutBack;

    [Header("Click")]
    [SerializeField] private float clickScale = 0.9f;
    [SerializeField] private float clickDuration = 0.1f;
    [SerializeField] private Ease clickEase = Ease.OutQuad;

    private RectTransform rectTransform;
    private Vector3 originalScale;
    private Tween activeTween;
    private bool isPointerDown;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        originalScale = rectTransform.localScale;
    }

    private void OnDisable()
    {
        activeTween?.Kill();
        rectTransform.localScale = originalScale;
        isPointerDown = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        AnimateTo(originalScale * hoverScale, hoverDuration, hoverEase);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerDown = false;
        AnimateTo(originalScale, hoverDuration, hoverEase);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        AnimateTo(originalScale * clickScale, clickDuration, clickEase);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPointerDown) return;
        isPointerDown = false;
        AnimateTo(originalScale * hoverScale, clickDuration, clickEase);
    }

    private void AnimateTo(Vector3 targetScale, float duration, Ease ease)
    {
        activeTween?.Kill();
        activeTween = rectTransform.DOScale(targetScale, duration).SetEase(ease).SetUpdate(true);
    }
}
