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

    [Header("Panel Bosses")]
    public GameObject panelBosses;

    [Header("Marcador")]
    public TMP_Text scoreJugador;
    public TMP_Text scoreCPU;

    [Header("Toast de punto")]
    public TMP_Text toastPunto;
    public float    toastDuracion = 2f;

    [Header("Game Over")]
    public TMP_Text gameOverTexto;
    public TMP_Text gameOverMarcadorFinal;
    [Tooltip("Texto de recompensa en el panel Victoria/Derrota (ej: +25 POLLOCOINS). Se deja vacío si no hubo.")]
    public TMP_Text gameOverTextoPollocoins;
    public GameObject botonReintentar;
    public GameObject botonContinuarTorneo;
    public GameObject botonNuevoTorneo;

    [Header("Nombre del jugador")]
    public string nombreJugador = "Jugador";

    [Header("Widget Pollocoins (tienda / recompensas)")]
    [Tooltip("Si es true, el Widget_Pollocoins vive dentro de la tienda y UIManager lo muestra/oculta según panelTienda.")]
    public GameObject panelTienda;
    [Tooltip("Alias del widget (si se deja vacío se usa el de EconomyManager).")]
    public GameObject widgetPollocoins;

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

    /// <summary>
    /// ¿Está activa la tienda? SOLO panelTienda (o panelSkins como respaldo).
    /// panelMapas (selección de mapas para jugar) NO cuenta como tienda:
    /// ahí el widget DEBE estar oculto por defecto.
    /// </summary>
    public bool TiendaActiva()
    {
        if (panelTienda != null) return panelTienda.activeInHierarchy;
        // Respaldo: la tienda vive en panelSkins (secciones Skins/Mapas de la tienda).
        if (panelSkins != null && panelSkins.activeInHierarchy) return true;
        return false;
    }

    /// <summary>
    /// Widget visible PERMANENTEMENTE solo con la tienda activa; oculto en el resto.
    /// Llamar tras cada MostrarPanel().
    /// </summary>
    public void ActualizarVisibilidadWidgetTienda()
    {
        GameObject widget = widgetPollocoins != null
            ? widgetPollocoins
            : (EconomyManager.Instance != null ? EconomyManager.Instance.widgetPollocoins : null);
        if (EconomyManager.Instance == null) return;
        // Sincronizar referencia si UIManager tiene alias propio.
        if (widgetPollocoins != null && EconomyManager.Instance.widgetPollocoins == null)
            EconomyManager.Instance.widgetPollocoins = widgetPollocoins;
        if (TiendaActiva())
            EconomyManager.Instance.MostrarWidgetPollocoins();
        else if (widget != null || EconomyManager.Instance.widgetPollocoins != null)
            EconomyManager.Instance.OcultarWidgetPollocoins();
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
        panelBosses?.SetActive(false);
        panel?.SetActive(true);
        // El widget solo vive con la tienda; en el resto se retrae.
        ActualizarVisibilidadWidgetTienda();
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
        // Releer la skin recién equipada justo antes de cargar la partida.
        GameManager.Instance?.AplicarSkinEquipadaAlIniciar();
        MostrarPanel(panelMapas);
        MapManager.Instance?.SincronizarCarruselConSeleccion();
    }

    // Confirmar mapa — va a dificultad si es VS CPU, o inicia PvP directamente
    public void OnClickConfirmarMapa()
    {
        // BLOQUEO OBLIGATORIO: si el mapa enfocado está BLOQUEADO no se inicia nada.
        if (MapManager.Instance != null && !MapManager.Instance.PuedeJugarMapaEnfocado()) { return; }
        // La skin pudo cambiarse en la tienda en esta misma sesión: aplicarla ya.
        GameManager.Instance?.AplicarSkinEquipadaAlIniciar();
        if (esperandoMapaPvP)
        {
            esperandoMapaPvP = false;
            // Salir del fondo demo también en PvP (GameplayCamera + controles).
            MainMenuBackgroundManager.Instance?.SalirDelDemoHaciaPartida();
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
        // Releer la skin recién equipada justo antes de cargar la partida.
        GameManager.Instance?.AplicarSkinEquipadaAlIniciar();
        MostrarPanel(panelMapas);
        MapManager.Instance?.SincronizarCarruselConSeleccion();
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

    // --- Card Historia ---
    /// <summary>Llamado por el botón Historia. Muestra el panel del selector de jefes.</summary>
    public void OnClickModoHistoria()
    {
        MostrarPanel(panelBosses);
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
        // Fondo dinámico: mapa aleatorio + demo CPU vs CPU en MenuCamera.
        MainMenuBackgroundManager.Instance?.EntrarAlMenuPrincipal();
    }

    /// <summary>
    /// Inicia una partida normal VS CPU con la dificultad indicada.
    /// BLOQUEO OBLIGATORIO: si el mapa está BLOQUEADO no cambia de escena.
    /// Configura la dificultad, llama a GameManager.IniciarPartida()
    /// (que reinicia scores, activa CPU, desactiva P2 y resetea la pelota)
    /// y oculta la UI del menú mostrando el HUD.
    /// </summary>
    public void IniciarPartida(Dificultad d)
    {
        if (MapManager.Instance != null && !MapManager.Instance.PuedeJugarMapaEnfocado()) { return; }
        Debug.Log($"[UIManager] 3. IniciarPartida({d})");
        // Salir del fondo demo: GameplayCamera + controles del jugador + mapa elegido.
        MainMenuBackgroundManager.Instance?.SalirDelDemoHaciaPartida();
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
        // Fondo dinámico: mapa aleatorio + demo CPU vs CPU en MenuCamera.
        MainMenuBackgroundManager.Instance?.EntrarAlMenuPrincipal();

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

        // Torneo = partida real: GameplayCamera (no menú).
        if (MainMenuBackgroundManager.Instance != null)
            MainMenuBackgroundManager.Instance.SalirDelDemoHaciaPartida();

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

        // Torneo = partida real: GameplayCamera (no menú).
        if (MainMenuBackgroundManager.Instance != null)
            MainMenuBackgroundManager.Instance.SalirDelDemoHaciaPartida();

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
    public void MostrarGameOver(bool ganoJugador, bool esTorneo = false, int pollocoinsGanados = 0)
    {
        MostrarPanel(panelGameOver);

        // Recompensa visible en el panel: "+25 POLLOCOINS" (vacío si no hubo).
        if (gameOverTextoPollocoins != null)
        {
            gameOverTextoPollocoins.text = pollocoinsGanados > 0
                ? "+" + pollocoinsGanados + " POLLOCOINS"
                : "";
        }

        // Widget: deslizar a pantalla + conteo rápido, esperar 2-3s y retraer.
        if (pollocoinsGanados > 0 && EconomyManager.Instance != null)
            EconomyManager.Instance.MostrarRecompensaPostPartida();

        // Si el jugador ganó un combate de jefe, registrar la victoria para
        // desbloquear al siguiente (Zeus -> Colossus -> Mirage) vía PlayerPrefs.
        if (ganoJugador && !esTorneo)
        {
            BossManager bossManager = FindObjectOfType<BossManager>();
            if (bossManager != null && bossManager.JefeActivo != null)
            {
                BossData jefeVencido = bossManager.JefeActivo.bossData;
                if (jefeVencido == null && bossManager.bosses != null)
                    jefeVencido = bossManager.bosses.Find(b => b != null && b.nombre == bossManager.JefeActivo.gameObject.name);
                if (jefeVencido != null)
                    bossManager.RegistrarVictoriaJefe(jefeVencido);
            }
        }

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