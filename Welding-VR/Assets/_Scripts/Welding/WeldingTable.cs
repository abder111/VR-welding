using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages piece placement and seam generation.
/// Seam is computed from the actual touching/facing edges of the two pieces —
/// not from hardcoded offsets — so it always appears exactly between them.
/// </summary>
public class WeldingTable : MonoBehaviour
{
    [HideInInspector] public Vector3 tableSurfaceCenter;
    [HideInInspector] public float   tableTopY;
    [HideInInspector] public float   tableSizeX;
    [HideInInspector] public float   tableSizeZ;
    [HideInInspector] public Vector3 tableSurfaceNormal  = Vector3.up;
    [HideInInspector] public Vector3 tableSurfaceRight   = Vector3.right;
    [HideInInspector] public Vector3 tableSurfaceForward = Vector3.forward;
    [HideInInspector] public float   tableSurfaceWidth;
    [HideInInspector] public float   tableSurfaceDepth;

    [HideInInspector] public WeldingHUD   hud;
    [HideInInspector] public WeldingTorch torch;
    [HideInInspector] public Material     beadMaterial;
    [HideInInspector] public Transform    workbenchTransform;

    private List<MetalPiece> placedPieces  = new List<MetalPiece>();
    private const int        MaxPieces     = 2;

    private GameObject currentSeamObj;
    private WeldSeam   currentSeam;
    private string     detectedJointName = "";

    private GameObject weldZoneIndicator;

    public WeldSeam CurrentSeam      => currentSeam;
    public string   DetectedJointName => detectedJointName;
    public int      PlacedPieceCount  => placedPieces.Count;
    public string   JointNameForHUD   => detectedJointName;

    void Start() => CreateWeldZoneIndicator();

    void Update()
    {
        if (currentSeam != null && currentSeam.IsComplete && placedPieces.Count == 2)
        {
            JoinPieces();
        }
    }


    // ──────────────────────────────────────────────────────────────
    // PUBLIC API
    // ──────────────────────────────────────────────────────────────

    public bool IsAboveTable(Vector3 pos, float range)
    {
        Vector3 flat  = new Vector3(pos.x, tableSurfaceCenter.y, pos.z);
        Vector3 tFlat = new Vector3(tableSurfaceCenter.x, tableSurfaceCenter.y, tableSurfaceCenter.z);
        bool inXZ  = Vector3.Distance(flat, tFlat) < Mathf.Max(tableSizeX, tableSizeZ) * 0.65f;
        bool inY   = pos.y > tableTopY - 0.15f && pos.y < tableTopY + range;
        return inXZ && inY;
    }

    public Vector3 GetSnapPosition(Vector3 pos)
    {
        float hx = tableSizeX * 0.42f;
        float hz = tableSizeZ * 0.42f;
        return new Vector3(
            Mathf.Clamp(pos.x, tableSurfaceCenter.x - hx, tableSurfaceCenter.x + hx),
            tableTopY + 0.012f,
            Mathf.Clamp(pos.z, tableSurfaceCenter.z - hz, tableSurfaceCenter.z + hz));
    }

    public void OnPiecePlaced(MetalPiece piece)
    {
        if (placedPieces.Contains(piece)) return;
        if (placedPieces.Count >= MaxPieces)
        {
            Debug.Log("[WeldingTable] Table full.");
            return;
        }
        placedPieces.Add(piece);
        Debug.Log($"[WeldingTable] Placed {piece.pieceType} ({placedPieces.Count}/{MaxPieces})");

        if (placedPieces.Count == MaxPieces)
            StartCoroutine(GenerateSeamDelayed());
    }

    public void OnPieceLifted(MetalPiece piece)
    {
        if (!placedPieces.Contains(piece)) return;
        placedPieces.Remove(piece);
        ClearSeam();
        Debug.Log($"[WeldingTable] Piece lifted. Remaining: {placedPieces.Count}");
    }

    public void ClearTable()
    {
        foreach (var p in placedPieces)
            if (p != null) { p.RemoveFromTable(); Destroy(p.gameObject); }
        placedPieces.Clear();
        ClearSeam();
        Debug.Log("[WeldingTable] Table cleared.");
    }

    void ClearSeam()
    {
        if (currentSeamObj != null) { Destroy(currentSeamObj); currentSeamObj = null; currentSeam = null; }
        detectedJointName = "";
        if (torch != null) torch.weldSeam = null;
        if (hud != null) hud.ClearSeam();
    }

