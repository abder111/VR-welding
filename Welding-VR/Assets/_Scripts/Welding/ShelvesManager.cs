using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Populates existing shelf objects (A, B, C, D) with parts pieces
/// </summary>
public class ShelvesManager : MonoBehaviour
{
    public WeldingTable weldingTable;
    
    private Dictionary<PieceType, Transform> slotTransforms = new Dictionary<PieceType, Transform>();
    
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

    public void InitializeShelves()
    {
        // Look for existing shelves A, B, C, D
        GameObject[] shelves = new GameObject[]
        {
            GameObject.Find("Environment/Objects/BoardShelf A"),
            GameObject.Find("Environment/Objects/BoardShelf B"),
            GameObject.Find("Environment/Objects/BoardShelf C"),
            GameObject.Find("Environment/Objects/BoardShelf D"),
        };

        Debug.Log("[ShelvesManager] Looking for shelves...");
        foreach (var shelf in shelves)
        {
            Debug.Log("[ShelvesManager] Shelf found: " + (shelf != null ? shelf.name : "NULL"));
        }

        int totalPiecesSpawned = 0;
        PieceType[] pieceTypes = (PieceType[])System.Enum.GetValues(typeof(PieceType));
        int pieceTypeIndex = 0;

        // Spawn multiple pieces on each shelf (at different stages/positions)
        foreach (GameObject shelf in shelves)
        {
            if (shelf == null)
            {
                Debug.LogWarning("[ShelvesManager] Shelf is null, skipping...");
                continue;
            }

            // Spawn 3 pieces per shelf at different positions along the shelf
            // Spacing them along the Z axis (or X axis depending on shelf orientation)
            for (int stage = 0; stage < 3; stage++)
            {
                // Calculate position at different stages
                float stageOffset = (stage - 1) * 0.15f; // -0.15, 0.0, +0.15
                Vector3 stagePosOffset = shelf.transform.forward * stageOffset;
                Vector3 shelfTopPos = shelf.transform.position + stagePosOffset + Vector3.up * 0.5f;
                
                PieceType pieceType = pieceTypes[pieceTypeIndex % pieceTypes.Length];
                
                SpawnPieceOnShelf(pieceType, shelfTopPos, shelf.transform);
                Debug.Log("[ShelvesManager] Spawned " + pieceType + " on shelf " + shelf.name + " stage " + stage + " at " + shelfTopPos);
                
                pieceTypeIndex++;
                totalPiecesSpawned++;
            }
        }

        Debug.Log("[ShelvesManager] Done! Populated shelves A, B, C, D with " + totalPiecesSpawned + " pieces total");
    }

    void SpawnPieceOnShelf(PieceType type, Vector3 worldPosition, Transform shelfParent)
    {
        int typeIdx = (int)type;
        Color pieceColor = PieceColors[typeIdx % PieceColors.Length];

        var pieceMat = WeldingShaders.CreateLit(pieceColor, 0.7f, 0.4f);

        var pieceGo = BuildPieceMesh(type, pieceMat);
        pieceGo.name = type.ToString() + "_Piece";
        pieceGo.transform.position = worldPosition;
        pieceGo.transform.rotation = shelfParent.rotation;

        // Rigidbody — allow physics interaction
        var rb = pieceGo.AddComponent<Rigidbody>();
        rb.mass = 0.8f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // MetalPiece component for welding system
        var mp = pieceGo.AddComponent<MetalPiece>();
        mp.pieceType = type;
        mp.weldingTable = weldingTable;
        mp.shelvesManager = this;
        mp.shelfPosition = worldPosition;
        mp.shelfRotation = shelfParent.rotation;

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
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(0.18f, 0.012f, 0.28f);
        Destroy(body.GetComponent<Collider>());
        return go;
    }

    GameObject BuildLBracket(Material mat)
    {
        var go = new GameObject("LBracket");
        var arm1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arm1.transform.SetParent(go.transform);
        arm1.transform.localPosition = new Vector3(0f, 0f, 0f);
        arm1.transform.localScale = new Vector3(0.16f, 0.012f, 0.12f);
        arm1.GetComponent<Renderer>().material = mat;
        Destroy(arm1.GetComponent<Collider>());
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
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale = new Vector3(0.06f, 0.1f, 0.06f);
        body.GetComponent<Renderer>().material = mat;
        Destroy(body.GetComponent<Collider>());
        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.03f;
        col.height = 0.2f;
        col.direction = 2;
        return go;
    }

    GameObject BuildTBracket(Material mat)
    {
        var go = new GameObject("TBracket");
        var base1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        base1.transform.SetParent(go.transform);
        base1.transform.localPosition = new Vector3(0f, 0f, 0f);
        base1.transform.localScale = new Vector3(0.18f, 0.012f, 0.1f);
        base1.GetComponent<Renderer>().material = mat;
        Destroy(base1.GetComponent<Collider>());
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
        var flange1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flange1.transform.SetParent(go.transform);
        flange1.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        flange1.transform.localScale = new Vector3(0.2f, 0.01f, 0.08f);
        flange1.GetComponent<Renderer>().material = mat;
        Destroy(flange1.GetComponent<Collider>());
        var flange2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flange2.transform.SetParent(go.transform);
        flange2.transform.localPosition = new Vector3(0f, 0.04f, 0.01f);
        flange2.transform.localScale = new Vector3(0.01f, 0.08f, 0.08f);
        flange2.GetComponent<Renderer>().material = mat;
        Destroy(flange2.GetComponent<Collider>());
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(0.2f, 0.08f, 0.08f);
        col.center = new Vector3(0f, 0.02f, 0.03f);
        return go;
    }

    public void OnPieceTaken(PieceType type, Vector3 worldPosition, Quaternion worldRotation)
    {
        StartCoroutine(RespawnAfterDelay(type, worldPosition, worldRotation, 3f));
    }

    IEnumerator RespawnAfterDelay(PieceType type, Vector3 worldPosition, Quaternion worldRotation, float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnPieceAt(type, worldPosition, worldRotation);
    }

    void SpawnPieceAt(PieceType type, Vector3 worldPosition, Quaternion worldRotation)
    {
        int typeIdx = (int)type;
        Color pieceColor = PieceColors[typeIdx % PieceColors.Length];
        var pieceMat = WeldingShaders.CreateLit(pieceColor, 0.7f, 0.4f);

        var pieceGo = BuildPieceMesh(type, pieceMat);
        pieceGo.name = type.ToString() + "_Piece";
        pieceGo.transform.position = worldPosition;
        pieceGo.transform.rotation = worldRotation;

        var rb = pieceGo.AddComponent<Rigidbody>();
        rb.mass = 0.8f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var mp = pieceGo.AddComponent<MetalPiece>();
        mp.pieceType = type;
        mp.weldingTable = weldingTable;
        mp.shelvesManager = this;
        mp.shelfPosition = worldPosition;
        mp.shelfRotation = worldRotation;

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
    }
}
