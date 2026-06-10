using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Creates and manages the parts shelf near the workbench.
/// Displays one of each metal piece type and respawns taken pieces.
/// </summary>
public class PartsShelf : MonoBehaviour
{
    [HideInInspector] public WeldingTable weldingTable;

    private Material[] pieceMaterials;
    private Dictionary<PieceType, Transform> slotTransforms = new Dictionary<PieceType, Transform>();
    private Dictionary<PieceType, bool> slotOccupied = new Dictionary<PieceType, bool>();

    // Piece color palette (one per type)
    private static readonly Color[] PieceColors = new Color[]
    {
        new Color(0.55f, 0.55f, 0.58f), // FlatPlate - steel grey
        new Color(0.70f, 0.55f, 0.30f), // LBracket - copper/brass
        new Color(0.45f, 0.50f, 0.55f), // PipeSection - dark steel
        new Color(0.60f, 0.60f, 0.65f), // TBracket - light steel
        new Color(0.50f, 0.50f, 0.52f), // ThickSlab - heavy iron
        new Color(0.65f, 0.60f, 0.50f), // AngleIron - iron
    };

    private static readonly string[] PieceLabels = new string[]
    {
        "Flat Plate", "L-Bracket", "Pipe", "T-Bracket", "Thick Slab", "Angle Iron"
    };

    void Start()
    {
        CreateShelfStructure();
        PopulateShelf();
    }

    // ─────────────────────────────────────────────
    // SHELF STRUCTURE
    // ─────────────────────────────────────────────

    void CreateShelfStructure()
    {
        var shelfMat = WeldingShaders.CreateLit(new Color(0.35f, 0.25f, 0.15f), 0f, 0.3f);

        // Back panel
        var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
        back.name = "ShelfBack";
        back.transform.SetParent(transform);
        back.transform.localPosition = new Vector3(0f, 0.3f, -0.04f);
        back.transform.localScale = new Vector3(1.3f, 0.8f, 0.04f);
        back.GetComponent<Renderer>().material = shelfMat;
        Destroy(back.GetComponent<Collider>());

        // Three horizontal shelves
        float[] shelfYs = { 0f, 0.28f, 0.56f };
        foreach (float y in shelfYs)
        {
            var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = "ShelfBoard";
            shelf.transform.SetParent(transform);
            shelf.transform.localPosition = new Vector3(0f, y, 0f);
            shelf.transform.localScale = new Vector3(1.3f, 0.02f, 0.22f);
            shelf.GetComponent<Renderer>().material = shelfMat;
        }

        // Side panels
        foreach (float x in new float[] { -0.65f, 0.65f })
        {
            var side = GameObject.CreatePrimitive(PrimitiveType.Cube);
            side.name = "ShelfSide";
            side.transform.SetParent(transform);
            side.transform.localPosition = new Vector3(x, 0.28f, -0.02f);
            side.transform.localScale = new Vector3(0.04f, 0.8f, 0.26f);
            side.GetComponent<Renderer>().material = shelfMat;
            Destroy(side.GetComponent<Collider>());
        }

        // Label sign above shelf
        var signObj = new GameObject("ShelfSign");
        signObj.transform.SetParent(transform);
        signObj.transform.localPosition = new Vector3(0f, 0.78f, -0.02f);
        signObj.transform.localRotation = Quaternion.identity;
        var signText = signObj.AddComponent<TextMesh>();
        signText.text = "PARTS SHELF\nGrab a piece!";
        signText.characterSize = 0.05f;
        signText.fontSize = 36;
        signText.anchor = TextAnchor.UpperCenter;
        signText.alignment = TextAlignment.Center;
        signText.color = Color.white;

        // Pre-calculate slot positions: 6 pieces, 2 per shelf row
        // shelfYs are the LOCAL Y of each board top surface
        // Pieces sit ON TOP of each board (+0.03 so they rest above it)
        PieceType[] types = (PieceType[])System.Enum.GetValues(typeof(PieceType));
        float[] boardTops = { 0.01f, 0.29f, 0.57f }; // top of each board in local Y
        float[] xs = { -0.38f, 0.38f };               // two columns

        int slotIdx = 0;
        foreach (PieceType pt in types)
        {
            int row = slotIdx / 2;
            int col = slotIdx % 2;
            var slotGo = new GameObject("Slot_" + pt.ToString());
            slotGo.transform.SetParent(transform);
            // localPosition: X left/right, Y on top of board, Z toward front of shelf
            slotGo.transform.localPosition = new Vector3(xs[col], boardTops[Mathf.Min(row, 2)], 0.06f);
            slotGo.transform.localRotation = Quaternion.identity;
            slotTransforms[pt] = slotGo.transform;
            slotOccupied[pt] = false;
            slotIdx++;
        }
    }

