using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Administra la selección de jefes desde la interfaz.
/// Muestra la información del jefe seleccionado, permite navegar entre ellos
/// y actualiza el estado de bloqueo y el botón de combate.
/// No contiene lógica de combate; solo prepara la selección.
/// </summary>
public class BossManager : MonoBehaviour
{
    [Header("Datos de jefes")]
    [Tooltip("Lista de jefes configurable desde el Inspector. Cada elemento se edita directamente en el Editor.")]
    public List<BossData> bosses = new List<BossData>();

    /// <summary>Índice del jefe actualmente seleccionado.</summary>
    public int currentBossIndex = 0;

    [Header("UI - Información del jefe")]
    public Image imagenJefe;
    public TMP_Text textoNombre;
    public TMP_Text textoTitulo;
    public TMP_Text textoDescripcion;
    public TMP_Text textoDificultad;

    [Header("UI - Navegación e indicadores")]
    public Button botonAnterior;
    public Button botonSiguiente;
    /// <summary>Indicadores inferiores (puntos) que reflejan el jefe seleccionado.</summary>
    public Image[] indicadores; // Imágenes rellenas/activas según selección

    [Header("UI - Estado del jefe")]
    public GameObject lockedPanel;      // Panel o overlay de bloqueo
    public Button botonCombatir;        // Botón para iniciar el combate

    [Header("Apariencia CPU Zeus (VS CPU)")]
    [Tooltip("Escala del modelo visual Zeus que se añade como hijo de la raqueta CPU (solo cuando el jefe seleccionado es Zeus). Ajustar en Inspector para coincidir con el tamaño de la raqueta.")]
    public Vector3 escalaZeusCpu = Vector3.one;

    /// <summary>Nombre del GameObject hijo (visual Zeus) añadido a la raqueta CPU.</summary>
    public const string NOMBRE_HIJO_ZEUS_VISUAL = "ZeusVisual_CPU";

