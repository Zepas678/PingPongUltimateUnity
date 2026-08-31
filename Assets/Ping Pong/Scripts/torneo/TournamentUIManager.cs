using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Maneja la interfaz visual del bracket del torneo.
/// Lee los datos de TournamentManager y actualiza los nombres en las llaves,
/// la ronda actual y el estado de los botones.
/// No duplica lógica del torneo.
/// </summary>
public class TournamentUIManager : MonoBehaviour
{
    [System.Serializable]
    public class SkinCPUConfig
    {
        public string nombreCPU;
        public GameObject skinPrefab;
    }

    [Header("Referencia al TournamentManager")]
    public TournamentManager torneo;

    [Header("Indicador de ronda")]
    public TMP_Text textoRonda;

    [Header("Slots de octavos (8 partidos, 16 nombres)")]
    public TMP_Text[] octavosNombresA = new TMP_Text[8]; // 8 jugadores "de arriba"
    public TMP_Text[] octavosNombresB = new TMP_Text[8]; // 8 jugadores "de abajo"

    [Header("Slots de cuartos (4 partidos, 8 nombres)")]
    public TMP_Text[] cuartosNombresA = new TMP_Text[4];
    public TMP_Text[] cuartosNombresB = new TMP_Text[4];

    [Header("Slots de semifinal (2 partidos, 4 nombres)")]
    public TMP_Text[] semiNombresA = new TMP_Text[2];
    public TMP_Text[] semiNombresB = new TMP_Text[2];

    [Header("Slots de final (1 partido, 2 nombres)")]
    public TMP_Text finalNombreA;
    public TMP_Text finalNombreB;

    [Header("Botones")]
    public GameObject botonJugar;
    public GameObject botonVolver;

    [Header("Skins fijas para cada CPU (15 slots, skinID = índice en la lista)")]
    public List<SkinCPUConfig> skinsCPU = new List<SkinCPUConfig>(15);

    // ─── Referencias estáticas para acceso desde GameManager ───
    /// <summary>
    /// Partido actual que se está jugando en el torneo.
    /// Se asigna al presionar JUGAR. La escena del partido lo lee
    /// para saber quién es el rival y registrar el ganador al terminar.
    /// </summary>
    public static Match PartidoActualJugando { get; private set; }

    /// <summary>
    /// Referencia estática al TournamentManager activo.
    /// Se asigna en Start() para que GameManager pueda registrar ganadores.
    /// Apunta exactamente al mismo objeto que usa la UI (campo 'torneo').
    /// </summary>
    public static TournamentManager TorneoActual { get; private set; }

    /// <summary>
    /// Referencia estática a esta instancia para acceder a las skins.
    /// </summary>
    public static TournamentUIManager Instance { get; private set; }

    // -------------------------------------------------------
    void Awake()
    {
        Instance = this;
    }

    // ──────────────────────────────────────────────
    void Start()
    {
        // Inicialización automática para pruebas: si no hay torneo, lo crea.
        if (torneo == null)
        {
            Debug.Log("[TournamentUIManager] Creando TournamentManager automáticamente (modo pruebas).");
            torneo = new TournamentManager();
            torneo.CrearNuevoTorneo();
        }

        // Exponer estáticamente la misma instancia para GameManager
        TorneoActual = torneo;

        // Suscribirse al evento de actualización del torneo
        torneo.OnTorneoActualizado += ActualizarBracket;

        // Mostrar estado inicial
        ActualizarBracket();
    }

