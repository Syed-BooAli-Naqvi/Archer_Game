using UnityEngine;

/// <summary>
/// Short camera shake (e.g. when running through a gate). Offsets the camera's local position on top of
/// whatever moves its parent, and restores it afterwards. Adds itself to the main camera on first use.
/// </summary>
[DefaultExecutionOrder(10000)]
public class CameraShake : MonoBehaviour
{
    private Vector3 restLocalPosition;
    private float timeLeft;
    private float duration;
    private float strength;

    public static void Shake(float duration, float strength)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        CameraShake shake = cam.GetComponent<CameraShake>();
        if (shake == null)
            shake = cam.gameObject.AddComponent<CameraShake>();

        shake.duration = Mathf.Max(0.01f, duration);
        shake.timeLeft = shake.duration;
        shake.strength = strength;
    }

    private void Awake()
    {
        restLocalPosition = transform.localPosition;
    }

    private void LateUpdate()
    {
        if (timeLeft <= 0f)
            return;

        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            transform.localPosition = restLocalPosition;
            return;
        }

        float falloff = timeLeft / duration;
        Vector2 jitter = Random.insideUnitCircle * strength * falloff;
        transform.localPosition = restLocalPosition + new Vector3(jitter.x, jitter.y, 0f);
    }
}
