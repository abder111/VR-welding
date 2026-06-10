using UnityEngine;

/// <summary>
/// Runtime bootstrap. Creates the welding torch, parts shelf, welding table zone, HUD,
/// and a Clear Table button.
///
/// Uses Physics.Raycast from above to find the exact surface Y of the workbench
/// at runtime, so placement never falls to the floor regardless of scale differences.
/// </summary>
public class WeldingSetup : MonoBehaviour
{
    private GameObject workbench;
    private Vector3 tableCenter;
    private Vector3 forward;
    private Vector3 right;
    private float tableTopY;
    private float tableSizeX;
    private float tableSizeZ;

    private GameObject torchRoot;
    private GameObject hudObj;
    private WeldingTable weldingTable;

    private Material beadMat;

    void Awake()
    {
        workbench = GameObject.Find("Environment/Objects/workbench");
        if (workbench == null)
        {
            Debug.LogError("[WeldingSetup] Could not find 'Environment/Objects/workbench'!");
            return;
        }

        var meshCol = workbench.GetComponent<MeshCollider>();
        Bounds bounds = meshCol != null
            ? meshCol.bounds
            : new Bounds(workbench.transform.position, new Vector3(2.5f, 1f, 2.3f));

        SampleWorkbenchSurface(meshCol, bounds,
            out tableCenter, out tableTopY, out tableSizeX, out tableSizeZ,
            out Vector3 surfaceNormal, out Vector3 surfaceRight, out Vector3 surfaceForward,
            out float surfaceWidth, out float surfaceDepth);

        forward = workbench.transform.forward;
        right = workbench.transform.right;

        Debug.Log(string.Format(
            "[WeldingSetup] Table surface at Y={0:F3}, center=({1:F3},{2:F3},{3:F3}), footprint=({4:F3}x{5:F3})",
            tableTopY, tableCenter.x, tableCenter.y, tableCenter.z, surfaceWidth, surfaceDepth));

        beadMat = WeldingShaders.CreateLitEmissive(new Color(0.3f, 0.25f, 0.2f), 0.6f, 0.3f);

        var tableObj = new GameObject("WeldingTableZone");
        weldingTable = tableObj.AddComponent<WeldingTable>();
        weldingTable.tableSurfaceCenter = tableCenter;
        weldingTable.tableTopY = tableTopY;
        weldingTable.tableSizeX = tableSizeX;
        weldingTable.tableSizeZ = tableSizeZ;
        weldingTable.tableSurfaceNormal = surfaceNormal;
        weldingTable.tableSurfaceRight = surfaceRight;
        weldingTable.tableSurfaceForward = surfaceForward;
        weldingTable.tableSurfaceWidth = surfaceWidth;
        weldingTable.tableSurfaceDepth = surfaceDepth;
        weldingTable.beadMaterial = beadMat;
        weldingTable.workbenchTransform = workbench.transform;

        WeldingTorch torchScript = null;
        torchRoot = FindTorchObject();

        if (torchRoot != null)
        {
            torchScript = torchRoot.GetComponent<WeldingTorch>();
            if (torchScript == null)
                torchScript = torchRoot.AddComponent<WeldingTorch>();

            torchRoot.transform.position = tableCenter + right * (tableSizeX * 0.3f) + Vector3.up * 0.08f;
            ConfigureTorch(torchRoot, torchScript);

            weldingTable.torch = torchScript;
            Debug.Log("[WeldingSetup] Torch found and configured at: " + torchRoot.transform.position);
        }
        else
        {
            Debug.LogWarning("[WeldingSetup] Torch not found - pieces will still spawn on shelves!");
        }

        var shelvesManagerGo = new GameObject("ShelvesManager");
        var shelvesManager = shelvesManagerGo.AddComponent<ShelvesManager>();
        shelvesManager.weldingTable = weldingTable;
        shelvesManager.InitializeShelves();

        hudObj = new GameObject("WeldingHUD");
        var hud = hudObj.AddComponent<WeldingHUD>();
        Vector3 hudPos = tableCenter + Vector3.up * 0.7f;
        hud.Initialize(null, torchScript, hudPos);
        hud.SetWeldingTable(weldingTable);
        weldingTable.hud = hud;

        Vector3 clearBtnPos = tableCenter
            - forward * (tableSizeZ * 0.42f)
            + right * (tableSizeX * 0.3f)
            + Vector3.up * 0.02f;
        CreateClearButton(clearBtnPos);

        if (torchRoot != null)
            Debug.Log("[WeldingSetup] Done. Torch at: " + torchRoot.transform.position + " | Table center: " + tableCenter);
        else
            Debug.Log("[WeldingSetup] Done without torch. Table center: " + tableCenter);

        Debug.Log("[WeldingSetup] Pick up parts from shelves A/B/C/D, place them on the glowing table zone, then weld!");
    }

