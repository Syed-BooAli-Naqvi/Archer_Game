using UnityEngine;

public class ArcherCameraController : MonoBehaviour
{
    public Transform cameraReference;
    public Transform player;

    [Header("Camera Settings")]
    public float cameraHeight = 10f;
    public float rotationX = 55f;

    private void LateUpdate()
    {
        if (cameraReference == null || player == null)
            return;

        Vector3 cameraPosition = cameraReference.position;

        // Follow player's X and Z position
        cameraPosition.x = player.position.x;
        cameraPosition.z = player.position.z;

        // Keep Y fixed
        cameraPosition.y = cameraHeight;

        // Keep rotation fixed
        cameraReference.SetPositionAndRotation(new Vector3(0, cameraPosition.y, cameraPosition.z), Quaternion.Euler(rotationX, 0f, 0f));
    }
}