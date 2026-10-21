using UnityEngine;

public class PowerScaler : MonoBehaviour
{
    [Header("Visuals")]
    public Transform red;
    public Transform blue;

    [Header("Bar")]
    [Tooltip("Total world-space width of the complete bar.")]
    public float totalWidth = 10f;

    [Header("Mesh")]
    [Tooltip("World width of mesh when localScale.x = 1.")]
    public float meshBaseSize = 10f;

    [Header("Power")]
    [Range(0f, 1f)]
    public float initialPower = 0.5f;

    [Range(0f, 1f)]
    public float power = 0.5f;

    public bool usedForAbility;

    [Range(0f, 0.1f)]
    public float changeAmount = 0.01f;

    private float leftEdge;
    private float rightEdge;

    private void Start()
    {
        // Store the original edges from the initial positions.
        float redWidth = red.localScale.x * meshBaseSize;
        float blueWidth = blue.localScale.x * meshBaseSize;

        leftEdge = red.position.x - redWidth * 0.5f;
        rightEdge = blue.position.x + blueWidth * 0.5f;

        totalWidth = rightEdge - leftEdge;

        // Set starting power from Inspector.
        power = Mathf.Clamp01(initialPower);

        UpdatePower();
    }

    private void UpdatePower()
    {
        if (red == null || blue == null)
            return;

        float redWidth = totalWidth * power;
        float blueWidth = totalWidth * (1f - power);

        float redScaleX = redWidth / meshBaseSize;
        float blueScaleX = blueWidth / meshBaseSize;

        // RED
        Vector3 redScale = red.localScale;
        redScale.x = redScaleX;
        red.localScale = redScale;

        Vector3 redPosition = red.position;
        redPosition.x = leftEdge + redWidth * 0.5f;
        red.position = redPosition;

        // BLUE
        Vector3 blueScale = blue.localScale;
        blueScale.x = blueScaleX;
        blue.localScale = blueScale;

        Vector3 bluePosition = blue.position;
        bluePosition.x = rightEdge - blueWidth * 0.5f;
        blue.position = bluePosition;
    }

    public void IncreaseRed()
    {
        power = Mathf.Clamp01(power + changeAmount);
        UpdatePower();
    }

    public void IncreaseBlue()
    {
        power = Mathf.Clamp01(power - changeAmount);
        UpdatePower();
    }
}