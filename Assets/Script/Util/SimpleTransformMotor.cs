using UnityEngine;

public class SimpleTransformMotor : MonoBehaviour
{
    public enum MoveType
    {
        Once,       // play start -> end, then stop
        Loop,       // play start -> end, snap back to start, repeat
        PingPong    // play start -> end -> start -> ... continuously
    }

    [Header("Target")]
    [Tooltip("If set, its position/rotation/scale is used as the end point. If left empty, the explicit target values below are used instead.")]
    [SerializeField] private Transform targetPoint;
    [SerializeField] private Space space = Space.World;

    [Header("Explicit End Point (used if Target Point is empty, added on top of the starting transform)")]
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private Vector3 rotationOffset;
    [SerializeField] private Vector3 scaleOffset;

    [Header("What To Animate")]
    [SerializeField] private bool animatePosition = true;
    [SerializeField] private bool animateRotation = true;
    [SerializeField] private bool animateScale = false;

    [Header("Tween")]
    [SerializeField] private float duration = 1f;
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private MoveType moveType = MoveType.PingPong;
    [Tooltip("Extra time to wait at each end point before continuing (Loop / PingPong only).")]
    [SerializeField] private float waitTimeAtPoints = 0f;
    [SerializeField] private bool playOnStart = true;
    [Tooltip("If true, the object begins at a random point along the tween (and, for Ping Pong, a random direction) instead of at the start point. Useful so multiple instances don't move in sync.")]
    [SerializeField] private bool randomizeStartTransform = false;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 startScale;

    private Vector3 endPosition;
    private Quaternion endRotation;
    private Vector3 endScale;

    private float timer;
    private bool movingForward = true;
    private bool isPlaying;
    private float waitTimer;

    private void Start()
    {
        CaptureStartState();
        CaptureEndState();

        if (randomizeStartTransform)
        {
            movingForward = moveType != MoveType.PingPong || Random.value > 0.5f;
            timer = Random.value * duration;
            float t = duration > 0f ? Mathf.Clamp01(timer / duration) : 1f;
            ApplyTransform(easeCurve.Evaluate(movingForward ? t : 1f - t));
        }

        if (playOnStart)
            Play();
    }

    private void Update()
    {
        if (!isPlaying)
            return;

        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            return;
        }

        timer += Time.deltaTime;
        float t = duration > 0f ? Mathf.Clamp01(timer / duration) : 1f;
        float evaluatedT = easeCurve.Evaluate(movingForward ? t : 1f - t);

        ApplyTransform(evaluatedT);

        if (t >= 1f)
            HandleSegmentComplete();
    }

    private void HandleSegmentComplete()
    {
        switch (moveType)
        {
            case MoveType.Once:
                isPlaying = false;
                break;

            case MoveType.Loop:
                timer = 0f;
                ApplyTransform(easeCurve.Evaluate(0f));
                waitTimer = waitTimeAtPoints;
                break;

            case MoveType.PingPong:
                movingForward = !movingForward;
                timer = 0f;
                waitTimer = waitTimeAtPoints;
                break;
        }
    }

    private void ApplyTransform(float t)
    {
        if (animatePosition)
        {
            Vector3 position = Vector3.LerpUnclamped(startPosition, endPosition, t);
            if (space == Space.World)
                transform.position = position;
            else
                transform.localPosition = position;
        }

        if (animateRotation)
        {
            // Slerping between start/end quaternions collapses to a no-op for multi-turn
            // offsets (e.g. 360 degrees == identity), so explicit offsets are interpolated
            // as raw Euler degrees instead. Target-point mode still slerps since there is
            // no "offset" to speak of, just an orientation to rotate towards.
            Quaternion rotation = targetPoint != null
                ? Quaternion.SlerpUnclamped(startRotation, endRotation, t)
                : startRotation * Quaternion.Euler(rotationOffset * t);

            if (space == Space.World)
                transform.rotation = rotation;
            else
                transform.localRotation = rotation;
        }

        if (animateScale)
        {
            transform.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
        }
    }

    private void CaptureStartState()
    {
        if (space == Space.World)
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
        }
        else
        {
            startPosition = transform.localPosition;
            startRotation = transform.localRotation;
        }

        startScale = transform.localScale;
    }

    private void CaptureEndState()
    {
        if (targetPoint != null)
        {
            if (space == Space.World)
            {
                endPosition = targetPoint.position;
                endRotation = targetPoint.rotation;
            }
            else
            {
                endPosition = targetPoint.localPosition;
                endRotation = targetPoint.localRotation;
            }

            endScale = targetPoint.localScale;
        }
        else
        {
            endPosition = startPosition + positionOffset;
            endRotation = startRotation * Quaternion.Euler(rotationOffset);
            endScale = startScale + scaleOffset;
        }
    }

    public void Play()
    {
        isPlaying = true;
    }

    public void Pause()
    {
        isPlaying = false;
    }

    public void Restart()
    {
        timer = 0f;
        waitTimer = 0f;
        movingForward = true;
        ApplyTransform(easeCurve.Evaluate(0f));
        isPlaying = true;
    }

    public void ResetToStart()
    {
        isPlaying = false;
        timer = 0f;
        waitTimer = 0f;
        movingForward = true;
        ApplyTransform(easeCurve.Evaluate(0f));
    }
}
