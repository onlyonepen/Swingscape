using System.Collections;
using UnityEngine;

public class ObtainGrapple : MonoBehaviour
{
    public LayerMask PlayerLayer;
    public GameObject[] ToSwitchActive;
    public OSTController ostController;
    [SerializeField] private bool grantOnStart = false; // Check to grant grapple immediately, skipping the trigger (test scenes)
    private bool _hasTriggered = false; // Safety flag

    private void Start()
    {
        if (GameValue.ObtainedGrapple)
        {
            gameObject.SetActive(false);
            foreach (GameObject obj in ToSwitchActive)
            {
                obj.SetActive(true);
            }
            ostController?.PlayOST();
            return;
        }

        if (grantOnStart) Grant();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Safety check to prevent re-triggering and spamming the Coroutine
        if (_hasTriggered) return;

        if ((1 << other.gameObject.layer & PlayerLayer) != 0)
        {
            Grant();
        }
    }

    private void Grant()
    {
        _hasTriggered = true;
        GlobalReference.Instance.player.Locomotion.canGrapple = true;
        GameValue.ObtainedGrapple = true;

        // Start the optimized activation
        StartCoroutine(EnableObjectsGradually());

        ostController?.PlayOST();
        gameObject.SetActive(false);
    }

    private IEnumerator EnableObjectsGradually()
    {
        foreach (GameObject obj in ToSwitchActive)
        {
            if (obj != null)
            {
                obj.SetActive(true);
                // This 'yield' tells Unity to wait until the NEXT frame 
                // before continuing the loop, preventing the CPU spike.
                yield return null; 
            }
        }
    }
}