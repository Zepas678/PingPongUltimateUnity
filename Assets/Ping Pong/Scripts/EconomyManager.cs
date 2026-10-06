using UnityEngine;
using System;
using System.Collections;
using TMPro;

/// <summary>
/// Economía del juego (Mapas y Skins). Singleton: EconomyManager.Instance.
/// - Clave principal: "POLLOCOINS_JUGADOR" (nueva).
/// - Clave legada: "MONEDAS_JUGADOR" (se migra sola la primera vez).
/// </summary>
public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    public const string CLAVE_POLLOCOINS = "POLLOCOINS_JUGADOR";
    public const string CLAVE_MONEDAS = "MONEDAS_JUGADOR"; // legada, solo migración

    [Header("Economía")]
    [Tooltip("Pollocoins iniciales si no hay guardado previo.")]
    public int monedasIniciales = 200;

    [Header("UI Shop (saldo permanente)")]
    [Tooltip("TMP_Text permanente en el Panel de la tienda. Se actualiza solo vía OnPollocoinsChanged.")]
    public TMP_Text textoShopPollocoins;

    [Header("Notificación flotante (HUD / post-partida)")]
    [Tooltip("Panel que se muestra unos segundos al ganar Pollocoins.")]
    public GameObject panelNotificacionPollocoins;
    [Tooltip("Texto dentro del panel de notificación (ej: +50 Pollocoins).")]
    public TMP_Text textoNotificacionPollocoins;
    [Tooltip("Segundos visible la notificación.")]
    public float duracionNotificacion = 2.5f;

    [Header("Widget Pollocoins (contador animado)")]
    [Tooltip("Raíz del Widget_Pollocoins (se muestra/oculta con deslizamiento). Si se deja vacío se usa el padre de textoShopPollocoins.")]
    public GameObject widgetPollocoins;
    [Tooltip("Texto del widget donde se anima el conteo. Si se deja vacío se usa textoShopPollocoins.")]
    public TMP_Text textoWidgetPollocoins;
    [Tooltip("Duración del conteo rápido desde el valor anterior al nuevo saldo.")]
    public float duracionConteoWidget = 0.8f;
    [Tooltip("Segundos que el widget queda visible tras una recompensa antes de retraerse.")]
    public float duracionVisibleWidget = 2.5f;
    [Tooltip("Desplazamiento fuera de pantalla (anclado) cuando está oculto. En píxeles hacia la IZQUIERDA.")]
    public float widgetOffsetOcultoX = 300f;
    [Tooltip("Duración del deslizamiento mostrar/ocultar.")]
    public float duracionSlideWidget = 0.35f;

    /// <summary>Se dispara cada vez que cambia el saldo (nuevo total).</summary>
    public event Action<int> OnPollocoinsChanged;
    /// <summary>Alias legado: se dispara junto a OnPollocoinsChanged.</summary>
    public event Action<int> OnCoinsChanged;

    Coroutine corrutinaNotificacion;
    Coroutine corrutinaConteoWidget;
    Coroutine corrutinaAutoOcultarWidget;
    // Último valor pintado en el widget (punto de partida del conteo rápido).
    int ultimoValorWidget = -1;
    // Posición anclada original del widget (visible) para el slide.
    Vector2 widgetPosVisible = Vector2.zero;
    bool widgetPosCapturada = false;

    /// <summary>Saldo actual (lee de PlayerPrefs).</summary>
    public int Pollocoins
    {
        get => PlayerPrefs.GetInt(CLAVE_POLLOCOINS, monedasIniciales);
        private set
        {
            PlayerPrefs.SetInt(CLAVE_POLLOCOINS, Mathf.Max(0, value));
            PlayerPrefs.Save();
            int total = PlayerPrefs.GetInt(CLAVE_POLLOCOINS, 0);
            OnPollocoinsChanged?.Invoke(total);
            OnCoinsChanged?.Invoke(total);
        }
    }

    /// <summary>Alias legado de Pollocoins (misma bolsa).</summary>
    public int Monedas
    {
        get => Pollocoins;
        private set => Pollocoins = value;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Migración: si hay saldo legacy y aún no hay clave nueva, copiarlo.
        if (!PlayerPrefs.HasKey(CLAVE_POLLOCOINS))
        {
            int inicial = PlayerPrefs.HasKey(CLAVE_MONEDAS)
                ? PlayerPrefs.GetInt(CLAVE_MONEDAS, monedasIniciales)
                : monedasIniciales;
            PlayerPrefs.SetInt(CLAVE_POLLOCOINS, inicial);
            PlayerPrefs.Save();
        }
    }

    void Start()
    {
        SuscribirTextoShop();
        ResolverWidgetPollocoins();
        ultimoValorWidget = Pollocoins;
        PintarWidgetInstantaneo(Pollocoins);
        // Notificar saldo inicial para pintar HUD/tienda al arrancar.
        OnPollocoinsChanged?.Invoke(Pollocoins);
        OnCoinsChanged?.Invoke(Pollocoins);
    }

    /// <summary>Saldo actual de monedas.</summary>
    public int ObtenerMonedas() => Pollocoins;

    /// <summary>Suma monedas (recompensas por victoria, etc.).</summary>
    public void AgregarMonedas(int cantidad) => AgregarPollocoins(cantidad);

    /// <summary>¿Alcanza el saldo para el costo indicado?</summary>
    public bool TieneMonedasSuficientes(int costo) => TieneSuficiente(costo);

    /// <summary>Intenta cobrar el costo. true y descuenta si alcanza; false si no.</summary>
    public bool ConsumirMonedas(int costo) => ConsumirPollocoins(costo);

    // ─── API Pollocoins (nueva) ───
    /// <summary>Saldo actual de Pollocoins.</summary>
    public int ObtenerPollocoins() => Pollocoins;

    /// <summary>Suma Pollocoins (recompensas, etc.).</summary>
    public void AgregarPollocoins(int cantidad)
    {
        if (cantidad == 0) return;
        Pollocoins = Pollocoins + cantidad;
    }

    /// <summary>¿Alcanza el saldo para el costo?</summary>
    public bool TieneSuficiente(int costo) => Pollocoins >= costo;

    /// <summary>Suma Pollocoins y notifica a la UI (alias expreso para recompensas).</summary>
    public void SumarPollocoins(int cantidad) => AgregarPollocoins(cantidad);

    /// <summary>Intenta cobrar. true y descuenta si alcanza; false sin cambios si no.</summary>
    public bool ConsumirPollocoins(int costo)
    {
        if (costo <= 0) return true;
        if (!TieneSuficiente(costo)) return false;
        Pollocoins = Pollocoins - costo;
        // Refresco inmediato: el setter ya invoca el evento, pero se fuerza aquí
        // el pintado directo + re-notificación por si algún texto se suscribió tarde.
        NotificarCambioPollocoins();
        return true;
    }

    /// <summary>
    /// Fuerza la actualización inmediata de la UI del saldo: pinta directo
    /// textoShopPollocoins y re-dispara OnPollocoinsChanged con el total actual.
    /// Llamar tras comprar/desbloquear para que el contador baje al instante.
    /// </summary>
    public void NotificarCambioPollocoins()
    {
        int total = Pollocoins;
        if (textoShopPollocoins != null)
            textoShopPollocoins.text = total.ToString();
        OnPollocoinsChanged?.Invoke(total);
        OnCoinsChanged?.Invoke(total);
    }

    void OnEnable() { SuscribirTextoShop(); ResolverWidgetPollocoins(); }

    void OnDisable()
    {
        if (textoShopPollocoins != null)
            OnPollocoinsChanged -= PintarTextoShop;
    }

    // ─── UI Shop: saldo permanente ───
    void SuscribirTextoShop()
    {
        if (textoShopPollocoins == null) return;
        textoShopPollocoins.text = Pollocoins.ToString();
        OnPollocoinsChanged -= PintarTextoShop;
        OnPollocoinsChanged += PintarTextoShop;
    }

    void PintarTextoShop(int total)
    {
        if (textoShopPollocoins != null && textoShopPollocoins != TextoWidgetEfectivo())
            textoShopPollocoins.text = total.ToString();
        // El widget lleva su propio conteo animado (no pintado directo).
        AnimarConteoWidget(total);
    }

    // ─── Widget Pollocoins: conteo rápido + mostrar/ocultar ───
    /// <summary>Resuelve widget/texto por defecto si las casillas están vacías.</summary>
    void ResolverWidgetPollocoins()
    {
        if (textoWidgetPollocoins == null)
            textoWidgetPollocoins = textoShopPollocoins;
        if (widgetPollocoins == null && textoWidgetPollocoins != null)
            widgetPollocoins = textoWidgetPollocoins.gameObject;
        // Si el texto de shop vive dentro del widget, no duplicar pintado.
        CapturarPosWidget();
    }

    TMP_Text TextoWidgetEfectivo()
    {
        return textoWidgetPollocoins != null ? textoWidgetPollocoins : textoShopPollocoins;
    }

    void CapturarPosWidget()
    {
        if (widgetPosCapturada || widgetPollocoins == null) return;
        RectTransform rt = widgetPollocoins.GetComponent<RectTransform>();
        if (rt == null) return;
        // Posición visible exacta (anchoredPosition) guardada en Start().
        widgetPosVisible = rt.anchoredPosition;
        widgetPosCapturada = true;
    }

    /// <summary>Posición oculta: fuera de pantalla por la IZQUIERDA.</summary>
    Vector2 PosicionOcultaWidget()
    {
        return widgetPosVisible - new Vector2(Mathf.Abs(widgetOffsetOcultoX), 0f);
    }

    void PintarWidgetInstantaneo(int total)
    {
        TMP_Text texto = TextoWidgetEfectivo();
        if (texto != null) texto.text = total.ToString();
        ultimoValorWidget = total;
    }

    /// <summary>
    /// Anima el incremento numérico (efecto contador rápido con corrutina):
    /// sube desde el valor anterior hasta el nuevo saldo en duracionConteoWidget.
    /// Devuelve la corrutina para poder esperarla (secuencia post-partida).
    /// Sin LeanTween/DOTween: solo Unity puro.
    /// </summary>
    public void AnimarConteoWidget(int nuevoTotal)
    {
        CorrutinaConteoWidget(nuevoTotal);
    }

    /// <summary>Inicia el conteo y devuelve su corrutina (null si fue instantáneo).</summary>
    public Coroutine CorrutinaConteoWidget(int nuevoTotal)
    {
        TMP_Text texto = TextoWidgetEfectivo();
        if (texto == null) return null;
        int desde = ultimoValorWidget < 0 ? nuevoTotal : ultimoValorWidget;
        if (desde == nuevoTotal)
        {
            texto.text = nuevoTotal.ToString();
            ultimoValorWidget = nuevoTotal;
            return null;
        }
        if (corrutinaConteoWidget != null) StopCoroutine(corrutinaConteoWidget);
        corrutinaConteoWidget = StartCoroutine(ConteoRapidoWidget(texto, desde, nuevoTotal));
        return corrutinaConteoWidget;
    }

    IEnumerator ConteoRapidoWidget(TMP_Text texto, int desde, int hasta)
    {
        float dur = Mathf.Max(0.1f, duracionConteoWidget);
        float t = 0f;
        while (t < 1f)
        {
            if (texto == null) yield break;
            t += Time.unscaledDeltaTime / dur;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // easeOutCubic
            texto.text = Mathf.RoundToInt(Mathf.Lerp(desde, hasta, e)).ToString();
            yield return null;
        }
        if (texto != null) texto.text = hasta.ToString();
        ultimoValorWidget = hasta;
        corrutinaConteoWidget = null;
    }

    /// <summary>
    /// Muestra/desliza el widget en pantalla: de posicionOculta a posicionVisible.
    /// Detiene cualquier secuencia previa para evitar cierres prematuros.
    /// </summary>
    public void MostrarWidgetPollocoins()
    {
        ResolverWidgetPollocoins();
        if (widgetPollocoins == null) return;
        CapturarPosWidget();
        DetenerSecuenciaWidget();
        RectTransform rt = widgetPollocoins.GetComponent<RectTransform>();
        // Partir desde la posición oculta (izquierda) para el slide-in.
        if (rt != null) rt.anchoredPosition = PosicionOcultaWidget();
        widgetPollocoins.SetActive(true);
        StopAllSlideWidget();
        corrutinaSlideWidget = StartCoroutine(SlideWidget(widgetPosVisible));
    }

    /// <summary>
    /// Oculta/retrae el widget: de posicionVisible a posicionOculta (izquierda)
    /// y LUEGO se desactiva (SetActive(false)).
    /// </summary>
    public void OcultarWidgetPollocoins()
    {
        if (widgetPollocoins == null) return;
        CapturarPosWidget();
        StopAllSlideWidget();
        corrutinaSlideWidget = StartCoroutine(SlideWidget(PosicionOcultaWidget(), desactivarAlTerminar: true));
    }

    Coroutine corrutinaSlideWidget;
    void StopAllSlideWidget()
    {
        if (corrutinaSlideWidget != null) { StopCoroutine(corrutinaSlideWidget); corrutinaSlideWidget = null; }
    }

    IEnumerator SlideWidget(Vector2 destino, bool desactivarAlTerminar = false)
    {
        RectTransform rt = widgetPollocoins != null ? widgetPollocoins.GetComponent<RectTransform>() : null;
        if (rt == null)
        {
            if (desactivarAlTerminar && widgetPollocoins != null) widgetPollocoins.SetActive(false);
            yield break;
        }
        Vector2 origen = rt.anchoredPosition;
        float dur = Mathf.Max(0.05f, duracionSlideWidget);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            rt.anchoredPosition = Vector2.Lerp(origen, destino, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }
        rt.anchoredPosition = destino;
        if (desactivarAlTerminar && widgetPollocoins != null) widgetPollocoins.SetActive(false);
        corrutinaSlideWidget = null;
    }

    /// <summary>
    /// Secuencia post-partida (MostrarGameOver):
    /// 1) Detiene cualquier corrutina previa del widget (anti cierres prematuros).
    /// 2) Muestra/desliza el widget hacia adentro.
    /// 3) Espera a que termine la animación de conteo numérico.
    /// 4) Espera al menos 3.5s visibles para que se vea lo ganado.
    /// 5) Desliza hacia afuera (izquierda) hasta ocultarlo por completo.
    /// </summary>
    public void MostrarRecompensaPostPartida()
    {
        ResolverWidgetPollocoins();
        if (widgetPollocoins == null) return;
        CapturarPosWidget();
        DetenerSecuenciaWidget();
        corrutinaSecuenciaWidget = StartCoroutine(SecuenciaRecompensaWidget());
    }

    Coroutine corrutinaSecuenciaWidget;
    /// <summary>Detiene conteo + slide + secuencia previa del widget.</summary>
    void DetenerSecuenciaWidget()
    {
        if (corrutinaConteoWidget != null) { StopCoroutine(corrutinaConteoWidget); corrutinaConteoWidget = null; }
        StopAllSlideWidget();
        if (corrutinaAutoOcultarWidget != null) { StopCoroutine(corrutinaAutoOcultarWidget); corrutinaAutoOcultarWidget = null; }
        if (corrutinaSecuenciaWidget != null) { StopCoroutine(corrutinaSecuenciaWidget); corrutinaSecuenciaWidget = null; }
    }

    IEnumerator SecuenciaRecompensaWidget()
    {
        // 2) Slide-in desde la izquierda.
        RectTransform rt = widgetPollocoins.GetComponent<RectTransform>();
        if (rt != null) rt.anchoredPosition = PosicionOcultaWidget();
        widgetPollocoins.SetActive(true);
        StopAllSlideWidget();
        corrutinaSlideWidget = StartCoroutine(SlideWidget(widgetPosVisible));
        if (corrutinaSlideWidget != null) yield return corrutinaSlideWidget;

        // 3) Conteo numérico hasta el saldo actual (esperar a que termine).
        Coroutine conteo = CorrutinaConteoWidget(Pollocoins);
        if (conteo != null) yield return conteo;

        // 4) Mínimo 3.5s visibles en pantalla.
        yield return new WaitForSecondsRealtime(Mathf.Max(3.5f, duracionVisibleWidget));

        // La tienda gestiona su propia visibilidad permanente: no retraer si sigue activa.
        if (UIManager.Instance != null && UIManager.Instance.TiendaActiva())
            { corrutinaSecuenciaWidget = null; yield break; }

        // 5) Slide-out a la izquierda + desactivar.
        StopAllSlideWidget();
        corrutinaSlideWidget = StartCoroutine(SlideWidget(PosicionOcultaWidget(), desactivarAlTerminar: true));
        if (corrutinaSlideWidget != null) yield return corrutinaSlideWidget;
        corrutinaSecuenciaWidget = null;
    }

    IEnumerator AutoOcultarWidgetDiferido()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, duracionVisibleWidget));
        // La tienda gestiona su propia visibilidad permanente: no retraer si el
        // panel de tienda está activo (UIManager decide).
        if (UIManager.Instance != null && UIManager.Instance.TiendaActiva())
            { corrutinaAutoOcultarWidget = null; yield break; }
        OcultarWidgetPollocoins();
        corrutinaAutoOcultarWidget = null;
    }

    // ─── Notificación flotante HUD / post-partida ───
    /// <summary>Suma la recompensa y muestra el panel flotante X segundos.</summary>
    public void MostrarNotificacionPollocoins(int ganados)
    {
        if (ganados != 0) AgregarPollocoins(ganados);
        if (panelNotificacionPollocoins == null) return;
        if (textoNotificacionPollocoins != null)
            textoNotificacionPollocoins.text = "+" + ganados + " Pollocoins";
        if (corrutinaNotificacion != null) StopCoroutine(corrutinaNotificacion);
        corrutinaNotificacion = StartCoroutine(OcultarNotificacionDiferido());
    }

    IEnumerator OcultarNotificacionDiferido()
    {
        if (panelNotificacionPollocoins != null)
            panelNotificacionPollocoins.SetActive(true);
        yield return new WaitForSeconds(Mathf.Max(0.5f, duracionNotificacion));
        if (panelNotificacionPollocoins != null)
            panelNotificacionPollocoins.SetActive(false);
        corrutinaNotificacion = null;
    }

    // ─── Recompensas post-partida (cantidades configurables en Inspector) ───
    [Header("Recompensas VS CPU (ganar)")]
    [Tooltip("Pollocoins por ganar en Fácil.")]
    public int recompensaCpuFacil = 25;
    [Tooltip("Pollocoins por ganar en Media/Normal.")]
    public int recompensaCpuNormal = 50;
    [Tooltip("Pollocoins por ganar en Difícil.")]
    public int recompensaCpuDificil = 100;
    [Tooltip("Pollocoins por ganar en Inhumano.")]
    public int recompensaCpuInhumano = 150;
    [Tooltip("Consuelo por perder contra la CPU (0 = nada).")]
    public int recompensaCpuDerrota = 5;

    [Header("Recompensas Torneo (ganar)")]
    [Tooltip("Pollocoins por ganar Octavos.")]
    public int recompensaTorneoOctavos = 30;
    [Tooltip("Pollocoins por ganar Cuartos.")]
    public int recompensaTorneoCuartos = 60;
    [Tooltip("Pollocoins por ganar Semifinal.")]
    public int recompensaTorneoSemifinal = 120;
    [Tooltip("Pollocoins por ganar la Final.")]
    public int recompensaTorneoFinal = 250;
    [Tooltip("Consuelo por perder en torneo (0 = nada).")]
    public int recompensaTorneoDerrota = 10;

    [Header("Recompensas Jefes (ganar)")]
    [Tooltip("Pollocoins por vencer a Zeus.")]
    public int recompensaJefeZeus = 150;
    [Tooltip("Pollocoins por vencer a Colossus.")]
    public int recompensaJefeColossus = 250;
    [Tooltip("Pollocoins por vencer a Mirage.")]
    public int recompensaJefeMirage = 400;
    [Tooltip("Recompensa genérica si el id de jefe no coincide con ninguno.")]
    public int recompensaJefeGenerico = 150;

    /// <summary>
    /// Recompensa VS CPU: suma Pollocoins segun dificultad y notifica.
    /// Devuelve la cantidad otorgada (0 si no hubo).
    /// </summary>
    public int OtorgarRecompensaCPU(Dificultad dificultad, bool gano)
    {
        int cantidad = 0;
        if (gano)
        {
            switch (dificultad)
            {
                case Dificultad.Facil:    cantidad = recompensaCpuFacil; break;
                case Dificultad.Media:    cantidad = recompensaCpuNormal; break;
                case Dificultad.Dificil:  cantidad = recompensaCpuDificil; break;
                case Dificultad.Inhumano: cantidad = recompensaCpuInhumano; break;
            }
        }
        else
        {
            cantidad = recompensaCpuDerrota;
        }
        if (cantidad != 0) { SumarPollocoins(cantidad); NotificarCambioPollocoins(); }
        return cantidad;
    }

    /// <summary>
    /// Recompensa de torneo segun la ronda jugada. Devuelve lo otorgado.
    /// TournamentManager.Ronda: 0 Octavos, 1 Cuartos, 2 Semifinal, 3 Final, 4 Terminado.
    /// </summary>
    public int OtorgarRecompensaTorneo(int rondaTorneo, bool gano)
    {
        int cantidad = 0;
        if (gano)
        {
            switch (rondaTorneo)
            {
                case 0:  cantidad = recompensaTorneoOctavos; break;
                case 1:  cantidad = recompensaTorneoCuartos; break;
                case 2:  cantidad = recompensaTorneoSemifinal; break;
                case 3:
                case 4:  cantidad = recompensaTorneoFinal; break;
                default: cantidad = recompensaTorneoOctavos; break;
            }
        }
        else
        {
            cantidad = recompensaTorneoDerrota;
        }
        if (cantidad != 0) { SumarPollocoins(cantidad); NotificarCambioPollocoins(); }
        return cantidad;
    }

    /// <summary>
    /// Recompensa por vencer a un jefe (id o nombre: zeus/colossus/mirage).
    /// Devuelve lo otorgado.
    /// </summary>
    public int OtorgarRecompensaJefe(string idJefe)
    {
        int cantidad = recompensaJefeGenerico;
        string id = (idJefe ?? "").Trim().ToLowerInvariant();
        if (id.Contains("zeus"))          cantidad = recompensaJefeZeus;
        else if (id.Contains("colossus")) cantidad = recompensaJefeColossus;
        else if (id.Contains("mirage"))   cantidad = recompensaJefeMirage;
        if (cantidad != 0) { SumarPollocoins(cantidad); NotificarCambioPollocoins(); }
        return cantidad;
    }
}