    // ─────────────────────────────────────────────
    // PIECE SPAWNING
    // ─────────────────────────────────────────────

    void PopulateShelf()
    {
        PieceType[] types = (PieceType[])System.Enum.GetValues(typeof(PieceType));
        foreach (var t in types)
        {
            SpawnPiece(t);
        }
    }

    void SpawnPiece(PieceType type)
    {
        if (!slotTransforms.ContainsKey(type)) return;

        var slotTf = slotTransforms[type];
        int typeIdx = (int)type;
        Color pieceColor = PieceColors[typeIdx % PieceColors.Length];

        var pieceMat = WeldingShaders.CreateLit(pieceColor, 0.7f, 0.4f);

        var pieceGo = BuildPieceMesh(type, pieceMat);
        pieceGo.transform.position = slotTf.position;
        pieceGo.transform.rotation = slotTf.parent != null ? slotTf.parent.rotation : Quaternion.identity;

        // Rigidbody — kinematic while on shelf so it doesn't fall off
        var rb = pieceGo.AddComponent<Rigidbody>();
        rb.mass = 0.8f;
        rb.useGravity = false;
        rb.isKinematic = true;

        // MetalPiece component
        var mp = pieceGo.AddComponent<MetalPiece>();
        mp.pieceType = type;
        mp.weldingTable = weldingTable;
        mp.shelfPosition = slotTf.position;
        mp.shelfRotation = slotTf.rotation;
        mp.parentShelf = this;

        // Label
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(pieceGo.transform);
        labelGo.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        labelGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var tm = labelGo.AddComponent<TextMesh>();
        tm.text = PieceLabels[typeIdx % PieceLabels.Length];
        tm.characterSize = 0.025f;
        tm.fontSize = 24;
        tm.anchor = TextAnchor.LowerCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.yellow;

        slotOccupied[type] = true;
    }

    // ─────────────────────────────────────────────
    // PIECE MESH BUILDERS
    // ─────────────────────────────────────────────

    GameObject BuildPieceMesh(PieceType type, Material mat)
    {
        switch (type)
        {
            case PieceType.FlatPlate:     return BuildFlatPlate(mat);
            case PieceType.LBracket:      return BuildLBracket(mat);
            case PieceType.PipeSection:   return BuildPipe(mat);
            case PieceType.TBracket:      return BuildTBracket(mat);
            case PieceType.ThickSlab:     return BuildThickSlab(mat);
            case PieceType.AngleIron:     return BuildAngleIron(mat);
            default:                      return BuildFlatPlate(mat);
        }
    }

    GameObject BuildFlatPlate(Material mat)
    {
        var go = new GameObject("FlatPlate");
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(go.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.18f, 0.012f, 0.28f);
        body.GetComponent<Renderer>().material = mat;
        // Use body collider
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(0.18f, 0.012f, 0.28f);
        Destroy(body.GetComponent<Collider>());
        return go;
    }

