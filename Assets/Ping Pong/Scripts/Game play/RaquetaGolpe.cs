using UnityEngine;
using System.Collections;

public class RaquetaGolpe : MonoBehaviour
{
    [Header("Configuración")]
    public float velocidadDestruccion = 60f;
    public float ventanaTiempo        = 0.3f;
    public float multiplicadorGolpe   = 1.5f;
    public float extraTamanoCollider  = 0.3f;

    [Header("Es jugador o CPU")]
    public bool esJugador = true;

    private BoxCollider      boxCollider;
    private Renderer         meshRenderer;
    private RaquetaAnimacion animacion;
    private Vector3          tamanoColliderOriginal;
    private Vector3          escalaOriginal;

    private bool  botonPresionado  = false;
    private bool  destruida        = false;
    private float tiempoBotonPress = -999f;
    private bool  golpeActivado    = false;

    /// <summary>Evento invocado cuando la raqueta termina de regenerarse.
    /// Permite a sistemas externos (p. ej. el visual Zeus) re-aplicar su apariencia.</summary>
    public System.Action onRegenerada;

    /// <summary>
    /// Si es true, al regenerarse la raqueta NO re-activa su renderer base
    /// (el que muestra la raqueta default). Lo usa el combate de Zeus para
    /// evitar el destello de la raqueta base antes de que quede visible Zeus.
    /// </summary>
    public bool ocultarRendererBaseAlRegenerar = false;

    // -------------------------------------------------------
    void Awake()
    {
        boxCollider            = GetComponent<BoxCollider>();
        meshRenderer           = GetComponent<Renderer>();
        animacion              = GetComponent<RaquetaAnimacion>();
        tamanoColliderOriginal = boxCollider != null ? boxCollider.size : Vector3.one;
        escalaOriginal         = transform.localScale;
    }

    void Start() { }

    // -------------------------------------------------------
    void Update()
    {
        if (!esJugador || destruida) return;

        // En modo PvP el jugador 1 solo usa J, Keypad2 es exclusivo del jugador 2
        bool presionoBoton = Input.GetKeyDown(KeyCode.J);
        if (presionoBoton) OnGolpe();

        if (botonPresionado && Time.time - tiempoBotonPress > ventanaTiempo)
        {
            botonPresionado = false;
            DesactivarModoGolpe();
        }
    }

    // -------------------------------------------------------
    public void OnGolpe()
    {
        if (!esJugador || destruida) return;
        botonPresionado  = true;
        tiempoBotonPress = Time.time;
        if (boxCollider != null)
            boxCollider.size = tamanoColliderOriginal + Vector3.one * extraTamanoCollider;

        // Mostrar animación de golpe inmediatamente al presionar J
        animacion?.AnimarGolpe();
    }

    public void OnGolpe(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        if (ctx.performed) OnGolpe();
    }

    // -------------------------------------------------------
    /// <summary>
    /// Consulta si la raqueta puede resistir un impacto a la velocidad indicada.
    /// Lógica por defecto de la raqueta regular: la CPU solo resiste si activó un
    /// golpe (golpeActivado); el jugador necesita presionar J a tiempo.
    /// Las subclases o controladores (p. ej. la raqueta de Colossus) pueden
    /// SOBRESCRIBIR este método para consumir cargas de resistencia/inmunidad
    /// antes de decidir si la raqueta se rompe (modo épico/Inhumano).
    /// </summary>
    public virtual bool PuedeResistir(float velocidadPelota)
    {
        if (destruida) return false;

        if (velocidadPelota >= velocidadDestruccion)
        {
            if (esJugador)
            {
                // Jugador necesita presionar J a tiempo
                bool aTiempo = botonPresionado &&
                               (Time.time - tiempoBotonPress) <= ventanaTiempo;
                if (!aTiempo)
                {
                    StartCoroutine(Destruir());
                    return false;
                }
            }
            else
            {
                // CPU resiste si activó el golpe (golpeActivado)
                if (!golpeActivado)
                {
                    StartCoroutine(Destruir());
                    return false;
                }
            }
        }
        return true;
    }

    // -------------------------------------------------------
    void ActivarModoGolpe()
    {
        if (boxCollider == null) return;
        boxCollider.size = tamanoColliderOriginal + Vector3.one * extraTamanoCollider;
    }