    static GameObject FindTorchObject()
    {
        string[] paths =
        {
            "Environment/Objects/torch",
            "Environment/Objects/tortche",
            "torch",
            "tortche"
        };

        foreach (var path in paths)
        {
            var found = GameObject.Find(path);
            if (found != null)
                return found;
        }

        return null;
    }

    static void ConfigureTorch(GameObject torchGo, WeldingTorch torchScript)
    {
        var rb = torchGo.GetComponent<Rigidbody>();
        if (rb == null)
            rb = torchGo.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (torchGo.GetComponent<Collider>() == null)
        {
            var col = torchGo.AddComponent<BoxCollider>();
            var renderer = torchGo.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Bounds b = renderer.bounds;
                col.center = torchGo.transform.InverseTransformPoint(b.center);
                col.size = torchGo.transform.InverseTransformVector(b.size);
                col.size = new Vector3(Mathf.Abs(col.size.x), Mathf.Abs(col.size.y), Mathf.Abs(col.size.z));
            }
            else
            {
                col.size = new Vector3(0.08f, 0.08f, 0.25f);
                col.center = new Vector3(0f, 0f, 0.1f);
            }
        }

        Transform tip = GetOrCreateTorchTip(torchGo.transform);
        torchScript.torchTip = tip;

        var vfx = torchGo.GetComponent<WeldingVFX>();
        if (vfx == null)
            vfx = torchGo.AddComponent<WeldingVFX>();
        torchScript.vfx = vfx;
        SetupTorchVfx(tip, vfx);
    }

    static Transform GetOrCreateTorchTip(Transform torchTransform)
    {
        var existing = torchTransform.Find("TorchTip");
        if (existing != null)
            return existing;

        var tipGo = new GameObject("TorchTip");
        tipGo.transform.SetParent(torchTransform, false);

        var meshFilter = torchTransform.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Bounds localBounds = meshFilter.sharedMesh.bounds;
            tipGo.transform.localPosition = new Vector3(
                localBounds.center.x,
                localBounds.max.y,
                localBounds.center.z);
        }
        else
        {
            tipGo.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        }

        return tipGo.transform;
    }

    static void SetupTorchVfx(Transform tip, WeldingVFX vfx)
    {
        var sparksTransform = tip.Find("SparkParticles");
        ParticleSystem sparks;
        if (sparksTransform == null)
        {
            var sparksGo = new GameObject("SparkParticles");
            sparksGo.transform.SetParent(tip, false);
            sparksGo.transform.localPosition = Vector3.zero;
            sparks = sparksGo.AddComponent<ParticleSystem>();
            ConfigureSparkParticles(sparks);
        }
        else
        {
            sparks = sparksTransform.GetComponent<ParticleSystem>();
            if (sparks == null)
                sparks = sparksTransform.gameObject.AddComponent<ParticleSystem>();
            ConfigureSparkParticles(sparks);
        }

        var lightTransform = tip.Find("ArcLight");
        Light arcLight;
        if (lightTransform == null)
        {
            var lightGo = new GameObject("ArcLight");
            lightGo.transform.SetParent(tip, false);
            lightGo.transform.localPosition = Vector3.zero;
            arcLight = lightGo.AddComponent<Light>();
            arcLight.type = LightType.Point;
            arcLight.range = 0.6f;
            arcLight.intensity = 2f;
            arcLight.color = new Color(0.6f, 0.8f, 1f);
        }
        else
        {
            arcLight = lightTransform.GetComponent<Light>();
            if (arcLight == null)
                arcLight = lightTransform.gameObject.AddComponent<Light>();
        }

        arcLight.enabled = false;
        vfx.Initialize(sparks, arcLight);
    }

    static void ConfigureSparkParticles(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = 0.12f;
        main.startSpeed = 1.5f;
        main.startSize = 0.008f;
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 35f;
        emission.enabled = false;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.004f;
    }

    /// <summary>
    /// Measures the workbench top-surface center, height, and footprint.
    /// Raycasts only the workbench mesh (ignores props above it like the welding mask).
    /// </summary>
    void SampleWorkbenchSurface(
        MeshCollider meshCol, Bounds bounds,
        out Vector3 center, out float topY, out float sizeX, out float sizeZ,
        out Vector3 normal, out Vector3 axisRight, out Vector3 axisForward,
        out float surfaceWidth, out float surfaceDepth)
    {
        normal = Vector3.up;
        float yawRad = workbench.transform.eulerAngles.y * Mathf.Deg2Rad;
        axisRight = new Vector3(-Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
        axisForward = new Vector3(Mathf.Cos(yawRad), 0f, Mathf.Sin(yawRad));

        topY = bounds.max.y;
        center = new Vector3(bounds.center.x, topY, bounds.center.z);

        // Footprint along workbench local axes (mesh X/Y are the table top after mesh import rotation)
        if (meshCol != null && meshCol.sharedMesh != null)
        {
            Vector3 meshSize = meshCol.sharedMesh.bounds.size;
            Vector3 scale = workbench.transform.lossyScale;
            surfaceWidth = meshSize.x * scale.x;
            surfaceDepth = meshSize.y * scale.y;
        }
        else
        {
            surfaceWidth = bounds.size.x;
            surfaceDepth = bounds.size.z;
        }

        sizeX = surfaceWidth;
        sizeZ = surfaceDepth;

        if (meshCol == null)
            return;

        float sumY = 0f;
        Vector3 normalSum = Vector3.zero;
        int hitCount = 0;

        const int steps = 16;
        for (int i = 0; i <= steps; i++)
        {
            for (int j = 0; j <= steps; j++)
            {
                float x = Mathf.Lerp(bounds.min.x, bounds.max.x, i / (float)steps);
                float z = Mathf.Lerp(bounds.min.z, bounds.max.z, j / (float)steps);
                Vector3 rayOrigin = new Vector3(x, bounds.max.y + 0.5f, z);

                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2f))
                    continue;
                if (!IsWorkbenchCollider(hit.collider))
                    continue;

                sumY += hit.point.y;
                normalSum += hit.normal;
                hitCount++;
            }
        }

        if (hitCount == 0)
            return;

        normal = (normalSum / hitCount).normalized;
        topY = sumY / hitCount;
        center = new Vector3(bounds.center.x, topY, bounds.center.z);

        if (normal.y < 0.9f)
        {
            axisRight = Vector3.ProjectOnPlane(workbench.transform.right, normal).normalized;
            axisForward = Vector3.Cross(normal, axisRight).normalized;
        }
    }

    bool IsWorkbenchCollider(Collider col)
    {
        if (col == null || workbench == null)
            return false;

        return col.gameObject == workbench || col.transform.IsChildOf(workbench.transform);
    }

    void CreateClearButton(Vector3 position)
    {
        var btn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        btn.name = "ClearTableButton";
        btn.transform.position = position;
        btn.transform.rotation = Quaternion.identity;
        btn.transform.localScale = new Vector3(0.08f, 0.015f, 0.08f);

        var mat = WeldingShaders.CreateLit(new Color(0.9f, 0.15f, 0.1f), 0.2f, 0.4f);
        btn.GetComponent<Renderer>().material = mat;
        btn.GetComponent<Collider>().isTrigger = true;

        var jb = btn.AddComponent<JointButton>();
        jb.onClick = () => weldingTable.ClearTable();

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(btn.transform);
        labelGo.transform.localPosition = new Vector3(0f, 2f, 0f);
        labelGo.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
        labelGo.transform.localScale = new Vector3(0.6f, 6f, 0.6f);
        var tm = labelGo.AddComponent<TextMesh>();
        tm.text = "CLEAR\nTABLE";
        tm.characterSize = 0.04f;
        tm.fontSize = 24;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
    }
}
