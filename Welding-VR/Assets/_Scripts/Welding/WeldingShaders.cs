using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Creates runtime materials that match the active render pipeline.
/// This project currently runs Built-in RP (no URP asset assigned), so we use
/// the Standard shader — URP/Lit shows magenta/violet on Built-in.
/// </summary>
public static class WeldingShaders
{
    static bool IsURP => GraphicsSettings.currentRenderPipeline != null;

    public static Material CreateLit(Color color, float metallic, float smoothness)
    {
        if (IsURP) return CreateURPLit(color, metallic, smoothness);
        return CreateBuiltInLit(color, metallic, smoothness);
    }

    public static Material CreateLitEmissive(Color color, float metallic, float smoothness)
    {
        var mat = CreateLit(color, metallic, smoothness);
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
        return mat;
    }

    public static Material CreateTransparentLit(Color color)
    {
        if (IsURP) return CreateURPTransparent(color);
        return CreateBuiltInTransparent(color);
    }

    public static Material CreateUnlit(Color color)
    {
        var shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(shader);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        else if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;
        return mat;
    }

    // ── Built-in Render Pipeline (Standard shader) ──

    static Material CreateBuiltInLit(Color color, float metallic, float smoothness)
    {
        var shader = Shader.Find("Standard");
        var mat = new Material(shader);
        mat.color = color;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Glossiness", smoothness);
        return mat;
    }

    static Material CreateBuiltInTransparent(Color color)
    {
        var mat = CreateBuiltInLit(color, 0f, 0.2f);
        mat.SetFloat("_Mode", 3f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        return mat;
    }

    // ── Universal Render Pipeline ──

    static Material CreateURPLit(Color color, float metallic, float smoothness)
    {
        var template = Resources.Load<Material>("Welding/WeldingPiece");
        var mat = template != null
            ? new Material(template)
            : new Material(Shader.Find("Universal Render Pipeline/Lit"));

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;

        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    static Material CreateURPTransparent(Color color)
    {
        var template = Resources.Load<Material>("Welding/WeldingZone");
        var mat = template != null
            ? new Material(template)
            : new Material(Shader.Find("Universal Render Pipeline/Lit"));

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;

        return mat;
    }
}
