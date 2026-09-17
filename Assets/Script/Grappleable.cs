using UnityEngine;

public class Grappleable : MonoBehaviour
{
    [SerializeField] private GrappleType type = GrappleType.Normal;
    public GrappleType Type { get => type; set => type = value; }

    public static GrappleType Resolve(GameObject obj) =>
        obj != null && obj.TryGetComponent(out Grappleable g) ? g.Type : GrappleType.Normal;
}
