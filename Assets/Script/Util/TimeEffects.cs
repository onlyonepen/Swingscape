using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Static owner of Time.timeScale for slow-mo / hit-stop so overlapping effects can't
/// leave it stuck. Plain static class instead of a scene singleton — Unity's SynchronizationContext
/// keeps the async continuations on the main thread with no MonoBehaviour required.</summary>
public static class TimeEffects
{
    private static CancellationTokenSource globalCts;

    // The sustained time scale to return to after a momentary hit-stop (1 = normal,
    // <1 = ongoing slow-mo such as the melee-impact effect).
    private static float baseTimeScale = 1f;
    private static bool globalHitStopActive = false;

    public static void TriggerGlobalHitStop(float durationSeconds)
    {
        CancelActiveGlobalHitStop();
        globalCts = new CancellationTokenSource();

        _ = ExecuteGlobalHitStop(durationSeconds, globalCts.Token);
    }

    /// <summary>Set a sustained time scale (e.g. combat slow-mo). Takes effect immediately
    /// unless a momentary hit-stop is currently freezing time, in which case it applies
    /// once that hit-stop ends.</summary>
    public static void SetBaseTimeScale(float scale)
    {
        baseTimeScale = scale;
        if (!globalHitStopActive) Time.timeScale = scale;
    }

    /// <summary>Restore the sustained time scale to normal (1).</summary>
    public static void ResetBaseTimeScale() => SetBaseTimeScale(1f);

    /// <summary>Set a sustained time scale for a fixed duration, then automatically restore it to normal.</summary>
    public static void TriggerTimedSlowMo(float scale, float durationSeconds)
    {
        SetBaseTimeScale(scale);
        _ = ExecuteTimedSlowMo(durationSeconds);
    }

    private static async Task ExecuteTimedSlowMo(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            await Task.Yield();
        }
        ResetBaseTimeScale();
    }

    private static async Task ExecuteGlobalHitStop(float duration, CancellationToken token)
    {
        globalHitStopActive = true;
        Time.timeScale = 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (token.IsCancellationRequested) return;

            // Must use unscaledDeltaTime because timeScale is 0
            elapsed += Time.unscaledDeltaTime;
            await Task.Yield();
        }

        globalHitStopActive = false;
        Time.timeScale = baseTimeScale; // return to the sustained scale, not a hard 1
    }

    public static void TriggerTargetedHitStop(GameObject target, float durationSeconds)
    {
        if (target == null) return;
        _ = ExecuteTargetedHitStop(target, durationSeconds);
    }

    private static async Task ExecuteTargetedHitStop(GameObject target, float duration)
    {
        if (target == null) return;

        // Try to cache components
        var animator = target.GetComponentInChildren<Animator>();
        var rb = target.GetComponent<Rigidbody>();
        var rb2d = target.GetComponent<Rigidbody2D>();

        // Store original states
        float originalAnimSpeed = animator != null ? animator.speed : 1f;

        Vector3 originalVelocity = Vector3.zero;
        Vector3 originalAngularVelocity = Vector3.zero;
        bool wasKinematic = false;

        Vector2 originalVelocity2D = Vector2.zero;
        float originalAngularVelocity2D = 0f;
        bool wasKinematic2D = false;

        // Pause components
        if (animator != null) animator.speed = 0f;

        if (rb != null)
        {
            // Unity 6 modern naming properties (replaces rb.velocity / rb.angularVelocity)
            originalVelocity = rb.linearVelocity;
            originalAngularVelocity = rb.angularVelocity;
            wasKinematic = rb.isKinematic;

            rb2d.bodyType = RigidbodyType2D.Kinematic;
        }
        else if (rb2d != null)
        {
            originalVelocity2D = rb2d.linearVelocity;
            originalAngularVelocity2D = rb2d.angularVelocity;
            wasKinematic2D = rb2d.bodyType == RigidbodyType2D.Kinematic;

            rb2d.bodyType = RigidbodyType2D.Kinematic;
        }

        // Wait using unscaled real time
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Fallback safety if the object gets destroyed mid-hitstop
            if (target == null) return;
            elapsed += Time.unscaledDeltaTime;
            await Task.Yield();
        }

        if (target == null) return;

        // Restore original states safely
        if (animator != null) animator.speed = originalAnimSpeed;

        if (rb != null)
        {
            rb.isKinematic = wasKinematic;
            rb.linearVelocity = originalVelocity;
            rb.angularVelocity = originalAngularVelocity;
        }
        else if (rb2d != null)
        {
            if (wasKinematic2D) rb2d.bodyType = RigidbodyType2D.Kinematic;
            else rb2d.bodyType = RigidbodyType2D.Dynamic;
            rb2d.linearVelocity = originalVelocity2D;
            rb2d.angularVelocity = originalAngularVelocity2D;
        }
    }

    private static void CancelActiveGlobalHitStop()
    {
        if (globalCts != null)
        {
            globalCts.Cancel();
            globalCts.Dispose();
            globalCts = null;
            globalHitStopActive = false;
            Time.timeScale = baseTimeScale; // Force recovery to the sustained scale
        }
    }
}