    GameObject BuildLBracket(Material mat)
    {
        var go = new GameObject("LBracket");
        // Horizontal arm
        var arm1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arm1.transform.SetParent(go.transform);
        arm1.transform.localPosition = new Vector3(0f, 0f, 0f);
        arm1.transform.localScale = new Vector3(0.16f, 0.012f, 0.12f);
        arm1.GetComponent<Renderer>().material = mat;
        Destroy(arm1.GetComponent<Collider>());
        // Vertical arm
        var arm2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arm2.transform.SetParent(go.transform);
        arm2.transform.localPosition = new Vector3(-0.074f, 0.055f, 0f);
        arm2.transform.localScale = new Vector3(0.012f, 0.1f, 0.12f);
        arm2.GetComponent<Renderer>().material = mat;
        Destroy(arm2.GetComponent<Collider>());
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(0.16f, 0.1f, 0.12f);
        col.center = new Vector3(0f, 0.03f, 0f);
        return go;
    }

    GameObject BuildPipe(Material mat)
    {
        var go = new GameObject("PipeSection");
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.transform.SetParent(go.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Lay on side
        body.transform.localScale = new Vector3(0.06f, 0.1f, 0.06f);
        body.GetComponent<Renderer>().material = mat;
        Destroy(body.GetComponent<Collider>());
        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.03f;
        col.height = 0.2f;
        col.direction = 2; // Z axis
        return go;
    }

    GameObject BuildTBracket(Material mat)
    {
        var go = new GameObject("TBracket");
        // Horizontal base
        var base1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        base1.transform.SetParent(go.transform);
        base1.transform.localPosition = new Vector3(0f, 0f, 0f);
        base1.transform.localScale = new Vector3(0.18f, 0.012f, 0.1f);
        base1.GetComponent<Renderer>().material = mat;
        Destroy(base1.GetComponent<Collider>());
        // Vertical stem
        var stem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stem.transform.SetParent(go.transform);
        stem.transform.localPosition = new Vector3(0f, 0.055f, 0f);
        stem.transform.localScale = new Vector3(0.012f, 0.1f, 0.1f);
        stem.GetComponent<Renderer>().material = mat;
        Destroy(stem.GetComponent<Collider>());
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(0.18f, 0.1f, 0.1f);
        col.center = new Vector3(0f, 0.03f, 0f);
        return go;
    }

    GameObject BuildThickSlab(Material mat)
    {
        var go = new GameObject("ThickSlab");
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(go.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.16f, 0.035f, 0.22f);
        body.GetComponent<Renderer>().material = mat;
        Destroy(body.GetComponent<Collider>());
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(0.16f, 0.035f, 0.22f);
        return go;
    }

    GameObject BuildAngleIron(Material mat)
    {
        var go = new GameObject("AngleIron");
        // Horizontal flange
        var flange1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flange1.transform.SetParent(go.transform);
        flange1.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        flange1.transform.localScale = new Vector3(0.2f, 0.01f, 0.08f);
        flange1.GetComponent<Renderer>().material = mat;
        Destroy(flange1.GetComponent<Collider>());
        // Vertical flange (thin wall going up)
        var flange2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flange2.transform.SetParent(go.transform);
        flange2.transform.localPosition = new Vector3(0f, 0.04f, 0.01f);
        flange2.transform.localScale = new Vector3(0.2f, 0.07f, 0.01f);
        flange2.GetComponent<Renderer>().material = mat;
        Destroy(flange2.GetComponent<Collider>());
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(0.2f, 0.08f, 0.1f);
        col.center = new Vector3(0f, 0.02f, 0.03f);
        return go;
    }

    // ─────────────────────────────────────────────
    // RESPAWN
    // ─────────────────────────────────────────────

    public void OnPieceTaken(PieceType type)
    {
        slotOccupied[type] = false;
        StartCoroutine(RespawnAfterDelay(type, 3f));
    }

    IEnumerator RespawnAfterDelay(PieceType type, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!slotOccupied[type])
            SpawnPiece(type);
    }
}
