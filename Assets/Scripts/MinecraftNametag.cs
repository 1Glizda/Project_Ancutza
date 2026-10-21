using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
public class MinecraftNametag : MonoBehaviour
{
    [Header("Nametag Settings")]
    [Tooltip("The name displayed on the nametag.")]
    [SerializeField] private string horseName = "Juan";

    [Tooltip("Vertical height above the horse root.")]
    [SerializeField] private float heightOffset = 3.55f;

    [Tooltip("Forward offset along the horse's facing direction.")]
    [SerializeField] private float forwardOffset = 0.8f;

    [Tooltip("Scale of the world-space canvas.")]
    [SerializeField] private float canvasScale = 0.015f;

    [Tooltip("Font size of the nametag.")]
    [SerializeField] private float fontSize = 24f;

    [Header("Distance & Visibility")]
    [SerializeField] private float maxVisibleDistance = 40f;
    [SerializeField] private float fadeStartDistance = 28f;

    [Header("Font & Styling")]
    [SerializeField] private TMP_FontAsset minecraftFont;

    private Transform targetHorse;
    private Camera targetCamera;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform bgRect;
    private Image bgImage;
    private TextMeshProUGUI shadowText;
    private TextMeshProUGUI mainText;

    public string HorseName
    {
        get => horseName;
        set
        {
            horseName = value;
            UpdateText();
        }
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (targetHorse == null)
        {
            targetHorse = transform.parent != null ? transform.parent : transform;
        }

        if (minecraftFont == null)
        {
#if UNITY_EDITOR
            minecraftFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Minecraft-Regular SDF.asset");
#endif
        }

        BuildUIHierarchy();
        UpdateText();
    }

    private void BuildUIHierarchy()
    {
        // 1. Canvas
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        RectTransform rootRect = GetComponent<RectTransform>();
        rootRect.localScale = Vector3.one * canvasScale;

        // 2. Background Object
        Transform bgTransform = transform.Find("Background");
        GameObject bgObj;
        if (bgTransform == null)
        {
            bgObj = new GameObject("Background");
            bgObj.transform.SetParent(transform, false);
        }
        else
        {
            bgObj = bgTransform.gameObject;
        }

        bgRect = bgObj.GetComponent<RectTransform>();
        if (bgRect == null) bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);

        bgImage = bgObj.GetComponent<Image>();
        if (bgImage == null) bgImage = bgObj.AddComponent<Image>();
        // Minecraft nametag background: dark translucent box (approx rgba(0, 0, 0, 0.40))
        bgImage.color = new Color(0f, 0f, 0f, 0.40f);
        bgImage.raycastTarget = false;

        // 3. Shadow Text (Minecraft drop-shadow at +2, -2)
        Transform shadowTransform = bgObj.transform.Find("ShadowText");
        GameObject shadowObj;
        if (shadowTransform == null)
        {
            shadowObj = new GameObject("ShadowText");
            shadowObj.transform.SetParent(bgObj.transform, false);
        }
        else
        {
            shadowObj = shadowTransform.gameObject;
        }

        RectTransform shadowRect = shadowObj.GetComponent<RectTransform>();
        if (shadowRect == null) shadowRect = shadowObj.AddComponent<RectTransform>();
        shadowRect.anchorMin = Vector2.zero;
        shadowRect.anchorMax = Vector2.one;
        // Pixel shadow offset in Minecraft: down-right
        shadowRect.offsetMin = new Vector2(2f, -2f);
        shadowRect.offsetMax = new Vector2(2f, -2f);

        shadowText = shadowObj.GetComponent<TextMeshProUGUI>();
        if (shadowText == null) shadowText = shadowObj.AddComponent<TextMeshProUGUI>();
        shadowText.alignment = TextAlignmentOptions.Center;
        shadowText.fontSize = fontSize;
        shadowText.color = new Color(0.247f, 0.247f, 0.247f, 1f); // 0x3F3F3F Minecraft text shadow
        shadowText.raycastTarget = false;
        if (minecraftFont != null) shadowText.font = minecraftFont;

        // 4. Main Text (pure white)
        Transform mainTransform = bgObj.transform.Find("MainText");
        GameObject mainObj;
        if (mainTransform == null)
        {
            mainObj = new GameObject("MainText");
            mainObj.transform.SetParent(bgObj.transform, false);
        }
        else
        {
            mainObj = mainTransform.gameObject;
        }

        RectTransform mainRect = mainObj.GetComponent<RectTransform>();
        if (mainRect == null) mainRect = mainObj.AddComponent<RectTransform>();
        mainRect.anchorMin = Vector2.zero;
        mainRect.anchorMax = Vector2.one;
        mainRect.offsetMin = Vector2.zero;
        mainRect.offsetMax = Vector2.zero;

        mainText = mainObj.GetComponent<TextMeshProUGUI>();
        if (mainText == null) mainText = mainObj.AddComponent<TextMeshProUGUI>();
        mainText.alignment = TextAlignmentOptions.Center;
        mainText.fontSize = fontSize;
        mainText.color = Color.white;
        mainText.raycastTarget = false;
        if (minecraftFont != null) mainText.font = minecraftFont;
    }

    public void UpdateText()
    {
        if (mainText == null || shadowText == null || bgRect == null)
        {
            BuildUIHierarchy();
        }

        if (mainText != null)
        {
            mainText.text = horseName;
            mainText.fontSize = fontSize;
            if (minecraftFont != null) mainText.font = minecraftFont;
        }

        if (shadowText != null)
        {
            shadowText.text = horseName;
            shadowText.fontSize = fontSize;
            if (minecraftFont != null) shadowText.font = minecraftFont;
        }

        if (mainText != null && bgRect != null)
        {
            // Calculate size with Minecraft-style padding
            float textWidth = mainText.preferredWidth;
            float textHeight = mainText.preferredHeight;

            // Minecraft nametag padding: ~10px horizontal, ~6px vertical
            bgRect.sizeDelta = new Vector2(textWidth + 12f, textHeight + 6f);
        }
    }

    private void LateUpdate()
    {
        Camera cam = targetCamera;
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (Camera.current != null)
            {
                cam = Camera.current;
            }
            else if (UnityEditor.SceneView.lastActiveSceneView != null && UnityEditor.SceneView.lastActiveSceneView.camera != null)
            {
                cam = UnityEditor.SceneView.lastActiveSceneView.camera;
            }
        }
#endif

        if (cam == null) return;

        if (targetHorse != null)
        {
            // Position above horse
            Vector3 worldPos = targetHorse.position + Vector3.up * heightOffset + targetHorse.forward * forwardOffset;
            transform.position = worldPos;
        }

        // Exact Minecraft billboarding: match camera rotation
        transform.rotation = cam.transform.rotation;

        // Distance check and fading
        float distance = Vector3.Distance(cam.transform.position, transform.position);
        if (canvasGroup != null)
        {
            if (distance > maxVisibleDistance)
            {
                canvasGroup.alpha = 0f;
            }
            else if (distance > fadeStartDistance)
            {
                float t = (distance - fadeStartDistance) / (maxVisibleDistance - fadeStartDistance);
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            }
            else
            {
                canvasGroup.alpha = 1f;
            }
        }
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
            {
                UpdateText();
            }
        };
#endif
    }
}
