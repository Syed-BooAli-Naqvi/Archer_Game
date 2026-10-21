using UnityEngine;

/// <summary>Plays a particle-effect prefab for a while, then lets it fade out and cleans it up.</summary>
public static class VfxUtil
{
    /// <param name="emitSeconds">How long it keeps emitting (looping effects are stopped after this).</param>
    /// <param name="follow">Optional transform to stick to (e.g. an aura on the player).</param>
    public static GameObject Play(GameObject prefab, Vector3 position, float emitSeconds, Transform follow = null, float scale = 1f)
    {
        if (prefab == null)
            return null;

        GameObject fx = Object.Instantiate(prefab, position, Quaternion.identity);
        if (follow != null)
            fx.transform.SetParent(follow, true);
        fx.transform.localScale *= scale;

        DifficultySettings.ApplyParticleBudget(fx);
        fx.AddComponent<VfxLifetime>().Begin(emitSeconds);
        return fx;
    }
}

/// <summary>Stops all particle systems after a set time, then destroys the object once the particles are gone.</summary>
public class VfxLifetime : MonoBehaviour
{
    private float stopAt;
    private bool stopped;

    public void Begin(float emitSeconds)
    {
        stopAt = Time.time + Mathf.Max(0.05f, emitSeconds);
    }

    private void Update()
    {
        if (!stopped && Time.time >= stopAt)
        {
            stopped = true;
            foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>())
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, 2.5f);
        }
    }
}
