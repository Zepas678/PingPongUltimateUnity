using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Maneja la lógica de puntos del juego de ping pong.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Referencia a la pelota")]
    public PingPongBall ball;

    [Header("Referencia al CPU")]
    public CPUControl cpu;

    [Header("Animaciones de raquetas")]
    public RaquetaAnimacion animRaquetaJugador;
    public RaquetaAnimacion animRaquetaCPU;

    [Header("Efectos de partículas")]
    public ParticleSystem volcanIzquierda;
    public ParticleSystem volcanDerecha;

    [Header("Velocidad mínima para volcanes")]
    public float velocidadVolcan = 110f;

    [Header("Golpe potenciado")]
    public RaquetaGolpe golpeRaquetaJugador;
    public RaquetaGolpe golpeRaquetaCPU;

    [Header("Dificultad")]
    public Dificultad dificultadSeleccionada = Dificultad.Facil;

    [Header("Modo PvP")]
    public RaquetaControlP2 controlJugador2;
    public CPUControl       cpuControl;
    private bool            esModoPvP = false;

    [Header("UI (opcional, asignar luego)")]
    public TMP_Text playerScoreText;
    public TMP_Text cpuScoreText;
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;

    [Header("Sonidos")]
    public AudioClip sonidoPuntoNormal;
    public AudioClip sonidoPuntoEpico;
    public AudioClip sonidoPuntoUltra;
    public AudioClip sonidoVolcan;

    public AudioClip sonidoVictoria;    
    public AudioClip sonidoDerrota;     
    private AudioSource audioSource;

    [Header("Reglas de puntuación")]
    public int pointsToWin     = 11;
    public int advantageNeeded = 2;

    private int  playerScore = 0;
    private int  cpuScore    = 0;
    private bool gameOver    = false;

    // ─── Modo Torneo ───
    /// <summary>Indica si la partida actual pertenece al modo torneo.</summary>
    public bool esModoTorneo = false;

#if UNITY_EDITOR
    [Header("Debug (Solo Editor)")]
    /// <summary>Activa las herramientas de depuración del torneo en el Editor.</summary>
    public bool debugMode = false;

    /// <summary>Marca para finalizar la partida como victoria del jugador (solo con debugMode activo).</summary>
    public bool debugWin = false;

    /// <summary>Marca para finalizar la partida como derrota del jugador (solo con debugMode activo).</summary>
    public bool debugLose = false;

    // Estado anterior para detectar cambios (flanco de subida)
    private bool prevDebugWin = false;
    private bool prevDebugLose = false;
