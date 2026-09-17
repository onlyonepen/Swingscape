using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class HitStopUtil : MonoBehaviour
{
    public static HitStopUtil Instance { get; private set; }

    private CancellationTokenSource _globalCts;

    // The sustained time scale to return to after a momentary hit-stop (1 = normal,
    // <1 = ongoing slow-mo such as the melee-impact effect). HitStopUtil is the single
    // owner of Time.timeScale so overlapping effects can't leave it stuck.
    private float _baseTimeScale = 1f;
    private bool _globalHitStopActive = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        CancelActiveGlobalHitStop();
    }

    public void TriggerGlobalHitStop(float durationSeconds)
    {
        CancelActiveGlobalHitStop();
        _globalCts = new CancellationTokenSource();
        
        _ = ExecuteGlobalHitStop(durationSeconds, _globalCts.Token);
    }

    /// <summary>Set a sustained time scale (e.g. combat slow-mo). Takes effect immediately
    /// unless a momentary hit-stop is currently freezing time, in which case it applies
    /// once that hit-stop ends.</summary>
    public void SetBaseTimeScale(float scale)
    {
        _baseTimeScale = scale;
        if (!_globalHitStopActive) Time.timeScale = scale;
    }

    /// <summary>Restore the sustained time scale to normal (1).</summary>
    public void ResetBaseTimeScale() => SetBaseTimeScale(1f);

    private async Task ExecuteGlobalHitStop(float duration, CancellationToken token)
    {
        _globalHitStopActive = true;
        Time.timeScale = 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (token.IsCancellationRequested) return;

            // Must use unscaledDeltaTime because timeScale is 0
            elapsed += Time.unscaledDeltaTime;
            await Task.Yield();
        }

        _globalHitStopActive = false;
        Time.timeScale = _baseTimeScale; // return to the sustained scale, not a hard 1
    }

    public void TriggerTargetedHitStop(GameObject target, float durationSeconds)
    {
        if (target == null) return;
        _ = ExecuteTargetedHitStop(target, durationSeconds);
    }

    private async Task ExecuteTargetedHitStop(GameObject target, float duration)
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

    private void CancelActiveGlobalHitStop()
    {
        if (_globalCts != null)
        {
            _globalCts.Cancel();
            _globalCts.Dispose();
            _globalCts = null;
            _globalHitStopActive = false;
            Time.timeScale = _baseTimeScale; // Force recovery to the sustained scale
        }
    }
}