    void JoinPieces()
    {
        if (placedPieces.Count < 2) return;

        MetalPiece rootPiece = placedPieces[0];
        MetalPiece childPiece = placedPieces[1];

        // 1. Parent child to root
        childPiece.transform.SetParent(rootPiece.transform, true);

        // 2. Parent the seam to root so beads stay on it
        if (currentSeamObj != null)
        {
            currentSeamObj.transform.SetParent(rootPiece.transform, true);
            // Disable the WeldSeam script so it stops tracking, but don't destroy it
            // so the HUD can still read it while the piece is on the table
            if (currentSeam != null) currentSeam.enabled = false;
            
            // Unlink from table so it isn't destroyed on lift
            currentSeamObj = null;
            currentSeam = null;
        }

        // 3. Remove physics & grabbing from child
        var childRb = childPiece.GetComponent<Rigidbody>();
        if (childRb != null) Destroy(childRb);
        Destroy(childPiece);

        // 4. Update the table's list
        placedPieces.RemoveAt(1);

        // 5. Update root piece renderers for highlighting
        rootPiece.RefreshRenderers();

        // 6. Unlink torch
        if (torch != null) torch.weldSeam = null;

        Debug.Log("[WeldingTable] Pieces physically joined.");
    }

    // ──────────────────────────────────────────────────────────────
    // SEAM GENERATION
    // ──────────────────────────────────────────────────────────────

    IEnumerator GenerateSeamDelayed()
    {
        yield return new WaitForSeconds(0.25f);
        GenerateSeam();
    }

    void GenerateSeam()
    {
        if (placedPieces.Count < 2) return;

        MetalPiece p1 = placedPieces[0];
        MetalPiece p2 = placedPieces[1];

        ClearSeam();

        Vector3 seamStart, seamEnd;
        ComputeSeam(p1, p2, out seamStart, out seamEnd, out detectedJointName);

        // Safety: ensure seam has meaningful length
        if (Vector3.Distance(seamStart, seamEnd) < 0.04f)
        {
            // fallback: midpoint line perpendicular to piece-to-piece axis
            Vector3 mid = (p1.transform.position + p2.transform.position) * 0.5f;
            Vector3 toP2 = (p2.transform.position - p1.transform.position).normalized;
            Vector3 perp = Vector3.Cross(toP2, Vector3.up).normalized;
            if (perp.magnitude < 0.01f) perp = Vector3.right;
            float len = Mathf.Max(GetBounds(p1).size.x, GetBounds(p1).size.z,
                                  GetBounds(p2).size.x, GetBounds(p2).size.z, 0.15f) * 0.8f;
            seamStart = mid - perp * len * 0.5f;
            seamEnd   = mid + perp * len * 0.5f;
            seamStart.y = seamEnd.y = tableTopY + 0.014f;
            detectedJointName = "Butt Joint";
        }

        int dots = Mathf.Clamp(Mathf.RoundToInt(Vector3.Distance(seamStart, seamEnd) / 0.018f), 8, 30);

        currentSeamObj = new GameObject("WeldSeam_Auto");
        currentSeam    = currentSeamObj.AddComponent<WeldSeam>();
        currentSeam.Initialize(seamStart, seamEnd, dots, beadMaterial);

        if (torch != null) torch.weldSeam = currentSeam;
        if (hud   != null) hud.Initialize(currentSeam, torch, hud.transform.position);

        Debug.Log($"[WeldingTable] Seam '{detectedJointName}' from {seamStart} to {seamEnd} ({dots} dots)");
    }

    // ──────────────────────────────────────────────────────────────
    // SEAM POSITION COMPUTATION
    // Works for: side-by-side, stacked, T-shapes, any orientation.
    // Strategy: find the two closest facing-edge points of the pieces.
    // ──────────────────────────────────────────────────────────────

