/* using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class ShieldMark : MonoBehaviour
{
    [SerializeField] private float lifeTime = 2f;
    [SerializeField] private float fadeTime = 1f;

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        StartCoroutine(FadeAndDestroy());
    }

    IEnumerator FadeAndDestroy()
    {
        // Espera antes de empezar a desvanecerse
        yield return new WaitForSeconds(lifeTime - fadeTime);

        Color c = sr.color;

        float t = 0f;

        while (t < fadeTime)
        {
            t += Time.deltaTime;

            c.a = Mathf.Lerp(1f, 0f, t / fadeTime);
            sr.color = c;

            yield return null;
        }

        Destroy(gameObject);
    }*/
