using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Skyrim-style item inspector. Double-click a bag slot to open: the item's 3D model
/// fills the screen on a darkened background while the game stays paused, and you hold
/// the left mouse button and drag to turn it any direction. Scroll to zoom. Double-click
/// again, or press I, to return to the bag.
///
/// The whole rig (an isolated camera, a render texture, lights, and the on-screen panel)
/// is built in code at runtime, the same self-building pattern the Hallucination, Pulse,
/// and Lighting systems use, so no scene wiring is needed. The only project requirement
/// is a layer named "ExamineItem", which the editor script ExamineLayerSetup creates
/// automatically.
///
/// Nothing here touches PlayerMovement or the player. It reuses each item's existing
/// worldPrefab as the inspect model (a clean, physics-stripped copy), and falls back to
/// the 2D icon for items that have no worldPrefab yet.
/// </summary>
public class ItemExamineController : MonoBehaviour
{
    /// <summary>True while the inspect view is open. The bag checks this to hand over input.</summary>
    public static bool IsExamining { get; private set; }

    /// <summary>
    /// True only on the exact frame the inspector closed itself (via I or double-click).
    /// The bag reads this so the same key or click that left the inspector does not also
    /// fall through and toggle the bag in the same frame. Update order between the two
    /// components is not guaranteed, so this is what makes I reliably return to the grid.
    /// </summary>
    public static bool ConsumedCloseThisFrame => Time.frameCount == closedOnFrame;
    private static int closedOnFrame = -1;

    [Header("Layer")]
    [Tooltip("Dedicated layer the inspect camera renders. Created automatically by ExamineLayerSetup")]
    [SerializeField] private string examineLayerName = "ExamineItem";

    [Header("Framing")]
    [Tooltip("Camera field of view. Lower looks more 'telephoto' and flattens perspective, Skyrim-like")]
    [SerializeField] private float fieldOfView = 30f;
    [Tooltip("How much of the view the item fills at default zoom. 1 = touches the edges, lower = more breathing room")]
    [Range(0.3f, 1f)]
    [SerializeField] private float fillFraction = 0.7f;

    [Header("Rotation / Zoom")]
    [SerializeField] private float dragRotateSpeed = 220f;
    [Tooltip("Slow auto-spin (degrees/sec) while you are NOT dragging. Set 0 for a dead-still item")]
    [SerializeField] private float idleSpinSpeed = 10f;
    [SerializeField] private float zoomSpeed = 0.15f;
    [Tooltip("How far you can zoom IN, as a multiplier on the auto-fit distance. Lower lets you get closer. 1 is the default framing. The camera is also stopped from entering the model")]
    [SerializeField] private float minZoom = 0.5f;
    [Tooltip("How far you can zoom OUT, as a multiplier on the auto-fit distance. Higher lets you pull back further (item looks smaller). The item is kept on-screen and never clipped away")]
    [SerializeField] private float maxZoom = 2f;
    [Tooltip("How quickly zoom eases toward the target. Higher = snappier, lower = slower and smoother. This is what makes scrolling glide instead of stepping")]
    [SerializeField] private float zoomSmoothing = 12f;

    [Header("Lights (isolated to the item)")]
    [SerializeField] private float keyLightIntensity = 1.4f;
    [SerializeField] private float fillLightIntensity = 0.6f;
    [SerializeField] private Color lightColor = new Color(1f, 0.97f, 0.9f, 1f);

    [Header("Background")]
    [Range(0f, 1f)]
    [SerializeField] private float backgroundDarken = 0.88f;

    [Header("Inspect Background Particle")]
    [Tooltip("Optional particle effect that plays BEHIND the item in the 3D inspect view, as a backdrop. Assign a looping ParticleSystem prefab, or leave empty for none. This is separate from each item's world-attract glow. Forced onto unscaled time in code so it animates while the bag is paused")]
    [SerializeField] private GameObject inspectBackgroundParticle;
    [Tooltip("How far behind the item the backdrop particle sits. Increase if a large item overlaps it")]
    [SerializeField] private float backgroundDistance = 3f;

    [Header("Labels")]
    [SerializeField] private bool showName = true;
    [SerializeField] private bool showHint = true;
    [SerializeField] private string hintText = "Drag to rotate     Scroll to zoom     Double-click or I to close";

