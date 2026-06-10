using UnityEngine;

/// <summary>
/// Core welding torch controller.
/// Handles grab detection (custom grab via OVRInput + keyboard fallback),
/// trigger input for welding, distance-based seam detection, and audio.
/// VFX (fire/arc light) activates immediately on trigger press while grabbed,
/// regardless of seam proximity.
/// </summary>
public class WeldingTorch : MonoBehaviour
{
    [HideInInspector] public Transform torchTip;
    [HideInInspector] public WeldSeam weldSeam;
    [HideInInspector] public WeldingVFX vfx;

    private Rigidbody rb;
    private Transform leftControllerAnchor;
    private Transform rightControllerAnchor;
    private Transform currentGrabAnchor;
    private AudioSource audioSource;

    private bool _isGrabbed = false;
    public bool IsGrabbed { get { return _isGrabbed; } }
    public bool IsWelding { get; private set; }

    // Tuning parameters
    private float grabRange = 0.25f;       // How close controller must be to grab
    private float weldRange = 0.06f;       // How close tip must be to seam to deposit beads
    private float depositCooldown = 0.05f; // Seconds between bead deposits
    private float lastDepositTime = 0f;

    // Tracking for score
    private Vector3 lastTipPosition;
    private float currentSpeed = 0f;

    private Vector3 originalPosition;
    private Quaternion originalRotation;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.useGravity = true;
        }

        originalPosition = transform.position;
        originalRotation = transform.rotation;

        // Find controller anchors in the OVR camera rig
        var leftAnchor  = GameObject.Find("Player/Camera Rig/TrackingSpace/LeftHandAnchor/LeftControllerAnchor");
        var rightAnchor = GameObject.Find("Player/Camera Rig/TrackingSpace/RightHandAnchor/RightControllerAnchor");

        if (leftAnchor  != null) leftControllerAnchor  = leftAnchor.transform;
        if (rightAnchor != null) rightControllerAnchor = rightAnchor.transform;

        // Setup audio for welding buzz
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = CreateWeldingBuzz();
        audioSource.loop = true;
        audioSource.volume = 0.3f;
        audioSource.spatialBlend = 1f;
        audioSource.playOnAwake = false;
    }

    void Update()
    {
        HandleGrab();

        if (_isGrabbed)
        {
            HandleWelding();
        }
    }

    // ─────────────────────────────────────────────
    // GRAB LOGIC
    // Uses OVRInput grip button + E key fallback.
    // ─────────────────────────────────────────────

    void HandleGrab()
    {
        bool vrGripPressed = false;
        try
        {
            vrGripPressed = OVRInput.Get(OVRInput.RawAxis1D.RHandTrigger) > 0.5f ||
                            OVRInput.Get(OVRInput.RawAxis1D.LHandTrigger) > 0.5f;
        }
        catch (System.Exception) { /* OVRInput not available */ }

        if (vrGripPressed && !_isGrabbed) TryGrab();
        else if (!vrGripPressed && _isGrabbed && !Input.GetKey(KeyCode.E)) Release();

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!_isGrabbed) TryGrab();
            else Release();
        }
    }

    void TryGrab()
    {
        Transform nearest = null;
        float nearestDist = grabRange;

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

        // Keyboard fallback: if no controller anchor in range, grab anyway
        if (nearest == null && rightControllerAnchor == null && leftControllerAnchor == null)
        {
            // Editor/keyboard mode: just grab directly to camera
            var cam = Camera.main;
            if (cam != null) Grab(cam.transform);
            else GrabInPlace();
            return;
        }

        if (nearest != null) Grab(nearest);
        else GrabInPlace(); // E pressed but nothing in range -> grab anyway for testing
    }

    void Grab(Transform anchor)
    {
        _isGrabbed = true;
        currentGrabAnchor = anchor;
        rb.isKinematic = true;
        rb.useGravity = false;
        transform.SetParent(anchor);
        transform.localPosition = new Vector3(0f, 0f, 0.08f);
        transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
    }

    // Fallback: grab in world space without parenting to an anchor
    void GrabInPlace()
    {
        _isGrabbed = true;
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Release()
    {
        _isGrabbed = false;
        currentGrabAnchor = null;
        transform.SetParent(null);
        rb.isKinematic = false;
        rb.useGravity = true;
        StopWeldingState();
    }

    // ─────────────────────────────────────────────
    // WELDING LOGIC
    //
    // TWO-PHASE behaviour:
    //   Phase 1 — Trigger pressed + torch grabbed:
    //             Fire VFX + audio IMMEDIATELY.
    //   Phase 2 — If a seam is assigned AND tip is
    //             within weldRange: deposit beads.
    // ─────────────────────────────────────────────

    void HandleWelding()
    {
        bool triggerPressed = false;

        try
        {
            triggerPressed = OVRInput.Get(OVRInput.RawAxis1D.RIndexTrigger) > 0.5f ||
                             OVRInput.Get(OVRInput.RawAxis1D.LIndexTrigger) > 0.5f;
        }
        catch (System.Exception) { /* OVRInput not available */ }

        triggerPressed = triggerPressed || Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space);

        if (triggerPressed)
        {
            // ── Phase 1: VFX fires immediately when trigger is held ──
            if (!IsWelding) StartWeldingState();

            // ── Phase 2: Bead deposit only when near a seam ──
            if (torchTip != null && weldSeam != null)
            {
                float distMoved = Vector3.Distance(torchTip.position, lastTipPosition);
                currentSpeed = Mathf.Lerp(currentSpeed, distMoved / Time.deltaTime, 0.1f);
                lastTipPosition = torchTip.position;

                float dist = weldSeam.DistanceFromSeam(torchTip.position);
                if (dist < weldRange)
                {
                    if (Time.time - lastDepositTime > depositCooldown)
                    {
                        weldSeam.TryDepositBead(torchTip.position, currentSpeed);
                        lastDepositTime = Time.time;
                    }
                }
            }
            else if (torchTip != null)
            {
                lastTipPosition = torchTip.position;
            }
        }
        else
        {
            // Trigger released — stop VFX
            if (IsWelding) StopWeldingState();
            if (torchTip != null) lastTipPosition = torchTip.position;
        }
    }

    void StartWeldingState()
    {
        IsWelding = true;
        if (vfx != null) vfx.StartWelding();
        if (audioSource != null && !audioSource.isPlaying) audioSource.Play();
    }

    void StopWeldingState()
    {
        IsWelding = false;
        if (vfx != null) vfx.StopWelding();
        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
    }

    // ─────────────────────────────────────────────
    // PROCEDURAL AUDIO
    // ─────────────────────────────────────────────

    AudioClip CreateWeldingBuzz()
    {
        int sampleRate = 22050;
        float duration  = 2f;
        int samples     = (int)(duration * sampleRate);
        float[] data    = new float[samples];

        System.Random rng = new System.Random(42);

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            data[i]  = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.25f;
            data[i] += Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.12f;
            data[i] += Mathf.Sin(2f * Mathf.PI *  60f * t) * 0.08f;
            data[i] += ((float)rng.NextDouble() * 2f - 1f) * 0.15f;
        }

        AudioClip clip = AudioClip.Create("WeldingBuzz", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
