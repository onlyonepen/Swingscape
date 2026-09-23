using UnityEngine;
using UnityEngine.Events;

public class TriggerEnterEvent : MonoBehaviour
{
    [SerializeField] private LayerMask triggerLayers = ~0;
    [SerializeField] private bool triggerOnce = false;
    [SerializeField] private UnityEvent onTriggerEnter;

    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && hasTriggered) return;
        if ((triggerLayers.value & (1 << other.gameObject.layer)) == 0) return;

        hasTriggered = true;
        onTriggerEnter.Invoke();
    }
}
