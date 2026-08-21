using UnityEngine;

/// <summary>
/// Code-built combat VFX. Nothing here needs a prefab, a material asset or an inspector
/// reference — every effect is generated at runtime from primitives and ONE shared additive
/// material. The point is that the fight can READ correctly with zero editor work; once real
/// art exists, assign the proper prefabs and switch these off at the call sites.
///
///   Spark(...)      — impact burst at a contact point. "Your hit landed."
///   Shockwave(...)  — expanding ground ring. Ground pound / heavy landing.
///   Wave(...)       — arcs trailing a rush. Charge.
///
/// Two things are deliberate:
///   • Colour is driven through a MaterialPropertyBlock, never through Renderer.material.
///     Touching .material instantiates a fresh material copy that Unity does not clean up, and
///     at ~14 shards per landed hit that leak adds up fast over a boss fight.
///   • Everything runs on UNSCALED time, so the effects still read at full speed while Yoru's
///     tail-aim slow-motion has the world clock at a tenth speed.
/// </summary>
public static class ProceduralImpactFX
{
    private static Material sharedAdditive;
    private static MaterialPropertyBlock mpb;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static Material Additive()
    {
        if (sharedAdditive != null) return sharedAdditive;

        // URP first, then built-in unlit fallbacks — whichever this project actually has.
        Shader s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        if (s == null) s = Shader.Find("Sprites/Default");

        sharedAdditive = new Material(s) { name = "ProceduralImpactFX (runtime)" };
        sharedAdditive.SetFloat("_Surface", 1f);   // transparent
        sharedAdditive.SetFloat("_Blend", 1f);     // additive
        sharedAdditive.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        sharedAdditive.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        sharedAdditive.SetInt("_ZWrite", 0);
        sharedAdditive.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        sharedAdditive.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return sharedAdditive;
    }

