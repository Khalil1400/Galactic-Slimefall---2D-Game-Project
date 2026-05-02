using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public static class RuntimeUiFont
{
    const string MediumFontResourcePath = "Fonts/Orbitron-Medium";
    const string BoldFontResourcePath = "Fonts/Orbitron-Bold";
    const string FallbackFontName = "LegacyRuntime.ttf";

    static Font _mediumFont;
    static Font _boldFont;
    static Font _fallbackFont;

    public static Font Get(bool bold = false)
    {
        if (bold)
        {
            _boldFont ??= Resources.Load<Font>(BoldFontResourcePath);
            if (_boldFont != null)
            {
                return _boldFont;
            }
        }

        _mediumFont ??= Resources.Load<Font>(MediumFontResourcePath);
        if (_mediumFont != null)
        {
            return _mediumFont;
        }

        _fallbackFont ??= Resources.GetBuiltinResource<Font>(FallbackFontName);
        return _fallbackFont;
    }

    public static StyleFontDefinition GetStyle(bool bold = false)
    {
        return new StyleFontDefinition(FontDefinition.FromFont(Get(bold)));
    }

    public static void Apply(Text text, bool bold = false)
    {
        if (text == null)
        {
            return;
        }

        text.font = Get(bold);
        text.material = null;
        text.alignByGeometry = true;
        text.resizeTextForBestFit = false;
        text.raycastTarget = false;

        Outline outline = text.GetComponent<Outline>();
        if (outline == null)
        {
            outline = text.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }

    public static void Apply(VisualElement element, bool bold = false)
    {
        if (element == null)
        {
            return;
        }

        element.style.unityFontDefinition = GetStyle(bold);
    }
}
