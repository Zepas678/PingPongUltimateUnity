using UnityEngine;
using System.Collections;

/// <summary>
/// Efecto de onda expansiva — aparece en la posición de la pelota
/// cuando supera la velocidad de "barrera del sonido" y se desvanece.
/// 
/// Usa SpriteRenderer con un sprite circular simple.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ShockwaveEffect : MonoBehaviour
{
    [Header("Configuración del efecto")]
    public float escalaMaxima   = 15f;
    public float duracion       = 0.6f;
    public AnimationCurve curvaEscala = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Posición fija (centro de la mesa)")]
    public bool usarPosicionFija = false; // false = aparece donde está la pelota
    public Vector3 posicionFija  = Vector3.zero;

    [Header("Color modo épico")]
    public Color colorEpico = new Color(1f, 0.5f, 0f, 0.8f); // naranja

    private SpriteRenderer rend;
    private Color          colorBase;

    void Awake()
    {
        rend = GetComponent<SpriteRenderer>();
        if (rend != null)
            colorBase = rend.color;

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Activa el efecto en la posición indicada.
    /// </summary>
    public void Reproducir(Vector3 posicion)
    {
        // Usar posición fija en el centro si está activado, para mejor visibilidad
        transform.position = usarPosicionFija ? posicionFija : posicion;

        if (rend != null) rend.color = colorEpico;
        colorBase = colorEpico;

        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(Animar());
    }

    IEnumerator Animar()
    {
        float t = 0f;
        Vector3 escalaInicial = Vector3.one * 0.1f;
        Vector3 escalaFinal   = Vector3.one * escalaMaxima;

        while (t < 1f)
        {
            t += Time.deltaTime / duracion;
            float curva = curvaEscala.Evaluate(t);

            transform.localScale = Vector3.Lerp(escalaInicial, escalaFinal, curva);

            if (rend != null)
            {
                Color c = colorBase;
                c.a = Mathf.Lerp(colorBase.a, 0f, t);
                rend.color = c;
            }

            yield return null;
        }

        gameObject.SetActive(false);
        if (rend != null) rend.color = colorBase;
    }
}