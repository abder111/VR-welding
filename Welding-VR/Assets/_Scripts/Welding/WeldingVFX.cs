using UnityEngine;

/// <summary>
/// Controls the welding torch VFX:
///   - Spark/fire particle system on the torch tip
///   - Flickering arc point light
///   - Activates immediately on trigger press (no seam required)
/// </summary>
public class WeldingVFX : MonoBehaviour
{
    private ParticleSystem sparkParticles;
    private Light arcLight;
    private bool isActive = false;

    // Flicker state
    private float baseIntensity = 4f;
    private float flickerSpeed  = 18f;
    private float flickerAmount = 1.8f;

    public void Initialize(ParticleSystem sparks, Light light)
    {
        sparkParticles = sparks;
        arcLight = light;

        // Upgrade particle settings for a more dramatic fire/arc look
        if (sparkParticles != null) UpgradeParticles();

        // Upgrade arc light
        if (arcLight != null)
        {
            arcLight.type      = LightType.Point;
            arcLight.range     = 1.5f;
            arcLight.intensity = baseIntensity;
            arcLight.color     = new Color(0.55f, 0.85f, 1f); // cool blue-white arc
            arcLight.enabled   = false;
        }

        StopWelding();
    }

    void UpgradeParticles()
    {
        // ── Main module ──
        var main = sparkParticles.main;
        main.startLifetime         = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startSpeed            = new ParticleSystem.MinMaxCurve(1.2f,  3.5f);
        main.startSize             = new ParticleSystem.MinMaxCurve(0.006f, 0.018f);
        main.startColor            = new ParticleSystem.MinMaxGradient(
                                        new Color(1f,   0.85f, 0.3f),   // yellow-hot
                                        new Color(1f,   0.4f,  0.05f)); // orange
        main.maxParticles          = 200;
        main.simulationSpace       = ParticleSystemSimulationSpace.World;
        main.gravityModifier       = 0.3f;

        // ── Emission ──
        var emission = sparkParticles.emission;
        emission.rateOverTime = 80f;
        emission.enabled = false; // off until StartWelding

        // ── Shape: narrow cone from tip ──
        var shape = sparkParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 22f;
        shape.radius    = 0.003f;

        // ── Color over lifetime: yellow → orange → dark red → transparent ──
        var col = sparkParticles.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 1f, 0.6f), 0f),
                new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.4f),
                new GradientColorKey(new Color(0.6f, 0.1f, 0.05f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f,   0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0f,   1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // ── Size over lifetime: shrink toward end ──
        var sizeLife = sparkParticles.sizeOverLifetime;
        sizeLife.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(0.6f, 0.6f);
        sizeCurve.AddKey(1f, 0f);
        sizeLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // ── Velocity over lifetime: upward drift + random scatter ──
        var vel = sparkParticles.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        vel.y = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

        // ── Renderer: additive blend for glow ──
        var rend = sparkParticles.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var mat = WeldingShaders.CreateUnlit(new Color(1f, 0.7f, 0.2f, 1f));
            mat.SetFloat("_Mode", 1f); // attempt cutout/additive
            // Force additive blending
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            rend.material = mat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sortingFudge = -10f;
        }
    }

    public void StartWelding()
    {
        if (isActive) return;
        isActive = true;

        if (sparkParticles != null)
        {
            var emission = sparkParticles.emission;
            emission.enabled = true;
            sparkParticles.Play();
        }

        if (arcLight != null)
            arcLight.enabled = true;
    }

    public void StopWelding()
    {
        isActive = false;

        if (sparkParticles != null)
        {
            var emission = sparkParticles.emission;
            emission.enabled = false;
            sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (arcLight != null)
            arcLight.enabled = false;
    }

    void Update()
    {
        // Flicker the arc light while active for a realistic arc effect
        if (isActive && arcLight != null && arcLight.enabled)
        {
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f);
            arcLight.intensity = baseIntensity + (noise - 0.5f) * 2f * flickerAmount;
            // Slight color temperature shift
            float warm = noise * 0.15f;
            arcLight.color = new Color(0.55f + warm, 0.85f - warm * 0.3f, 1f - warm * 0.5f);
        }
    }
}