    void ComputeSeam(MetalPiece p1, MetalPiece p2,
                     out Vector3 seamStart, out Vector3 seamEnd,
                     out string jointName)
    {
        Bounds b1 = GetBounds(p1);
        Bounds b2 = GetBounds(p2);

        Vector3 pos1 = p1.transform.position;
        Vector3 pos2 = p2.transform.position;
        Vector3 toP2 = (pos2 - pos1);

        float seamY = tableTopY + 0.014f;

        // ── Determine primary relationship ──
        float verticalSep = Mathf.Abs(pos1.y - pos2.y);
        float horizSep    = new Vector2(toP2.x, toP2.z).magnitude;

        bool stacked    = verticalSep > 0.04f && verticalSep > horizSep * 0.6f;
        bool p1Standing = b1.size.y > Mathf.Max(b1.size.x, b1.size.z) * 1.3f;
        bool p2Standing = b2.size.y > Mathf.Max(b2.size.x, b2.size.z) * 1.3f;
        bool tJoint     = p1Standing ^ p2Standing; // exactly one is vertical

        if (stacked)
        {
            // One piece on top of another → seam runs around/across the overlap edge
            jointName = "Lap Joint";
            // Seam at the shared horizontal plane, along the shorter overlap width
            Vector3 overlapCenter = new Vector3(
                (b1.center.x + b2.center.x) * 0.5f,
                seamY,
                (b1.center.z + b2.center.z) * 0.5f);

            float overlapX = Mathf.Min(b1.size.x, b2.size.x) * 0.8f;
            float overlapZ = Mathf.Min(b1.size.z, b2.size.z) * 0.8f;

            // Seam along the longer overlap axis
            if (overlapX >= overlapZ)
            {
                seamStart = overlapCenter - new Vector3(overlapX * 0.5f, 0, 0);
                seamEnd   = overlapCenter + new Vector3(overlapX * 0.5f, 0, 0);
            }
            else
            {
                seamStart = overlapCenter - new Vector3(0, 0, overlapZ * 0.5f);
                seamEnd   = overlapCenter + new Vector3(0, 0, overlapZ * 0.5f);
            }
            seamStart.y = seamEnd.y = seamY;
        }
        else if (tJoint)
        {
            // T-Joint: flat piece + standing piece
            jointName = "T-Joint (Fillet)";
            MetalPiece flatPiece     = p1Standing ? p2 : p1;
            MetalPiece standingPiece = p1Standing ? p1 : p2;
            Bounds     flatB         = GetBounds(flatPiece);
            Bounds     standB        = GetBounds(standingPiece);

            // Seam runs along base of the standing piece, across flat piece surface
            Vector3 seamBase = new Vector3(standingPiece.transform.position.x,
                                            seamY,
                                            standingPiece.transform.position.z);
            // Direction along the flat piece's longer horizontal axis
            float dx = flatB.size.x, dz = flatB.size.z;
            float len = Mathf.Min(dx, dz, standB.size.x, standB.size.z);
            len = Mathf.Max(len, 0.12f) * 0.85f;

            Vector3 seamDir = (dx >= dz) ? Vector3.right : Vector3.forward;
            // Align to flat piece orientation
            if (flatPiece.transform.eulerAngles.y > 30f)
            {
                float yr = flatPiece.transform.eulerAngles.y * Mathf.Deg2Rad;
                seamDir = new Vector3(Mathf.Sin(yr), 0, Mathf.Cos(yr));
            }

            seamStart = seamBase - seamDir * len * 0.5f;
            seamEnd   = seamBase + seamDir * len * 0.5f;
            seamStart.y = seamEnd.y = seamY;
        }
        else
        {
            // Side-by-side (butt joint, V-groove, etc.)
            // Seam sits exactly at the midpoint gap, perpendicular to the line between pieces
            jointName = (p1.pieceType == PieceType.ThickSlab || p2.pieceType == PieceType.ThickSlab)
                         ? "V-Groove Joint" : "Butt Joint";

            Vector3 horizontal = new Vector3(toP2.x, 0, toP2.z).normalized;
            Vector3 perp       = Vector3.Cross(horizontal, Vector3.up).normalized;
            if (perp.magnitude < 0.01f) perp = Vector3.right;

            // Midpoint between the two closest facing edges
            Vector3 edge1 = pos1 + horizontal * (b1.extents.x * 0.8f);
            Vector3 edge2 = pos2 - horizontal * (b2.extents.x * 0.8f);
            Vector3 mid   = (edge1 + edge2) * 0.5f;
            mid.y = seamY;

            // Length = shorter piece's cross-width
            float len = Mathf.Max(
                Mathf.Min(b1.size.z, b2.size.z),
                Mathf.Min(b1.size.x, b2.size.x),
                0.12f) * 0.85f;

            seamStart = mid - perp * len * 0.5f;
            seamEnd   = mid + perp * len * 0.5f;
            seamStart.y = seamEnd.y = seamY;
        }
    }

    Bounds GetBounds(MetalPiece piece)
    {
        var rends = piece.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(piece.transform.position, Vector3.one * 0.15f);
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    // ──────────────────────────────────────────────────────────────
    // WELD ZONE VISUAL
    // ──────────────────────────────────────────────────────────────

    void CreateWeldZoneIndicator()
    {
        weldZoneIndicator = GameObject.CreatePrimitive(PrimitiveType.Quad);
        weldZoneIndicator.name = "WeldZone";
        float w    = tableSurfaceWidth > 0f ? tableSurfaceWidth : tableSizeX;
        float d    = tableSurfaceDepth > 0f ? tableSurfaceDepth : tableSizeZ;
        float yaw  = workbenchTransform != null ? workbenchTransform.eulerAngles.y : 0f;
        weldZoneIndicator.transform.position   = tableSurfaceCenter + tableSurfaceNormal * 0.001f;
        weldZoneIndicator.transform.rotation   = Quaternion.Euler(90f, yaw, 0f);
        weldZoneIndicator.transform.localScale = new Vector3(w, d, 1f);
        Destroy(weldZoneIndicator.GetComponent<Collider>());
        weldZoneIndicator.GetComponent<Renderer>().material =
            WeldingShaders.CreateTransparentLit(new Color(0.2f, 0.5f, 1f, 0.15f));
    }
}