#endif

    // -------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        CargarClipsPorDefecto();
    }

    // -------------------------------------------------------
    void Start()
    {
        if (cpu != null)
            cpu.SetDificultad(dificultadSeleccionada);

        // Aplicar la raqueta equipada en la tienda al arrancar el partido.
        AplicarSkinEquipadaAlIniciar();

        if (ball != null)
            ball.CongelarPelota();
    }

    /// <summary>
    /// Garantiza que la raqueta lleve la skin recién equipada justo antes de
    /// cargar la partida: relee PlayerPrefs y la aplica a la raqueta 3D.
    /// Llamar desde UIManager al pulsar JUGAR / confirmar modo (sin reiniciar escena).
    /// </summary>
    public void AplicarSkinEquipadaAlIniciar()
    {
        // TODO DEBUG-SKIN: log temporal de diagnóstico. Quitar al estabilizar.
        int skinGuardada = PlayerPrefs.GetInt(SkinManager.CLAVE_SKIN_EQUIPADA, 0);
        Debug.Log($"[Skin Debug] Skin guardada en PlayerPrefs: {skinGuardada}");

        // Vía 1: SkinManager presente en escena (caso normal).
        SkinManager skins = SkinManager.Instance != null
            ? SkinManager.Instance
            : FindObjectOfType<SkinManager>();

        // Resolver el Renderer de la raqueta del jugador con respaldo en cascada:
        // 1) referencia del SkinManager, 2) RaquetaGolpe del jugador,
        // 3) búsqueda por jerarquía (tag Player / nombre Raqueta* / cualquier RaquetaGolpe jugador).
        Renderer raqueta = skins != null ? skins.rendererRaquetaJugador : null;
        if (raqueta == null && golpeRaquetaJugador != null)
            raqueta = golpeRaquetaJugador.GetComponent<Renderer>();
        if (raqueta == null)
            raqueta = BuscarRendererRaquetaJugador();
        if (raqueta == null)
        {
            Debug.LogWarning("[Skin Debug] rendererRaquetaJugador es NULL y no se encontró Renderer de raqueta en la jerarquía del jugador. Asigna la referencia en SkinManager o nombra la raqueta 'Raqueta*'.");
            return;
        }

        Debug.Log($"[Skin Debug] Renderer destino: {raqueta.gameObject.name} (ruta: {RutaJerarquia(raqueta.transform)})");

        if (skins != null)
        {
            // Guardar la referencia auto-resuelta para futuras aplicaciones.
            skins.rendererRaquetaJugador = raqueta;
            if (skins.AplicarSkinEquipadaConLog(raqueta))
            {
                SuscribirReaplicacionSkin(raqueta);
                return;
            }
            Debug.LogWarning($"[Skin Debug] SkinManager no pudo aplicar la skin {skinGuardada} (prefab/material nulo o índice fuera de rango).");
            return;
        }

        // Vía 2 (sin SkinManager en escena): no hay lista de prefabs a mano.
        Debug.Log($"[Skin Debug] Skin equipada leída: {skinGuardada} (sin SkinManager en escena, no se pudo aplicar material).");
    }

    // -------------------------------------------------------
    void OnEnable()
    {
        SkinManager.OnSkinEquipada -= AlEquiparSkinTiempoReal;
        SkinManager.OnSkinEquipada += AlEquiparSkinTiempoReal;
    }

    void OnDisable()
    {
        SkinManager.OnSkinEquipada -= AlEquiparSkinTiempoReal;
    }

    /// <summary>
    /// La tienda avisó de un EQUIP en la misma sesión: reaplicar de inmediato
    /// a la raqueta 3D sin esperar al Start() ni reiniciar.
    /// </summary>
    void AlEquiparSkinTiempoReal(int indice)
    {
        AplicarSkinEquipadaAlIniciar();
    }

    /// <summary>
    /// Busca el Renderer de la raqueta del jugador en la jerarquía:
    /// 1) objeto taggeado "Player" y sus hijos con RaquetaGolpe.esJugador,
    /// 2) cualquier RaquetaGolpe con esJugador == true,
    /// 3) objeto llamado Raqueta* con Renderer.
    /// </summary>
    Renderer BuscarRendererRaquetaJugador()
    {
        // 1) Bajo el objeto del jugador (tag Player).
        try
        {
            GameObject jugador = GameObject.FindGameObjectWithTag("Player");
            if (jugador != null)
            {
                RaquetaGolpe[] golpes = jugador.GetComponentsInChildren<RaquetaGolpe>(true);
                foreach (RaquetaGolpe g in golpes)
                {
                    if (g != null && g.esJugador)
                    {
                        Renderer r = g.GetComponent<Renderer>();
                        if (r != null) return r;
                    }
                }
            }
        }
        catch (System.Exception) { /* tag inexistente: seguir con el resto */ }

        // 2) Cualquier RaquetaGolpe de jugador en escena.
        RaquetaGolpe[] todos = FindObjectsOfType<RaquetaGolpe>(true);
        foreach (RaquetaGolpe g in todos)
        {
            if (g != null && g.esJugador)
            {
                Renderer r = g.GetComponent<Renderer>();
                if (r != null) return r;
            }
        }

        // 3) Por nombre: Raqueta* con Renderer.
        GameObject[] raquetas = GameObject.FindGameObjectsWithTag("Untagged");
        foreach (GameObject go in raquetas)
        {
            if (go != null && go.name.ToLowerInvariant().Contains("raqueta"))
            {
                Renderer r = go.GetComponent<Renderer>();
                if (r != null) return r;
            }
        }
        return null;
    }

    /// <summary>
    /// Re-aplica la skin si la raqueta se regenera tras romperse o si el objeto
    /// del jugador se instancia dinámicamente después del Start().
    /// </summary>
    void SuscribirReaplicacionSkin(Renderer raqueta)
    {
        if (raqueta == null) return;
        RaquetaGolpe golpe = raqueta.GetComponent<RaquetaGolpe>();
        if (golpe == null && golpeRaquetaJugador != null) golpe = golpeRaquetaJugador;
        if (golpe == null) return;
        golpe.onRegenerada -= ReaplicarSkinTrasRegenerar;
        golpe.onRegenerada += ReaplicarSkinTrasRegenerar;
    }

    void ReaplicarSkinTrasRegenerar()
    {
        SkinManager skins = SkinManager.Instance != null
            ? SkinManager.Instance
            : FindObjectOfType<SkinManager>();
        Renderer raqueta = skins != null ? skins.rendererRaquetaJugador : null;
        if (raqueta == null && golpeRaquetaJugador != null)
            raqueta = golpeRaquetaJugador.GetComponent<Renderer>();
        if (raqueta == null) raqueta = BuscarRendererRaquetaJugador();
        if (skins != null && raqueta != null)
            skins.AplicarSkinEquipadaConLog(raqueta);
    }

    static string RutaJerarquia(Transform t)
    {
        string ruta = t != null ? t.name : "?";
        while (t != null && t.parent != null)
        {
            t = t.parent;
            ruta = t.name + "/" + ruta;
        }
        return ruta;
    }

    // -------------------------------------------------------