    void DesactivarModoGolpe()
    {
        if (boxCollider == null) return;
        boxCollider.size = tamanoColliderOriginal;
    }

    // -------------------------------------------------------
    /// velocidadPelota: solo anima y agranda collider si supera velocidadDestruccion
    public void ActivarGolpeCPU(float velocidadPelota)
    {
        if (destruida || golpeActivado) return;
        golpeActivado = true;

        // Solo activar collider extra y animación si la pelota va rápido
        if (velocidadPelota >= velocidadDestruccion)
        {
            if (boxCollider != null)
                boxCollider.size = tamanoColliderOriginal + Vector3.one * extraTamanoCollider;
            animacion?.AnimarGolpe();
            CancelInvoke(nameof(DesactivarModoGolpe));
            Invoke(nameof(DesactivarModoGolpe), 0.3f);
        }
    }

    public void ResetGolpe() => golpeActivado = false;

    // -------------------------------------------------------
    IEnumerator Destruir()
    {
        if (destruida) yield break;
        destruida = true;
        DesactivarModoGolpe();

        // Guardar posición actual — la animación ocurre aquí, no en el centro
        Vector3 posicionDestruccion = transform.position;
        Vector3 escalaInicio        = transform.localScale;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.3f;
            transform.position   = posicionDestruccion;
            transform.localScale = Vector3.Lerp(escalaInicio, Vector3.zero, Mathf.Clamp01(t));
            yield return null;
        }

        transform.localScale = Vector3.zero;
        if (meshRenderer != null) meshRenderer.enabled = false;
        if (boxCollider != null)  boxCollider.enabled  = false;

        Debug.Log($"[Raqueta] {gameObject.name} DESTRUIDA");
    }

    /// <summary>
    /// Fuerza la destrucción visual de la raqueta: marca destruida y lanza la
    /// animación de colapso (malla desactivada, collider desactivado). La usan
    /// sistemas externos (p. ej. BossColossus cuando su inmunidad se agota) para
    /// que la raqueta del CPU se rompa de verdad y luego se regenere con Regenerar().
    /// </summary>
    public void ForzarDestruccion()
    {
        if (destruida) return;
        StartCoroutine(Destruir());
    }

    // -------------------------------------------------------
    public void Regenerar()
    {
        if (!destruida) return;
        StartCoroutine(EsperarYRegenerar());
    }

    IEnumerator EsperarYRegenerar()
    {
        // Esperar a que la animación de destrucción termine
        yield return new WaitUntil(() => transform.localScale.sqrMagnitude < 0.0001f);
        yield return new WaitForSeconds(0.3f);

        if (!destruida) yield break;

        yield return StartCoroutine(AnimarRegeneracion());
    }

    IEnumerator AnimarRegeneracion()
    {
        // Si la raqueta base debe permanecer oculta (combate de Zeus activo),
        // no re-activar su renderer al regenerarse: evita el destello de la
        // raqueta default durante la animación de reaparición.
        if (meshRenderer != null && !ocultarRendererBaseAlRegenerar) meshRenderer.enabled = true;
        if (boxCollider != null)  boxCollider.enabled  = true;

        destruida       = false;
        botonPresionado = false;
        golpeActivado   = false;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.4f;
            transform.localScale = Vector3.Lerp(Vector3.zero, escalaOriginal,
                                                 Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }

        transform.localScale = escalaOriginal;
        Debug.Log($"[Raqueta] {gameObject.name} REGENERADA");

        // Avisar a posibles suscriptores (p. ej. BossManager para re-ocultar la
        // raqueta base y mantener únicamente el visual Zeus durante el combate).
        if (onRegenerada != null) onRegenerada();
    }

    // -------------------------------------------------------
    /// <summary>
    /// Cambia el material de la raqueta sin modificar el prefab completo.
    /// Usado por el modo torneo para aplicar skins a la raqueta del CPU.
    /// </summary>
    public void AplicarMaterial(Material material)
    {
        if (meshRenderer != null)
            meshRenderer.material = material;
    }

    // -------------------------------------------------------
    public bool EstaDestruida  => destruida;
    public bool BotonPresionado => botonPresionado;
}
