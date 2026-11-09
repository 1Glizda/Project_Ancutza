using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple geometric sound occlusion. Casts rays from this AudioSource to the AudioListener;
/// only building structure (walls, windows, doors, floors, roofs - matched by name) muffles the sound.
/// Props such as tables, chairs, barrels and characters never occlude.
/// Several rays are used so a partly blocked path (doorway, corner) is only partly muffled.
/// Turning away from the source (listener facing away) also muffles it, like ears facing away from a sound.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class AudioOcclusion : MonoBehaviour
{
    [Header("Occlusion Detection")]
    [Tooltip("Layers that can block sound.")]
    [SerializeField] private LayerMask occluderMask = ~0;

    [Tooltip("A collider only occludes if its name or one of its parents' names contains one of these words (case-insensitive).")]
    [SerializeField] private string[] occluderKeywords = { "wall", "window", "door", "barn wood", "floor", "platform", "roof", "ceiling", "_movable" };

    [Tooltip("Colliders whose name or parents' names contain one of these words never occlude (props, characters).")]
    [SerializeField] private string[] ignoreKeywords = { "table", "chair", "barrel", "crate", "lamp", "chandelier", "axe", "candle", "firewood", "fireplace", "torch", "props", "npc", "guest", "lautar", "player", "carpet", "canvas_on_wall" };

    [Tooltip("How many parent levels are searched for the keywords.")]
    [SerializeField] private int parentSearchDepth = 6;

    [Tooltip("Sideways spread (meters) of the extra rays around the direct line, so corners and doorways partly occlude.")]
    [SerializeField] private float raySpread = 0.6f;

    [Tooltip("Seconds between occlusion checks.")]
    [SerializeField] private float checkInterval = 0.1f;

    [Header("Looking Away")]
    [Tooltip("Muffle the sound when the listener faces away from the source.")]
    [SerializeField] private bool occludeWhenLookingAway = true;

    [Tooltip("Angle (degrees) between the view direction and the source where muffling starts.")]
    [SerializeField] private float lookAwayStartAngle = 90f;

    [Tooltip("Angle (degrees) where the look-away muffling is at its maximum (180 = directly behind).")]
    [SerializeField] private float lookAwayFullAngle = 180f;

    [Tooltip("Maximum muffling from looking away alone (0..1; a wall is 1).")]
    [Range(0f, 1f)]
    [SerializeField] private float lookAwayStrength = 0.5f;

    [Header("Muffling")]
    [Tooltip("Low-pass cutoff (Hz) when the sound is fully in the open.")]
    [SerializeField] private float openCutoff = 22000f;

    [Tooltip("Low-pass cutoff (Hz) when fully blocked by walls.")]
    [SerializeField] private float occludedCutoff = 900f;

    [Tooltip("Volume multiplier when fully blocked.")]
    [Range(0f, 1f)]
    [SerializeField] private float occludedVolume = 0.35f;

    [Tooltip("Extra muffling per additional wall beyond the first (0 = none).")]
    [Range(0f, 1f)]
    [SerializeField] private float perExtraWallStrength = 0.25f;

    [Tooltip("How fast the muffling follows changes (higher = faster).")]
    [SerializeField] private float smoothing = 6f;

    private AudioSource source;
    private AudioLowPassFilter lowPass;
    private float baseVolume;
    private float occlusion;        // current smoothed 0..1
    private float targetOcclusion;
    private float nextCheck;
    private readonly RaycastHit[] hits = new RaycastHit[16];
    private readonly Dictionary<Collider, bool> occluderCache = new Dictionary<Collider, bool>();

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;

        lowPass = GetComponent<AudioLowPassFilter>();
        if (lowPass == null) lowPass = gameObject.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = openCutoff;
    }

    private void OnDisable()
    {
        if (lowPass != null) lowPass.cutoffFrequency = openCutoff;
        if (source != null) source.volume = baseVolume;
    }

    private void Update()
    {
        if (source == null || !source.isPlaying)
        {
            // Keep the base volume current while idle so inspector/script changes are respected.
            if (source != null) baseVolume = source.volume;
            return;
        }

        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + checkInterval;
            targetOcclusion = ComputeOcclusion();
        }

        occlusion = Mathf.Lerp(occlusion, targetOcclusion, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

        // Logarithmic frequency interpolation sounds more natural than linear.
        float cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(openCutoff), Mathf.Log(occludedCutoff), occlusion));
        lowPass.cutoffFrequency = cutoff;
        source.volume = baseVolume * Mathf.Lerp(1f, occludedVolume, occlusion);
    }

    private float ComputeOcclusion()
    {
        AudioListener listener = FindListener();
        if (listener == null) return 0f;

        Vector3 from = transform.position;
        Vector3 to = listener.transform.position;
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.5f) return 0f;
        if (source != null && source.spatialBlend > 0.5f && dist > source.maxDistance + 1.5f)
            return 1f;
        dir /= dist;

        float lookAway = 0f;
        if (occludeWhenLookingAway && dist > 1.5f)
        {
            // Angle between where the listener faces and where the sound comes from.
            // dir points source -> listener, so the direction to the source is -dir.
            float angle = Vector3.Angle(listener.transform.forward, -dir);
            lookAway = Mathf.InverseLerp(lookAwayStartAngle, lookAwayFullAngle, angle);
            lookAway = lookAway * lookAway * (3f - 2f * lookAway) * lookAwayStrength; // smoothstep
        }

        // In Low-End PC Mode, cast a single center ray for 4x lower CPU cost while preserving full sound occlusion.
        if (Han.CheckIn.HanLowEndOptimizer.IsLowEndModeEnabled)
        {
            float singleWallOcclusion = Mathf.Clamp01(RayBlock(from, to));
            return 1f - (1f - singleWallOcclusion) * (1f - lookAway);
        }

        Vector3 side = Vector3.Cross(dir, Vector3.up);
        if (side.sqrMagnitude < 0.001f) side = Vector3.right;
        side.Normalize();
        Vector3 up = Vector3.Cross(side, dir).normalized;

        // Centre ray counts double, side/up rays once each.
        float blocked = 0f;
        float total = 0f;
        total += 2f; blocked += 2f * RayBlock(from, to);
        total += 1f; blocked += RayBlock(from + side * raySpread, to);
        total += 1f; blocked += RayBlock(from - side * raySpread, to);
        total += 1f; blocked += RayBlock(from + up * raySpread, to);

        float wallOcclusion = Mathf.Clamp01(blocked / total);

        // Combine both effects without exceeding 1.
        return 1f - (1f - wallOcclusion) * (1f - lookAway);
    }

    /// <summary>Returns 0 when clear; 1 for one wall, rising slightly for extra walls (capped at 1).</summary>
    private float RayBlock(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return 0f;

        int count = Physics.RaycastNonAlloc(from, dir / dist, hits, dist, occluderMask, QueryTriggerInteraction.Ignore);
        int walls = 0;
        for (int i = 0; i < count; i++)
        {
            Collider c = hits[i].collider;
            if (c == null) continue;
            if (c.transform.IsChildOf(transform) || c.GetComponentInParent<CharacterController>() != null) continue;

            if (!IsOccluder(c)) continue;
            walls++;
        }

        if (walls == 0) return 0f;
        return Mathf.Clamp01(0.8f + (walls - 1) * perExtraWallStrength);
    }

    private bool IsOccluder(Collider c)
    {
        if (occluderCache.TryGetValue(c, out bool cached)) return cached;

        bool matchesOccluder = false;
        bool matchesIgnore = false;
        Transform t = c.transform;
        for (int depth = 0; t != null && depth <= parentSearchDepth; depth++, t = t.parent)
        {
            string n = t.name;
            if (!matchesIgnore && ContainsAny(n, ignoreKeywords)) matchesIgnore = true;
            if (!matchesOccluder && ContainsAny(n, occluderKeywords)) matchesOccluder = true;
        }

        bool result = matchesOccluder && !matchesIgnore;
        occluderCache[c] = result;
        return result;
    }

    private static bool ContainsAny(string name, string[] words)
    {
        if (words == null) return false;
        for (int i = 0; i < words.Length; i++)
        {
            if (!string.IsNullOrEmpty(words[i]) &&
                name.IndexOf(words[i], System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    private AudioListener cachedListener;

    private AudioListener FindListener()
    {
        if (cachedListener != null && cachedListener.isActiveAndEnabled) return cachedListener;
        cachedListener = FindFirstObjectByType<AudioListener>();
        return cachedListener;
    }
}
