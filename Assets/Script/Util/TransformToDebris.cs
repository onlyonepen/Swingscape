using UnityEngine;
using VInspector;

[RequireComponent(typeof(Prefracture), typeof(MeshCollider))]
public class TransformToDebris : MonoBehaviour
{
    [Button]
    public void Break()
    {
        GetComponent<Prefracture>().ComputeFracture();
    }

    public void FractureCallback()
    {
        Debug.Log("asa");
    }
}
