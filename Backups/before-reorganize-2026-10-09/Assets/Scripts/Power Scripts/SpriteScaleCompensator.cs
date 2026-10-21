using UnityEngine;

[ExecuteAlways]
public class SpriteScaleCompensator : MonoBehaviour
{
    [Tooltip("The world-space scale this object should always visually appear at, regardless of parent scaling.")]
    public Vector3 targetScale = Vector3.one;

    [Tooltip("If true, compensates for the full inherited world scale (all ancestors). If false, only compensates for the direct parent's localScale.")]
    public bool useFullWorldScale = true;

    private void LateUpdate()
    {
        ApplyCompensation();
    }

    private void OnValidate()
    {
        ApplyCompensation();
    }

    private void ApplyCompensation()
    {
        if (transform.parent == null)
        {
            transform.localScale = targetScale;
            return;
        }

        Vector3 parentScale = useFullWorldScale
            ? transform.parent.lossyScale
            : transform.parent.localScale;

        Vector3 compensated = new Vector3(
            SafeDivide(targetScale.x, parentScale.x),
            SafeDivide(targetScale.y, parentScale.y),
            SafeDivide(targetScale.z, parentScale.z)
        );

        transform.localScale = compensated;
    }

    private float SafeDivide(float numerator, float denominator)
    {
        const float epsilon = 0.0001f;
        if (Mathf.Abs(denominator) < epsilon)
            return numerator; // avoid divide-by-zero / inf when parent scale collapses to 0
        return numerator / denominator;
    }
}