    // ──────────────────────────────────────────────
    void Start()
    {
        // Mostrar el jefe inicial
        ActualizarUI();
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Actualiza toda la UI según el jefe seleccionado actualmente.
    /// </summary>
    public void ActualizarUI()
    {
        if (bosses == null || bosses.Count == 0)
        {
            Debug.LogWarning("[BossManager] No hay jefes configurados en la lista.");
            return;
        }

        // Asegurar que el índice esté dentro de rango
        currentBossIndex = Mathf.Clamp(currentBossIndex, 0, bosses.Count - 1);

        BossData jefe = bosses[currentBossIndex];
        if (jefe == null) return;

        // 1. Mostrar información del jefe
        if (textoNombre != null) textoNombre.text = jefe.nombre;
        if (textoTitulo != null) textoTitulo.text = jefe.titulo;
        if (textoDescripcion != null) textoDescripcion.text = jefe.descripcion;
        if (textoDificultad != null) textoDificultad.text = "Dificultad: " + jefe.dificultad;
        if (imagenJefe != null) imagenJefe.sprite = jefe.imagen;

        // 2. Actualizar el color de tema
        ActualizarColorTema(jefe);

        // 3. Actualizar indicadores (puntos)
        ActualizarIndicadores();

        // 4. Actualizar estado de bloqueo
        ActualizarEstadoBloqueo(jefe);

        // 5. Actualizar botones de navegación
        ActualizarBotonesNavegacion();

        // 6. Actualizar botón de combate
        ActualizarBotonCombatir(jefe);
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Navega al siguiente jefe.
    /// </summary>
    public void NextBoss()
    {
        if (bosses == null || bosses.Count == 0) return;

        currentBossIndex = (currentBossIndex + 1) % bosses.Count;
        ActualizarUI();
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Navega al jefe anterior.
    /// </summary>
    public void PreviousBoss()
    {
        if (bosses == null || bosses.Count == 0) return;

        currentBossIndex = (currentBossIndex - 1 + bosses.Count) % bosses.Count;
        ActualizarUI();
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Inicia el combate contra el jefe seleccionado.
    /// Reutiliza el flujo normal de una partida VS CPU: selecciona el mapa
    /// con MapManager y llama a UIManager.IniciarPartida() con la dificultad del jefe.
    /// </summary>
    public void StartBossBattle()
    {
        Debug.Log("[BossManager] 1. StartBossBattle()");
        if (bosses == null || bosses.Count == 0) return;

        BossData jefe = bosses[currentBossIndex];
        if (jefe == null) return;

        if (!jefe.desbloqueado)
        {
            Debug.LogWarning("[BossManager] El jefe '" + jefe.nombre + "' está bloqueado.");
            return;
        }

        // Verificar que el jefe tenga un mapa configurado
        if (string.IsNullOrEmpty(jefe.nombreMapa))
        {
            Debug.LogWarning("[BossManager] El jefe '" + jefe.nombre + "' no tiene un mapa configurado.");
            return;
        }

        // Buscar el MapManager de la escena
        MapManager mapManager = FindObjectOfType<MapManager>();
        if (mapManager == null)
        {
            Debug.LogError("[BossManager] No se encontró un MapManager en la escena.");
            return;
        }

        // Buscar el UIManager de la escena
        if (UIManager.Instance == null)
        {
            Debug.LogError("[BossManager] No se encontró un UIManager en la escena.");
            return;
        }

        Debug.Log("Iniciando combate contra " + jefe.nombre + " (mapa: " + jefe.nombreMapa + ")");

        // 1. Seleccionar el mapa del jefe usando el MapManager existente
        mapManager.SeleccionarMapaPorNombre(jefe.nombreMapa);
        Debug.Log("[BossManager] 2. MapManager.SeleccionarMapaPorNombre() llamado");

        // 2. Iniciar la partida con el mismo flujo que una partida VS CPU,
        //    usando la dificultad del jefe. UIManager.IniciarPartida() se encarga
        //    de ocultar la UI, mostrar el HUD y llamar a GameManager.IniciarPartida().
        // Solo el jefe Zeus usa la dificultad máxima (Inhumano); los demás jefes
        // conservan su dificultad propia.
        Dificultad dificultadCombate = EsJefeZeus(jefe) ? Dificultad.Inhumano : DificultadDelJefe(jefe.dificultad);
        UIManager.Instance.IniciarPartida(dificultadCombate);
        Debug.Log("[BossManager] 3. UIManager.IniciarPartida() llamado");

        // 3. Una vez que la partida fue creada por GameManager, activar el combate del jefe.
        BossZeus bossZeus = FindObjectOfType<BossZeus>();
        if (bossZeus != null)
        {
            bossZeus.IniciarCombate();

            // Si el jefe seleccionado es Zeus, aplicar su modelo visual a la raqueta CPU.
            AplicarVisualZeusCPU(jefe, bossZeus);
        }
        else
        {
            Debug.LogWarning("[BossManager] No se encontró un BossZeus en la escena para iniciar el combate.");
        }
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Añade el modelo visual de Zeus (BossZeus.jefe → raquetaZeus) como hijo de la
    /// raqueta de la CPU, solo cuando el jefe seleccionado es Zeus y en modo VS CPU.
    /// Conserva collider, RaquetaGolpe, CPUControl, Animator y lógica; no duplica hijos.
    /// </summary>
    private void AplicarVisualZeusCPU(BossData jefe, BossZeus bossZeus)
    {
        // Solo si es Zeus y hay modelo y GameManager con raqueta CPU.
        if (jefe == null || bossZeus == null || bossZeus.jefe == null) return;
        if (!EsJefeZeus(jefe)) return;
        if (GameManager.Instance == null || GameManager.Instance.golpeRaquetaCPU == null) return;

        Transform raizCPU = GameManager.Instance.golpeRaquetaCPU.transform;

        // Nombre lógico del modelo Zeus para detección de duplicados.
        const string nombreHijoZeus = NOMBRE_HIJO_ZEUS_VISUAL;

        // Evitar duplicados: si ya existe el hijo visual, no instanciar otro.
        // (La suscripción al evento de regeneración ya está hecha la primera vez,
        // por lo que si el hijo ya existe, aquí solo nos aseguramos de suscribirnos
        // de nuevo si hiciera falta sin duplicarla.)
        if (raizCPU.Find(nombreHijoZeus) != null)
        {
            SuscribirRegeneracionCPU();
            return;
        }

        // ── 1. Instanciar Zeus como hijo visual de la raqueta CPU ──
        GameObject instanciaZeus = Instantiate(bossZeus.jefe.gameObject, raizCPU);
        instanciaZeus.name = nombreHijoZeus;
        instanciaZeus.transform.localPosition = new Vector3(0f, 0.005f, -0.0207f);
        instanciaZeus.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // ── 2. Buscar recursivamente renderers y meshes de la instancia ──
        Renderer[] renderersZeus = instanciaZeus.GetComponentsInChildren<Renderer>(true);
        MeshFilter[] meshesZeus = instanciaZeus.GetComponentsInChildren<MeshFilter>(true);

        Debug.Log(
            "[ZEUS VISUAL] Instancia=" + instanciaZeus.name +
            " | Renderers=" + renderersZeus.Length +
            " | MeshFilters=" + meshesZeus.Length +
            " | hijos=" + instanciaZeus.transform.childCount +
            " | activeSelf=" + instanciaZeus.activeSelf +
            " | activeInHierarchy=" + instanciaZeus.activeInHierarchy
        );

        foreach (Renderer r in renderersZeus)
        {
            Debug.Log(
                "[ZEUS VISUAL RENDERER] " + r.name +
                " | enabled=" + r.enabled +
                " | active=" + r.gameObject.activeInHierarchy
            );
        }

        Debug.Log(
            "[ZEUS SOURCE] nombre=" + bossZeus.jefe.name +
            " | hijos=" + bossZeus.jefe.childCount +
            " | renderers=" +
            bossZeus.jefe.GetComponentsInChildren<Renderer>(true).Length
        );

        // ── 3. ¿Tiene Zeus al menos un Renderer válido y activo? ──
        bool zeusTieneRendererValido = false;
        for (int i = 0; i < renderersZeus.Length; i++)
        {
            if (renderersZeus[i] != null && renderersZeus[i].enabled)
            {
                zeusTieneRendererValido = true;
                break;
            }
        }

        if (zeusTieneRendererValido)
        {
            // ── 4a. Con renderer válido: escalar y ocultar SOLO el renderer
            // original de la raqueta CPU (sin eliminarlo) ──
            instanciaZeus.transform.localScale = new Vector3(0.05216377f, 0.0129063f, 0.0384122f);

            Renderer[] renderersOriginales = GameManager.Instance.golpeRaquetaCPU.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer br in renderersOriginales)
            {
                // No ocultar los renderers de la instancia Zeus.
                if (br.transform.IsChildOf(instanciaZeus.transform)) continue;
                br.enabled = false;
            }

            // Suscribirse a la regeneración de la raqueta CPU: cuando se destruye
            // visualmente y se regenera (modo épico), AnimarRegeneracion reactiva el
            // renderer base; este evento permite re-ocultarla y mantener solo Zeus.
            SuscribirRegeneracionCPU();

            // Evitar el destello de la raqueta default durante regeneraciones posteriores:
            // mientras dure el combate de Zeus, AnimarRegeneracion no re-activa la base.
            GameManager.Instance.golpeRaquetaCPU.ocultarRendererBaseAlRegenerar = true;

            Debug.Log($"[BossManager] Modelo visual Zeus aplicado a la raqueta CPU (hijo '{nombreHijoZeus}').");
        }
        else
        {
            // ── 4b. Sin renderer válido: NO ocultar la raqueta original y
            // destruir la instancia inválida ──
            Destroy(instanciaZeus);
            Debug.LogWarning("[ZEUS VISUAL] La instancia Zeus no tiene Renderers válidos: se destruyó y se conserva la raqueta original.");
        }
    }

    /// <summary>Devuelve true si el jefe seleccionado es Zeus (por nombre).</summary>
    private bool EsJefeZeus(BossData jefe) =>
        jefe != null && !string.IsNullOrWhiteSpace(jefe.nombre) &&
        jefe.nombre.ToLowerInvariant() == "zeus";

    // ──────────────────────────────────────────────
    /// <summary>
    /// Suscribe (de forma idempotente, sin duplicados) el handler que re-oculta
    /// la raqueta base cuando esta se regenera durante el combate de Zeus.
    /// </summary>
    private void SuscribirRegeneracionCPU()
    {
        if (GameManager.Instance == null || GameManager.Instance.golpeRaquetaCPU == null) return;
        RaquetaGolpe raquetaCPU = GameManager.Instance.golpeRaquetaCPU;

        // Siempre des-suscribir primero para garantizar una única suscripción.
        raquetaCPU.onRegenerada -= ReaplicarOcultadoZeus;
        raquetaCPU.onRegenerada += ReaplicarOcultadoZeus;
    }

    /// <summary>
    /// Handler del evento onRegenerada de la raqueta CPU: tras una regeneración
    /// (destrucción visual y reaparición, típico del modo épico), re-oculta los
    /// renderers de la raqueta base y mantiene únicamente el visual Zeus, siempre
    /// que el combate de Zeus siga activo (el hijo ZeusVisual_CPU exista).
    /// </summary>
    private void ReaplicarOcultadoZeus()
    {
        if (GameManager.Instance == null || GameManager.Instance.golpeRaquetaCPU == null) return;
        Transform raizCPU = GameManager.Instance.golpeRaquetaCPU.transform;

        // Si el visual Zeus ya no existe, el combate de Zeus terminó o se limpió:
        // no hacer nada (la raqueta base puede mostrarse con normalidad).
        Transform zeusVisual = raizCPU.Find(BossManager.NOMBRE_HIJO_ZEUS_VISUAL);
        if (zeusVisual == null) return;

        Renderer[] renderersBase = raizCPU.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderersBase.Length; i++)
        {
            Renderer br = renderersBase[i];
            if (br == null) continue;
            // No ocultar los renderers del visual Zeus.
            if (br.transform.IsChildOf(zeusVisual)) continue;
            br.enabled = false;
        }

        Debug.Log("[BossManager] Reaplicado ocultado de raqueta base tras regeneración (solo Zeus visible).");
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Convierte el valor entero de dificultad del jefe al enum Dificultad.
    /// </summary>
    private Dificultad DificultadDelJefe(int dificultad)
    {
        switch (dificultad)
        {
            case 0:  return Dificultad.Facil;
            case 1:  return Dificultad.Media;
            case 2:  return Dificultad.Dificil;
            case 3:  return Dificultad.Inhumano;
            default: return Dificultad.Facil;
        }
    }

    // ──────────────────────────────────────────────
    // Métodos privados de UI
    // ──────────────────────────────────────────────

    private void ActualizarColorTema(BossData jefe)
    {
        if (indicadores == null || jefe == null) return;

        for (int i = 0; i < indicadores.Length; i++)
        {
            if (indicadores[i] == null) continue;

            // Solo el indicador del jefe seleccionado usa el color de tema
            if (i == currentBossIndex)
                indicadores[i].color = jefe.colorTema;
        }
    }

    private void ActualizarIndicadores()
    {
        if (indicadores == null) return;

        for (int i = 0; i < indicadores.Length; i++)
        {
            if (indicadores[i] == null) continue;

            // Mostrar solo los indicadores que tienen jefe asignado
            indicadores[i].gameObject.SetActive(i < bosses.Count);

            // Los indicadores no seleccionados vuelven a un estado neutro
            if (i != currentBossIndex)
            {
                Color neutro = indicadores[i].color;
                neutro.a = 0.3f; // Apagado / no seleccionado
                indicadores[i].color = neutro;
            }
            else
            {
                Color activo = indicadores[i].color;
                activo.a = 1f; // Encendido / seleccionado
                indicadores[i].color = activo;
            }
        }

        // Reaplicar el color de tema al indicador seleccionado
        if (currentBossIndex >= 0 && currentBossIndex < bosses.Count && bosses[currentBossIndex] != null)
            ActualizarColorTema(bosses[currentBossIndex]);
    }

    private void ActualizarEstadoBloqueo(BossData jefe)
    {
        if (lockedPanel == null) return;

        // Mostrar el panel de bloqueo solo si el jefe está bloqueado
        lockedPanel.SetActive(!jefe.desbloqueado);
    }

    private void ActualizarBotonesNavegacion()
    {
        // Con navegación cíclica ambos botones siempre están disponibles
        if (botonAnterior != null) botonAnterior.interactable = bosses.Count > 1;
        if (botonSiguiente != null) botonSiguiente.interactable = bosses.Count > 1;
    }

    private void ActualizarBotonCombatir(BossData jefe)
    {
        if (botonCombatir == null) return;

        // El botón Combatir solo se habilita si el jefe está desbloqueado
        botonCombatir.interactable = jefe.desbloqueado;
    }
}