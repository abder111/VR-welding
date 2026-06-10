using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Weld seam: a line of dot-markers between two pieces.
/// - Orange dot markers show the path clearly
/// - Segments track per-dot welding progress
/// - Scoring based on how closely the torch tip follows the line
/// </summary>
public class WeldSeam : MonoBehaviour
{
    public Vector3 SeamStart    { get; private set; }
    public Vector3 SeamEnd      { get; private set; }
    public int     SegmentCount { get; private set; }
    public float   CoveragePercent => scoredBeads == 0 ? 0f :
        (float)weldedCount / SegmentCount * 100f;
    public string  FinalGrade   { get; private set; } = "";
    public bool    IsComplete   => weldedCount >= SegmentCount;

    // Scoring
    private float totalOffsetError = 0f;   // sum of |distance from seam| per bead
    private float totalSpeedError  = 0f;
    private int   scoredBeads      = 0;
    private int   weldedCount      = 0;
    private float idealSpeed       = 0.04f; // m/s — comfortable torch travel

    // Internals
    private bool[]        weldedSegments;
    private GameObject[]  dotMarkers;        // orange guide dots
    private GameObject[]  beadObjects;       // weld bead visuals
    private Material      beadMaterial;
    private float         seamLength;
    private float         segmentLength;
    private float         weldRange = 0.08f; // how close tip must be to deposit

    // ──────────────────────────────────────────────────────────────
    // INITIALIZE
    // ──────────────────────────────────────────────────────────────
    public void Initialize(Vector3 start, Vector3 end, int segments, Material beadMat)
    {
        SeamStart     = start;
        SeamEnd       = end;
        SegmentCount  = segments;
        beadMaterial  = beadMat;

        weldedSegments = new bool[segments];
        beadObjects    = new GameObject[segments];
        seamLength     = Vector3.Distance(start, end);
        segmentLength  = seamLength / segments;

        SpawnDotMarkers(segments);
        // No LineRenderer — dots are clearer
    }

    // ──────────────────────────────────────────────────────────────
    // DOT MARKERS  (orange spheres along the seam)
    // ──────────────────────────────────────────────────────────────
    void SpawnDotMarkers(int count)
    {
        dotMarkers = new GameObject[count];
        var mat = WeldingShaders.CreateUnlit(new Color(1f, 0.45f, 0f, 1f)); // bright orange

        for (int i = 0; i < count; i++)
        {
            float t   = (i + 0.5f) / count;
            Vector3 p = Vector3.Lerp(SeamStart, SeamEnd, t);
            p.y      += 0.012f;   // float just above surface so it's visible

            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "SeamDot_" + i;
            dot.transform.position   = p;
            dot.transform.localScale = Vector3.one * 0.022f;
            dot.transform.SetParent(transform);
            dot.GetComponent<Renderer>().material = mat;
            Object.Destroy(dot.GetComponent<Collider>());

            dotMarkers[i] = dot;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // WELD ATTEMPT  — call every frame while trigger is held
    // Returns true if a new bead was laid
    // ──────────────────────────────────────────────────────────────
    public bool TryDepositBead(Vector3 tipPos, float currentSpeed)
    {
        if (weldedSegments == null) return false;

        Vector3 seamDir    = (SeamEnd - SeamStart).normalized;
        float   proj       = Vector3.Dot(tipPos - SeamStart, seamDir);
        proj               = Mathf.Clamp(proj, 0f, seamLength);

        int idx = Mathf.Clamp(Mathf.FloorToInt(proj / segmentLength), 0, SegmentCount - 1);

        // Accept the nearest un-welded segment within range
        float distToLine = DistanceFromSeam(tipPos);
        if (distToLine > weldRange) return false;
        if (weldedSegments[idx])   return false;

        // ── Mark welded ──
        weldedSegments[idx] = true;
        weldedCount++;

        // ── Score ──
        totalOffsetError += distToLine;
        totalSpeedError  += Mathf.Abs(currentSpeed - idealSpeed);
        scoredBeads++;

        if (IsComplete && string.IsNullOrEmpty(FinalGrade))
            CalculateFinalGrade();

        // ── Hide the orange dot, show a gold bead ──
        if (dotMarkers[idx] != null)
            dotMarkers[idx].SetActive(false);

        SpawnBead(idx, proj);
        return true;
    }

    void SpawnBead(int idx, float projection)
    {
        Vector3 seamDir = (SeamEnd - SeamStart).normalized;
        Vector3 pos     = SeamStart + seamDir * (idx * segmentLength + segmentLength * 0.5f);
        pos.y          += 0.006f;

        var bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bead.name = "WeldBead_" + idx;
        bead.transform.position   = pos;
        bead.transform.localScale = new Vector3(0.018f, 0.009f, 0.018f);
        bead.transform.SetParent(transform);
        Object.Destroy(bead.GetComponent<Collider>());

        var beadColor = new Color(0.35f, 0.22f, 0.08f);
        var mat = WeldingShaders.CreateLitEmissive(beadColor, 0.6f, 0.5f);
        bead.GetComponent<Renderer>().material = mat;
        
        var weldBead = bead.AddComponent<WeldBead>();
        weldBead.Initialize();
        
        beadObjects[idx] = bead;
    }

    // ──────────────────────────────────────────────────────────────
    // HELPERS
    // ──────────────────────────────────────────────────────────────
    public float DistanceFromSeam(Vector3 point)
    {
        Vector3 dir  = (SeamEnd - SeamStart).normalized;
        float   proj = Mathf.Clamp(Vector3.Dot(point - SeamStart, dir), 0f, seamLength);
        return Vector3.Distance(point, SeamStart + dir * proj);
    }

    void CalculateFinalGrade()
    {
        if (scoredBeads == 0) { FinalGrade = "F"; return; }

        float avgOffset = totalOffsetError / scoredBeads;  // 0 = perfect
        float avgSpeed  = totalSpeedError  / scoredBeads;

        // Normalise: weldRange = worst offset, 0.06 m/s off = worst speed
        float normOffset = Mathf.Clamp01(avgOffset / weldRange);
        float normSpeed  = Mathf.Clamp01(avgSpeed  / 0.06f);

        float score = 100f - normOffset * 60f - normSpeed * 40f;

        if      (score >= 90f) FinalGrade = "S";   // perfect
        else if (score >= 78f) FinalGrade = "A";
        else if (score >= 65f) FinalGrade = "B";
        else if (score >= 50f) FinalGrade = "C";
        else if (score >= 35f) FinalGrade = "D";
        else                   FinalGrade = "F";
    }
}
