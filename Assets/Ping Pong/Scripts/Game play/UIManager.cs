using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.IO;

public class UIManager : MonoBehaviour, IPointerEnterHandler
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

    [Header("Panel Help")]
    public GameObject panelHelp;

    [Header("Panel Options")]
    public GameObject panelOptions;

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

    [Header("Audio UI")]
    public AudioClip sonidoClick;
    public AudioClip sonidoAbrirMenu;
    public AudioClip sonidoCerrarMenu;
    public AudioClip sonidoHover;
    [Range(0f,1f)] public float volumenClick = 0.5f;
    [Range(0f,1f)] public float volumenHover = 0.2f;
    [Range(0f,1f)] public float volumenAbrir = 0.6f;
    [Range(0f,1f)] public float volumenCerrar = 0.6f;
    private AudioSource audioSourceUI;
    [Header("Música Fondo")]
    public AudioSource musicaSource;
    public AudioClip[] cancionesMenu;
    public float volumenMusica = 0.4f;
    private int ultimaCancion = -1;
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
        audioSourceUI = GetComponent<AudioSource>();
        if (audioSourceUI == null) audioSourceUI = gameObject.AddComponent<AudioSource>();
        audioSourceUI.volume = 0.6f;
        if (musicaSource == null)
        {
            GameObject mgo = new GameObject("MusicaMenu");
            mgo.transform.SetParent(transform);
            musicaSource = mgo.AddComponent<AudioSource>();
            musicaSource.loop = false;
            musicaSource.playOnAwake = false;
            musicaSource.volume = volumenMusica;
        }
        StartCoroutine(ReproducirMusicaAleatoria());
    }

    void Start()
    {
        MostrarPanel(panelMenuPrincipal);
        if (toastPunto != null)
            toastPunto.gameObject.SetActive(false);
        AgregarHoverBotones();
    }

    void AgregarHoverBotones()
    {
        Button[] botones = FindObjectsOfType<Button>(true);
        foreach (Button btn in botones)
        {
            EventTrigger trigger = btn.GetComponent<EventTrigger>();
            if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();
            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => ReproducirSonidoHover());
            trigger.triggers.Add(entry);
        }
    }

    void ReproducirSonidoHover()
    {
        if (audioSourceUI == null) return;
        if (sonidoHover != null)
            audioSourceUI.PlayOneShot(sonidoHover, volumenHover);
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
    IEnumerator ReproducirMusicaAleatoria()
    {
        while (true)
        {
            if (musicaSource == null || cancionesMenu == null || cancionesMenu.Length == 0)
            {
                yield return null;
                continue;
            }
            if (!musicaSource.isPlaying)
            {
                int idx = Random.Range(0, cancionesMenu.Length);
                while (cancionesMenu.Length > 1 && idx == ultimaCancion) idx = Random.Range(0, cancionesMenu.Length);
                ultimaCancion = idx;
                musicaSource.clip = cancionesMenu[idx];
                musicaSource.volume = volumenMusica;
                musicaSource.Play();
            }
            yield return null;
        }
    }

    public void BajarVolumenMusica(float volumenObjetivo, float duracion)
    {
        if (musicaSource == null) return;
        StartCoroutine(BajarVolumenMusicaCoroutine(volumenObjetivo, duracion));
    }

    public void SubirVolumenMusica(float volumenMaximo, float duracion)
    {
        if (musicaSource == null) return;
        StartCoroutine(SubirVolumenMusicaCoroutine(volumenMaximo, duracion));
    }

    IEnumerator SubirVolumenMusicaCoroutine(float volumenMaximo, float duracion)
    {
        float inicio = musicaSource.volume;
        float destino = volumenMaximo > 0 ? volumenMaximo : volumenMusica;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duracion);
            musicaSource.volume = Mathf.Lerp(inicio, destino, t);
            yield return null;
        }
        musicaSource.volume = destino;
    }

    IEnumerator BajarVolumenMusicaCoroutine(float volumenObjetivo, float duracion)
    {
        float inicio = musicaSource.volume;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duracion);
            musicaSource.volume = Mathf.Lerp(inicio, volumenObjetivo, t);
            yield return null;
        }
        musicaSource.volume = volumenObjetivo;
    }

    public void MostrarPanel(GameObject panel)
    {
        if (panel == panelHUD && BossManager.Instance != null)
        {
            BossManager.bloquearMenuJefes = true;
            BossManager.Instance.combateIniciado = true;
            if (!BossManager.Instance.BatallaIniciada && !BossManager.Instance.startBossBattleEnCurso)
            {
                BossManager.Instance.SalirModoHistoriaJefes();
            }
        }
        if (panel == panelMenuPrincipal && musicaSource != null)
        {
            if (!musicaSource.isPlaying) StartCoroutine(ReproducirMusicaAleatoria());
            musicaSource.volume = volumenMusica;
        }
        if (panel == panelMenuPrincipal && audioSourceUI != null && sonidoCerrarMenu != null) audioSourceUI.PlayOneShot(sonidoCerrarMenu, volumenCerrar);
        if (panel != panelMenuPrincipal && panel != null && audioSourceUI != null && sonidoAbrirMenu != null) audioSourceUI.PlayOneShot(sonidoAbrirMenu, volumenAbrir);
        panelHUD?.SetActive(false);
        panelHelp?.SetActive(false);
        panelOptions?.SetActive(false);
        panelMenuPrincipal?.SetActive(false);
        panelSkins?.SetActive(false);
        panelModos?.SetActive(false);
        panelMapas?.SetActive(false);
        panelDificultad?.SetActive(false);
        panelPausa?.SetActive(false);
        panelGameOver?.SetActive(false);
        panelTorneo?.SetActive(false);
        panel?.SetActive(true);
        if (panelHelp != null && panel != panelHelp) panelHelp.SetActive(false);
        if (panelOptions != null && panel != panelOptions) panelOptions.SetActive(false);
        if (panel != panelBosses)
            StartCoroutine(OcultarPanelJefesSeguroUI());
        // El widget solo vive con la tienda; en el resto se retrae.
        ActualizarVisibilidadWidgetTienda();

        // Refrescar el preview 3D de skins cada vez que se muestra el panel de skins,
        // para asegurar que el modelo gira tras volver de una partida.
        if (panel == panelSkins)
        {
            SkinManager.Instance?.RefrescarPreview();
            RepararBtnHabilidad();
        }
    }

    /// <summary>
    /// Repara el botón Btn_Habilidades: ApagarPorNombre("Habilidad") en
    /// MainMenuBackgroundManager deshabilita TODOS sus componentes (Image, Button,
    /// Event Trigger) y Panel_Skins inicia inactivo, así que MenuTabManager no
    /// recibe callbacks. UIManager está siempre activo y usa
    /// Resources.FindObjectsOfTypeAll para encontrar el botón incluso si está
    /// inactivo o desactivado en la jerarquía.
    /// </summary>
    public void RepararBtnHabilidad()
    {
        GameObject[] todos;
        try { todos = Resources.FindObjectsOfTypeAll<GameObject>(); }
        catch (System.Exception) { return; }
        if (todos == null) return;
        foreach (GameObject go in todos)
        {
            if (go == null || !go.name.Equals("Btn_Habilidades", System.StringComparison.OrdinalIgnoreCase)) continue;
            // Reactivar el GameObject si estuviera inactivo.
            if (!go.activeSelf) go.SetActive(true);
            // Habilitar TODOS los componentes MonoBehaviour (Image, Button, Event Trigger, etc.)
            var comps = go.GetComponents<MonoBehaviour>();
            foreach (var c in comps)
            {
                if (c == null || c is UIManager || c is MenuTabManager) continue;
                try { c.enabled = true; }
                catch (System.Exception) { }
            }
            // Si el Image no tiene Sprite, asignar color de fondo visible.
            var img = go.GetComponent<Image>();
            if (img != null && img.sprite == null)
                img.color = new Color(0.2f, 0.6f, 1f, 1f);
            Debug.Log($"[UIManager] Btn_Habilidades reparado: Image={img?.enabled}, Button={go.GetComponent<Button>()?.enabled}");
            return;
        }
    }

    /// <summary>
    /// Oculta el panel de jefes después de un frame para evitar el error de
    /// sincronización 'IsInSyncWithParentSerializedObject' del editor.
    /// </summary>
    System.Collections.IEnumerator OcultarPanelJefesSeguroUI()
    {
        yield return null;
        if (panelBosses != null) panelBosses.SetActive(false);
        GameObject panelJefesGO = GameObject.Find("Panel_Jefes");
        if (panelJefesGO != null)
            panelJefesGO.SetActive(false);
    }

    // -------------------------------------------------------
    public void OnClickPlay()
    {
        if (BossManager.Instance != null) { BossManager.bloquearMenuJefes = false; BossManager.Instance.combateIniciado = false; }
        MostrarPanel(panelModos);
        AnimarEntradaModos();
    }

    /// <summary>
    /// Abre el menú de jefes (reactiva BossManager apagado por el demo) y pinta
    /// la ficha completa del jefe actual (nombre/título/dificultad/descripción).
    /// BossCard se enciende como ÚLTIMO paso (nadie la apaga después).
    /// </summary>
    public void OnClickBosses()
    {
        BossManager.bloquearMenuJefes = false;
        BossManager bm0 = BossManager.Instance != null ? BossManager.Instance : FindObjectOfType<BossManager>();
        if (bm0 != null) { bm0.combateIniciado = false; bm0.EntrarModoHistoriaJefes(); }
        if (MainMenuBackgroundManager.Instance != null)
            MainMenuBackgroundManager.Instance.AbrirPanelJefes();
        MostrarPanel(panelBosses);
        BossManager bm = BossManager.Instance != null ? BossManager.Instance : FindObjectOfType<BossManager>();
        if (bm != null)
        {
            bm.ActualizarUI();
            // BossCard lo ÚLTIMO: tras ActualizarUI nadie debe apagarla.
            if (bm.bossInfoCard != null) bm.bossInfoCard.SetActive(true);
        }
        else Debug.LogWarning("[UIManager] OnClickBosses: no hay BossManager en escena para pintar la ficha.");
    }

    /// <summary>
    /// Botón VOLVER dentro del menú de jefes: cierra panelBosses, apaga BossManager
    /// (ciclo de vida) y vuelve al menú principal.
    /// </summary>
    public void OnClickVolverDesdeBosses()
    {
        BossManager bm = BossManager.Instance != null ? BossManager.Instance : FindObjectOfType<BossManager>();
        if (bm != null) bm.SalirModoHistoriaJefes();
        if (panelBosses != null) panelBosses.SetActive(false);
        MostrarPanel(panelMenuPrincipal);
    }

    public void OnClickSkins()
    {
        MostrarPanel(panelSkins);
        SkinManager.Instance?.RefrescarPreview();
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
            BajarVolumenMusica(0f, 0.4f);
            if (BossManager.Instance != null) { BossManager.bloquearMenuJefes = true; BossManager.Instance.combateIniciado = true; BossManager.Instance.SalirModoHistoriaJefes(); }
            if (panelBosses != null) panelBosses.SetActive(false);
            esperandoMapaPvP = false;
            // Salir del fondo demo también en PvP (GameplayCamera + controles).
            MainMenuBackgroundManager.Instance?.SalirDelDemoHaciaPartida();
            GameManager.Instance?.IniciarPartidaPvP();
        BajarVolumenMusica(0f, 0.4f);
        if (BossManager.Instance != null) { BossManager.bloquearMenuJefes = true; BossManager.Instance.combateIniciado = true; BossManager.Instance.SalirModoHistoriaJefes(); }
        if (panelBosses != null) panelBosses.SetActive(false);
            MostrarPanel(panelHUD);
        }
        else
        {
            BajarVolumenMusica(0f, 0.4f);
            if (BossManager.Instance != null) { BossManager.bloquearMenuJefes = true; BossManager.Instance.combateIniciado = true; BossManager.Instance.SalirModoHistoriaJefes(); }
            if (panelBosses != null) panelBosses.SetActive(false);
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
        BajarVolumenMusica(0f, 0.4f);
        if (BossManager.Instance != null) { BossManager.bloquearMenuJefes = true; BossManager.Instance.combateIniciado = true; BossManager.Instance.SalirModoHistoriaJefes(); }
        if (panelBosses != null) panelBosses.SetActive(false);
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

    public void OnClickHelp()
    {
        if (panelHelp != null)
        {
            panelHelp.SetActive(true);
            if (audioSourceUI != null && sonidoAbrirMenu != null) audioSourceUI.PlayOneShot(sonidoAbrirMenu, volumenAbrir);
        }
    }

    public void CambiarVolumenMaster(float valor){ if(float.IsNaN(valor)||float.IsInfinity(valor)) return; float v=valor>1f?valor/100f:valor; v=Mathf.Clamp01(v); AudioListener.volume=v; AudioListener.pause=false; }

    public void CambiarVolumenMusica(float valor){ if(float.IsNaN(valor)||float.IsInfinity(valor)) return; float v=valor>1f?valor/100f:valor; v=Mathf.Clamp01(v); volumenMusica=v; if(musicaSource!=null){ musicaSource.mute=false; musicaSource.volume=volumenMusica; if(!musicaSource.isPlaying && cancionesMenu!=null && cancionesMenu.Length>0){ /* mantener */ } } }

    public void CambiarVolumenSFX(float valor){ if(float.IsNaN(valor)||float.IsInfinity(valor)) return; float v=valor>1f?valor/100f:valor; v=Mathf.Clamp01(v); volumenClick=v*0.5f; volumenHover=v*0.2f; volumenAbrir=v*0.6f; volumenCerrar=v*0.6f; if(audioSourceUI!=null){ audioSourceUI.mute=false; audioSourceUI.volume=v; } }

    public void OnClickOptions()
    {
        if (panelOptions != null)
        {
            panelOptions.SetActive(true);
            if (audioSourceUI != null && sonidoAbrirMenu != null) audioSourceUI.PlayOneShot(sonidoAbrirMenu, volumenAbrir);
        }
    }

    public void OnClickCerrarOptions()
    {
        if (panelOptions != null)
        {
            panelOptions.SetActive(false);
            if (audioSourceUI != null && sonidoCerrarMenu != null) audioSourceUI.PlayOneShot(sonidoCerrarMenu, volumenCerrar);
        }
    }

    public void OnClickVolverAlMenu()
    {
        BossManager.bloquearMenuJefes = false;
        if (BossManager.Instance != null) { BossManager.Instance.combateIniciado = false; BossManager.Instance.iniciandoCombate = false; BossManager.Instance.enabled = true; }
        GameObject.Find("Panel_Jefes")?.SetActive(false);
        if (panelBosses != null) panelBosses.SetActive(false);
        GameObject pb = GameObject.Find("Panel_Jefes");
        if (pb != null) pb.SetActive(false);
        if (panelBosses != null) panelBosses.SetActive(false);
        GameManager.Instance?.DetenerVolcanes();
        // Al regresar al menú desde un combate: ciclo de vida OFF de jefes.
        BossManager bm = BossManager.Instance != null ? BossManager.Instance : FindObjectOfType<BossManager>();
        if (bm != null) bm.SalirModoHistoriaJefes();
        GameManager.Instance?.VolverAlMenu();

        var practiceUI = FindObjectOfType<PracticeUI>();
        if (practiceUI != null)
        {
            practiceUI.HidePracticeUI();
            practiceUI.gameObject.SetActive(false);
        }

        MostrarPanel(panelMenuPrincipal);
        // Refrescar el preview 3D de skins al volver al menú (por si el panel de skins
        // ya estaba abierto o se abre después, evitando que el modelo quede estático).
        SkinManager.Instance?.RefrescarPreview();
        // Fondo dinámico: mapa aleatorio + demo CPU vs CPU en MenuCamera.
        MainMenuBackgroundManager.Instance?.EntrarAlMenuPrincipal();
        // Reparar el botón HAB: ApagarPorNombre("Habilidad") deshabilita sus componentes
        // (Image, Button, Event Trigger) y Panel_Skins inicia inactivo, así que
        // MenuTabManager no recibe callbacks. UIManager está siempre activo.
        RepararBtnHabilidad();
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
        string metodo = "UIManager.IniciarPartida";
        string path = Application.persistentDataPath + "/crash_debug.txt";
        System.IO.File.AppendAllText(path, $"[{System.DateTime.Now:HH:mm:ss.fff}] {metodo} >>> ENTRA >>> dificultad={d}\n");
        BajarVolumenMusica(0f, 0.4f);
        // Saltar la comprobación de mapa bloqueado cuando es un combate de jefe
        // (JefeActivo ya está asignado por StartBossBattle antes de llamar aquí).
        bool esCombateDeJefe = BossManager.Instance != null && BossManager.Instance.JefeActivo != null;
        if (!esCombateDeJefe && MapManager.Instance != null && !MapManager.Instance.PuedeJugarMapaEnfocado()) { return; }
        Debug.Log($"[UIManager] 3. IniciarPartida({d})");
        if (BossManager.Instance != null) { BossManager.bloquearMenuJefes = true; BossManager.Instance.combateIniciado = true; BossManager.Instance.SalirModoHistoriaJefes(); }
        if (panelBosses != null) panelBosses.SetActive(false);
        MainMenuBackgroundManager.Instance?.SalirDelDemoHaciaPartida();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.dificultadSeleccionada = d;
            GameManager.Instance.IniciarPartida();
        }
        MostrarPanel(panelHUD);
        System.IO.File.AppendAllText(path, $"[{System.DateTime.Now:HH:mm:ss.fff}] {metodo} <<< SALE <<<\n");
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

        // Refrescar el preview 3D de skins al volver al menú.
        SkinManager.Instance?.RefrescarPreview();
        // Reparar el botón HAB (ver comentario en OnClickVolverAlMenu).
        RepararBtnHabilidad();
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

            bool mostrarNuevoTorneo = torneoTerminado || (!jugadorTienePartido && !torneoTerminado);

            if (botonReintentar != null)        botonReintentar.SetActive(false);
            if (botonContinuarTorneo != null)   botonContinuarTorneo.SetActive(!mostrarNuevoTorneo);
            if (botonNuevoTorneo != null)       botonNuevoTorneo.SetActive(mostrarNuevoTorneo);
        }
    }

    public void ReproducirSonidoClick()
    {
        if (audioSourceUI == null) return;
        if (sonidoClick != null)
            audioSourceUI.PlayOneShot(sonidoClick, volumenClick);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ReproducirSonidoHover();
    }
    public void AnimarEntradaModos()
    {
        ReproducirSonidoClick();
        if (audioSourceUI != null && sonidoAbrirMenu != null) audioSourceUI.PlayOneShot(sonidoAbrirMenu, volumenAbrir);
        if (panelModos != null && UIAnimationManager.Instance != null)
            UIAnimationManager.Instance.ScaleIn(panelModos.GetComponent<RectTransform>());
    }

    public void AnimarSalidaModos()
    {
        ReproducirSonidoClick();
        if (audioSourceUI != null && sonidoCerrarMenu != null) audioSourceUI.PlayOneShot(sonidoCerrarMenu, volumenCerrar);
        if (panelModos != null && UIAnimationManager.Instance != null)
            UIAnimationManager.Instance.ScaleOut(panelModos.GetComponent<RectTransform>());
    }

    public void AnimarEntradaShop()
    {
        ReproducirSonidoClick();
        if (audioSourceUI != null && sonidoAbrirMenu != null) audioSourceUI.PlayOneShot(sonidoAbrirMenu, volumenAbrir);
        if (panelTienda != null && UIAnimationManager.Instance != null)
            UIAnimationManager.Instance.SlideFromRight(panelTienda.GetComponent<RectTransform>());
    }

    public void AnimarSalidaShop()
    {
        ReproducirSonidoClick();
        if (audioSourceUI != null && sonidoCerrarMenu != null) audioSourceUI.PlayOneShot(sonidoCerrarMenu, volumenCerrar);
        if (panelTienda != null && UIAnimationManager.Instance != null)
            UIAnimationManager.Instance.SlideToRight(panelTienda.GetComponent<RectTransform>());
    }

    public void AnimarSelectorMapasVsCPU()
    {
        ReproducirSonidoClick();
        if (audioSourceUI != null && sonidoAbrirMenu != null) audioSourceUI.PlayOneShot(sonidoAbrirMenu, volumenAbrir);
        if (panelMapas != null && UIAnimationManager.Instance != null)
            UIAnimationManager.Instance.SlideFromLeft(panelMapas.GetComponent<RectTransform>());
    }

    public void AnimarPanelTorneoHistoria()
    {
        ReproducirSonidoClick();
        if (audioSourceUI != null && sonidoAbrirMenu != null) audioSourceUI.PlayOneShot(sonidoAbrirMenu, volumenAbrir);
        if (panelTorneo != null && UIAnimationManager.Instance != null)
            UIAnimationManager.Instance.SlideFromBottom(panelTorneo.GetComponent<RectTransform>());
        if (panelBosses != null && UIAnimationManager.Instance != null)
            UIAnimationManager.Instance.SlideFromBottom(panelBosses.GetComponent<RectTransform>());
    }
}
