using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>Nombre GENÉRICO del hijo visual del jefe añadido a la raqueta CPU.</summary>
    public const string NOMBRE_HIJO_JEFE_VISUAL = "JefeVisual_CPU";

    /// <summary>
    /// BossController preparado para la batalla actual (asignado por StartBossBattle
    /// ANTES de crear la partida). GameManager.DetenerJefeActivo() lo respeta y NO le
    /// aplica FinalizarCombate() durante la limpieza global, para que el combate del
    /// jefe recién seleccionado no se cancele antes de IniciarCombate().
    /// </summary>
    public BossController JefeActivo { get; set; }

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

        // Diagnóstico: mostrar qué jefe se está evaluando y qué rama tomará el código.
        Debug.Log("[BossManager] Evaluando jefe: " + jefe.nombre + " | ¿EsColossus?: " + EsJefeColossus(jefe));

        if (!jefe.desbloqueado)
        {
            Debug.LogWarning("[BossManager] El jefe '" + jefe.nombre + "' está bloqueado.");
            return;
        }

        // Buscar el MapManager de la escena (se usa la instancia singleton).
        if (MapManager.Instance == null)
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

        Debug.Log("Iniciando combate contra " + jefe.nombre);

        // 1. Seleccionar y activar el mapa del jefe ANTES de iniciar la partida.
        //    La responsabilidad de cargar/activar el mapa es 100% de MapManager,
        //    guiado por los datos del jefe (BossData.mapaAsociado / nombreMapa),
        //    lo que hace la carga de escenario automática y modular para cualquier jefe.
        Debug.Log($"[BossManager] Cargando mapa para {jefe.nombre}. Mapa asignado: {(jefe.mapaAsociado != null ? jefe.mapaAsociado.nombre : (string.IsNullOrEmpty(jefe.nombreMapa) ? "Ninguno (usando fallback)" : jefe.nombreMapa))}");
        MapManager.Instance.SeleccionarMapaBoss(jefe);
        Debug.Log("[BossManager] 2. Selección de mapa completada.");

        // Registrar qué jefe se va a activar en esta batalla ANTES de iniciar la
        // partida, para que GameManager.DetenerJefeActivo() (invocado al crear la
        // partida) NO le aplique FinalizarCombate() a este jefe.
        JefeActivo = ResolverControllerDelJefeActivo(jefe);

        // 2. Iniciar la partida PRIMERO (antes de habilitar el nuevo jefe).
        //    UIManager.IniciarPartida() llama a GameManager.IniciarPartida(), que
        //    ejecuta DetenerJefeActivo(): ahí se limpia de forma global cualquier
        //    combate de jefe ANTERIOR (Zeus, Colossus, etc.) y sus efectos.
        //    Gracias a JefeActivo, el jefe recién seleccionado se respeta en esa
        //    limpieza y no es cancelado antes de IniciarCombate().
        // Zeus y Colossus combaten SIEMPRE en dificultad Inhumana (perfil de IA
        // máximo: nunca falla, siempre golpea, incluso en modo épico/ultra).
        // El resto de jefes usa la dificultad definida en su BossData.
        Dificultad dificultadCombate = (EsJefeZeus(jefe) || EsJefeColossus(jefe))
            ? Dificultad.Inhumano
            : DificultadDelJefe(jefe.dificultad);
        UIManager.Instance.IniciarPartida(dificultadCombate);
        Debug.Log("[BossManager] 3. UIManager.IniciarPartida() llamado");

        // 3. Desactivar los scripts de los jefes que NO son el seleccionado.
        //    Evita que Zeus (u otro jefe) ejecute sus habilidades por error
        //    durante el combate de otro jefe. El jefe seleccionado se conserva.
        DesactivarOtrosJefes(jefe);

        // 4. Una vez que la partida fue creada por GameManager, activar el combate
        //    del jefe seleccionado usando su clase específica (o BossController genérico).
        if (EsJefeZeus(jefe))
        {
            BossZeus bossZeus = FindObjectOfType<BossZeus>();
            if (bossZeus != null)
            {
                bossZeus.enabled = true;
                bossZeus.IniciarCombate();

                // Aplicar el modelo visual del jefe a la raqueta CPU (método genérico).
                AplicarVisualJefeCPU(jefe, bossZeus);
            }
            else
            {
                Debug.LogWarning("[BossManager] No se encontró un BossZeus en la escena para iniciar el combate de Zeus.");
            }
        }
        else if (EsJefeColossus(jefe))
        {
            BossColossus colossusController = BuscarBossColossusEnEscena();

            if (colossusController != null)
            {
                colossusController.enabled = true;
                colossusController.IniciarCombate();

                // Aplicar el modelo visual del jefe a la raqueta CPU (método genérico).
                AplicarVisualJefeCPU(jefe, colossusController);
            }
            else
            {
                Debug.LogError("[BossManager] ERROR: No se encontró un BossColossus en la escena. Verifica que el GameObject del jefe Colossus exista y tenga asignado el script BossColossus.cs.");
            }
        }
        else
        {
            // Compatibilidad genérica: cualquier otro jefe derivado de BossController.
            BossController controller = BuscarControllerDelJefe(jefe);
            if (controller != null)
            {
                controller.enabled = true;
                controller.IniciarCombate();

                // Aplicar el modelo visual del jefe a la raqueta CPU (método genérico).
                AplicarVisualJefeCPU(jefe, controller);
            }
            else
            {
                Debug.LogWarning($"[BossManager] No se encontró en la escena un BossController para el jefe '{jefe.nombre}'.");
            }
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
            Destroy(instanciaZeus);
            Debug.LogWarning("[ZEUS VISUAL] La instancia Zeus no tiene Renderers válidos: se destruyó y se conserva la raqueta original.");
        }
    }

    // ──────────────────────────────────────────────
    // Asignación GENÉRICA de Skins para Jefes
    // ──────────────────────────────────────────────

    /// <summary>
    /// Limpieza COMPLETA de la skin visual del jefe en la raqueta CPU.
    /// Destruye el hijo JefeVisual_CPU (y ZeusVisual_CPU si existiera), des-suscribe
    /// el evento de regeneración, reactiva los Renderers de la raqueta base y restaura
    /// el estado normal. Invocar al iniciar partidas normales o al volver al menú.
    /// </summary>
    public void LimpiarVisualJefeCPU()
    {
        if (GameManager.Instance == null || GameManager.Instance.golpeRaquetaCPU == null) return;
        Transform raizCPU = GameManager.Instance.golpeRaquetaCPU.transform;
        RaquetaGolpe raquetaCPU = GameManager.Instance.golpeRaquetaCPU;

        // 1. Destruir el hijo visual genérico
        Transform hijoGen = raizCPU.Find(NOMBRE_HIJO_JEFE_VISUAL);
        if (hijoGen != null) Destroy(hijoGen.gameObject);

        // 2. Destruir el hijo visual específico de Zeus
        Transform hijoZeus = raizCPU.Find(NOMBRE_HIJO_ZEUS_VISUAL);
        if (hijoZeus != null) Destroy(hijoZeus.gameObject);

        // 3. Des-suscribir el evento de regeneración
        raquetaCPU.onRegenerada -= ReaplicarOcultadoJefe;

        // 4. Restaurar el flag de ocultado automático
        raquetaCPU.ocultarRendererBaseAlRegenerar = false;

        // 5. Reactivar TODOS los Renderer de la raqueta base de la CPU
        Renderer[] renderersBase = raquetaCPU.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer br in renderersBase)
        {
            if (br != null) br.enabled = true;
        }

        Debug.Log("[BossManager] Limpieza de skin de jefe completada: raqueta CPU restaurada a estado normal.");
    }

    /// <summary>
    /// Añade el modelo visual del jefe (bossController.jefe) como hijo de la raqueta CPU,
    /// aplicando su Transform exacto (posición, rotación y escala) según el jefe.
    /// </summary>
    private void AplicarVisualJefeCPU(BossData jefe, BossController bossController)
    {
        if (jefe == null || bossController == null || bossController.jefe == null) return;
        if (GameManager.Instance == null || GameManager.Instance.golpeRaquetaCPU == null) return;

        Transform raizCPU = GameManager.Instance.golpeRaquetaCPU.transform;

        // Limpiar cualquier skin previa instalada
        LimpiarVisualJefeCPU();

        // ── 1. Instanciar la Skin del Jefe como hijo de la raqueta CPU ──
        GameObject instanciaSkin = Instantiate(bossController.jefe.gameObject, raizCPU);
        instanciaSkin.name = NOMBRE_HIJO_JEFE_VISUAL;

        // ── 2. Aplicar Transform específico según el Jefe ──
        if (EsJefeZeus(jefe))
        {
            instanciaSkin.transform.localPosition = new Vector3(0f, 0.032f, -0.0134f);
            instanciaSkin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            instanciaSkin.transform.localScale = new Vector3(0.04985384f, 0.01177737f, 0.09542381f);
        }
        else if (EsJefeColossus(jefe))
        {
            instanciaSkin.transform.localPosition = Vector3.zero;
            instanciaSkin.transform.localRotation = Quaternion.identity;
            instanciaSkin.transform.localScale = new Vector3(1.271941f, 0.3898578f, 1.216918f);
        }
        else
        {
            // Valores fallback por defecto
            instanciaSkin.transform.localPosition = new Vector3(0f, 0.005f, -0.0207f);
            instanciaSkin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            instanciaSkin.transform.localScale = Vector3.one;
        }

        // ── 3. Validar renderers en el prefab instanciado ──
        Renderer[] renderersSkin = instanciaSkin.GetComponentsInChildren<Renderer>(true);

        bool skinTieneRenderers = false;
        foreach (Renderer r in renderersSkin)
        {
            if (r != null)
            {
                r.enabled = true;
                skinTieneRenderers = true;
            }
        }

        if (skinTieneRenderers)
        {
            // Ocultar mallas base de la raqueta original de la CPU
            Renderer[] renderersOriginales = GameManager.Instance.golpeRaquetaCPU.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer br in renderersOriginales)
            {
                if (br.transform.IsChildOf(instanciaSkin.transform)) continue;
                br.enabled = false;
            }

            // Suscribirse al evento de regeneración para mantener la skin en repeticiones/rondas
            SuscribirRegeneracionCPU();
            GameManager.Instance.golpeRaquetaCPU.ocultarRendererBaseAlRegenerar = true;

            Debug.Log($"[BossManager] Skin visual de '{jefe.nombre}' aplicada correctamente a la raqueta CPU.");
        }
        else
        {
            Destroy(instanciaSkin);
            Debug.LogWarning($"[BossManager] El prefab visual para '{jefe.nombre}' no contiene Renderers válidos.");
        }
    }

    /// <summary>
    /// Handler GENÉRICO del evento onRegenerada de la raqueta CPU: tras una regeneración
    /// re-oculta los renderers de la raqueta base y mantiene únicamente el visual del
    /// jefe activo (JefeVisual_CPU o ZeusVisual_CPU), siempre que el combate siga activo.
    /// </summary>
    private void ReaplicarOcultadoJefe()
    {
        if (GameManager.Instance == null || GameManager.Instance.golpeRaquetaCPU == null) return;
        Transform raizCPU = GameManager.Instance.golpeRaquetaCPU.transform;

        // Buscar el visual del jefe activo (nombre genérico o específico de Zeus)
        Transform jefeVisual = raizCPU.Find(NOMBRE_HIJO_JEFE_VISUAL);
        if (jefeVisual == null) jefeVisual = raizCPU.Find(NOMBRE_HIJO_ZEUS_VISUAL);
        if (jefeVisual == null) return;

        Renderer[] renderersBase = raizCPU.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderersBase.Length; i++)
        {
            Renderer br = renderersBase[i];
            if (br == null) continue;
            if (br.transform.IsChildOf(jefeVisual)) continue;
            br.enabled = false;
        }
    }

    /// <summary>Devuelve true si el jefe seleccionado es Zeus (por nombre).</summary>
    private bool EsJefeZeus(BossData jefe) =>
        jefe != null && !string.IsNullOrWhiteSpace(jefe.nombre) &&
        jefe.nombre.ToLowerInvariant() == "zeus";

    /// <summary>Devuelve true si el jefe seleccionado es Colossus (por nombre),
    /// ignorando mayúsculas/minúsculas y espacios adicionales.</summary>
    private bool EsJefeColossus(BossData jefe) =>
        jefe != null &&
        !string.IsNullOrWhiteSpace(jefe.nombre) &&
        jefe.nombre.Trim().ToLowerInvariant().Contains("colossus");

    // ──────────────────────────────────────────────
    /// <summary>
    /// Desactiva los scripts de TODOS los jefes que NO correspondan al jefe
    /// seleccionado, finalizando su combate, cancelando corrutinas y deshabilitando
    /// el componente para que no ejecuten sus habilidades por error.
    /// Garantiza que solo el jefe activo tenga combateActivo = true.
    /// </summary>
    private void DesactivarOtrosJefes(BossData jefeSeleccionado)
    {
        if (jefeSeleccionado == null) return;

        bool esZeus      = EsJefeZeus(jefeSeleccionado);
        bool esColossus  = EsJefeColossus(jefeSeleccionado);

        // ── BossZeus ──
        BossZeus[] zeusEnEscena = FindObjectsOfType<BossZeus>(true);
        foreach (BossZeus z in zeusEnEscena)
        {
            if (z == null) continue;

            // Si el jefe seleccionado ES Zeus, mantenerlo habilitado.
            if (esZeus)
            {
                z.enabled = true;
                continue;
            }

            // Cualquier otro caso: finalizar su combate y desactivar su script.
            z.FinalizarCombate();     // combateActivo = false
            z.StopAllCoroutines();    // cancelar rayo/teletransporte pendiente
            z.DesactivarHabilidad();  // limpiar habilidad activa
            z.enabled = false;

            Debug.Log($"[BossManager] Script de BossZeus ('{z.gameObject.name}') desactivado. Jefe activo: '{jefeSeleccionado.nombre}'.");
        }

        // ── BossColossus ──
        BossColossus[] colossusEnEscena = FindObjectsOfType<BossColossus>(true);
        foreach (BossColossus c in colossusEnEscena)
        {
            if (c == null) continue;

            // Si el jefe seleccionado ES Colossus, mantenerlo habilitado.
            if (esColossus)
            {
                c.enabled = true;
                continue;
            }

            // Cualquier otro caso: finalizar su combate y desactivar su script.
            c.FinalizarCombate();     // combateActivo = false
            c.StopAllCoroutines();    // cancelar Modo Fortaleza pendiente
            c.DesactivarHabilidad();  // limpiar habilidad activa
            c.enabled = false;

            Debug.Log($"[BossManager] Script de BossColossus ('{c.gameObject.name}') desactivado. Jefe activo: '{jefeSeleccionado.nombre}'.");
        }

        // ── Compatibilidad genérica: cualquier otro BossController ──
        // Desactivar los demás jefes que deriven de BossController pero no
        // correspondan al jefe seleccionado.
        BossController[] controllersEnEscena = FindObjectsOfType<BossController>(true);
        foreach (BossController bc in controllersEnEscena)
        {
            if (bc == null) continue;

            // Los ya gestionados por su clase específica se omiten.
            if (bc is BossZeus || bc is BossColossus) continue;

            // Determinar si este BossController corresponde al jefe seleccionado.
            bool coincide = false;
            if (bc.bossData != null && !string.IsNullOrWhiteSpace(jefeSeleccionado.nombre))
                coincide = string.Equals(
                    bc.bossData.nombre, jefeSeleccionado.nombre,
                    System.StringComparison.OrdinalIgnoreCase
                );
            if (!coincide && !string.IsNullOrWhiteSpace(bc.gameObject.name))
                coincide = string.Equals(
                    bc.gameObject.name, jefeSeleccionado.nombre,
                    System.StringComparison.OrdinalIgnoreCase
                );

            if (coincide)
            {
                bc.enabled = true;
                continue;
            }

            bc.FinalizarCombate();
            bc.StopAllCoroutines();
            bc.DesactivarHabilidad();
            bc.enabled = false;

            Debug.Log($"[BossManager] Script de jefe '{bc.gameObject.name}' desactivado. Jefe activo: '{jefeSeleccionado.nombre}'.");
        }
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Busca en la escena un BossController genérico que corresponda al jefe
    /// seleccionado (por nombre de BossData o por nombre del GameObject),
    /// excluyendo Zeus y Colossus que se gestionan por su clase específica.
    /// </summary>
    private BossController BuscarControllerDelJefe(BossData jefe)
    {
        if (jefe == null) return null;

        BossController[] todos = FindObjectsOfType<BossController>(true);
        foreach (BossController bc in todos)
        {
            if (bc == null) continue;
            if (bc is BossZeus || bc is BossColossus) continue;

            if (bc.bossData != null && !string.IsNullOrWhiteSpace(bc.bossData.nombre) &&
                !string.IsNullOrWhiteSpace(jefe.nombre) &&
                string.Equals(bc.bossData.nombre, jefe.nombre, System.StringComparison.OrdinalIgnoreCase))
                return bc;

            if (!string.IsNullOrWhiteSpace(bc.gameObject.name) &&
                !string.IsNullOrWhiteSpace(jefe.nombre) &&
                string.Equals(bc.gameObject.name, jefe.nombre, System.StringComparison.OrdinalIgnoreCase))
                return bc;
        }

        return null;
    }
// ──────────────────────────────────────────────
    /// <summary>
    /// Resuelve el BossController concreto del jefe seleccionado para la batalla
    /// actual (BossZeus, BossColossus o un BossController genérico).
    /// Devuelve null si el jefe no se encuentra en la escena.
    /// </summary>
    private BossController ResolverControllerDelJefeActivo(BossData jefe)
    {
        if (jefe == null) return null;

        if (EsJefeZeus(jefe))
        {
            BossZeus zeus = FindObjectOfType<BossZeus>();
            if (zeus != null) return zeus;
        }
        else if (EsJefeColossus(jefe))
        {
            // Incluye objetos/instancias deshabilitados en la escena.
            BossColossus colossus = BuscarBossColossusEnEscena();
            if (colossus != null) return colossus;
        }
        else
        {
            BossController generico = BuscarControllerDelJefe(jefe);
            if (generico != null) return generico;
        }

        return null;
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Busca el controlador de Colossus INCLUYENDO objetos e instancias
    /// deshabilitadas en la escena. El script del jefe puede estar apagado
    /// (p. ej. tras DesactivarOtrosJefes()) y aun así debe encontrarse.
    /// </summary>
    private BossColossus BuscarBossColossusEnEscena()
    {
        // Resources.FindObjectsOfTypeAll incluye assets/prefabs y objetos
        // inactivos de la escena; se filtra solo a los que pertenecen a una
        // escena cargada (acotado con b.gameObject.scene.isLoaded).
        BossColossus colossusController = Resources.FindObjectsOfTypeAll<BossColossus>()
            .FirstOrDefault(b => b.gameObject.scene.isLoaded);

        // Fallback: buscar en los hijos de este BossManager (incluye inactivos).
        if (colossusController == null)
        {
            colossusController = GetComponentInChildren<BossColossus>(true);
        }

        return colossusController;
    }

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
        raquetaCPU.onRegenerada -= ReaplicarOcultadoJefe;
        raquetaCPU.onRegenerada += ReaplicarOcultadoJefe;
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

        // El color de tema se aplica SOLO al indicador del jefe seleccionado.
        int indice = currentBossIndex;
        if (indice < 0 || indice >= indicadores.Length) return;

        if (indicadores[indice] != null)
        {
            Color tema = jefe.colorTema;
            tema.a = 1f; // El indicador activo SIEMPRE con opacidad plena.
            indicadores[indice].color = tema;
        }
    }

    private void ActualizarIndicadores()
    {
        if (indicadores == null) return;

        for (int i = 0; i < indicadores.Length; i++)
        {
            if (indicadores[i] == null) continue;

            // Mostrar solo los indicadores que tienen jefe asignado
            bool tieneJefe = i < bosses.Count;
            indicadores[i].gameObject.SetActive(tieneJefe);
            if (!tieneJefe) continue;

            if (i == currentBossIndex)
            {
                // Indicador ACTIVO: opacidad 1.0
                Color activo = indicadores[i].color;
                activo.a = 1f;
                indicadores[i].color = activo;
            }
            else
            {
                // Indicadores INACTIVOS: opacidad reducida (0.3)
                Color neutro = indicadores[i].color;
                neutro.a = 0.3f;
                indicadores[i].color = neutro;
            }
        }

        // Aplicar el color de tema únicamente al indicador seleccionado
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