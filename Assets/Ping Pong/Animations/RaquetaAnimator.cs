using UnityEngine;
using System.Collections;

public class RaquetaAnimacion : MonoBehaviour
{
    [Tooltip("Activar si la raqueta es la CPU")]
    public bool esCPU = false;

    [Header("Duraciones")]
    public float duracionMovimiento = 0.12f;
    public float duracionGolpe      = 0.10f;

    private Quaternion rotacionBase;
    private int  zonaActual = 0;
    private bool animando   = false;

    // Rotaciones absolutas JUGADOR
    static readonly Quaternion P_IZQUIERDA = Quaternion.Euler(200f,   0f,  90f);
    static readonly Quaternion P_DERECHA   = Quaternion.Euler(330f,   0f,  90f);
    static readonly Quaternion P_GOLPE     = Quaternion.Euler(224f, -90f, 180f);

    // Rotaciones absolutas CPU
    static readonly Quaternion C_IZQUIERDA = Quaternion.Euler(200f,   0f, 270f);
    static readonly Quaternion C_DERECHA   = Quaternion.Euler(350f,   0f, 270f);
    static readonly Quaternion C_GOLPE     = Quaternion.Euler(290f, -90f, 360f);

    Quaternion RotIzquierda => esCPU ? C_IZQUIERDA : P_IZQUIERDA;
    Quaternion RotDerecha   => esCPU ? C_DERECHA   : P_DERECHA;
    Quaternion RotGolpe     => esCPU ? C_GOLPE     : P_GOLPE;

    void Start()
    {
        rotacionBase = transform.localRotation;
    }

    // -------------------------------------------------------
    public void AnimarMovimiento(int zonaDestino)
    {
        if (animando) return;

        int dir = zonaDestino;
        if (zonaDestino == 0)
            dir = zonaActual > 0 ? -1 : 1;

        zonaActual = zonaDestino;

        Quaternion target = dir > 0 ? RotDerecha : RotIzquierda;
        StartCoroutine(AnimarHacia(target, duracionMovimiento));
    }

    // -------------------------------------------------------
    public void AnimarGolpe()
    {
        if (!enabled || !gameObject.activeInHierarchy) return;
        StopAllCoroutines();
        animando = false;
        StartCoroutine(AnimarHacia(RotGolpe, duracionGolpe));
    }

    // -------------------------------------------------------
    public void AnimarFestejo()
    {
        if (!enabled || !gameObject.activeInHierarchy) return;
        StopAllCoroutines();
        animando = false;
        StartCoroutine(RepetirFestejo());
    }

    /// Repite el giro de festejo varias veces durante los 2 segundos de celebración
    IEnumerator RepetirFestejo()
    {
        float duracionTotal = 2f;
        float t = 0f;
        while (t < duracionTotal)
        {
            yield return StartCoroutine(AnimarGiro());
            t += 0.4f; // duración de cada giro
        }
    }

    // -------------------------------------------------------
    IEnumerator AnimarHacia(Quaternion target, float duracion)
    {
        animando = true;

        // Ir hacia la inclinación
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / (duracion * 0.5f);
            transform.localRotation = Quaternion.Slerp(rotacionBase, target, Mathf.Clamp01(t));
            yield return null;
        }

        // Volver a la base
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / (duracion * 0.5f);
            transform.localRotation = Quaternion.Slerp(target, rotacionBase, Mathf.Clamp01(t));
            yield return null;
        }

        transform.localRotation = rotacionBase;
        animando = false;
    }

    // -------------------------------------------------------
    IEnumerator AnimarGiro()
    {
        animando       = true;
        float duracion = 0.4f;
        float t        = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duracion;
            float angulo = Mathf.Lerp(0f, 360f, t);
            // Giro en Z para que se vea como girar la paleta sobre su cara
            transform.localRotation = rotacionBase * Quaternion.Euler(0f, 0f, angulo);
            yield return null;
        }

        transform.localRotation = rotacionBase;
        animando = false;
    }
}