    void OnDestroy()
    {
        if (torneo != null)
            torneo.OnTorneoActualizado -= ActualizarBracket;
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Lee toda la información del torneo y actualiza los textos del bracket.
    /// Se llama automáticamente cuando el torneo notifica cambios.
    /// </summary>
    public void ActualizarBracket()
    {
        if (torneo == null) return;

        // 0. Limpiar toda la UI del bracket antes de redibujar
        LimpiarBracket();

        // 1. Mostrar ronda actual
        ActualizarTextoRonda();

        // 2. Llenar octavos (siempre visibles, se actualizan aunque la ronda ya pasó)
        LlenarOctavos();

        // 3. Llenar cuartos
        LlenarCuartos();

        // 4. Llenar semifinales
        LlenarSemifinales();

        // 5. Llenar final
        LlenarFinal();

        // 6. Actualizar visibilidad del botón JUGAR
        ActualizarBotonJugar();
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Limpia todos los textos del bracket dejándolos en "--------".
    /// Se ejecuta siempre al inicio de ActualizarBracket().
    /// </summary>
    private void LimpiarBracket()
    {
        // Octavos
        for (int i = 0; i < 8; i++)
        {
            if (octavosNombresA[i] != null) octavosNombresA[i].text = "--------";
            if (octavosNombresB[i] != null) octavosNombresB[i].text = "--------";
        }

        // Cuartos
        for (int i = 0; i < 4; i++)
        {
            if (cuartosNombresA[i] != null) cuartosNombresA[i].text = "--------";
            if (cuartosNombresB[i] != null) cuartosNombresB[i].text = "--------";
        }

        // Semifinales
        for (int i = 0; i < 2; i++)
        {
            if (semiNombresA[i] != null) semiNombresA[i].text = "--------";
            if (semiNombresB[i] != null) semiNombresB[i].text = "--------";
        }

        // Final
        if (finalNombreA != null) finalNombreA.text = "--------";
        if (finalNombreB != null) finalNombreB.text = "--------";
    }

    // ──────────────────────────────────────────────
    private void ActualizarTextoRonda()
    {
        if (textoRonda == null) return;

        switch (torneo.RondaActual)
        {
            case TournamentManager.Ronda.Octavos:   textoRonda.text = "OCTAVOS DE FINAL";   break;
            case TournamentManager.Ronda.Cuartos:   textoRonda.text = "CUARTOS DE FINAL";    break;
            case TournamentManager.Ronda.Semifinal: textoRonda.text = "SEMIFINAL";            break;
            case TournamentManager.Ronda.Final:      textoRonda.text = "FINAL";               break;
            case TournamentManager.Ronda.Terminado:  textoRonda.text = "¡TORNEO TERMINADO!";  break;
        }
    }

    // ──────────────────────────────────────────────
    private void LlenarOctavos()
    {
        if (torneo.Octavos == null) return;

        for (int i = 0; i < torneo.Octavos.Count && i < 8; i++)
        {
            var match = torneo.Octavos[i];
            if (octavosNombresA[i] != null)
                octavosNombresA[i].text = match.jugadorA?.nombre ?? "?";

            if (octavosNombresB[i] != null)
                octavosNombresB[i].text = match.jugadorB?.nombre ?? "?";
        }
    }

    // ──────────────────────────────────────────────
    private void LlenarCuartos()
    {
        if (torneo.Cuartos == null) return;

        for (int i = 0; i < torneo.Cuartos.Count && i < 4; i++)
        {
            var match = torneo.Cuartos[i];
            if (cuartosNombresA[i] != null)
                cuartosNombresA[i].text = match.jugadorA?.nombre ?? "—";

            if (cuartosNombresB[i] != null)
                cuartosNombresB[i].text = match.jugadorB?.nombre ?? "—";
        }
    }

    // ──────────────────────────────────────────────
    private void LlenarSemifinales()
    {
        if (torneo.Semifinales == null) return;

        for (int i = 0; i < torneo.Semifinales.Count && i < 2; i++)
        {
            var match = torneo.Semifinales[i];
            if (semiNombresA[i] != null)
                semiNombresA[i].text = match.jugadorA?.nombre ?? "—";

            if (semiNombresB[i] != null)
                semiNombresB[i].text = match.jugadorB?.nombre ?? "—";
        }
    }

    // ──────────────────────────────────────────────
    private void LlenarFinal()
    {
        if (torneo.Final == null) return;

        if (finalNombreA != null)
            finalNombreA.text = torneo.Final.jugadorA?.nombre ?? "—";

        if (finalNombreB != null)
            finalNombreB.text = torneo.Final.jugadorB?.nombre ?? "—";
    }

    // ──────────────────────────────────────────────
    private void ActualizarBotonJugar()
    {
        if (botonJugar == null) return;

        // Mostrar el botón JUGAR solo si hay un partido pendiente del jugador
        bool hayPartidoPendiente = torneo.ObtenerPartidoDelJugador() != null;
        botonJugar.SetActive(hayPartidoPendiente);
    }

    // ──────────────────────────────────────────────
    // Métodos públicos para botones (solo UI)
    // ──────────────────────────────────────────────

    /// <summary>Llamado por el botón JUGAR.</summary>
    public void OnClickJugar()
    {
        if (torneo == null)
        {
            Debug.LogError("[TournamentUIManager] No hay torneo para jugar.");
            return;
        }

        // 1. Obtener el partido pendiente del jugador
        Match partido = torneo.ObtenerPartidoDelJugador();
        if (partido == null)
        {
            Debug.LogWarning("[TournamentUIManager] No hay partido pendiente del jugador.");
            return;
        }

        // 2. Guardar el Match para que GameManager lo use
        PartidoActualJugando = partido;

        // 3. Transición de UI: ocultar panel del torneo, mostrar HUD
        if (UIManager.Instance != null)
        {
            UIManager.Instance.MostrarPanel(UIManager.Instance.panelHUD);
        }

        // 4. Delegar el inicio de la partida a GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.IniciarPartidaTorneo();
        }
        else
        {
            Debug.LogError("[TournamentUIManager] No hay GameManager en la escena.");
        }
    }

    /// <summary>Llamado por el botón VOLVER.</summary>
    public void OnClickVolver()
    {
        Debug.Log("[TournamentUIManager] Botón VOLVER presionado.");
        // Volver al menú principal limpiando el estado del torneo
        if (GameManager.Instance != null)
        {
            GameManager.Instance.DetenerVolcanes();
        }
        Time.timeScale = 1f;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.MostrarPanel(UIManager.Instance.panelMenuPrincipal);
        }
    }
}