#if UNITY_EDITOR
    void Update()
    {
        if (!Application.isPlaying) return;
        if (!debugMode) return;

        // Detectar flanco de subida en debugWin
        if (debugWin && !prevDebugWin)
        {
            debugWin = false;
            prevDebugWin = false;

            if (gameOver) return;

            // Forzar puntuación para que EndGame vea que el jugador ganó
            playerScore = pointsToWin;
            cpuScore = 0;
            UpdateUI();
            EndGame("¡Ganaste! (Debug)");
            return;
        }

        // Detectar flanco de subida en debugLose
        if (debugLose && !prevDebugLose)
        {
            debugLose = false;
            prevDebugLose = false;

            if (gameOver) return;

            // Forzar puntuación para que EndGame vea que el CPU ganó
            playerScore = 0;
            cpuScore = pointsToWin;
            UpdateUI();
            EndGame("CPU gana (Debug)");
            return;
        }

        // Actualizar estado anterior
        prevDebugWin = debugWin;
        prevDebugLose = debugLose;
    }
#endif

    // -------------------------------------------------------
    public void RegisterPoint(bool scoredForPlayer, float velocidadPelota = 0f)
    {
        if (gameOver) return;

        if (scoredForPlayer)
        {
            playerScore++;
            Debug.Log($"[GameManager] PUNTO JUGADOR | {playerScore} - {cpuScore}");

            // Notificar al jefe activo (p. ej. Colossus) de que el JUGADOR le
            // anotó un punto: evento real (no polling frame a frame) para hacer
            // crecer al jefe, rearmar su inmunidad y recalcular la escala.
            // No-op si no hay combate de jefe en curso (JefeActivo == null).
            BossManager bossManager = FindObjectOfType<BossManager>();
            if (bossManager != null && bossManager.JefeActivo != null)
                bossManager.JefeActivo.OnPuntoDelJugador();
        }
        else
        {
            cpuScore++;
            Debug.Log($"[GameManager] PUNTO CPU | {playerScore} - {cpuScore}");
            // Nota: cuando anota la CPU (Colossus), el jefe NO crece ni se rearma:
            // los puntos del rival no cuentan a su favor.
        }

        UpdateUI();

        ComboManager.Instance?.RomperComboPorPunto();

        if (velocidadPelota >= velocidadVolcan)
            ActivarVolcanes(true);

        UIManager.Instance?.MostrarToast(scoredForPlayer);
        PlayPointSound(velocidadPelota);

        if (scoredForPlayer && animRaquetaJugador != null)
            animRaquetaJugador.AnimarFestejo();
        else if (!scoredForPlayer && animRaquetaCPU != null)
            animRaquetaCPU.AnimarFestejo();

        if (CheckVictory())
            return;

        Invoke(nameof(ResetRound), 2f);
    }

    // -------------------------------------------------------
    private bool CheckVictory()
    {
        int deuceScore = pointsToWin - 1;

        bool playerWins = false;
        bool cpuWins    = false;

        if (playerScore >= deuceScore && cpuScore >= deuceScore)
        {
            if (playerScore - cpuScore >= advantageNeeded) playerWins = true;
            else if (cpuScore - playerScore >= advantageNeeded) cpuWins = true;
        }
        else
        {
            if (playerScore >= pointsToWin) playerWins = true;
            if (cpuScore    >= pointsToWin) cpuWins    = true;
        }

        if (playerWins) { EndGame("¡Ganaste!"); return true; }
        if (cpuWins)    { EndGame("CPU gana");  return true; }
        return false;
    }

    // -------------------------------------------------------
    private void ResetRound()
    {
        // En modo VS CPU, avisar al CPU del saque
        if (!esModoPvP && cpu != null)
            cpu.ActivarModoSaque();

        RegenerarRaquetas(0f);

        if (ball != null)
            ball.ResetBall();
    }

    public void RegenerarRaquetas(float delay = 0f)
    {
        if (delay <= 0f)
        {
            RegenerarRaquetasAhora();
            return;
        }

        CancelInvoke(nameof(RegenerarRaquetasAhora));
        Invoke(nameof(RegenerarRaquetasAhora), delay);
    }

    void RegenerarRaquetasAhora()
    {
        golpeRaquetaJugador?.Regenerar();
        golpeRaquetaCPU?.Regenerar();
    }

    // -------------------------------------------------------
    private void EndGame(string message)
    {
        gameOver = true;
        Debug.Log($"[GameManager] FIN DE PARTIDA — {message}");

        if (ball != null)
            ball.CongelarPelota();

        ActivarVolcanes(true, continuo: true);

        bool ganoJugador = playerScore > cpuScore;

        // Hook de victoria de jefe: si el jugador ganó (juego normal, debugWin o
        // CheckVictory), registrarla para desbloquear al siguiente.
        // JefeActivo persiste durante todo el combate (solo se limpia al volver
        // al menú); si por error llegó null, fallback al seleccionado en el menú.
        if (ganoJugador)
        {
            BossData jefeAAcreditar = null;
            if (BossManager.Instance != null)
            {
                if (BossManager.Instance.JefeActivo != null)
                    jefeAAcreditar = BossManager.Instance.ResolverBossDataVictoria(BossManager.Instance.JefeActivo);
                if (jefeAAcreditar == null)
                    jefeAAcreditar = BossManager.Instance.ObtenerJefeActualSeleccionado();
            }
            if (jefeAAcreditar != null)
            {
                Debug.Log($"[GameManager] Victoria de jefe detectada para: {jefeAAcreditar.nombre}");
                BossManager.Instance.RegistrarVictoriaJefe(jefeAAcreditar);
            }
            else
            {
                Debug.Log("[GameManager] Fin de partida ganado sin JefeActivo (partida normal, no jefe).");
            }
        }

        // Si es modo torneo, registrar el resultado automáticamente
        if (esModoTorneo)
        {
            Match partido = TournamentUIManager.PartidoActualJugando;
            TournamentManager tm = TournamentUIManager.TorneoActual;
            if (partido != null && tm != null && !partido.jugado)
            {
                // Determinar quién ganó (el Competidor que NO es el jugador si perdió, o el jugador si ganó)
                Competidor ganador = ganoJugador
                    ? (partido.jugadorA?.esJugador == true ? partido.jugadorA : partido.jugadorB)
                    : (partido.jugadorA?.esJugador == true ? partido.jugadorB : partido.jugadorA);

                if (ganador != null)
                {
                    tm.RegistrarGanador(partido, ganador);
                    Debug.Log($"[GameManager] Torneo: {ganador.nombre} ganó el partido.");
                }
            }
        }

        // Recompensa Pollocoins + texto en el panel de fin de partida.
        // Prioridad: jefe > torneo > VS CPU. PvP no otorga.
        int pollocoinsGanados = CalcularRecompensaFinPartida(ganoJugador);

        UIManager.Instance?.MostrarGameOver(ganoJugador, esModoTorneo, pollocoinsGanados);
    }

    /// <summary>
    /// Calcula y otorga la recompensa Pollocoins al terminar la partida.
    /// Jefe vencido > ronda de torneo > dificultad VS CPU. PvP = 0.
    /// Devuelve la cantidad otorgada para mostrarla en el panel ("+25 POLLOCOINS").
    /// </summary>
    int CalcularRecompensaFinPartida(bool ganoJugador)
    {
        if (EconomyManager.Instance == null) return 0;
        // PvP entre humanos: sin recompensa.
        if (esModoPvP && !esModoTorneo) return 0;

        // 1) Jefe: si hay combate de jefe activo y el jugador ganó.
        if (ganoJugador && BossManager.Instance != null && BossManager.Instance.JefeActivo != null)
        {
            BossData jefe = BossManager.Instance.ResolverBossDataVictoria(BossManager.Instance.JefeActivo);
            if (jefe == null)
                jefe = BossManager.Instance.ObtenerJefeActualSeleccionado();
            string idJefe = jefe != null ? (jefe.nombre ?? "") : BossManager.Instance.JefeActivo.gameObject.name;
            return EconomyManager.Instance.OtorgarRecompensaJefe(idJefe);
        }

        // 2) Torneo: según la ronda que se acaba de jugar.
        if (esModoTorneo && TournamentUIManager.TorneoActual != null)
        {
            int ronda = (int)TournamentUIManager.TorneoActual.RondaActual;
            return EconomyManager.Instance.OtorgarRecompensaTorneo(ronda, ganoJugador);
        }

        // 3) VS CPU normal: según dificultad seleccionada.
        if (!esModoTorneo && !esModoPvP)
            return EconomyManager.Instance.OtorgarRecompensaCPU(dificultadSeleccionada, ganoJugador);

        return 0;
    }

    // -------------------------------------------------------
    /// <summary>
    /// Cancela cualquier combate de jefe activo (p. ej. Zeus) y limpia sus
    /// efectos al volver a una partida normal (VS CPU o PvP): establece
    /// combateActivo = false, destruye estrella/rayo, detiene coroutines y
    /// elimina el visual especial ZeusVisual_CPU de la raqueta CPU.
    /// </summary>
    private void DetenerJefeActivo()
    {
        // El jefe preparado para la batalla actual (asignado por BossManager
        // ANTES de crear la partida) se respeta: su combate NO se cancela aquí
        // porque BossManager lo reiniciará a continuación con IniciarCombate().
        // Solo se limpian los DEMÁS jefes.
        BossController jefePreparado = null;
        BossManager bossManager = FindObjectOfType<BossManager>();
        if (bossManager != null)
            jefePreparado = bossManager.JefeActivo;

        BossController[] bosses = FindObjectsByType<BossController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < bosses.Length; i++)
        {
            if (bosses[i] == null) continue;

            // No finalizar el combate del jefe que acaba de ser seleccionado
            // para la batalla actual.
            if (jefePreparado != null && bosses[i] == jefePreparado)
                continue;

            bosses[i].FinalizarCombate();
        }

        // Limpiar la skin del jefe en la raqueta CPU (método genérico de BossManager):
        // destruye JefeVisual_CPU y ZeusVisual_CPU, des-suscribe regeneración y
        // reactiva los renderers de la raqueta base. Así la raqueta queda limpia para
        // partidas normales (VS CPU, Torneo, 2 Jugadores).
        // NOTA: NO se pone JefeActivo = null aquí: BossManager lo asignó ANTES de
        // crear la partida y debe persistir durante TODO el combate para que
        // EndGame() sepa contra qué jefe se jugó. Solo se limpia al volver al menú.
        if (bossManager != null)
            bossManager.LimpiarVisualJefeCPU();
    }

    /// <summary>
    /// Limpieza explícita al volver al menú principal / reinicio total.
    /// Único punto donde JefeActivo debe ponerse en null.
    /// </summary>
    public void LimpiarJefeActivoAlVolverAlMenu()
    {
        BossManager bossManager = FindObjectOfType<BossManager>();
        if (bossManager != null)
            bossManager.JefeActivo = null;
    }

    // -------------------------------------------------------
    private void UpdateUI()
    {
        UIManager.Instance?.ActualizarMarcador(playerScore, cpuScore);

        if (playerScoreText != null) playerScoreText.text = playerScore.ToString();
        if (cpuScoreText    != null) cpuScoreText.text    = cpuScore.ToString();
    }

    // -------------------------------------------------------
    public void IniciarPartida()
    {
        Debug.Log("[GameManager] 4. IniciarPartida()");

        // Al iniciar una partida normal VS CPU se cancela cualquier combate de
        // jefe activo (p. ej. Zeus) y se limpian sus efectos/estado/visual.
        DetenerJefeActivo();

        DestroyPracticeModeObjects();

        esModoPvP   = false;
        playerScore = 0;
        cpuScore    = 0;
        gameOver    = false;

        // Asegurarse que CPU AI está activo y P2 desactivado
        if (cpu != null)                cpu.enabled             = true;
        if (cpuControl != null)         cpuControl.enabled      = true;
        if (controlJugador2 != null)    controlJugador2.enabled = false;
        if (golpeRaquetaCPU != null)    golpeRaquetaCPU.esJugador = false;

        if (cpu != null)
            cpu.SetDificultad(dificultadSeleccionada);

        UpdateUI();

        if (ball != null)
            ball.ResetBall();
    }

    // -------------------------------------------------------
    /// <summary>
    /// Inicia una partida de torneo. Lee el partido desde TournamentUIManager,
    /// configura la dificultad según el rival y elige un mapa aleatorio.
    /// </summary>
    public void IniciarPartidaTorneo()
    {
        Debug.Log("[GameManager.IniciarPartidaTorneo] === INICIO ===");

        Match partido = TournamentUIManager.PartidoActualJugando;
        if (partido == null)
        {
            Debug.LogError("[GameManager.IniciarPartidaTorneo] ERROR: PartidoActualJugando es null.");
            return;
        }
        Debug.Log($"[GameManager.IniciarPartidaTorneo] Partido encontrado: {partido}");

        DestroyPracticeModeObjects();

        esModoPvP     = false;
        esModoTorneo  = true;
        playerScore   = 0;
        cpuScore      = 0;
        gameOver      = false;
        Debug.Log("[GameManager.IniciarPartidaTorneo] Scores reiniciados. Modo torneo activado.");

        if (cpu != null)                cpu.enabled             = true;
        if (cpuControl != null)         cpuControl.enabled      = true;
        if (controlJugador2 != null)    controlJugador2.enabled = false;
        if (golpeRaquetaCPU != null)    golpeRaquetaCPU.esJugador = false;
        Debug.Log("[GameManager.IniciarPartidaTorneo] CPU activado, P2 desactivado.");

        // Determinar rival (el que NO es jugador)
        Competidor rival = null;
        if (partido.jugadorA != null && partido.jugadorA.esJugador)
            rival = partido.jugadorB;
        else if (partido.jugadorB != null && partido.jugadorB.esJugador)
            rival = partido.jugadorA;

        if (rival == null)
        {
            Debug.LogError("[GameManager.IniciarPartidaTorneo] ERROR: No se pudo determinar el rival.");
            return;
        }
        Debug.Log($"[GameManager.IniciarPartidaTorneo] Rival: {rival.nombre} | Dificultad: {rival.dificultad}");

        // Configurar dificultad según la del rival
        switch (rival.dificultad)
        {
            case DificultadCPU.Facil:    dificultadSeleccionada = Dificultad.Facil;    break;
            case DificultadCPU.Media:    dificultadSeleccionada = Dificultad.Facil;    break;
            case DificultadCPU.Dificil:  dificultadSeleccionada = Dificultad.Dificil;  break;
            case DificultadCPU.Inhumano: dificultadSeleccionada = Dificultad.Inhumano; break;
        }
        Debug.Log($"[GameManager.IniciarPartidaTorneo] Dificultad asignada: {dificultadSeleccionada}");

        if (cpu != null)
            cpu.SetDificultad(dificultadSeleccionada);

        // Buscar skin del rival por nombre y copiar solo el material (solo torneo)
        if (TournamentUIManager.Instance != null && golpeRaquetaCPU != null)
        {
            Material materialSkin = null;
            foreach (var skin in TournamentUIManager.Instance.skinsCPU)
            {
                if (skin != null && skin.nombreCPU == rival.nombre && skin.skinPrefab != null)
                {
                    Renderer rend = skin.skinPrefab.GetComponentInChildren<Renderer>();
                    if (rend != null)
                        materialSkin = rend.sharedMaterial;
                    break;
                }
            }
            if (materialSkin != null)
                golpeRaquetaCPU.AplicarMaterial(materialSkin);
        }

        // Mapa aleatorio sin repetir el anterior (solo para torneo)
        if (MapManager.Instance != null)
        {
            MapManager.Instance.SeleccionarMapaAleatorio();
        }
        else
        {
            Debug.LogWarning("[GameManager.IniciarPartidaTorneo] MapManager.Instance es null, no se cambió el mapa.");
        }

        UpdateUI();

        if (ball != null)
            ball.ResetBall();
        else
            Debug.LogWarning("[GameManager.IniciarPartidaTorneo] ball es null, no se reseteó.");

        Debug.Log($"[GameManager.IniciarPartidaTorneo] Partida iniciada vs {rival.nombre} (dif: {dificultadSeleccionada})");
        Debug.Log("[GameManager.IniciarPartidaTorneo] === FIN ===");
    }

    // -------------------------------------------------------
    public void IniciarPartidaPvP()
    {
        Debug.Log($"[PvP] Antes — cpu.enabled={cpu?.enabled} | cpuControl.enabled={cpuControl?.enabled} | j2.enabled={controlJugador2?.enabled}");

        // ════════════════════════════════════════════════════════
        // DIAGNÓSTICO: inventario completo de componentes en escena
        // ════════════════════════════════════════════════════════
        Debug.Log("========== DIAG PvP: CPUControl ==========");
        var diagCPU = FindObjectsByType<CPUControl>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[DIAG] CPUControl totales: {diagCPU.Length}");
        foreach (var c in diagCPU)
        {
            string esRef = (c == cpu) ? " ← REF cpu" : (c == cpuControl) ? " ← REF cpuControl" : "";
            Debug.Log($"[DIAG]   \"{c.name}\" id={c.GetInstanceID()} enabled={c.enabled} activeHierarchy={c.gameObject.activeInHierarchy} parent={(c.transform.parent?.name ?? "ninguno")}{esRef}");
        }

        Debug.Log("========== DIAG PvP: RaquetaControlP2 ==========");
        var diagP2 = FindObjectsByType<RaquetaControlP2>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[DIAG] RaquetaControlP2 totales: {diagP2.Length}");
        foreach (var p2 in diagP2)
        {
            string esRef = (p2 == controlJugador2) ? " ← REF controlJugador2" : "";
            Debug.Log($"[DIAG]   \"{p2.name}\" id={p2.GetInstanceID()} enabled={p2.enabled} activeHierarchy={p2.gameObject.activeInHierarchy} parent={(p2.transform.parent?.name ?? "ninguno")}{esRef}");
        }

        Debug.Log("========== DIAG PvP: RaquetaControl (J1) ==========");
        var diagR = FindObjectsByType<RaquetaControl>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[DIAG] RaquetaControl totales: {diagR.Length}");
        foreach (var r in diagR)
        {
            Debug.Log($"[DIAG]   \"{r.name}\" id={r.GetInstanceID()} enabled={r.enabled} activeHierarchy={r.gameObject.activeInHierarchy} parent={(r.transform.parent?.name ?? "ninguno")}");
        }

        Debug.Log("========== DIAG PvP: RaquetaGolpe ==========");
        var diagG = FindObjectsByType<RaquetaGolpe>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[DIAG] RaquetaGolpe totales: {diagG.Length}");
        foreach (var g in diagG)
        {
            string esRef = (g == golpeRaquetaJugador) ? " ← REF golpeRaquetaJugador" : (g == golpeRaquetaCPU) ? " ← REF golpeRaquetaCPU" : "";
            Debug.Log($"[DIAG]   \"{g.name}\" id={g.GetInstanceID()} esJugador={g.esJugador} enabled={g.enabled} activeHierarchy={g.gameObject.activeInHierarchy} parent={(g.transform.parent?.name ?? "ninguno")}{esRef}");
        }

        Debug.Log("========== FIN DIAG PvP ==========");
        // ════════════════════════════════════════════════════════

        // ══ IMPORTANTE: marcar modo PvP ANTES de destruir objetos de práctica ══
        // PracticeModeManager.OnDestroy() revisa EsModoPvP para no sobrescribir el estado.
        esModoPvP   = true;

        // Al iniciar una partida PvP se cancela cualquier combate de jefe activo
        // (p. ej. Zeus) y se limpian sus efectos/estado/visual.
        DetenerJefeActivo();

        DestroyPracticeModeObjects();
        playerScore = 0;
        cpuScore    = 0;
        gameOver    = false;

        // ── PASO 1: Auto‑reparar referencias si están null ──
        if (cpu == null)
        {
            cpu = FindFirstObjectByType<CPUControl>(FindObjectsInactive.Include);
            Debug.Log($"[PvP] cpu recuperada automáticamente: {cpu?.name ?? "null"}");
        }
        if (cpuControl == null)
        {
            // cpuControl apunta al mismo objeto que cpu típicamente, o podría ser otro
            // Buscamos el que esté en un hijo o un CPUControl diferente
            var todos = FindObjectsByType<CPUControl>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in todos)
            {
                if (c != cpu)
                {
                    cpuControl = c;
                    break;
                }
            }
        }
        if (controlJugador2 == null)
        {
            controlJugador2 = FindFirstObjectByType<RaquetaControlP2>(FindObjectsInactive.Include);
            Debug.Log($"[PvP] controlJugador2 recuperado: {controlJugador2?.name ?? "null"}");
        }

        // ── PASO 2: Deshabilitar las referencias serializadas ──
        if (cpu != null)                cpu.enabled             = false;
        if (cpuControl != null)         cpuControl.enabled      = false;
        if (controlJugador2 != null)    controlJugador2.enabled = true;
        if (golpeRaquetaCPU != null)    golpeRaquetaCPU.esJugador = true;

        // ── PASO 3: BARRIDO DE SEGURIDAD ────────────────────
        // Buscar TODOS los CPUControl en la escena y desactivarlos
        // (esto cubre el caso donde la instanciación del mapa creó nuevos objetos CPUControl)
        var todosLosCPU = FindObjectsByType<CPUControl>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in todosLosCPU)
        {
            if (c.enabled)
            {
                Debug.Log($"[PvP] CPUControl extra encontrado y desactivado: {c.name}");
                c.enabled = false;
            }
        }

        // Buscar TODOS los RaquetaControlP2 en la escena y activarlos
        var todosLosP2 = FindObjectsByType<RaquetaControlP2>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var p2 in todosLosP2)
        {
            if (!p2.enabled)
            {
                Debug.Log($"[PvP] RaquetaControlP2 activado: {p2.name}");
                p2.enabled = true;
            }
        }

        UpdateUI();

        if (ball != null)
            ball.ResetBall();

        Debug.Log("[GameManager] Modo PvP iniciado");
        Debug.Log($"[PvP] Después — cpu.enabled={cpu?.enabled} | cpuControl.enabled={cpuControl?.enabled} | j2.enabled={controlJugador2?.enabled}");
    }

    // -------------------------------------------------------
    public void VolverAlMenu()
    {
        DestroyPracticeModeObjects();

        // Restaurar modo VS CPU al salir
        if (esModoPvP) TerminarModoPvP();

        playerScore = 0;
        cpuScore    = 0;
        gameOver    = false;

        if (ball != null)
            ball.CongelarPelota();

        UpdateUI();
    }

    // -------------------------------------------------------
    public void TerminarModoPvP()
    {
        esModoPvP = false;
        // Reactivar CPU completamente
        if (cpu != null)                cpu.enabled             = true;
        if (cpuControl != null)         cpuControl.enabled      = true;
        if (controlJugador2 != null)    controlJugador2.enabled = false;
        if (golpeRaquetaCPU != null)    golpeRaquetaCPU.esJugador = false;
    }

    void DestroyPracticeModeObjects()
    {
        if (PracticeModeManager.Instance != null)
            Destroy(PracticeModeManager.Instance.gameObject);

        var practiceUI = FindObjectOfType<PracticeUI>();
        if (practiceUI != null)
            Destroy(practiceUI.gameObject);
    }

    // -------------------------------------------------------
    public void RestartGame()
    {
        playerScore = 0;
        cpuScore    = 0;
        gameOver    = false;

        if (esModoPvP)
            IniciarPartidaPvP();
        else
        {
            if (cpu != null)
                cpu.SetDificultad(dificultadSeleccionada);
        }

        RegenerarRaquetas(0f);

        UpdateUI();

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (ball != null)
            ball.ResetBall();
    }

    // -------------------------------------------------------
    void ActivarVolcanes(bool epico, bool continuo = false)
    {
        if (sonidoVolcan != null && audioSource != null && !continuo)
            audioSource.PlayOneShot(sonidoVolcan);

        if (volcanIzquierda != null)
        {
            var main     = volcanIzquierda.main;
            var emission = volcanIzquierda.emission;

            if (epico)
            {
                main.startSpeed    = new ParticleSystem.MinMaxCurve(15f, 25f);
                main.startSize     = new ParticleSystem.MinMaxCurve(1.5f, 3f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            }

            main.loop = continuo;
            emission.rateOverTime = continuo ? 40f : 0f;
            volcanIzquierda.Play();
        }

        if (volcanDerecha != null)
        {
            var main     = volcanDerecha.main;
            var emission = volcanDerecha.emission;

            if (epico)
            {
                main.startSpeed    = new ParticleSystem.MinMaxCurve(15f, 25f);
                main.startSize     = new ParticleSystem.MinMaxCurve(1.5f, 3f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            }

            main.loop = continuo;
            emission.rateOverTime = continuo ? 40f : 0f;
            volcanDerecha.Play();
        }
    }

    public void DetenerVolcanes()
    {
        if (volcanIzquierda != null)
        {
            var main     = volcanIzquierda.main;
            var emission = volcanIzquierda.emission;
            main.loop             = false;
            emission.rateOverTime = 0f;
            volcanIzquierda.Stop();
            volcanIzquierda.Clear();
        }
        if (volcanDerecha != null)
        {
            var main     = volcanDerecha.main;
            var emission = volcanDerecha.emission;
            main.loop             = false;
            emission.rateOverTime = 0f;
            volcanDerecha.Stop();
            volcanDerecha.Clear();
        }
    }

    // -------------------------------------------------------
    void PlayPointSound(float velocidadPelota)
    {
        if (audioSource == null) return;

        AudioClip clip = null;
        if (velocidadPelota >= 200f)
            clip = sonidoPuntoUltra != null ? sonidoPuntoUltra : sonidoPuntoEpico != null ? sonidoPuntoEpico : sonidoPuntoNormal;
        else if (velocidadPelota >= 150f)
            clip = sonidoPuntoEpico != null ? sonidoPuntoEpico : sonidoPuntoNormal;
        else
            clip = sonidoPuntoNormal;

        if (clip != null)
            audioSource.PlayOneShot(clip);
    }

    void CargarClipsPorDefecto()
    {
        sonidoPuntoNormal = ResolverClip(sonidoPuntoNormal, "Assets/Ping Pong/Sonidos/freesound_community-success_bell-6776.mp3");
        sonidoPuntoEpico  = ResolverClip(sonidoPuntoEpico, "Assets/Ping Pong/Sonidos/Voicy_Ultra combooooo.mp3");
        sonidoPuntoUltra  = ResolverClip(sonidoPuntoUltra, "Assets/Ping Pong/Sonidos/ultra-killer-instinct.mp3");
        sonidoVolcan      = ResolverClip(sonidoVolcan, "Assets/Ping Pong/Sonidos/Pelota/dragon-studio-nuclear-explosion-386181.mp3");
    }

    AudioClip ResolverClip(AudioClip currentClip, string path)
    {
        if (currentClip != null) return currentClip;
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
#else
        return null;
#endif
    }

    public int PlayerScore  => playerScore;
    public int CpuScore     => cpuScore;
    public bool IsGameOver  => gameOver;
    public bool EsModoPvP   => esModoPvP;
}