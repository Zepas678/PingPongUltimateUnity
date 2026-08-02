using UnityEngine;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Paneles")]
    public GameObject panelHUD;
    public GameObject panelMenuPrincipal;
    public GameObject panelSkins;
    public GameObject panelModos;
    public GameObject panelMapas;
    public GameObject panelDificultad;
    public GameObject panelPausa;
    public GameObject panelGameOver;
    [Header("Panel Torneo")]
    public GameObject panelTorneo;

    [Header("Marcador")]
    public TMP_Text scoreJugador;
    public TMP_Text scoreCPU;

    [Header("Toast de punto")]
    public TMP_Text toastPunto;
    public float    toastDuracion = 2f;

    [Header("Game Over")]
    public TMP_Text gameOverTexto;
    public TMP_Text gameOverMarcadorFinal;
    public GameObject botonReintentar;
    public GameObject botonContinuarTorneo;
    public GameObject botonNuevoTorneo;

    [Header("Nombre del jugador")]
    public string nombreJugador = "Jugador";

    // Flag para saber si venimos del modo PvP al confirmar mapa
    private bool esperandoMapaPvP = false;

    // -------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        MostrarPanel(panelMenuPrincipal);
        if (toastPunto != null)
            toastPunto.gameObject.SetActive(false);
    }

    // -------------------------------------------------------
    public void MostrarPanel(GameObject panel)
    {
        panelHUD?.SetActive(false);
        panelMenuPrincipal?.SetActive(false);
        panelSkins?.SetActive(false);
        panelModos?.SetActive(false);
        panelMapas?.SetActive(false);
        panelDificultad?.SetActive(false);
        panelPausa?.SetActive(false);
        panelGameOver?.SetActive(false);
        panelTorneo?.SetActive(false);
        panel?.SetActive(true);
    }

    // -------------------------------------------------------
    public void OnClickPlay()
    {
        MostrarPanel(panelSkins);
    }

    public void OnClickSkins()
    {
        MostrarPanel(panelSkins);
    }

    public void OnClickModoVsCPU_DesdeSkins()
    {
        MostrarPanel(panelModos);
    }

    public void OnClickModoVsCPU()
    {
        esperandoMapaPvP = false;
        MostrarPanel(panelMapas);
    }

    // Confirmar mapa — va a dificultad si es VS CPU, o inicia PvP directamente
    public void OnClickConfirmarMapa()
    {
        if (esperandoMapaPvP)
        {
            esperandoMapaPvP = false;
            GameManager.Instance?.IniciarPartidaPvP();
            MostrarPanel(panelHUD);
        }
        else
        {
            MostrarPanel(panelDificultad);
        }
    }

    public void OnClickVolverMapas()
    {
        MostrarPanel(panelModos);
    }

    // --- Card PvP --- solo marca el flag y va a mapas, NO inicia la partida todavía
    public void OnClickModoPvP()
    {
        esperandoMapaPvP = true;
        MostrarPanel(panelMapas);
    }

    // --- Card Torneo ---
    public void OnClickModoTorneo()
    {
        Debug.Log("[UIManager] Abriendo panel de torneo.");
        MostrarPanel(panelTorneo);
    }

    // --- Cards no implementados ---
    public void OnClickModoProximamente()
    {
        Debug.Log("[UIManager] Modo aún no disponible");
    }

    // --- Modo Práctica ---
    public void OnClickModoPractica()
    {
        GameManager.Instance?.DetenerVolcanes();

        var existing = FindObjectOfType<PracticeModeManager>();
        PracticeModeManager pm;

        if (existing == null)
        {
            var pmGO = new GameObject("PracticeModeManager");
            pm = pmGO.AddComponent<PracticeModeManager>();
        }
        else
        {
            pm = existing;
            pm.gameObject.SetActive(true);
        }

        pm.ball          = GameManager.Instance != null ? GameManager.Instance.ball : null;
        pm.playerRaqueta = GameManager.Instance != null ? GameManager.Instance.golpeRaquetaJugador : null;
        if (GameManager.Instance != null && GameManager.Instance.cpu != null)
            pm.wallSpawnTransform = GameManager.Instance.cpu.transform;

        var ui = FindObjectOfType<PracticeUI>();
        if (ui == null)
        {
            var uiGO = new GameObject("PracticeUI");
            ui = uiGO.AddComponent<PracticeUI>();
        }

        ui.Initialize(pm);
        ui.ShowPracticeUI();
        pm.ResetPracticeSession();

        MapManager.Instance?.SeleccionarPractica();
        MostrarPanel(panelHUD);
    }

    public void OnClickVolverModos()
    {
        MostrarPanel(panelModos);
    }

    public void OnClickDificultadFacil()    => IniciarPartida(Dificultad.Facil);
    public void OnClickDificultadDificil()  => IniciarPartida(Dificultad.Dificil);
    public void OnClickDificultadInhumano() => IniciarPartida(Dificultad.Inhumano);

    public void OnClickVolverAlMenu()
    {
        GameManager.Instance?.DetenerVolcanes();
        GameManager.Instance?.VolverAlMenu();

        var practiceUI = FindObjectOfType<PracticeUI>();
        if (practiceUI != null)
        {
            practiceUI.HidePracticeUI();
            practiceUI.gameObject.SetActive(false);
        }

        MostrarPanel(panelMenuPrincipal);
    }

    void IniciarPartida(Dificultad d)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.dificultadSeleccionada = d;
            GameManager.Instance.IniciarPartida();
        }
        MostrarPanel(panelHUD);
    }

    // -------------------------------------------------------
    public void OnClickContinuar()
    {
        MostrarPanel(panelHUD);
        Time.timeScale = 1f;
    }

    public void OnClickPausa()
    {
        panelPausa?.SetActive(true);
        Time.timeScale = 0f;
    }

    public void OnClickSalir()
    {
        GameManager.Instance?.DetenerVolcanes();
        Time.timeScale = 1f;
        MostrarPanel(panelMenuPrincipal);
        GameManager.Instance?.VolverAlMenu();

        var practiceUI = FindObjectOfType<PracticeUI>();
        if (practiceUI != null)
        {
            practiceUI.HidePracticeUI();
            practiceUI.gameObject.SetActive(false);
        }
    }

    public void OnClickReintentar()
    {
        GameManager.Instance?.DetenerVolcanes();
        MostrarPanel(panelHUD);
        GameManager.Instance?.RestartGame();
    }

    // -------------------------------------------------------
    /// <summary>Llamado por el botón Continuar del Game Over en modo torneo.</summary>
    public void OnClickContinuarTorneo()
    {
        Debug.Log("[UIManager] Continuar torneo presionado.");

        // Detener volcanes y restaurar time scale
        GameManager.Instance?.DetenerVolcanes();
        Time.timeScale = 1f;

        // Volver al panel del torneo para ver el bracket actualizado
        MostrarPanel(panelTorneo);
    }

    // -------------------------------------------------------
    /// <summary>Llamado por el botón Nuevo Torneo cuando el jugador fue eliminado o ganó.</summary>
    public void OnClickNuevoTorneo()
    {
        Debug.Log("[UIManager] Nuevo Torneo presionado.");

        // Detener volcanes y restaurar time scale
        GameManager.Instance?.DetenerVolcanes();
        Time.timeScale = 1f;

        // Reiniciar el torneo con nuevos competidores
        // CrearNuevoTorneo() ya invoca OnTorneoActualizado, el bracket se actualiza solo
        if (TournamentUIManager.TorneoActual != null)
        {
            TournamentUIManager.TorneoActual.CrearNuevoTorneo();
        }

        // Mostrar el panel del torneo
        MostrarPanel(panelTorneo);
    }

    // -------------------------------------------------------
    public void ActualizarMarcador(int puntosJugador, int puntosCPU)
    {
        if (scoreJugador != null) scoreJugador.text = puntosJugador.ToString();
        if (scoreCPU     != null) scoreCPU.text     = puntosCPU.ToString();
    }

    // -------------------------------------------------------
    public void MostrarToast(bool fueJugador)
    {
        if (toastPunto == null) return;

        string nombre = fueJugador ? nombreJugador.ToUpper() : "CPU";
        toastPunto.text  = $"¡ {nombre} ANOTA !";
        toastPunto.color = fueJugador
            ? new Color(0.95f, 0.25f, 0.25f)
            : new Color(1.0f, 0.85f, 0.1f);

        StopCoroutine(nameof(OcultarToast));
        StartCoroutine(nameof(OcultarToast));
    }

    IEnumerator OcultarToast()
    {
        toastPunto.gameObject.SetActive(true);

        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            SetToastAlpha(t / 0.2f);
            yield return null;
        }

        yield return new WaitForSeconds(toastDuracion);

        t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            SetToastAlpha(1f - (t / 0.3f));
            yield return null;
        }

        toastPunto.gameObject.SetActive(false);
    }

    void SetToastAlpha(float a)
    {
        if (toastPunto == null) return;
        Color c = toastPunto.color;
        c.a = a;
        toastPunto.color = c;
    }

    // -------------------------------------------------------
    public void MostrarGameOver(bool ganoJugador, bool esTorneo = false)
    {
        MostrarPanel(panelGameOver);

        if (gameOverTexto != null)
            gameOverTexto.text = ganoJugador
                ? $"¡ {nombreJugador.ToUpper()} GANA !"
                : "¡ CPU GANA !";

        if (gameOverMarcadorFinal != null && GameManager.Instance != null)
            gameOverMarcadorFinal.text = $"{GameManager.Instance.PlayerScore}  —  {GameManager.Instance.CpuScore}";

        if (!esTorneo)
        {
            // Modo normal: solo botón Reintentar
            if (botonReintentar != null)        botonReintentar.SetActive(true);
            if (botonContinuarTorneo != null)   botonContinuarTorneo.SetActive(false);
            if (botonNuevoTorneo != null)       botonNuevoTorneo.SetActive(false);
        }
        else
        {
            // Modo torneo: determinar si el jugador sigue vivo o fue eliminado
            bool torneoTerminado = TournamentUIManager.TorneoActual != null
                && TournamentUIManager.TorneoActual.RondaActual == TournamentManager.Ronda.Terminado;

            bool jugadorTienePartido = TournamentUIManager.TorneoActual != null
                && TournamentUIManager.TorneoActual.ObtenerPartidoDelJugador() != null;

            // ObtenerPartidoDelJugador() solo devuelve partidos con !jugado.
            // Si no hay partido pendiente y el torneo no terminó, el jugador fue eliminado.
            bool mostrarNuevoTorneo = torneoTerminado || (!jugadorTienePartido && !torneoTerminado);

            if (botonReintentar != null)        botonReintentar.SetActive(false);
            if (botonContinuarTorneo != null)   botonContinuarTorneo.SetActive(!mostrarNuevoTorneo);
            if (botonNuevoTorneo != null)       botonNuevoTorneo.SetActive(mostrarNuevoTorneo);
        }
    }
}