    /// <summary>Per-renderer colour with no material instancing. Writes both URP and built-in names.</summary>
    private static void Tint(Renderer r, Color c)
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColorId, c);
        mpb.SetColor(ColorId, c);
        r.SetPropertyBlock(mpb);
    }

    private static MeshRenderer Dress(GameObject go, Color c)
    {
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = Additive();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        Tint(mr, c);
        return mr;
    }

    // ───────────────────────────────────────────────────────────────────── impact spark ──

    /// <summary>
    /// A short burst of glowing shards thrown outward from the contact point plus a centre flash.
    /// This is the "hit landed" confirmation that sits on top of hitstop and camera shake.
    /// </summary>
    public static void Spark(Vector3 point, bool heavy, Color? tint = null)
    {
        Color c = tint ?? (heavy ? new Color(1f, 0.55f, 0.15f) : new Color(1f, 0.92f, 0.6f));
        int shards = heavy ? 14 : 8;
        float speed = heavy ? 7f : 4.5f;
        float size = heavy ? 0.13f : 0.08f;
        float life = heavy ? 0.32f : 0.22f;

        var root = new GameObject("FX_HitSpark");
        root.transform.position = point;

        for (int i = 0; i < shards; i++)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(shard.GetComponent<Collider>());
            shard.transform.SetParent(root.transform, false);
            shard.transform.localScale = new Vector3(size * 0.35f, size * 0.35f, size * 1.8f);

            // Spread around a cone, biased upward so shards arc off the body rather than into it.
            float a = (i / (float)shards) * Mathf.PI * 2f;
            float y = Mathf.Lerp(0.1f, 0.9f, (i * 0.6180339f) % 1f);
            Vector3 dir = new Vector3(Mathf.Cos(a), y, Mathf.Sin(a)).normalized;
            shard.transform.rotation = Quaternion.LookRotation(dir);

            Dress(shard, c);

            var mv = shard.AddComponent<FXMover>();
            mv.baseColor = c;
            mv.velocity = dir * speed * Random.Range(0.6f, 1.25f);
            mv.gravity = -14f;
            mv.life = life;
            mv.shrink = true;
        }

        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(flash.GetComponent<Collider>());
        flash.transform.SetParent(root.transform, false);
        flash.transform.localScale = Vector3.one * (heavy ? 0.55f : 0.32f);
        Dress(flash, c);
        var fm = flash.AddComponent<FXMover>();
        fm.baseColor = c;
        fm.life = heavy ? 0.12f : 0.08f;
        fm.shrink = true;

        Object.Destroy(root, life + 0.35f);
    }

    // ───────────────────────────────────────────────────────────────────────── shockwave ──

    /// <summary>
    /// Flat ring expanding across the ground. Ground pound, heavy landing, phase flip.
    /// Pass the attack's REAL AoE as maxRadius so the ring never lies about its reach.
    /// </summary>
    public static void Shockwave(Vector3 groundPoint, float maxRadius, float duration, Color? tint = null)
    {
        Color c = tint ?? new Color(1f, 0.45f, 0.2f);

        var ring = new GameObject("FX_Shockwave");
        ring.transform.position = groundPoint + Vector3.up * 0.06f;
        ring.AddComponent<MeshFilter>().sharedMesh = RingMesh();

        var mr = ring.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Additive();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Tint(mr, c);

        var grow = ring.AddComponent<FXRing>();
        grow.baseColor = c;
        grow.maxRadius = maxRadius;
        grow.life = duration;

        Object.Destroy(ring, duration + 0.1f);
    }

    // ──────────────────────────────────────────────────────────────────────── charge wave ──

    /// <summary>
    /// Arcs thrown backwards along the travel direction — reads as air being torn open by a rush.
    /// Spawn repeatedly while the charge clip plays.
    /// </summary>
    public static void Wave(Vector3 origin, Vector3 forward, float scale, Color? tint = null)
    {
        Color c = tint ?? new Color(0.55f, 0.8f, 1f);
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        var root = new GameObject("FX_ChargeWave");
        root.transform.position = origin;
        root.transform.rotation = Quaternion.LookRotation(forward);

        for (int i = 0; i < 3; i++)
        {
            var arc = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(arc.GetComponent<Collider>());
            arc.transform.SetParent(root.transform, false);
            arc.transform.localPosition = new Vector3(0f, 0.4f + i * 0.55f, 0f);
            arc.transform.localScale = new Vector3(scale * (1.6f - i * 0.35f), 0.09f, 0.09f);

            Dress(arc, c);

            var mv = arc.AddComponent<FXMover>();
            mv.baseColor = c;
            mv.velocity = -forward * 6f;   // trails behind the rush
            mv.life = 0.35f;
            mv.shrink = true;
        }

        Object.Destroy(root, 0.6f);
    }

    // ───────────────────────────────────────────────────────────────────────────── meshes ──

    private static Mesh cachedRing;

    /// <summary>Unit-radius flat annulus on the XZ plane, built once and reused.</summary>
    private static Mesh RingMesh()
    {
        if (cachedRing != null) return cachedRing;

        const int seg = 64;
        const float inner = 0.82f;

        var verts = new Vector3[seg * 2];
        var tris = new int[seg * 6];

        for (int i = 0; i < seg; i++)
        {
            float a = (i / (float)seg) * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            verts[i * 2] = dir * inner;
            verts[i * 2 + 1] = dir;

            int n = (i + 1) % seg;
            int t = i * 6;
            tris[t] = i * 2;
            tris[t + 1] = n * 2;
            tris[t + 2] = i * 2 + 1;
            tris[t + 3] = n * 2;
            tris[t + 4] = n * 2 + 1;
            tris[t + 5] = i * 2 + 1;
        }

        cachedRing = new Mesh { name = "FX_RingMesh" };
        cachedRing.vertices = verts;
        cachedRing.triangles = tris;
        cachedRing.RecalculateBounds();
        return cachedRing;
    }

    // ────────────────────────────────────────────────────────────────────────── behaviours ──

    /// <summary>Ballistic mover that fades (and optionally shrinks) over its life. Unscaled time.</summary>
    private class FXMover : MonoBehaviour
    {
        public Vector3 velocity;
        public float gravity;
        public float life = 0.25f;
        public bool shrink;
        public Color baseColor = Color.white;

        private float t;
        private Vector3 startScale;
        private Renderer rend;

        private void Start()
        {
            startScale = transform.localScale;
            rend = GetComponent<Renderer>();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;

            velocity += Vector3.up * gravity * dt;
            transform.position += velocity * dt;

            float k = Mathf.Clamp01(t / life);
            if (shrink) transform.localScale = startScale * (1f - k);
            if (rend != null)
            {
                Color c = baseColor;
                c.a = 1f - k;
                Tint(rend, c);
            }

            if (t >= life) Destroy(gameObject);
        }
    }

    /// <summary>Expanding, fading ground ring. Unscaled time.</summary>
    private class FXRing : MonoBehaviour
    {
        public float maxRadius = 5f;
        public float life = 0.5f;
        public Color baseColor = Color.white;

        private float t;
        private Renderer rend;

        private void Start()
        {
            rend = GetComponent<Renderer>();
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / life);

            // Fast out, slow settle — reads as a pressure wave, not a linear scale-up.
            float r = maxRadius * (1f - Mathf.Pow(1f - k, 3f));
            transform.localScale = new Vector3(r, 1f, r);

            if (rend != null)
            {
                Color c = baseColor;
                c.a = 1f - k;
                Tint(rend, c);
            }

            if (t >= life) Destroy(gameObject);
        }
    }
}