    // --- runtime rig (all built in code) ---
    private static ItemExamineController instance;
    private Camera examineCamera;
    private RenderTexture renderTexture;
    private Transform rigRoot;       // sits far from the world
    private Transform modelHolder;   // the thing the player rotates
    private GameObject currentModel;
    private Canvas canvas;
    private Image darkenImage;
    private RawImage itemView;       // shows the render texture (3D path)
    private Image iconFallback;      // shows the 2D icon (no-worldPrefab path)
    private TextMeshProUGUI nameLabel;
    private TextMeshProUGUI hintLabel;
    private int examineLayer;

    private float fitDistance = 3f;
    private float modelMaxExtent = 1f;
    private float zoom = 1f;
    private float targetZoom = 1f;
    private GameObject activeBackground;

    // input arming so the double-click that OPENED this does not instantly close it
    private bool armed;
    private float armTime;
    private float lastClickTime = -1f;
    private Vector3 lastClickPos;
    private const float DoubleClickWindow = 0.35f;
    private const float ClickMoveThreshold = 14f;

    #region Lifecycle
    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        IsExamining = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (renderTexture != null) renderTexture.Release();
    }

    /// <summary>
    /// Open the inspector for an item, creating the controller GameObject if one does not
    /// exist yet. The bag slots call this on double-click.
    /// </summary>
    public static void ShowItem(InventoryItem item)
    {
        if (item == null) return;
        if (instance == null)
        {
            GameObject go = new GameObject("ItemExamineController");
            instance = go.AddComponent<ItemExamineController>();
        }
        instance.Open(item);
    }
    #endregion

    #region Open / Close
    public void Open(InventoryItem item)
    {
        if (item == null) return;
        EnsureRig();

        SpawnModel(item);

        if (nameLabel != null)
        {
            nameLabel.gameObject.SetActive(showName);
            nameLabel.text = item.itemName;
        }
        if (hintLabel != null)
        {
            hintLabel.gameObject.SetActive(showHint);
            hintLabel.text = hintText;
        }

        zoom = 1f;
        targetZoom = 1f;
        canvas.gameObject.SetActive(true);
        examineCamera.enabled = true;
        if (activeBackground != null) activeBackground.SetActive(true);
        IsExamining = true;

        // Disarm input briefly so the opening double-click does not register as a close.
        armed = false;
        armTime = Time.unscaledTime + 0.3f;
        lastClickTime = -1f;
    }

    public void Close()
    {
        if (currentModel != null) { Destroy(currentModel); currentModel = null; }
        if (activeBackground != null) activeBackground.SetActive(false);
        if (canvas != null) canvas.gameObject.SetActive(false);
        if (examineCamera != null) examineCamera.enabled = false;
        IsExamining = false;
        closedOnFrame = Time.frameCount; // swallow this frame's closing input from the bag
    }
    #endregion

    #region Update (unscaled time: the bag pauses the game)
    private void Update()
    {
        if (!IsExamining) return;

        if (!armed && Time.unscaledTime >= armTime) armed = true;
        if (!armed) return;

        // --- close: I key, or a genuine double-click anywhere ---
        if (Input.GetKeyDown(KeyCode.I)) { Close(); return; }
        if (Input.GetMouseButtonDown(0))
        {
            float now = Time.unscaledTime;
            bool near = (Input.mousePosition - lastClickPos).sqrMagnitude < ClickMoveThreshold * ClickMoveThreshold;
            if (lastClickTime > 0f && now - lastClickTime <= DoubleClickWindow && near)
            {
                Close();
                return;
            }
            lastClickTime = now;
            lastClickPos = Input.mousePosition;
        }

        if (currentModel == null || modelHolder == null) return;

        // --- rotate while holding the left button, otherwise gently auto-spin ---
        if (Input.GetMouseButton(0))
        {
            float dx = Input.GetAxis("Mouse X");
            float dy = Input.GetAxis("Mouse Y");
            modelHolder.Rotate(Vector3.up, -dx * dragRotateSpeed * Time.unscaledDeltaTime, Space.World);
            modelHolder.Rotate(examineCamera.transform.right, dy * dragRotateSpeed * Time.unscaledDeltaTime, Space.World);
        }
        else if (idleSpinSpeed != 0f)
        {
            modelHolder.Rotate(Vector3.up, idleSpinSpeed * Time.unscaledDeltaTime, Space.World);
        }

        // --- zoom with the scroll wheel (eased so it glides instead of stepping) ---
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            targetZoom = Mathf.Clamp(targetZoom - scroll * zoomSpeed, minZoom, maxZoom);
        }
        if (!Mathf.Approximately(zoom, targetZoom))
        {
            // Frame-rate independent easing on unscaled time (the bag pauses the game).
            float t = 1f - Mathf.Exp(-zoomSmoothing * Time.unscaledDeltaTime);
            zoom = Mathf.Lerp(zoom, targetZoom, t);
            if (Mathf.Abs(zoom - targetZoom) < 0.0005f) zoom = targetZoom;
            PositionCamera();
        }
    }
    #endregion

    #region Rig construction (self-built, no scene setup)
    private void EnsureRig()
    {
        if (canvas != null) return; // already built once

        examineLayer = LayerMask.NameToLayer(examineLayerName);
        if (examineLayer < 0)
        {
            Debug.LogWarning($"[ItemExamine] Layer '{examineLayerName}' not found, using Default. " +
                             "ExamineLayerSetup creates it in the editor; if this is a build, open the project " +
                             "in the editor once so the layer is saved, then rebuild.");
            examineLayer = 0;
        }

        // The rig lives far below the world so scene point/spot lights cannot reach the item.
        rigRoot = new GameObject("ExamineRig").transform;
        rigRoot.SetParent(transform, false);
        rigRoot.position = new Vector3(0f, -5000f, 0f);

        modelHolder = new GameObject("ExamineModelHolder").transform;
        modelHolder.SetParent(rigRoot, false);
        modelHolder.localPosition = Vector3.zero;

        // Camera renders ONLY the examine layer onto a transparent render texture.
        renderTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
        GameObject camGo = new GameObject("ExamineCamera");
        camGo.transform.SetParent(rigRoot, false);
        examineCamera = camGo.AddComponent<Camera>();
        examineCamera.clearFlags = CameraClearFlags.SolidColor;
        examineCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        examineCamera.cullingMask = 1 << examineLayer;
        examineCamera.fieldOfView = fieldOfView;
        examineCamera.nearClipPlane = 0.01f;
        examineCamera.farClipPlane = 200f;
        examineCamera.targetTexture = renderTexture;
        examineCamera.enabled = false;

        // Key + fill light, locked to the examine layer so the rest of the scene is unaffected.
        Light key = new GameObject("ExamineKeyLight").AddComponent<Light>();
        key.transform.SetParent(camGo.transform, false);
        key.transform.localRotation = Quaternion.Euler(25f, -20f, 0f);
        key.type = LightType.Directional;
        key.intensity = keyLightIntensity;
        key.color = lightColor;
        key.cullingMask = 1 << examineLayer;

        Light fill = new GameObject("ExamineFillLight").AddComponent<Light>();
        fill.transform.SetParent(camGo.transform, false);
        fill.transform.localRotation = Quaternion.Euler(-15f, 160f, 0f);
        fill.type = LightType.Directional;
        fill.intensity = fillLightIntensity;
        fill.color = lightColor;
        fill.cullingMask = 1 << examineLayer;

        // Optional backdrop particle that renders behind the item in the inspect view.
        // It sits on the examine layer (so the inspect camera sees it) and behind the
        // model along +Z. The inspect view runs while the game is paused, so its particle
        // systems are forced onto unscaled time here, otherwise they would sit frozen.
        if (inspectBackgroundParticle != null)
        {
            activeBackground = Instantiate(inspectBackgroundParticle, rigRoot);
            activeBackground.transform.localPosition = new Vector3(0f, 0f, backgroundDistance);
            activeBackground.transform.localRotation = Quaternion.identity;
            SetLayerRecursive(activeBackground.transform, examineLayer);

            foreach (ParticleSystem ps in activeBackground.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.useUnscaledTime = true;
            }

            activeBackground.SetActive(false); // shown only while the inspect view is open
        }

        BuildUI();
    }

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("ExamineCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000; // above the bag
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Darkened backdrop. raycastTarget true so clicks cannot reach bag slots beneath.
        darkenImage = NewImage("Darken", canvasGo.transform);
        StretchFull(darkenImage.rectTransform);
        darkenImage.color = new Color(0f, 0f, 0f, backgroundDarken);
        darkenImage.raycastTarget = true;

        float side = Mathf.Min(Screen.width, Screen.height) * 0.85f;

        // The render texture, centered and square (3D path).
        GameObject viewGo = new GameObject("ItemView");
        viewGo.transform.SetParent(canvasGo.transform, false);
        itemView = viewGo.AddComponent<RawImage>();
        itemView.texture = renderTexture;
        itemView.raycastTarget = false;
        CenterSquare(itemView.rectTransform, side);

        // The 2D icon (fallback path for items without a worldPrefab).
        iconFallback = NewImage("IconFallback", canvasGo.transform);
        iconFallback.raycastTarget = false;
        iconFallback.preserveAspect = true;
        iconFallback.enabled = false;
        CenterSquare(iconFallback.rectTransform, side * 0.7f);

        nameLabel = NewText("ItemName", canvasGo.transform, 40f, TextAlignmentOptions.Center);
        if (nameLabel != null)
        {
            RectTransform nr = nameLabel.rectTransform;
            nr.anchorMin = new Vector2(0.5f, 1f);
            nr.anchorMax = new Vector2(0.5f, 1f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.sizeDelta = new Vector2(1000f, 70f);
            nr.anchoredPosition = new Vector2(0f, -60f);
        }

        hintLabel = NewText("Hint", canvasGo.transform, 24f, TextAlignmentOptions.Center);
        if (hintLabel != null)
        {
            hintLabel.color = new Color(0.85f, 0.85f, 0.85f, 0.8f);
            RectTransform hr = hintLabel.rectTransform;
            hr.anchorMin = new Vector2(0.5f, 0f);
            hr.anchorMax = new Vector2(0.5f, 0f);
            hr.pivot = new Vector2(0.5f, 0f);
            hr.sizeDelta = new Vector2(1200f, 50f);
            hr.anchoredPosition = new Vector2(0f, 50f);
        }

        canvasGo.SetActive(false);
    }
    #endregion

    #region Model spawn + framing
    private void SpawnModel(InventoryItem item)
    {
        if (currentModel != null) { Destroy(currentModel); currentModel = null; }

        if (item.worldPrefab != null)
        {
            currentModel = Instantiate(item.worldPrefab, modelHolder);
            StripForDisplay(currentModel);
            SetLayerRecursive(currentModel.transform, examineLayer);

            modelHolder.localRotation = Quaternion.identity;
            FitModel(currentModel);
            PositionCamera();

            if (itemView != null) itemView.enabled = true;
            if (iconFallback != null) iconFallback.enabled = false;
        }
        else
        {
            // No 3D model assigned for this item: show the 2D icon big instead.
            if (itemView != null) itemView.enabled = false;
            if (iconFallback != null)
            {
                iconFallback.enabled = true;
                iconFallback.sprite = item.icon;
            }
        }
    }

    /// <summary>
    /// Make a world prefab safe to display: kill physics and behaviour so the inspect
    /// copy is purely visual (no pickup triggers, no bobbing, no collisions).
    /// </summary>
    private static void StripForDisplay(GameObject go)
    {
        foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
    }

    /// <summary>
    /// Center the model's bounds on the holder pivot and work out the camera distance so
    /// it fills the view, regardless of how big or small the source model is.
    /// </summary>
    private void FitModel(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { fitDistance = 3f; return; }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        // Slide the model so its bounds center sits on the holder origin (the pivot it spins around).
        go.transform.position += modelHolder.position - b.center;

        float maxExtent = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (maxExtent < 0.0001f) maxExtent = 1f;
        modelMaxExtent = maxExtent;

        float halfFov = fieldOfView * 0.5f * Mathf.Deg2Rad;
        float needed = (maxExtent * 0.5f) / Mathf.Tan(halfFov);
        fitDistance = needed / Mathf.Max(0.3f, fillFraction);

        // Push the far clip comfortably past the farthest the camera can pull back to, so
        // the item is never clipped away (vanishes) at full zoom-out, even with a large
        // model or a high Max Zoom.
        examineCamera.farClipPlane = fitDistance * maxZoom + maxExtent + 50f;
        examineCamera.nearClipPlane = 0.01f;
    }

    private void PositionCamera()
    {
        if (examineCamera == null || modelHolder == null) return;
        float dist = fitDistance * zoom;
        // Never let the camera pull INTO the model on full zoom-in (which would make it
        // vanish from the inside). Keep it just outside the model's own size.
        float minSafe = modelMaxExtent * 0.5f + 0.05f;
        if (dist < minSafe) dist = minSafe;
        examineCamera.transform.position = modelHolder.position - Vector3.forward * dist;
        examineCamera.transform.LookAt(modelHolder.position);
    }

    private static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
    }
    #endregion

    #region UI helpers
    private static Image NewImage(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<Image>();
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, float size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.alignment = align;
        t.raycastTarget = false;
        t.color = new Color(0.96f, 0.94f, 0.88f, 1f);
        return t;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void CenterSquare(RectTransform rt, float side)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(side, side);
        rt.anchoredPosition = Vector2.zero;
    }
    #endregion
}