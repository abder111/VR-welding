using UnityEngine;

public enum PieceType
{
    FlatPlate,
    LBracket,
    PipeSection,
    TBracket,
    ThickSlab,
    AngleIron
}

/// <summary>
/// Attached to each grabbable metal piece.
/// Handles grab (E key / Quest grip), physics, snap-to-table placement,
/// and un-placement (grab again from the table to reposition).
/// </summary>
public class MetalPiece : MonoBehaviour
{
    [HideInInspector] public PieceType pieceType;
    [HideInInspector] public WeldingTable weldingTable;

    private Rigidbody rb;
    private bool _isGrabbed = false;
    private Transform grabAnchor;
    private Vector3 grabOffset;
    private Quaternion grabRotOffset;

    private bool _isPlaced = false;
    private float snapRange = 0.35f;
    private Renderer[] renderers;

    private bool _isHighlighted = false;
    private Color baseColor;
    private Color highlightColor = new Color(0.5f, 1f, 0.5f);

    private Transform rightControllerAnchor;
    private Transform leftControllerAnchor;

    [HideInInspector] public Vector3 shelfPosition;
    [HideInInspector] public Quaternion shelfRotation;
    [HideInInspector] public PartsShelf parentShelf;
    [HideInInspector] public ShelvesManager shelvesManager;

    public bool IsPlaced  => _isPlaced;
    public bool IsGrabbed => _isGrabbed;

    void Start()
    {
        rb        = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();

        if (renderers.Length > 0)
            baseColor = renderers[0].material.color;

        var rightAnchor = GameObject.Find("Player/Camera Rig/TrackingSpace/RightHandAnchor/RightControllerAnchor");
        var leftAnchor  = GameObject.Find("Player/Camera Rig/TrackingSpace/LeftHandAnchor/LeftControllerAnchor");
        if (rightAnchor != null) rightControllerAnchor = rightAnchor.transform;
        if (leftAnchor  != null) leftControllerAnchor  = leftAnchor.transform;
    }

    void Update()
    {
        // NOTE: no early-return when _isPlaced — pieces on the table are still grabbable.
        HandleGrab();

        if (_isGrabbed)
        {
            MoveWithHand();

            if (!_isPlaced)
                CheckTableProximity();
        }
    }

    // ─────────────────────────────────────────────
    // GRAB LOGIC
    // ─────────────────────────────────────────────

    void HandleGrab()
    {
        bool vrGrip = false;
        try
        {
            vrGrip = OVRInput.Get(OVRInput.RawAxis1D.RHandTrigger) > 0.5f ||
                     OVRInput.Get(OVRInput.RawAxis1D.LHandTrigger) > 0.5f;
        }
        catch { }

        if (vrGrip && !_isGrabbed) TryGrab();
        else if (!vrGrip && _isGrabbed && !Input.GetKey(KeyCode.E)) Release();

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!_isGrabbed) TryGrab();
            else             Release();
        }
    }

    void TryGrab()
    {
        Transform nearest    = null;
        float     nearestDist = 0.4f;

        if (rightControllerAnchor != null)
        {
            float d = Vector3.Distance(transform.position, rightControllerAnchor.position);
            if (d < nearestDist) { nearest = rightControllerAnchor; nearestDist = d; }
        }
        if (leftControllerAnchor != null)
        {
            float d = Vector3.Distance(transform.position, leftControllerAnchor.position);
            if (d < nearestDist) { nearest = leftControllerAnchor; nearestDist = d; }
        }

        // Keyboard / editor fallback: no controller anchors present, grab via camera
        if (nearest == null && rightControllerAnchor == null && leftControllerAnchor == null)
        {
            var cam = Camera.main;
            nearest = cam != null ? cam.transform : null;
        }

        if (nearest == null) return; // nothing to attach to

        // If the piece was snapped to the table, lift it off first
        if (_isPlaced) LiftFromTable();

        grabAnchor    = nearest;
        grabOffset    = nearest.InverseTransformPoint(transform.position);
        grabRotOffset = Quaternion.Inverse(nearest.rotation) * transform.rotation;
        _isGrabbed    = true;
        rb.isKinematic = true;
        rb.useGravity  = false;
    }

    void Release()
    {
        _isGrabbed = false;
        grabAnchor = null;
        rb.isKinematic = false;
        rb.useGravity  = true;
        SetHighlight(false);

        if (weldingTable != null && weldingTable.IsAboveTable(transform.position, snapRange))
            SnapToTable();
    }

    void MoveWithHand()
    {
        if (grabAnchor == null) return;
        transform.position = grabAnchor.TransformPoint(grabOffset);
        transform.rotation = grabAnchor.rotation * grabRotOffset;
    }

    // ─────────────────────────────────────────────
    // TABLE SNAP / LIFT
    // ─────────────────────────────────────────────

    void CheckTableProximity()
    {
        if (weldingTable == null) return;
        bool near = weldingTable.IsAboveTable(transform.position, snapRange);
        SetHighlight(near);
    }

    void SnapToTable()
    {
        if (weldingTable == null) return;
        Vector3 snappedPos = weldingTable.GetSnapPosition(transform.position);
        transform.position = snappedPos;

        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);

        rb.isKinematic = true;
        rb.useGravity  = false;
        _isPlaced      = true;

        SetHighlight(false);
        StartCoroutine(PlacementFlash());
        weldingTable.OnPiecePlaced(this);

        if (shelvesManager != null)
            shelvesManager.OnPieceTaken(pieceType, shelfPosition, shelfRotation);
        else if (parentShelf != null)
            parentShelf.OnPieceTaken(pieceType);
    }

    /// <summary>
    /// Lift the piece off the table without destroying the seam or
    /// clearing the other piece — lets the player reposition freely.
    /// </summary>
    void LiftFromTable()
    {
        if (!_isPlaced) return;
        _isPlaced = false;
        rb.isKinematic = false;
        rb.useGravity  = false; // gravity re-enabled on Release()

        // Tell the table this piece left so it can clear the seam
        if (weldingTable != null)
            weldingTable.OnPieceLifted(this);
    }

    /// <summary>Called by WeldingTable.ClearTable() to reset physics state.</summary>
    public void RemoveFromTable()
    {
        _isPlaced      = false;
        rb.isKinematic = false;
        rb.useGravity  = true;
        SetHighlight(false);
    }

    // ─────────────────────────────────────────────
    // VISUAL HELPERS
    // ─────────────────────────────────────────────

    void SetHighlight(bool on)
    {
        if (_isHighlighted == on) return;
        _isHighlighted = on;
        if (renderers == null) return;
        foreach (var r in renderers)
            if (r != null && r.material != null)
                r.material.color = on ? highlightColor : baseColor;
    }

    System.Collections.IEnumerator PlacementFlash()
    {
        foreach (var r in renderers)
            if (r != null && r.material != null)
                r.material.color = Color.green;

        yield return new WaitForSeconds(0.3f);

        foreach (var r in renderers)
            if (r != null && r.material != null)
                r.material.color = baseColor;
    }

    public void RefreshRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }
}
