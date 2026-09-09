using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SpeechBubble : MonoBehaviour
{
    [SerializeField] private float bubbleHeight = 2.5f;

    private Canvas _canvas;
    private Image _backgroundImage;
    private Text _textComponent;
    private Camera _mainCamera;
    private Coroutine _currentCoroutine;

    private void Awake()
    {
        _mainCamera = Camera.main;

        // Create Canvas
        GameObject canvasObj = new GameObject("SpeechBubbleCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = new Vector3(0, bubbleHeight, 0);
        
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 100;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        // Using a larger size delta and small scale so the Text renders nicely
        canvasRect.sizeDelta = new Vector2(200f, 100f);
        canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f); // Effective world size 2x1

        // Create Background Image
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        _backgroundImage = bgObj.AddComponent<Image>();
        _backgroundImage.color = new Color(0f, 0f, 0f, 0.75f);
        
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Create Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(canvasObj.transform, false);
        _textComponent = textObj.AddComponent<Text>();
        _textComponent.alignment = TextAnchor.MiddleCenter;
        _textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        _textComponent.verticalOverflow = VerticalWrapMode.Truncate;
        _textComponent.fontSize = 24;
        _textComponent.color = Color.white;
        _textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 10f);
        textRect.offsetMax = new Vector2(-10f, -10f);

        canvasObj.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_canvas != null && _canvas.gameObject.activeInHierarchy && _mainCamera != null)
        {
            Vector3 lookDir = _canvas.transform.position - _mainCamera.transform.position;
            lookDir.y = 0; // Billboard Y-axis only
            if (lookDir != Vector3.zero)
            {
                _canvas.transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
    }

    public void ShowMessage(string text, float duration = 4f)
    {
        if (_currentCoroutine != null)
        {
            StopCoroutine(_currentCoroutine);
        }
        _currentCoroutine = StartCoroutine(ShowMessageCoroutine(text, duration));
    }

    public void ShowTypewriter(string text, float charsPerSecond = 30f, float displayDuration = 3f)
    {
        if (_currentCoroutine != null)
        {
            StopCoroutine(_currentCoroutine);
        }
        _currentCoroutine = StartCoroutine(ShowTypewriterCoroutine(text, charsPerSecond, displayDuration));
    }

    public void Hide()
    {
        if (_currentCoroutine != null)
        {
            StopCoroutine(_currentCoroutine);
            _currentCoroutine = null;
        }
        if (_canvas != null)
        {
            _canvas.gameObject.SetActive(false);
        }
    }

    private IEnumerator ShowMessageCoroutine(string text, float duration)
    {
        _textComponent.text = text;
        _canvas.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        _canvas.gameObject.SetActive(false);
    }

    private IEnumerator ShowTypewriterCoroutine(string text, float charsPerSecond, float displayDuration)
    {
        _canvas.gameObject.SetActive(true);
        _textComponent.text = "";
        
        if (charsPerSecond <= 0) charsPerSecond = 30f;
        float delay = 1f / charsPerSecond;

        for (int i = 0; i < text.Length; i++)
        {
            _textComponent.text += text[i];
            yield return new WaitForSeconds(delay);
        }

        yield return new WaitForSeconds(displayDuration);
        _canvas.gameObject.SetActive(false);
    }
}
