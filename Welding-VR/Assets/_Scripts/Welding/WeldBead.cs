using UnityEngine;

/// <summary>
/// Attached to each deposited weld bead.
/// Animates emission color from liquid metal (white/yellow) to cool slag.
/// </summary>
public class WeldBead : MonoBehaviour
{
    private Material mat;
    private float coolTime = 15f;
    private float elapsed = 0f;

    public void Initialize()
    {
        mat = GetComponent<Renderer>().material;
        mat.EnableKeyword("_EMISSION");
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / coolTime);

        // Define cooling stages for realistic metal heat
        Color emission;
        if (t < 0.1f)
        {
            // Liquid white/yellow to bright orange
            float localT = t / 0.1f;
            emission = Color.Lerp(new Color(1f, 1f, 0.8f) * 4f, new Color(1f, 0.5f, 0f) * 3f, localT);
        }
        else if (t < 0.4f)
        {
            // Bright orange to cherry red
            float localT = (t - 0.1f) / 0.3f;
            emission = Color.Lerp(new Color(1f, 0.5f, 0f) * 3f, new Color(0.8f, 0.1f, 0f) * 2f, localT);
        }
        else if (t < 0.8f)
        {
            // Cherry red to dull red
            float localT = (t - 0.4f) / 0.4f;
            emission = Color.Lerp(new Color(0.8f, 0.1f, 0f) * 2f, new Color(0.3f, 0.0f, 0.0f) * 0.5f, localT);
        }
        else
        {
            // Dull red to completely cooled (black emission)
            float localT = (t - 0.8f) / 0.2f;
            emission = Color.Lerp(new Color(0.3f, 0.0f, 0.0f) * 0.5f, Color.black, localT);
        }

        mat.SetColor("_EmissionColor", emission);

        if (t >= 1f)
        {
            enabled = false; // Stop updating once fully cooled
        }
    }
}
