using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UIAnimationManager : MonoBehaviour
{
    public static UIAnimationManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    Dictionary<RectTransform, Vector3> escalasOriginales = new Dictionary<RectTransform, Vector3>();

    public Coroutine ScaleIn(RectTransform rt, float dur = 0.3f)
    {
        if (rt == null) return null;
        if (!escalasOriginales.ContainsKey(rt))
            escalasOriginales[rt] = rt.localScale == Vector3.zero ? Vector3.one : rt.localScale;
        Vector3 escalaOriginal = escalasOriginales[rt];
        if (escalaOriginal == Vector3.zero) escalaOriginal = Vector3.one;
        rt.localScale = escalaOriginal * 0.9f;
        rt.gameObject.SetActive(true);
        return StartCoroutine(ScaleTo(rt, escalaOriginal, dur, EaseOutBack));
    }

    public Coroutine ScaleOut(RectTransform rt, float dur = 0.25f)
    {
        if (rt == null) return null;
        return StartCoroutine(ScaleTo(rt, Vector3.zero, dur, EaseIn, () => rt.gameObject.SetActive(false)));
    }

    Vector2 shopPosOriginal;
    bool shopPosCapturada = false;
    Coroutine corrutinaShopActual;

    public Coroutine SlideFromRight(RectTransform rt, float dur = 0.3f)
    {
        if (rt == null) return null;
        if (!shopPosCapturada) { shopPosOriginal = rt.anchoredPosition; shopPosCapturada = true; }
        if (corrutinaShopActual != null) StopCoroutine(corrutinaShopActual);
        rt.anchoredPosition = new Vector2(Screen.width, shopPosOriginal.y);
        rt.gameObject.SetActive(true);
        corrutinaShopActual = StartCoroutine(MoveTo(rt, shopPosOriginal, dur, EaseOutCubic));
        return corrutinaShopActual;
    }

    public Coroutine SlideToRight(RectTransform rt, float dur = 0.3f)
    {
        if (rt == null) return null;
        if (!shopPosCapturada) { shopPosOriginal = rt.anchoredPosition; shopPosCapturada = true; }
        if (corrutinaShopActual != null) StopCoroutine(corrutinaShopActual);
        Vector2 off = new Vector2(Screen.width, shopPosOriginal.y);
        corrutinaShopActual = StartCoroutine(MoveTo(rt, off, dur, EaseInCubic, () => rt.gameObject.SetActive(false)));
        return corrutinaShopActual;
    }

    public Coroutine SlideFromLeft(RectTransform rt, float dur = 0.3f)
    {
        if (rt == null) return null;
        Vector2 target = rt.anchoredPosition;
        rt.anchoredPosition = new Vector2(-Screen.width, target.y);
        rt.gameObject.SetActive(true);
        return StartCoroutine(MoveTo(rt, target, dur, EaseOutCubic));
    }

    public Coroutine SlideFromBottom(RectTransform rt, float dur = 0.3f)
    {
        if (rt == null) return null;
        Vector2 target = rt.anchoredPosition;
        rt.anchoredPosition = new Vector2(target.x, -Screen.height);
        rt.gameObject.SetActive(true);
        return StartCoroutine(MoveTo(rt, target, dur, EaseOutCubic));
    }

    public Coroutine Fade(CanvasGroup cg, float from, float to, float dur, System.Action onDone = null)
    {
        if (cg == null) return null;
        cg.alpha = from;
        cg.gameObject.SetActive(true);
        return StartCoroutine(FadeRoutine(cg, to, dur, onDone));
    }

    IEnumerator ScaleTo(RectTransform rt, Vector3 target, float dur, System.Func<float,float> ease, System.Action onDone = null)
    {
        Vector3 start = rt.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, dur);
            float e = ease(Mathf.Clamp01(t));
            rt.localScale = Vector3.LerpUnclamped(start, target, e);
            yield return null;
        }
        rt.localScale = target;
        onDone?.Invoke();
    }

    IEnumerator MoveTo(RectTransform rt, Vector2 target, float dur, System.Func<float,float> ease, System.Action onDone = null)
    {
        Vector2 start = rt.anchoredPosition;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, dur);
            float e = ease(Mathf.Clamp01(t));
            rt.anchoredPosition = Vector2.LerpUnclamped(start, target, e);
            yield return null;
        }
        rt.anchoredPosition = target;
        onDone?.Invoke();
    }

    IEnumerator FadeRoutine(CanvasGroup cg, float to, float dur, System.Action onDone)
    {
        float start = cg.alpha;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, dur);
            cg.alpha = Mathf.Lerp(start, to, Mathf.Clamp01(t));
            yield return null;
        }
        cg.alpha = to;
        onDone?.Invoke();
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
    static float EaseIn(float x) => x * x;
    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
    static float EaseInCubic(float x) => x * x * x;
}
