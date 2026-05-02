using UnityEngine;
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class WorldSpaceFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 worldOffset = new(0f, 0.9f, 0f);

    RectTransform _rectTransform;
    Canvas _canvas;
    CanvasGroup _canvasGroup;
    Camera _camera;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _camera = Camera.main;
    }

    void LateUpdate()
    {
        if (_canvas == null)
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        if (target == null || _canvas == null)
        {
            return;
        }

        if (_camera == null)
        {
            _camera = Camera.main;
        }

        if (_camera == null)
        {
            return;
        }

        Vector3 screenPosition = _camera.WorldToScreenPoint(target.position + worldOffset);
        bool visible = screenPosition.z > 0f;
        _canvasGroup.alpha = visible ? 1f : 0f;

        if (!visible)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            screenPosition,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _camera,
            out Vector2 localPoint
        );

        float scaleFactor = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        localPoint.x = Mathf.Round(localPoint.x * scaleFactor) / scaleFactor;
        localPoint.y = Mathf.Round(localPoint.y * scaleFactor) / scaleFactor;
        _rectTransform.anchoredPosition = localPoint;
    }
}

