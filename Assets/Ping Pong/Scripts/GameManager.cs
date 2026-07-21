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

    if (ball != null)
        ball.CongelarPelota();
    }

    // -------------------------------------------------------
    public void RegisterPoint(bool scoredForPlayer, float velocidadPelota = 0f)
    {
        if (gameOver) return;

        if (scoredForPlayer)
        {
            playerScore++;
            Debug.Log($"[GameManager] PUNTO JUGADOR | {playerScore} - {cpuScore}");
        }
        else
        {
            cpuScore++;
            Debug.Log($"[GameManager] PUNTO CPU | {playerScore} - {cpuScore}");
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
        UIManager.Instance?.MostrarGameOver(ganoJugador);
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