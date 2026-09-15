using UnityEngine;
using System.Collections;

/// <summary>
/// Jefe Colossus: fortaleza y resistencia.
///
/// Mecánica principal - Absorción e Impacto Pesado:
/// Cuando la pelota golpea su raqueta a alta velocidad (Modo Épico/Ultra),
/// Colossus absorbe el impacto reduciendo el riesgo de destrucción de su raqueta.
/// La tolerancia a golpes épicos crece con cada fase (1 en Fase 1 ... 11 en Fase 11).
///
/// Habilidad activa - Modo Fortaleza:
/// Entra en 'Modo Fortaleza' por un tiempo determinado (duracionFortaleza):
///   - Incrementa visualmente la escala de su raqueta para abarcar más espacio.
///   - Devuelve la pelota con un impulso extra (multiplicadorFuerzaGolpe).
///
/// Sistema de Fases:
/// Fases 1 a 11. La fase avanza cuando Colossus anota un punto o recibe
/// golpes rápidos de alta velocidad. Cada fase aumenta en 1 la cantidad de
/// rebotes épicos que aguanta antes de que su raqueta pueda ser destruida.
/// </summary>
public class BossColossus : BossController
{
    /// <summary>Instancia única referenciada por PingPongBall y CPUControl para notificar golpes.</summary>
    public static BossColossus Instance { get; private set; }

    private void Awake()
    {
        Debug.Log($"[BossColossus Debug] Awake ejecutado. Objeto activo: {gameObject.activeInHierarchy}");

        // Resolver la raqueta lo antes posible para que GuardarEscalaOriginal() y
        // toda la lógica de escala/rotación tengan referencia válida en Start().
        ResolverReferenciaRaqueta();

        Instance = this;

        // NOTA: la restricción de modo (!EsModoBatallaDeJefe) se evalúa en Start(),
        // NO en Awake: BossManager.JefeActivo aún no está asignado durante Awake,
        // por lo que la comprobación prematura deshabilitaba la skin también en
        // el modo Batalla de Jefe. Start() (y Update como respaldo) garantizan
        // que la skin colosus se oculte únicamente en modos sin jefe activo.
    }

    // ──────────────────────────────────────────────
    // Configuración: Modo Fortaleza
    // ──────────────────────────────────────────────
    [Header("Colossus - Modo Fortaleza")]
    [Tooltip("Duración en segundos del Modo Fortaleza al activarse.")]
    public float duracionFortaleza = 4f;

    [Tooltip("Multiplicador aplicado a la velocidad de la pelota al devolverla durante Modo Fortaleza.")]
    public float multiplicadorFuerzaGolpe = 1.6f;

    [Tooltip("Multiplicador de escala aplicado a la raqueta durante Modo Fortaleza (X=ancho, Y=alto, Z=largo). Se multiplica sobre la escala original, nunca la reemplaza.")]
    public Vector3 escalaFortaleza = new Vector3(1.35f, 1f, 1.7f);

    [Tooltip("Velocidad mínima de la pelota para considerar un golpe como 'épico' (Modo Épico/Ultra).")]
    public float umbralVelocidadEpica = 110f;

    [Tooltip("Umbral de velocidad ALTA (Shockwave) desde el cual se aplica la mecánica de durabilidad de Colossus: por debajo de este valor la raqueta del jefe SIEMPRE resiste sin gastar cargas de inmunidad ni destruirse. A partir de él, cada impacto consume UNA carga (rebotesResistenciaRestantes) hasta agotarse y romper. 100f es el límite clásico de la velocidad de shockwave.")]
    public float umbralVelocidadShockwave = 100f;

    // ──────────────────────────────────────────────
    // Configuración: Fases
    // ──────────────────────────────────────────────
    [Header("Colossus - Fases")]
    [Tooltip("Fase actual (1 a 11). Cada fase aumenta la tolerancia a golpes épicos en 1.")]
    [Range(1, 11)]
    public int faseActual = 1;

    [Tooltip("Si es true, la fase también avanza automáticamente al absorber el máximo de golpes épicos de la fase actual.")]
    public bool avanzarFasePorGolpesMaximos = true;

    // ──────────────────────────────────────────────
    // Efectos visuales y de audio
    // ──────────────────────────────────────────────
    [Header("Colossus - Efectos")]
    [Tooltip("Prefab de partículas que se instancia al activar Modo Fortaleza y al absorber un golpe épico.")]
    public GameObject prefabParticulasFortaleza;

    [Tooltip("Efecto de partículas YA COLOCADO en la escena que reproduce el bucle de inmunidad (ParticleSystem.Play) al absorber un impacto a alta velocidad. Opcional: si se deja vacío, solo se instancia prefabParticulasFortaleza.")]
    [SerializeField] private ParticleSystem efectoInmunidad;

    [Tooltip("Sonido reproducido al activar Modo Fortaleza.")]
    public AudioClip sonidoFortaleza;

    [Tooltip("Sonido reproducido al absorber un golpe épico.")]
    public AudioClip sonidoGolpeAbsorbido;

    [Tooltip("Sonido reproducido al avanzar de fase.")]
    public AudioClip sonidoCambioFase;

    // ──────────────────────────────────────────────
    // Efectos de Audio - Modo Épico y Coloso
    // ──────────────────────────────────────────────
    [Header("Efectos de Audio - Modo Épico y Coloso")]
    [Tooltip("AudioSource dedicado a los efectos de sonido del Modo Épico. Si se deja vacío, usa el AudioSource del propio objeto (asignado automáticamente en Start).")]
    [SerializeField] private AudioSource audioSourceColossus;

    [Tooltip("Sonido reproducido al ABSORBER un impacto a alta velocidad (>= umbralVelocidadShockwave) consumiendo una carga de resistencia.")]
    [SerializeField] private AudioClip sonidoAbsorcionFortaleza;

    [Tooltip("Sonido reproducido al AGOTARSE las cargas de resistencia: la raqueta de Colossus se rompe en modo épico (punto para el jugador).")]
    [SerializeField] private AudioClip sonidoRupturaRaqueta;

    [Tooltip("Sonido reproducido al CAMBIAR DE FASE / CRECIMIENTO COLOSAL al recibir un punto del jugador.")]
    [SerializeField] private AudioClip sonidoCambioDeFase;

    // ──────────────────────────────────────────────
    // Skin de Colossus (inyección dinámica)
    // ──────────────────────────────────────────────
    [Header("Skin de Colossus (Inyección Dinámica)")]
    [Tooltip("Prefab de la skin de Blender del Colosus. Se instancia DINÁMICAMENTE dentro de SkinRoot al iniciar la batalla de jefe. Dejar vacío si la raqueta base ya tiene el modelo colosus como hijo.")]
    public GameObject prefabSkinColossus;
    private GameObject instanciaSkinActual;

    // ──────────────────────────────────────────────
    // Onda de Choque — habilidad especial
    // ──────────────────────────────────────────────
    [Header("Colossus - Onda de Choque")]
    [Tooltip("Prefab del efecto/tremor de onda de choque que se instancia en la mesa al disparar la habilidad.")]
    public GameObject prefabOndaDeChoque;

    [Tooltip("Sonido reproducido al disparar la onda de choque.")]
    public AudioClip sonidoOndaDeChoque;

    [Tooltip("Tiempo de recarga (cooldown) entre ondas de choque, en segundos.")]
    public float cooldownOndaDeChoque = 3f;

    [Tooltip("Multiplicador de velocidad aplicado a la pelota al ser impulsada por la onda de choque.")]
    public float multiplicadorImpulsoOnda = 1.5f;

    [Tooltip("Altura vertical (Y) a la que se instancia el efecto de la onda respecto a la superficie de la mesa.")]
    public float alturaEfectoOnda = 0.1f;

    [Tooltip("Rotación del efecto de la onda (Euler). Usar (90,0,0) si la dona/torus viene en vertical.")]
    public Vector3 rotacionEfectoOnda = new Vector3(0f, 0f, 0f);

    [Tooltip("Duración antes de auto-destruir el efecto de la onda instanciado (si el prefab no se destruye solo).")]
    public float duracionEfectoOnda = 2f;

    // ──────────────────────────────────────────────
    // Configuración: Crecimiento progresivo e inmunidad
    // ──────────────────────────────────────────────
    [Header("Colossus - Crecimiento y Resistencia")]
    [Tooltip("Factor de crecimiento de la escala de la raqueta por cada punto disputado (puntos totales). 0.15 = +15% por punto (~x2 al llegar a 6-7 puntos).")]
    public float crecimientoPorPunto = 0.15f;

    [Tooltip("Escala BASE (mínima/absoluta) del transform de la raqueta al empezar el combate (p. ej. 73.9, 23.5, 313.7).")]
    public Vector3 escalaInicialRaqueta = new Vector3(73.9f, 23.5f, 313.7f);

    [Tooltip("Escala MÁXIMA (colosal/absoluta) del transform de la raqueta, alcanzada al llegar a puntosParaEscalaMaxima puntos jugados (p. ej. 507.8, 155.5, 2071.0).")]
    public Vector3 escalaMaximaRaqueta = new Vector3(507.8f, 155.5f, 2071.0f);

    [Tooltip("Puntos jugados totales requeridos para alcanzar la escala gigante (interpolación lerp).")]
    public int puntosParaEscalaMaxima = 7;

    [Tooltip("Máxima fracción del ancho real de la mesa que la raqueta puede cubrir (0.85-0.95 recomendado).")]
    [Range(0.5f, 1f)]
    public float maxFraccionAnchoMesa = 0.9f;

    [Tooltip("Si es true, la inmunidad se rearma cuando el jugador le anota un punto a Colossus (resistencia restante = puntos del JUGADOR).")]
    public bool reiniciarInmunidadPorPunto = true;

    // ──────────────────────────────────────────────
    // Configuración: Dificultad Inhumana (modo épico)
    // ──────────────────────────────────────────────
    [Header("Colossus - Dificultad Inhumana (modo épico)")]
    [Tooltip("Si es true, al iniciar el combate se FUERZA la dificultad del CPU a Inhumano (CPUControl.SetDificultad(Dificultad.Inhumano)) y se persiste en GameManager.dificultadSeleccionada, de modo que el bot reacciona perfecto, nunca falla y siempre golpea incluso en modo épico/ultra.")]
    public bool forzarDificultadInhumano = true;

    [Tooltip("Multiplicador de resistencia por puntos en dificultad Inhumano: la inmunidad rearmada = cargasBaseResistenciaInhumano + (puntosTotales * multiplicador). Con 2.0 y 5 puntos disputados la raqueta tolera 1 + 10 = 11 golpes épicos antes de romper.")]
    public float multiplicadorResistenciaInhumano = 2f;

    [Tooltip("Cargas base de resistencia que se suman al cálculo Inhumano (para que NUNCA rompa con un único impacto épico, incluso con 0 puntos).")]
    public int cargasBaseResistenciaInhumano = 1;

    // Indica que la dificultad Inhumana ya fue aplicada en este combate (solo diagnóstico/log).
    private bool dificultadInhumanoForzada = false;

    // ──────────────────────────────────────────────
    // Configuración: Collider independiente y centrado
    // ──────────────────────────────────────────────
    [Header("Colossus - Collider y Posición")]
    [Tooltip("Transform del collider independiente de la raqueta (p. ej. 'Collider_Raqueta (2)' con ColliderSeguidor). Se escala proporcionalmente al crecimiento de la raqueta. Si no se asigna, se busca automáticamente un ColliderSeguidor cuyo objetivo sea la raqueta.")]
    public Transform transformColliderRaqueta;

    [Tooltip("Puntos del JUGADOR anotados contra Colossus a partir de los cuales la raqueta se fija al centro de la mesa (bloqueo/restricción del movimiento lateral). Rango sugerido 5-7.")]
    public int puntosParaCentrar = 6;

    [Tooltip("Fracción del ancho de mesa que, al ser superada por el ancho de la raqueta, activa el centrado/bloqueo lateral (0.8 = 80%).")]
    [Range(0.2f, 1f)]
    public float fraccionAnchoParaCentrar = 0.8f;

    [Tooltip("Eje lateral real de la raqueta: false = Z (movimiento por destinoZ de CPUControl), true = X.")]
    public bool ejeLateralEsX = false;

    // ──────────────────────────────────────────────
    // Malla visual — activación del modelo de Blender
    // ──────────────────────────────────────────────
    [Header("Colossus - Malla Visual")]
    [Tooltip("Si es true, al resolver la malla visual (inicio de partida y regeneración) se ACTIVAN los hijos del modelo 3D de la raqueta del Coloso: activa el GameObject y habilita el Renderer de la malla resuelta, y recorre todos los MeshRenderer/SkinnedMeshRenderer hijos (incluidos los que vienen DESACTIVADOS desde Blender) para que la malla nueva con sus accesorios se muestre completa.")]
    public bool activarAccesoriosMalla = true;

    [Tooltip("Rotación LOCAL que debe tener el modelo visual 'colosus' DENTRO de SkinRoot para verse erguido (p. ej. Euler(0, -180, 180)). Se aplica sobre el modelo visual en cada resolución/restauración SI la rotación capturada del Editor es identidad (no configurada).")]
    public Quaternion rotacionVisualFallback = Quaternion.Euler(0f, -180f, 180f);

    [Tooltip("Escala LOCAL base que debe tener el modelo visual 'colosus' DENTRO de SkinRoot (p. ej. -1.081282, -1.392614, -1.022877). Se aplica si la escala capturada es cero o inválida, para que la raqueta no se aplane ni desaparezca.")]
    public Vector3 escalaVisualFallback = new Vector3(-1.081282f, -1.392614f, -1.022877f);

    // ──────────────────────────────────────────────
    // Estado interno
    // ──────────────────────────────────────────────
    private enum EstadoColossus
    {
        Normal,
        Fortaleza
    }

    private EstadoColossus estado = EstadoColossus.Normal;
    private bool modoVerificado = false; // true una vez evaluado si el modo actual es Batalla de Jefe

    private AudioSource audioSource;
    private Transform raquetaTransform;
    private Vector3 escalaOriginalRaqueta;
    private Quaternion rotacionInicialRaqueta; // rotación LOCAL exacta del Inspector al iniciar
    private bool escalaOriginalGuardada = false;
    private float tiempoFinFortaleza;
    private int golpesEpicosAbsorbidosEnFase;

    // ── Malla visual de la raqueta y Animator ──
    // La malla 3D (MeshRenderer) puede vivir en un hijo del transform principal.
    // Se cachea aquí para escalarla explícitamente y garantizar el cambio visual.
    private Transform raquetaVisualTransform;      // transform que contiene la malla 3D (modelo colosus)
    private Vector3 escalaOriginalVisual;          // localScale original de la malla/hijo visual
    private Quaternion rotacionInicialVisual;      // localRotation del MODELO VISUAL (colosus) capturada del Inspector
    private bool rotacionVisualGuardada = false;
    private Animator raquetaAnimator;              // Animator de la raqueta (puede pisar m_LocalScale)
    private bool raquetaVisualResuelto = false;
    private MeshRenderer rendererMallaDefaultOculto; // MeshRenderer de la RAÍZ (raqueta default) oculto de forma persistente

    // ── Estados de la Onda de Choque ──
    private enum EstadoOndaDeChoque
    {
        Listo,
        Recargando
    }

    private EstadoOndaDeChoque estadoOnda = EstadoOndaDeChoque.Listo;
    private GameObject efectoOndaActual;

    // ── Sistema de crecimiento e inmunidad ──
    private bool escalaBaseCalculada = false;
    private Vector3 escalaBaseAcumulada; // escala base con crecimiento por puntaje
    private int rebotesResistenciaRestantes; // N = puntos del JUGADOR anotados contra Colossus

    // Evita el DOBLE consumo de una carga en el MISMO impacto épico: el override
    // PuedeResistir (PingPongBall) y RegistrarGolpeEnRaqueta (evento de golpe CPU)
    // se disparan en el mismo frame; si PuedeResistir consumió, RegistrarGolpeEnRaqueta
    // debe saltar su resta y solo limpiar la bandera.
    private bool golpeAbsorbidoEnEsteImpacto = false;
    private int ultimoPuntajeJugadorObservado = -1; // para detectar cambios en los puntos del JUGADOR

    // ── Collider independiente y centrado ──
    private Vector3 escalaOriginalCollider;      // escala base del collider separado
    private bool escalaOriginalColliderGuardada = false;
    private bool raquetaCentrada = false;        // true cuando la raqueta está fija en el centro
    private bool centroMesaDisponible = false;   // true si se calculó el centro de la mesa
    private Vector3 centroMesaGuardado = Vector3.zero;

    // Rango absoluto del collider independiente (para Vector3.Lerp).
    // Se autocalcula (por componente) respecto al rango de la raqueta cuando no
    // se asigna en el Inspector a través de transformColliderRaqueta.
    private Vector3 escalaInicialCollider = Vector3.one;
    private Vector3 escalaMaximaCollider   = Vector3.one;

    /// <summary>True si la raqueta está fijada al centro de la mesa (bloqueo lateral activo).</summary>
    public bool RaquetaCentrada => raquetaCentrada;

    /// <summary>true mientras la inmunidad por rebotes esté activa (resistencia > 0).</summary>
    public bool InmunidadActiva => rebotesResistenciaRestantes > 0;

    /// <summary>true cuando el Modo Fortaleza está activo. Otros sistemas pueden consultarlo para aplicar el impulso extra.</summary>
    public bool ModoFortalezaActivo => estado == EstadoColossus.Fortaleza;

    // ──────────────────────────────────────────────
    void Start()
    {
        // ── RESTRICCIÓN DE MODO: BossColossus SOLO en Batalla de Jefe ──
        // En CPU vs CPU / Torneo / otros modos NO se llama StartBossBattle(), por lo
        // que BossManager.JefeActivo queda null: este script se desactiva para que el
        // jefe no aparezca ni ejecute sus efectos globalmente. (combateActivo actúa
        // como red de seguridad por si JefeActivo aún no está resuelto en este frame.)
        if (!EsModoBatallaDeJefe())
        {
            Debug.Log("[BossColossus] Modo sin batalla de jefe (CPU vs CPU / Torneo / 2 Jugadores). Script desactivado para evitar la aparición del jefe.");

            // Limpieza gráfica completa: ocultar la skin de Blender (SkinRoot/colosus)
            // y reactivar la malla NORMAL de la raqueta CPU. Deshabilitar el script no
            // basta: los objetos 3D ya activados seguirían renderizándose encima.
            DesactivarVisualesJefeParaModoNormal();

            // Apagar explícitamente cualquier SkinRoot que persista en el objeto
            // (doble seguridad por si DesactivarVisualesJefeParaModoNormal no lo cubrió).
            Transform skinRoot = BuscarSkinRoot(raquetaTransform != null ? raquetaTransform : transform);
            if (skinRoot != null)
            {
                skinRoot.gameObject.SetActive(false);
                Debug.Log($"[BossColossus] SkinRoot desactivado explícitamente para modo normal: {skinRoot.name}");
            }

            this.enabled = false;
            return;
        }

        Debug.Log($"[BossColossus Debug] Start ejecutado. Raqueta asignada: {(raquetaTransform != null ? raquetaTransform.name : "NULA")}");

        // Asegurar la referencia a la raqueta (re-intento en Start por si Awake
        // corrió antes de que la escena resolviera los hijos del modelo).
        ResolverReferenciaRaqueta();

        audioSource = GetComponent<AudioSource>();

        // AudioSource dedicado del Modo Épico: si no se asignó en el Inspector,
        // reutilizar el AudioSource del propio objeto (el de los demás sonidos).
        if (audioSourceColossus == null)
            audioSourceColossus = audioSource;

        // Buscar la raqueta de la CPU y guardar su escala base.
        GuardarEscalaOriginal();
        // Guardar la referencia y escala del collider independiente (ColliderSeguidor).
        GuardarColliderOriginal();

        // ── DIAGNÓSTICO + VISIBILIDAD FORZADA: activar el GameObject de la raqueta
        //    y los renderers de la skin (la malla de Blender puede llegar inactiva).
        //    OJO: el renderer de la RAÍZ (raqueta default) NO se reactiva; se oculta
        //    de forma persistente para que solo se vea la skin de Blender (SkinRoot). ──
        if (raquetaTransform != null)
        {
            raquetaTransform.gameObject.SetActive(true);

            Renderer[] renderers = raquetaTransform.GetComponentsInChildren<Renderer>(true);
            Debug.Log($"[BossColossus Debug] Renderers encontrados en raqueta: {renderers.Length}");

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;

                // No reactivar el renderer de la raíz (raqueta default): se oculta siempre.
                if (r.transform == raquetaTransform) continue;

                r.enabled = true;
                r.gameObject.SetActive(true);
                Debug.Log($"[BossColossus Debug] Renderer activado: {r.gameObject.name} en layer {r.gameObject.layer}");
            }

            // Re-ocultar la malla default de la raíz por si quedó encendida de antes.
            DesactivarMallaDefault();
        }
    }

    void Update()
    {
        // Respaldo de la restricción de modo (una sola vez): si Start() corrió antes
        // de que BossManager resolviera JefeActivo, este Update re-evalúa y desactiva
        // el script si se confirma que NO hay batalla de jefe en curso.
        if (!modoVerificado)
        {
            modoVerificado = true;
            if (!EsModoBatallaDeJefe())
            {
                Debug.Log("[BossColossus] Modo sin batalla de jefe confirmado. Script desactivado.");

                // Limpieza gráfica completa (ver explicación en Start).
                DesactivarVisualesJefeParaModoNormal();

                this.enabled = false;
                return;
            }
        }

        // Desactivación automática del Modo Fortaleza al cumplirse el tiempo.
        if (estado == EstadoColossus.Fortaleza && Time.time >= tiempoFinFortaleza)
        {
            DesactivarModoFortaleza();
        }

        // Seguimiento de los puntos del JUGADOR (los que alimentan el crecimiento):
        // si cambió (el jugador logró anotarle un punto a Colossus), reaplicar la
        // escala base acumulada (crecimiento permanente) y re-evaluar el centrado
        // (bloqueo lateral) por tamaño de raqueta.
        // La inmunidad/resistencia NO se rearma aquí: solo por eventos
        // (IniciarCombate, OnPuntoDelJugador, ReaplicarEstadoColossusTrasRegeneracion),
        // para que las cargas permanezcan estables durante la jugada (decreciendo
        // con cada golpe épico hasta agotarse y romper la raqueta).
        if (combateActivo)
        {
            int puntajeActual = ObtenerPuntosValidosParaCrecimiento();
            if (puntajeActual != ultimoPuntajeJugadorObservado)
            {
                ultimoPuntajeJugadorObservado = puntajeActual;
                RecalcularEscalaBasePorPuntaje();
                EvaluarCentradoRaqueta();
                Debug.Log($"[BossColossus] Puntos del jugador detectados: {puntajeActual}. Escala y centrado reajustados (inmunidad estable).");
            }

            // Restricción del movimiento lateral: si la raqueta está centrada
            // (cubre casi toda la mesa), forzar su posición lateral al centro
            // CADA FRAME para bloquear el desplazamiento de la CPU.
            if (raquetaCentrada && raquetaTransform != null)
            {
                Vector3 pos = raquetaTransform.position;
                if (ejeLateralEsX)
                    pos.x = centroMesaGuardado.x;
                else
                    pos.z = centroMesaGuardado.z;
                raquetaTransform.position = pos;
            }
        }
    }

    // ──────────────────────────────────────────────
    // BossController — overrides
    // ──────────────────────────────────────────────

    public override void IniciarCombate()
    {
        base.IniciarCombate();

        faseActual = 1;
        golpesEpicosAbsorbidosEnFase = 0;
        estado = EstadoColossus.Normal;

        // Colossus SIEMPRE combate en la dificultad máxima: forzar el perfil de
        // IA del CPU (CPUControl) a Inhumano y persistir
        // GameManager.dificultadSeleccionada para que ResetRound/RestartGame no
        // la rebajen durante el combate.
        ForzarDificultadInhumano();

        // Escala base acumulada según el puntaje actual de la CPU y rearmar
        // la inmunidad (resistencia restante = N rebotes = puntaje actual).
        RecalcularEscalaBasePorPuntaje();
        ReiniciarInmunidad();
        EvaluarCentradoRaqueta();

        // Suscribir el handler que reaplica la escala colosal y la resistencia
        // por puntos cuando la raqueta del CPU se regenera tras ser destruida
        // por un golpe épico (mismo patrón idempotente que en BossManager).
        SuscribirRegeneracionRaquetaCPU();

        Debug.Log("[BossColossus] Combate iniciado. Fase 1 — tolerancia a golpes épicos: 1.");
    }

    public override void FinalizarCombate()
    {
        // Cancelar corrutinas activas (p. ej. Modo Fortaleza o la recarga de la
        // Onda de Choque) de forma limpia. Se hace PRIMERO para que una corrutina
        // a medio terminar no vuelva a modificar la escala o el estado después.
        DetenerCorrutinasFortaleza();

        // Desuscribir el handler de regeneración: la raqueta puede regenerarse
        // durante el combate, pero tras finalizar no debe volver a modificar
        // la escala/inmunidad de Colossus.
        DesuscribirRegeneracionRaquetaCPU();

        // Limpiar cualquier efecto residual de la Onda de Choque instanciado.
        LimpiarEfectoOndaActual();
        estadoOnda = EstadoOndaDeChoque.Listo;

        // Restaurar la escala original de la raqueta (null-safe: no hace nada
        // si no hay raqueta resuelta o el combate se interrumpió antes).
        RestaurarEscalaRaqueta();

        // Reiniciar sistemas de crecimiento e inmunidad para la próxima batalla.
        escalaBaseCalculada = false;
        escalaBaseAcumulada = Vector3.zero;
        rebotesResistenciaRestantes = 0;
        ultimoPuntajeJugadorObservado = -1;

        // Limpiar el estado de centrado/bloqueo lateral y el collider.
        raquetaCentrada = false;
        centroMesaDisponible = false;

        base.FinalizarCombate();

        // Reiniciar el sistema de fases para la próxima batalla.
        faseActual = 1;
        golpesEpicosAbsorbidosEnFase = 0;

        Debug.Log("[BossColossus] Combate finalizado.");
    }

    public override void ActivarHabilidad()
    {
        // La habilidad especial se dispara como Onda de Choque (con cooldown).
        // También puede activarse manualmente desde BossController/BossManager.
        DispararOndaDeChoque();
    }

    public override void DesactivarHabilidad()
    {
        // Detener corrutinas (recarga de onda / Modo Fortaleza) de forma limpia
        // y eliminar cualquier efecto residual instanciado.
        DetenerCorrutinasFortaleza();
        LimpiarEfectoOndaActual();
        estadoOnda = EstadoOndaDeChoque.Listo;
    }

    // ──────────────────────────────────────────────
    // Sistema de Fases
    // ──────────────────────────────────────────────

    public void AvanzarFase()
    {
        if (faseActual >= 11)
        {
            Debug.Log("[BossColossus] Ya está en la fase máxima (11). No se puede avanzar más.");
            return;
        }

        faseActual++;
        golpesEpicosAbsorbidosEnFase = 0;

        ReproducirSonido(sonidoCambioFase);

        Debug.Log($"[BossColossus] Fase avanzada a {faseActual}. Tolerancia a golpes épicos: {faseActual}.");
    }

    // ──────────────────────────────────────────────
    // Absorción e Impacto Pesado
    // ──────────────────────────────────────────────

    public bool RegistrarGolpeEnRaqueta(float velocidadPelota)
    {
        // La inmunidad impenetrable: mientras queden rebotes de resistencia,
        // Colossus no pierde la postura aunque el golpe sea épico/ultra.
        // SIN SONIDO: el rebote/absorción de inmunidad no reproduce audio;
        // el sonido queda reservado únicamente para el disparo de la onda.
        if (rebotesResistenciaRestantes > 0)
        {
            // Si el golpe ya fue absorbido/consumido por PuedeResistir en este
            // mismo impacto (override invocado por PingPongBall), NO volver a
            // restar: solo limpiar la bandera para el próximo impacto.
            if (golpeAbsorbidoEnEsteImpacto)
            {
                golpeAbsorbidoEnEsteImpacto = false;
                return true;
            }

            rebotesResistenciaRestantes--;
            EfectoParticulas(prefabParticulasFortaleza);

            Debug.Log($"[BossColossus] GOLPE ABSORBIDO por inmunidad. Resistencia restante: {rebotesResistenciaRestantes}.");

            // Al agotar el último rebote de la inmunidad, la Fortaleza/Inmunidad
            // se desactiva (la raqueta vuelve a su escala BASE acumulada, sin el
            // extra temporal de Fortaleza; el crecimiento por puntaje permanece).
            if (rebotesResistenciaRestantes == 0)
            {
                estado = EstadoColossus.Normal;
                AplicarEscalaBaseAcumulada();
                Debug.Log("[BossColossus] Inmunidad agotada. Fortaleza desactivada hasta el siguiente punto.");
            }

            return true;
        }

        // Sin inmunidad: aplicar la absorción por fases (mecánica antigua).
        if (velocidadPelota < umbralVelocidadEpica)
        {
            return false;
        }

        if (golpesEpicosAbsorbidosEnFase < faseActual)
        {
            golpesEpicosAbsorbidosEnFase++;

            // SIN SONIDO: absorción por fases de un golpe épico no reproduce audio;
            // los sonidos quedan reservados para el disparo de la onda de choque.
            EfectoParticulas(prefabParticulasFortaleza);

            Debug.Log($"[BossColossus] Golpe épico ABSORBIDO ({golpesEpicosAbsorbidosEnFase}/{faseActual}) en Fase {faseActual}.");

            if (avanzarFasePorGolpesMaximos && golpesEpicosAbsorbidosEnFase >= faseActual)
            {
                Debug.Log($"[BossColossus] Tolerancia de Fase {faseActual} agotada. Avanzando de fase...");
                AvanzarFase();
            }

            return true;
        }

        Debug.Log($"[BossColossus] Golpe épico NO absorbido. Tolerancia agotada ({faseActual}/{faseActual}) en Fase {faseActual}.");
        return false;
    }


    /// <summary>
    /// Punto de entrada usado por PingPongBall cuando la CPU golpea la pelota.
    /// Registra el golpe en la mecánica de Absorción e Impacto Pesado y, si la
    /// Onda de Choque está lista, dispara la habilidad especial de Colossus
    /// (impulso pesado hacia el campo del jugador + efecto visual/sonoro).
    /// </summary>
    public void ActivarHabilidadDesdeGolpeCPU(float velocidadXAntesDelGolpe)
    {
        RegistrarGolpeEnRaqueta(velocidadXAntesDelGolpe);
        DispararOndaDeChoque();
    }
    // ──────────────────────────────────────────────
    // Modo Fortaleza — habilidad activa
    // ──────────────────────────────────────────────

    private IEnumerator ModoFortalezaRoutine()
    {
        estado = EstadoColossus.Fortaleza;
        tiempoFinFortaleza = Time.time + duracionFortaleza;

        // NO se modifica la escala de la raqueta aquí: el tamaño solo cambia al
        // anotar punto (RecalcularEscalaBasePorPuntaje). La Fortaleza solo aporta
        // un efecto visual, SIN sonido (los sonidos quedan reservados para la onda).
        EfectoParticulas(prefabParticulasFortaleza);

        Debug.Log($"[BossColossus] Modo Fortaleza ACTIVADO por {duracionFortaleza}s. Impulso: x{multiplicadorFuerzaGolpe}.");

        yield return new WaitForSeconds(duracionFortaleza);

        DesactivarModoFortaleza();
    }

    private void DesactivarModoFortaleza()
    {
        if (estado != EstadoColossus.Fortaleza) return;

        estado = EstadoColossus.Normal;
        RestaurarEscalaRaqueta();

        Debug.Log("[BossColossus] Modo Fortaleza desactivado. Escala original restaurada.");
    }

    /// <summary>
    /// Devuelve el multiplicador de impulso actual.
    /// Durante Modo Fortaleza retorna multiplicadorFuerzaGolpe; en estado Normal retorna 1.
    /// CPUControl / PingPongBall pueden consultarlo para aplicar el impulso al devolver la pelota.
    /// </summary>
    public float ObtenerMultiplicadorImpulso()
    {
        return ModoFortalezaActivo ? multiplicadorFuerzaGolpe : 1f;
    }

    // ──────────────────────────────────────────────
// ──────────────────────────────────────────────
    // Onda de Choque — habilidad especial
    // ──────────────────────────────────────────────

    /// <summary>
    /// Dispara la Onda de Choque de Colossus si está disponible (estado Listo).
    /// Aplica un impulso pesado a la pelota hacia el campo del jugador, instancia
    /// el efecto visual (tremor/onda) en la mesa dentro de sus límites, reproduce
    /// el sonido de habilidad y pone la habilidad en Recargando durante su cooldown.
    /// </summary>
    public void DispararOndaDeChoque()
    {
        if (!combateActivo) return;
        if (estadoOnda != EstadoOndaDeChoque.Listo) return; // idempotencia

        estadoOnda = EstadoOndaDeChoque.Recargando;

        // 1. Impulso pesado a la pelota hacia el campo del jugador.
        ImpulsarPelotaHaciaJugador();

        // 2. Efecto visual/sonoro en la mesa (dentro de los límites).
        InstanciarEfectoOnda();
        ReproducirSonido(sonidoOndaDeChoque);

        Debug.Log($"[BossColossus] Onda de Choque disparada | cooldown={cooldownOndaDeChoque}s | impulso=x{multiplicadorImpulsoOnda}.");

        // 3. Gestión de recarga (cooldown) de la habilidad.
        StartCoroutine(CorutinaRecargaOnda());
    }

    /// <summary>
    /// Aplica un impulso directo a la pelota aumentando su velocidad en la dirección
    /// en la que viaja (tras un raquetazo de la CPU se dirige al campo del jugador),
    /// simulando la fuerza pesada del Coloso. Usa la velocidad actual como base y
    /// la multiplica por multiplicadorImpulsoOnda (respetando un tope del doble).
    /// </summary>
    private void ImpulsarPelotaHaciaJugador()
    {
        PingPongBall pelota = ball;
        if (pelota == null && gameManager != null)
            pelota = gameManager.ball;
        if (pelota == null) return;

        Rigidbody rbPelota = pelota.GetComponent<Rigidbody>();
        if (rbPelota == null) return;

        Vector3 velocidad = rbPelota.linearVelocity;
        float magnitud = velocidad.magnitude;
        if (magnitud < 0.01f) return; // no hay movimiento, no aplicar impulso

        // Mantener la dirección actual (ya apunta hacia el campo del jugador
        // porque la CPU acaba de golpear) y aumentar la velocidad.
        float nuevaMagnitud = Mathf.Min(magnitud * multiplicadorImpulsoOnda, magnitud * 2f);
        Vector3 dir = velocidad / magnitud;
        dir.y = Mathf.Max(dir.y, 0f); // conserva un pequeño componente ascendente si lo hay

        rbPelota.linearVelocity = dir.normalized * nuevaMagnitud;

        // Sincronizar físicas tras mover la pelota para evitar desincronización.
        Physics.SyncTransforms();
    }

    /// <summary>
    /// Instancia el prefab de la Onda de Choque en la posición de la raqueta de la
    /// CPU (frente al impacto), recortada a los límites de la mesa calculados con
    /// ObtenerDatosMesa() (heredado de BossController). El efecto se adelanta hacia
    /// el frente del jefe (-forward * 1.0f), se escala con el tamaño actual de la
    /// raqueta (CalcularFactorEscalaJefe) y se auto-destruye a los 0.6 segundos
    /// (partículas con startLifetime 0.5s para que el efecto sea breve y dinámico).
    /// </summary>
    private void InstanciarEfectoOnda()
    {
        if (prefabOndaDeChoque == null) return;

        Vector3 posicion = (raquetaTransform != null) ? raquetaTransform.position : transform.position;

        // Adelantar el efecto al FRENTE de la raqueta, hacia la mesa/campo del jugador:
        // el eje local forward suele estar invertido en el modelo 3D, por lo que se
        // usa -forward * 1.0f para que el efecto salga por la cara frontal expuesta
        // (NO embebido dentro de la geometría de la malla).
        Vector3 dirHaciaAdelante = (raquetaTransform != null) ? -raquetaTransform.forward : -transform.forward;
        Vector3 dirArriba = (raquetaTransform != null) ? raquetaTransform.up : transform.up;

        // Elevar ligeramente el efecto para alinearlo con el CENTRO de la goma negra
        // de la raqueta (offset vertical dirArriba * 0.5f). Si necesita subir/bajar más,
        // ajustar el multiplicador (0.3f - 0.8f).
        posicion += (dirHaciaAdelante * 1.0f) + (dirArriba * 0.5f);

        // Mantener el efecto dentro de los límites X/Z de la mesa.
        if (ObtenerDatosMesa(out Vector3 centroMesa, out Vector3 tamanoMesa))
        {
            float mitadX = tamanoMesa.x * 0.5f;
            float mitadZ = tamanoMesa.z * 0.5f;

            // Clamp SOLO en X/Z: el plano horizontal se restringe a la mesa, pero
            // la altura (Y) NO se toca. La posición vertical ya fue calculada con el
            // offset de la raqueta (dirArriba * 0.5f), que alinea el efecto con el
            // CENTRO de la goma negra. Si en su lugar fijáramos posicion.y con el
            // centro de la mesa (centroMesa.y + alturaEfectoOnda), el efecto volvería
            // a caer a la altura del piso/mesa perdiendo la alineación con la raqueta.
            posicion.x = Mathf.Clamp(posicion.x, centroMesa.x - mitadX, centroMesa.x + mitadX);
            posicion.z = Mathf.Clamp(posicion.z, centroMesa.z - mitadZ, centroMesa.z + mitadZ);
        }
        else
        {
            // Sin datos de mesa: solo offset pequeño sobre la posición actual.
            posicion.y += alturaEfectoOnda;
        }

        LimpiarEfectoOndaActual(); // nunca dejar más de un efecto a la vez
        efectoOndaActual = Instantiate(prefabOndaDeChoque, posicion, Quaternion.Euler(rotacionEfectoOnda));

        // Escalar el efecto dinámicamente en proporción al tamaño actual de la
        // raqueta/jefe: cuando Colossus crece (anota punto o cambia de fase), el
        // siguiente shockwave se instancia proporcionalmente más grande.
        efectoOndaActual.transform.localScale *= CalcularFactorEscalaJefe();

        // Si el efecto es un Particle System, forzar una duración CORTA de 0.5 segundos
        // para que el efecto sea dinámico y no se quede estancado en pantalla.
        ParticleSystem ps = efectoOndaActual.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            // ParticleSystem.main devuelve la estructura MainModule POR VALOR (CS1612):
            // usar una variable local intermedia para modificar startLifetime.
            var mainModule = ps.main;
            mainModule.startLifetime = 0.5f; // Duración corta y dinámica (0.5s)
        }

        // Sincronizar físicas para que los colliders del efecto queden posicionados.
        Physics.SyncTransforms();

        // Limpieza rápida del GameObject temporal del efecto (0.6s).
        Destroy(efectoOndaActual, 0.6f);
    }

    /// <summary>
    /// Destruye el efecto de la Onda de Choque actualmente instanciado.
    /// </summary>
    private void LimpiarEfectoOndaActual()
    {
        if (efectoOndaActual != null)
        {
            Destroy(efectoOndaActual);
            efectoOndaActual = null;
        }
    }

    /// <summary>
    /// Corrutina de recarga (cooldown): espera cooldownOndaDeChoque y vuelve a
    /// poner la Onda de Choque en estado Listo.
    /// </summary>
    private IEnumerator CorutinaRecargaOnda()
    {
        yield return new WaitForSeconds(cooldownOndaDeChoque);
        estadoOnda = EstadoOndaDeChoque.Listo;
        Debug.Log("[BossColossus] Onda de Choque lista de nuevo.");
    }
    // Utilidades de escala
    // ──────────────────────────────────────────────

    private void EscalarRaqueta(Vector3 multiplicador)
    {
        if (raquetaTransform == null) GuardarEscalaOriginal();
        if (raquetaTransform != null && escalaOriginalGuardada)
        {
            // Multiplicar componente a componente sobre la escala ORIGINAL.
            // Se clampan a un mínimo de 0.01 los multiplicadores para que la
            // raqueta nunca quede invisible (escala 0 o negativa invierte normales).
            Vector3 nuevaEscala = new Vector3(
                escalaOriginalRaqueta.x * Mathf.Max(multiplicador.x, 0.01f),
                escalaOriginalRaqueta.y * Mathf.Max(multiplicador.y, 0.01f),
                escalaOriginalRaqueta.z * Mathf.Max(multiplicador.z, 0.01f)
            );
            raquetaTransform.localScale = nuevaEscala;
        }
    }

    // ──────────────────────────────────────────────
    // Crecimiento permanente por puntaje e inmunidad por rebotes
    // ──────────────────────────────────────────────

    /// <summary>
    /// Devuelve los PUNTOS DEL JUGADOR (gameManager.PlayerScore) que alimentan el
    /// crecimiento, la resistencia y el centrado de Colossus.
    /// Los puntos que anota la CPU (Colossus) NO cuentan a su favor: si la CPU
    /// marca un punto (CpuScore++), la escala y las cargas de resistencia NO
    /// incrementan. El jefe solo crece/refuerza cuando el JUGADOR logra anotarle
    /// un punto.
    /// </summary>
    private int ObtenerPuntosValidosParaCrecimiento()
    {
        GameManager gm = gameManager != null ? gameManager : GameManager.Instance;
        if (gm != null)
        {
            return gm.PlayerScore; // Solo los puntos anotados por el jugador
        }
        return 0;
    }

    /// <summary>
    /// Calcula la escala base acumulada interpolando (Vector3.Lerp) entre la
    /// escala BASE del prefab (escalaInicialRaqueta) y la escala COLOSAL
    /// (escalaMaximaRaqueta) según los puntos del JUGADOR anotados contra Colossus:
    ///     t = clamp01(puntosDelJugador / puntosParaEscalaMaxima)
    ///     nuevaEscala = Vector3.Lerp(escalaInicialRaqueta, escalaMaximaRaqueta, t)
    /// Luego se limita el ancho real (X o Z) a maxFraccionAnchoMesa de la mesa
    /// y se sincroniza el collider independiente con el mismo t. Aplica la escala
    /// a la raqueta de inmediato (y a su visual/Animator si procede).
    /// </summary>
    private void RecalcularEscalaBasePorPuntaje()
    {
        if (raquetaTransform == null) GuardarEscalaOriginal();
        if (!escalaOriginalGuardada) return;

        int puntos = Mathf.Max(0, ObtenerPuntosValidosParaCrecimiento());

        // ── Progreso interpolado: 0 en 0 puntos ... 1 en puntosParaEscalaMaxima ──
        float t = Mathf.Clamp01((float)puntos / (float)Mathf.Max(1, puntosParaEscalaMaxima));

        // ── Rango absoluto: desde la escala BASE del prefab hasta la escala
        //    COLOSAL (valores absolutos del Transform de la raqueta). ──
        Vector3 nuevaEscala = Vector3.Lerp(escalaBaseInicialAjustada(), escalaMaximaRaqueta, t);

        // ── Limitar el ancho real (X o Z) a la fracción máxima de la mesa. ──
        bool anchoEnX = !ejeLateralEsX;
        if (ObtenerDatosMesa(out Vector3 centroMesa, out Vector3 tamanoMesa))
        {
            float anchoMesa = anchoEnX ? tamanoMesa.x : tamanoMesa.z;

            if (anchoMesa > 0.01f)
            {
                float anchoMaximo = anchoMesa * maxFraccionAnchoMesa;
                if (anchoEnX && nuevaEscala.x > anchoMaximo)
                    nuevaEscala.x = anchoMaximo;
                else if (!anchoEnX && nuevaEscala.z > anchoMaximo)
                    nuevaEscala.z = anchoMaximo;
            }

            // Registrar el centro de la mesa (para el centrado/bloqueo lateral).
            centroMesaDisponible = true;
            centroMesaGuardado = centroMesa;
        }
        else
        {
            centroMesaDisponible = false;
        }

        escalaBaseAcumulada = nuevaEscala;
        escalaBaseCalculada = true;

        // Aplicar la escala a la raqueta de forma permanente (solo en punto/rally).
        // Si hay un Animator que pueda escribir m_LocalScale, se desactiva de forma
        // temporal durante la asignación para que no pise el valor recién aplicado.
        bool animatorEstabaActivo = false;
        if (raquetaAnimator != null && raquetaAnimator.enabled)
        {
            animatorEstabaActivo = true;
            raquetaAnimator.enabled = false;
        }

        // Aplica la escala al root y al transform de la malla visual (hijo/si fuera
        // un modelo separado) para asegurar que el cambio se refleje en la escena.
        AplicarEscalaVisual(nuevaEscala);

        if (raquetaAnimator != null && animatorEstabaActivo)
            raquetaAnimator.enabled = true;

        // Sincronizar el collider independiente (Collider_Raqueta (2)) con el nuevo
        // tamaño usando el MISMO t de interpolación (lerp simétrico).
        SincronizarColliderRaqueta();

        // Actualizar la matriz de colisión de Unity tras las transformaciones.
        Physics.SyncTransforms();

        // ── DIAGNÓSTICO: escala real aplicada en la escena ──
        Debug.Log(
            "[BossColossus] ESCALA APLICADA | " +
            "puntosTotales=" + puntos + " | t=" + t + " | " +
            "escalaBaseAcumulada=" + escalaBaseAcumulada + " | " +
            "root.name=" + (raquetaTransform != null ? raquetaTransform.name : "NULL") + " | " +
            "root.localScale=" + (raquetaTransform != null ? raquetaTransform.localScale.ToString() : "N/A") + " | " +
            "root.lossyScale=" + (raquetaTransform != null ? raquetaTransform.lossyScale.ToString() : "N/A") + " | " +
            "visual.name=" + (raquetaVisualTransform != null ? raquetaVisualTransform.name : "N/A") + " | " +
            "visual.localScale=" + (raquetaVisualTransform != null ? raquetaVisualTransform.localScale.ToString() : "N/A") + " | " +
            "visual.lossyScale=" + (raquetaVisualTransform != null ? raquetaVisualTransform.lossyScale.ToString() : "N/A") + " | " +
            "animator=" + (raquetaAnimator != null ? raquetaAnimator.name : "NULL") + " | " +
            "collider.localScale=" + (transformColliderRaqueta != null ? transformColliderRaqueta.localScale.ToString() : "N/A")
        );
    }

    /// <summary>
    /// Devuelve la escala inicial configurada (escalaInicialRaqueta) con un clamp
    /// mínimo por componente (0.01) para evitar que valores 0 invaliden el modelo.
    /// </summary>
    private Vector3 escalaBaseInicialAjustada()
    {
        return new Vector3(
            Mathf.Max(escalaInicialRaqueta.x, 0.01f),
            Mathf.Max(escalaInicialRaqueta.y, 0.01f),
            Mathf.Max(escalaInicialRaqueta.z, 0.01f)
        );
    }

    /// <summary>
    /// Aplica la escala base acumulada (devolviendo la raqueta a su tamaño
    /// permanente de crecimiento, sin el multiplicador temporal de Fortaleza).
    /// </summary>
    private void AplicarEscalaBaseAcumulada()
    {
        if (raquetaTransform != null && escalaBaseCalculada)
            raquetaTransform.localScale = escalaBaseAcumulada;

        // La escala colosal (por puntaje) debe conservarse: sincronizar el
        // collider independiente y la física con el MISMO tamaño para que nunca
        // regresen al estado base durante el combate.
        SincronizarColliderRaqueta();
        Physics.SyncTransforms();
    }

    /// <summary>
    /// Rearma la inmunidad por rebotes: la resistencia restante se reinicia según
    /// los PUNTOS DEL JUGADOR anotados contra Colossus
    /// (ObtenerPuntosValidosParaCrecimiento). Mínimo 1 para que la inmunidad
    /// tenga sentido al comienzo. Los puntos que anota la CPU no recargan la
    /// resistencia del jefe.
    /// </summary>
    private void ReiniciarInmunidad()
    {
        int puntos = Mathf.Max(1, ObtenerPuntosValidosParaCrecimiento());
        rebotesResistenciaRestantes = CalcularResistenciaInmunidad(puntos);
        Debug.Log($"[BossColossus] Inmunidad rearmada: {rebotesResistenciaRestantes} rebotes " +
                  $"{(forzarDificultadInhumano ? "Inhumano" : "Normal")}: puntosJugador={ObtenerPuntosValidosParaCrecimiento()}"+
                  (forzarDificultadInhumano ? $" x {multiplicadorResistenciaInhumano} + {cargasBaseResistenciaInhumano} base" : "") + ").");
    }

    /// <summary>
    /// Calcula las cargas de resistencia (rebotes absorbibles) según la dificultad:
    ///  - Normal: N = puntos totales disputados (mínimo 1).
    ///  - Inhumano (forzado por Colossus): cargasBase + puntos * multiplicador,
    ///    garantizando que un único impacto épico jamás reduzca la resistencia a 0
    ///    y que el jefe aguante múltiples impactos en modo épico.
    /// </summary>
    private int CalcularResistenciaInmunidad(int puntos)
    {
        if (forzarDificultadInhumano)
        {
            return Mathf.Max(1, cargasBaseResistenciaInhumano +
                                Mathf.RoundToInt(Mathf.Max(0, puntos) * multiplicadorResistenciaInhumano));
        }
        return Mathf.Max(1, puntos);
    }

    /// <summary>
    /// Fuerza la dificultad Inhumana en el controlador de la CPU (CPUControl.SetDificultad)
    /// y la persiste en GameManager.dificultadSeleccionada para que ninguna
    /// re-aplicación de dificultad (ResetRound / RestartGame / nueva partida) rebaje
    /// el perfil del bot durante el combate de Colossus.
    /// La referencia al CPU no se pierde al regenerar la raqueta: este método se
    /// vuelve a invocar desde ReaplicarEstadoColossusTrasRegeneracion() para
    /// garantizar que el nivel de dificultad no se reinicie con la reaparición.
    /// </summary>
    private void ForzarDificultadInhumano()
    {
        if (!forzarDificultadInhumano) return;

        GameManager gm = gameManager != null ? gameManager : GameManager.Instance;
        if (gm != null)
        {
            gm.dificultadSeleccionada = Dificultad.Inhumano;
            if (gm.cpu != null)
                gm.cpu.SetDificultad(Dificultad.Inhumano);
        }

        if (!dificultadInhumanoForzada)
        {
            dificultadInhumanoForzada = true;
            Debug.Log("[BossColossus] Dificultad Inhumana FORZADA en CPU. Perfil: reacción perfecta (vel 800), 0% fallo, 100% golpe.");
        }
    }
    // ──────────────────────────────────────────────
    // Regeneración de la raqueta (golpe épico) — preservar la escala colosal
    // ──────────────────────────────────────────────

    /// <summary>
    /// Suscribe (idempotente, sin duplicados) el handler que reaplica la escala
    /// colosal y la resistencia por puntos después de que la raqueta del CPU se
    /// regenera (destrucción visual por golpe épico). Mismo patrón que usa
    /// BossManager para re-ocultar la raqueta base.
    /// </summary>
    private void SuscribirRegeneracionRaquetaCPU()
    {
        if (gameManager == null) return;
        RaquetaGolpe raquetaCPU = gameManager.golpeRaquetaCPU;
        if (raquetaCPU == null) return;

        // Siempre des-suscribir primero para garantizar una única suscripción.
        raquetaCPU.onRegenerada -= ReaplicarEstadoColossusTrasRegeneracion;
        raquetaCPU.onRegenerada += ReaplicarEstadoColossusTrasRegeneracion;

        Debug.Log("[BossColossus] Suscrito a onRegenerada de la raqueta CPU (reaplicación de escala colosal e inmunidad).");
    }

    /// <summary>
    /// Desuscribe el handler de regeneración (FinalizarCombate / limpieza).
    /// </summary>
    private void DesuscribirRegeneracionRaquetaCPU()
    {
        if (gameManager == null) return;
        RaquetaGolpe raquetaCPU = gameManager.golpeRaquetaCPU;
        if (raquetaCPU == null) return;

        raquetaCPU.onRegenerada -= ReaplicarEstadoColossusTrasRegeneracion;
    }

    /// <summary>
    /// Handler del evento onRegenerada de la raqueta CPU (se dispara al final de
    /// AnimarRegeneracion de RaquetaGolpe, cuando el transform quedó en la escala
    /// BASE del prefab). Aquí se reaplica de inmediato el estado colosal:
    ///   1) RecalcularEscalaBasePorPuntaje(): escala lerp con t = puntos totales;
    ///   2) SincronizarColliderRaqueta() con el MISMO t (lerp simétrico);
    ///   3) ReiniciarInmunidad(): carga = ObtenerPuntosValidosParaCrecimiento() (puntos del jugador);
    ///   4) Physics.SyncTransforms() para que la física tome el nuevo tamaño en el
    ///      MISMO frame en que la raqueta reaparece.
    /// De esta forma la raqueta conserva el tamaño gigante ganado por el marcador
    /// y rearma exactamente N cargas de resistencia (N = puntos disputados).
    /// </summary>
    private void ReaplicarEstadoColossusTrasRegeneracion()
    {
        if (!combateActivo) return;
        if (gameManager == null || gameManager.golpeRaquetaCPU == null) return;

        Debug.Log("[BossColossus] Raqueta del CPU regenerada. Reaplicando escala colosal e inmunidad por puntos...");

        // Re-ocultar la malla default de la raíz: RaquetaGolpe.AnimarRegeneracion()
        // reactiva el renderer base (meshRenderer.enabled = true) al reaparecer; esto
        // lo vuelve a apagar para que solo la skin de Blender (SkinRoot/colosus) se vea.
        DesactivarMallaDefault();

        // Sonido de regeneración / cambio de fase: la raqueta reaparece, se avisa
        // al jugador con el clip de transición (seguro, sin depender del estado
        // del AudioSource del objeto).
        ReproducirSonidoSeguro(sonidoCambioDeFase);

        // 0) La regeneración de la raqueta no debe reiniciar el nivel de dificultad
        //    del CPU: re-forzar el perfil Inhumano (CPUControl + GameManager).
        ForzarDificultadInhumano();

        // 1) Recalcular y aplicar la escala gigante (t = puntos / puntosParaEscalaMaxima)
        //    al root y al visual (AplicarEscalaVisual), limpiando el Animator si pisa.
        RecalcularEscalaBasePorPuntaje();

        // 1b) Reasignar la rotación LOCAL guardada del Inspector tras regenerar:
        //     AnimarRegeneracion de RaquetaGolpe anima solo localScale desde cero,
        //     pero por seguridad se restablece rotacionInicialRaqueta para que la
        //     raqueta del Coloso NUNCA quede acostada/desorientada al reaparecer.
        if (raquetaTransform != null)
            raquetaTransform.localRotation = rotacionInicialRaqueta;

        // 2) Sincronizar el collider independiente (Collider_Raqueta (2)) con el mismo t
        //    y 4) forzar la actualización de físicas en este mismo frame.
        SincronizarColliderRaqueta();
        Physics.SyncTransforms();

        // 3) Rearmar la inmunidad: N rebotes = puntos del JUGADOR (Colossus solo crece
        //    cuando el jugador le anota; los puntos de la CPU no cuentan a su favor).
        ReiniciarInmunidad();
    }

    private void RestaurarEscalaRaqueta()
    {
        if (raquetaTransform != null && escalaOriginalGuardada)
        {
            // Reasignar la rotación LOCAL guardada del Inspector: garantiza que la
            // raqueta quede ERGUIDA (p. ej. X: 90, Y: 0, Z: -277.444) y no vuelva a
            // caer acostada tras regenerar/restablecer el transform.
            raquetaTransform.localRotation = rotacionInicialRaqueta;

            // Si hay escala base acumulada (crecimiento por puntaje), restaurar esa.
            // Si no, usar la escala original fija del prefab.
            raquetaTransform.localScale = escalaBaseCalculada ? escalaBaseAcumulada : escalaOriginalRaqueta;
        }

        // Restaurar también las transformaciones del MODELO VISUAL (colosus dentro de
        // SkinRoot): su escala LOCAL base de la escena y su rotación erguida fija, para
        // que el hijo no se acueste ni se deforme al reaparecer/regenerar.
        if (raquetaVisualTransform != null && rotacionVisualGuardada)
        {
            raquetaVisualTransform.localRotation = rotacionInicialVisual;

            if (escalaOriginalVisual != Vector3.zero && escalaOriginalVisual.x != 0f)
                raquetaVisualTransform.localScale = escalaOriginalVisual;
        }

        // Re-ocultar la malla default de la raíz tras cualquier restauración del transform.
        DesactivarMallaDefault();

        // Mantener el collider independiente sincronizado con el tamaño que se está
        // restaurando (base/acumulado) para que nunca quede discordante con el visual.
        SincronizarColliderRaqueta();
        Physics.SyncTransforms();
    }

    /// <summary>
    /// Resuelve automáticamente la referencia a la RAÍZ LÓGICA de la raqueta del
    /// Coloso (el GameObject con CPUControl / RaquetaGolpe / colliders, p. ej.
    /// "untitled1 (2)"). El modelo 3D de Blender ("colosus") NO es la raíz: es un
    /// sub-objeto HIJO puramente visual que se localiza aparte (BuscarModeloBlender)
    /// para el render (ver ResolverMallaVisual).
    ///
    /// Orden:
    ///   1) GameManager.golpeRaquetaCPU (raíz lógica por referencia).
    ///   2) 'jefe' base.
    ///   3) Subir desde el modelo de Blender hasta el ancestro con RaquetaGolpe/CPUControl.
    /// Se invoca en Awake() y al inicio de Start() sin riesgo de re-asignar.
    /// </summary>
    private void ResolverReferenciaRaqueta()
    {
        // Si ya hay referencia, no hacer nada.
        if (raquetaTransform != null) return;

        raquetaTransform = ResolverRaizLogicaRaqueta();

        Debug.Log($"[BossColossus Debug] Referencia resuelta automáticamente: {(raquetaTransform != null ? raquetaTransform.name : "SEGUIR SIN ENCONTRAR")}");
    }

    /// <summary>
    /// Devuelve la RAÍZ LÓGICA de la raqueta del Coloso (con CPUControl/RaquetaGolpe),
    /// nunca el modelo visual de Blender. Fuentes en orden: golpeRaquetaCPU, 'jefe',
    /// o subir desde el modelo "colosus" hasta el ancestro que tenga esos componentes.
    /// </summary>
    private Transform ResolverRaizLogicaRaqueta()
    {
        Transform candidato = null;

        // 1. Referencia explícita del GameManager (contiene RaquetaGolpe / CPUControl).
        if (gameManager != null && gameManager.golpeRaquetaCPU != null)
            candidato = gameManager.golpeRaquetaCPU.transform;

        // 2. Fallback: el 'jefe' base.
        if (candidato == null && jefe != null)
            candidato = jefe;

        // 3. Último recurso: partir del modelo visual y subir en la jerarquía.
        if (candidato == null)
            candidato = BuscarModeloBlender();

        // Subir hasta encontrar un ancestro con RaquetaGolpe o CPUControl (la raíz lógica).
        Transform raiz = candidato;
        while (raiz != null)
        {
            if (raiz.GetComponent<RaquetaGolpe>() != null) return raiz;
            if (raiz.GetComponent<CPUControl>()  != null) return raiz;
            raiz = raiz.parent;
        }

        return candidato;
    }

    /// <summary>
    /// Busca el transform del MODELO VISUAL de Blender (hijo "colosus"/"colosus_skin"/"raqueta")
    /// dentro de la jerarquía del jefe. PRIMERO comprueba si existe un contenedor
    /// "SkinRoot": si existe, el modelo se busca SOLO dentro de SkinRoot y sus
    /// descendientes (estructura de skins del SkinManager). Si no hay SkinRoot, se
    /// busca directamente en el ámbito visual (raíz lógica de la raqueta o el jefe).
    /// Excluye el propio transform raíz (el modelo nunca es la raíz). Devuelve null
    /// si no se encuentra.
    /// </summary>
    private Transform BuscarModeloBlender()
    {
        // Ámbito visual: la raíz lógica (untitled1 (2)) o, en su defecto, el propio jefe.
        Transform raizVisual = (raquetaTransform != null) ? raquetaTransform : transform;

        // 1) Buscar el contenedor visual "SkinRoot" dentro de la raíz lógica.
        Transform skinRoot = BuscarSkinRoot(raizVisual);

        // 2) Buscar el modelo de Blender dentro del ámbito correcto:
        //    si existe SkinRoot, solo se mira dentro de él (y sus descendientes).
        Transform ambito = (skinRoot != null) ? skinRoot : raizVisual;
        return BuscarModeloEn(ambito);
    }

    /// <summary>
    /// Busca un hijo llamado "SkinRoot" (case-insensitive) en el transform indicado:
    /// primero como hijo directo y luego de forma recursiva en toda la jerarquía.
    /// Devuelve null si no existe.
    /// </summary>
    private Transform BuscarSkinRoot(Transform raiz)
    {
        if (raiz == null) return null;

        // 1. Hijo directo (Transform.Find es case-sensitive; intentar variantes).
        Transform encontrado = raiz.Find("SkinRoot");
        if (encontrado != null) return encontrado;

        // 2. Recursivo por nombre parcial (ignora mayúsculas).
        Transform[] hijos = raiz.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < hijos.Length; i++)
        {
            Transform t = hijos[i];
            if (t == null || t == raiz) continue;
            if (t.name.ToLower().Contains("skinroot"))
                return t;
        }

        return null;
    }

    /// <summary>
    /// Busca el modelo de Blender ("colosus"/"colosus_skin"/"raqueta") por nombre
    /// DENTRO del ámbito indicado (el que debe ser SkinRoot si existe): primero hijo
    /// directo, luego recursivo (ignore case). Excluye el propio ámbito (el modelo
    /// nunca es la raíz del ámbito).
    /// </summary>
    private Transform BuscarModeloEn(Transform ambito)
    {
        if (ambito == null) return null;

        // 1. Hijo directo por nombre EXACTO.
        Transform modelo = ambito.Find("colosus");
        if (modelo == null) modelo = ambito.Find("colosus_skin");
        if (modelo == null) modelo = ambito.Find("raqueta");

        // 2. Recursivo por nombre parcial (ignora mayúsculas), excluyendo el ámbito.
        if (modelo == null)
        {
            Transform[] todosLosHijos = ambito.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < todosLosHijos.Length; i++)
            {
                Transform t = todosLosHijos[i];
                if (t == null || t == ambito) continue; // el modelo nunca es la raíz del ámbito

                string nombre = t.name.ToLower();
                if (nombre.Contains("colosus") || nombre.Contains("colossus") || nombre.Contains("raqueta"))
                {
                    modelo = t;
                    break;
                }
            }
        }

        // Log de diagnóstico: ayuda a saber en qué ámbito se halló el modelo.
        if (modelo != null)
        {
            string contenedor = (ambito == raquetaTransform) ? "raíz lógica" : ambito.name;
            Debug.Log($"[BossColossus Debug] Modelo de Blender '{modelo.name}' localizado dentro de: {contenedor}");
        }

        return modelo;
    }

    /// <summary>
    /// True si el modo de juego actual es una Batalla de Jefe ESPECÍFICAMENTE contra
    /// BossColossus (Modo Historia -> BossColossus). Valida 3 puntos:
    ///   1. combateActivo ya arrancó en este script (red de seguridad).
    ///   2. BossManager.JefeActivo existe y es ESTE jefe (por referencia, GameObject o nombre).
    ///   3. Excluye Torneo, VS CPU y 2 Jugadores (en esos modos JefeActivo es null o no es Colossus).
    /// </summary>
    private bool EsModoBatallaDeJefe()
    {
        // 1. Si el combate ya arrancó explícitamente en el script
        if (combateActivo) return true;

        // 2. Verificar que exista BossManager y que el jefe activo sea ESPECÍFICAMENTE Colossus
        BossManager bossManager = FindObjectOfType<BossManager>();
        if (bossManager == null || bossManager.JefeActivo == null) return false;

        // Validar que el jefe activo sea este BossColossus (por tipo, GameObject o nombre)
        bool esEsteJefe = bossManager.JefeActivo == this ||
                          bossManager.JefeActivo.gameObject == this.gameObject ||
                          bossManager.JefeActivo.name.Contains("Colossus");

        return esEsteJefe;
    }

    /// <summary>
    /// Oculta de forma PERSISTENTE el MeshRenderer/MeshFilter de la RAÍZ lógica de la
    /// raqueta (la raqueta default "untitled1 (2)"), para que la skin de Blender
    /// (colosus dentro de SkinRoot) sea la única visible.
    ///
    /// Se cachea el renderer la primera vez y se re-aplica el apagado en cada
    /// restauración/regeneración: AplicarEscalaVisual(), RestaurarEscalaRaqueta(),
    /// ReaplicarEstadoColossusTrasRegeneracion() o el Start() podrían volver a
    /// habilitarlo; esta llamada garantiza que NUNCA vuelva a quedar activo.
    /// Se respeta el caso degenerado donde la raíz ES el modelo visible (no se apaga).
    /// </summary>
    private void DesactivarMallaDefault()
    {
        if (raquetaTransform == null) return;

        // Cachear el MeshRenderer de la raíz la primera vez.
        if (rendererMallaDefaultOculto == null)
            rendererMallaDefaultOculto = raquetaTransform.GetComponent<MeshRenderer>();

        // Si el renderer de la raíz es a su vez el modelo visual (caso sin SkinRoot),
        // NO apagarlo: se respeta la única malla visible.
        if (rendererMallaDefaultOculto != null &&
            (raquetaVisualTransform == null || rendererMallaDefaultOculto.transform != raquetaVisualTransform))
        {
            rendererMallaDefaultOculto.enabled = false;
        }

        // También desactivar cualquier SkinnedMeshRenderer directo de la raíz (modelo
        // por huesos en la propia raíz), salvo que sea el visual activo.
        SkinnedMeshRenderer skinnedRaiz = raquetaTransform.GetComponent<SkinnedMeshRenderer>();
        if (skinnedRaiz != null && (raquetaVisualTransform == null || skinnedRaiz.transform != raquetaVisualTransform))
            skinnedRaiz.enabled = false;
    }

    /// <summary>
    /// Limpieza gráfica completa para MODOS NO-BOSS (VS CPU, Torneo, etc.):
    /// desactiva por completo el objeto visual de la skin de Blender (SkinRoot y/o el
    /// modelo colosus) y REACTIVA la malla NORMAL de la raqueta CPU. Combate el
    /// solapamiento en el que la skin del jefe se empalma sobre la raqueta normal:
    /// deshabilitar el script (this.enabled = false) NO basta porque los objetos 3D ya
    /// activados en la jerarquía siguen renderizándose.
    /// </summary>
    private void DesactivarVisualesJefeParaModoNormal()
    {
        Transform skinRoot = BuscarSkinRoot(raquetaTransform != null ? raquetaTransform : transform);
        Transform modeloBlender = BuscarModeloBlender();

        // Destruir el objeto de la skin si no estamos en modo boss
        if (modeloBlender != null && modeloBlender != raquetaTransform)
        {
            Destroy(modeloBlender.gameObject);
        }
        else if (skinRoot != null)
        {
            // Recorrer hijos de SkinRoot y destruir los que sean el modelo colosus
            foreach (Transform hijo in skinRoot)
            {
                string nombre = hijo.name.ToLower();
                if (nombre.Contains("colosus") || nombre.Contains("colossus"))
                {
                    Destroy(hijo.gameObject);
                }
            }
        }

        // Reactivar la malla normal de la raqueta CPU (raíz lógica untitled1 (2))
        if (raquetaTransform != null)
        {
            MeshRenderer meshDefault = raquetaTransform.GetComponent<MeshRenderer>();
            if (meshDefault != null)
                meshDefault.enabled = true;

            SkinnedMeshRenderer skinnedDefault = raquetaTransform.GetComponent<SkinnedMeshRenderer>();
            if (skinnedDefault != null)
                skinnedDefault.enabled = true;
        }
    }

    private void GuardarEscalaOriginal()
    {
        if (gameManager != null && gameManager.golpeRaquetaCPU != null)
        {
            raquetaTransform = gameManager.golpeRaquetaCPU.transform;
        }
        else if (jefe != null)
        {
            raquetaTransform = jefe;
        }

        if (raquetaTransform != null)
        {
            // ── GUARDA DE ESCALA CERO: si la escala tiene algún componente en 0
            //    la raqueta queda INVISIBLE (mesh colapsada). Se restablece a un
            //    valor positivo equivalente (el que estaba configurado, o uno base)
            //    para que el modelo siempre se renderice. ──
            if (raquetaTransform.localScale == Vector3.zero || raquetaTransform.localScale.x == 0f ||
                raquetaTransform.localScale.y == 0f || raquetaTransform.localScale.z == 0f)
            {
                Debug.LogWarning("[BossColossus Debug] La raqueta tenía escala con algún componente en 0, se restablece a su escala local.");
                raquetaTransform.localScale = new Vector3(-75.72f, -23.2f, -308.84f);
            }

            // Guardar la rotación LOCAL EXACTA configurada en el Inspector (p. ej.
            // X: 90, Y: 0, Z: -277.444) y la escala inicial, SIN forzar valores por
            // defecto (Quaternion.identity / Vector3.one). Así la raqueta se mantiene
            // erguida y con los accesorios orientados correctamente tras regenerar.
            rotacionInicialRaqueta = raquetaTransform.localRotation;

            escalaOriginalRaqueta = raquetaTransform.localScale;
            escalaOriginalGuardada = true;

            // Resolver la malla visual 3D: el primer MeshRenderer activo (puede estar
            // en un hijo). Si no hay mesh, el visual es el propio raquetaTransform.
            ResolverMallaVisual();
        }
        else
        {
            Debug.LogWarning("[BossColossus] No se pudo resolver la referencia a la raqueta del CPU. La escala no se guardó.");
        }
    }

    /// <summary>
    /// Resuelve el transform que contiene la malla 3D visible del MODELO DE BLENDER
    /// (hijo "colosus"/"raqueta") y detecta si existe un Animator que pueda
    /// sobrescribir m_LocalScale.
    ///
    /// Arquitectura: la RAÍZ ("untitled1 (2)") es lógica (CPUControl/RaquetaGolpe/
    /// colliders) y el modelo 3D de Blender vive como HIJO VISUAL, normalmente
    /// dentro de un contenedor de skin llamado "SkinRoot". Por eso aquí:
    ///   1) Se localiza "SkinRoot" (BuscarSkinRoot) y, dentro de él, el
    ///      MeshRenderer/MeshFilter del modelo de Blender (colosus/raqueta).
    ///   2) Si la raíz tiene su propio MeshRenderer (la malla básica/skin antigua),
    ///      se DESHABILITA (enabled=false) para que no tape ni se dibuje encima.
    ///   3) activarAccesoriosMalla reactiva la jerarquía visual DENTRO de SkinRoot
    ///      (o del modelo si no hay SkinRoot), conservando la rotación y escala
    ///      locales configuradas.
    /// </summary>
    private void ResolverMallaVisual()
    {
        raquetaVisualResuelto = true;
        raquetaVisualTransform = raquetaTransform;

        // 0) Raíz lógica, contenedor visual SkinRoot y modelo visual de Blender.
        Transform raizLogica = ResolverRaizLogicaRaqueta();
        Transform raizVisual = (raquetaTransform != null) ? raquetaTransform : transform;
        Transform skinRoot = BuscarSkinRoot(raizVisual);
        Transform modeloBlender = BuscarModeloBlender();

        if (skinRoot != null)
            Debug.Log($"[BossColossus Debug] SkinRoot localizado: {skinRoot.name}. La activación visual se limitará a este contenedor.");

        // ── INYECCIÓN DINÁMICA ESTILO BOSS ZEUS ──
        if (EsModoBatallaDeJefe())
        {
            // 1. Apagar la malla normal por defecto de la raqueta CPU
            if (raquetaTransform != null)
            {
                MeshRenderer meshDefault = raquetaTransform.GetComponent<MeshRenderer>();
                if (meshDefault != null) meshDefault.enabled = false;

                SkinnedMeshRenderer skinnedDefault = raquetaTransform.GetComponent<SkinnedMeshRenderer>();
                if (skinnedDefault != null) skinnedDefault.enabled = false;
            }

            // 2. Instanciar y activar la skin de Colossus (estilo Zeus)
            if (prefabSkinColossus != null && instanciaSkinActual == null)
            {
                Transform padreSkin = skinRoot != null ? skinRoot : raquetaTransform;
                if (padreSkin != null)
                {
                    instanciaSkinActual = Instantiate(prefabSkinColossus, padreSkin);
                    instanciaSkinActual.name = "colosus";

                    // Activar explícitamente la instancia (al igual que en BossZeus por si el prefab viene inactivo)
                    instanciaSkinActual.SetActive(true);

                    // Asegurar que todos sus Renderers estén habilitados
                    foreach (Renderer r in instanciaSkinActual.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r != null) r.enabled = true;
                    }

                    // Transform de respaldo
                    instanciaSkinActual.transform.localRotation = Quaternion.Euler(0f, -180f, 180f);
                    instanciaSkinActual.transform.localScale = new Vector3(-1.081282f, -1.392614f, -1.022877f);
                    instanciaSkinActual.transform.localPosition = Vector3.zero;

                    Debug.Log($"[BossColossus] Skin instanciada y activada dinámicamente en: {padreSkin.name}");

                    modeloBlender = BuscarModeloBlender();
                }
            }
        }

        // 1) MeshRenderer del MODELO DE BLENDER (hijo "colosus"/"raqueta").
        MeshRenderer meshModelo = null;
        if (modeloBlender != null)
        {
            meshModelo = modeloBlender.GetComponent<MeshRenderer>();
            if (meshModelo == null)
                meshModelo = modeloBlender.GetComponentInChildren<MeshRenderer>(true);
        }

        if (meshModelo != null)
        {
            raquetaVisualTransform = meshModelo.transform;

            MeshFilter filtroModelo = modeloBlender.GetComponent<MeshFilter>();
            if (filtroModelo != null)
                Debug.Log($"[BossColossus Debug] Modelo de Blender '{modeloBlender.name}' con MeshFilter y MeshRenderer localizados.");
        }
        else
        {
            // Fallback: primer mesh de la jerarquía (modelo no encontrado por nombre).
            MeshRenderer meshVisual = raquetaTransform.GetComponentInChildren<MeshRenderer>(true);
            if (meshVisual != null)
                raquetaVisualTransform = meshVisual.transform;
            else
            {
                // Algunos modelos usan SkinnedMeshRenderer (p. ej. si la raqueta tuviera animación por huesos).
                SkinnedMeshRenderer skinned = raquetaTransform.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (skinned != null)
                    raquetaVisualTransform = skinned.transform;
            }
        }

        // 2) Deshabilitar de forma PERSISTENTE el MeshRenderer de la RAÍZ lógica (malla
        //    básica/skin antigua de untitled1 (2)) para que no tape ni se dibuje
        //    encima del modelo de Blender. El renderer queda cacheado y se re-apaga
        //    en cada restauración/regeneración si algo intentara reactivarlo.
        DesactivarMallaDefault();
        // 3) activarAccesoriosMalla: activar renderers/GameObjects de la jerarquía visual
        //    (dentro de SkinRoot si existe; si no, del modelo de Blender) SIN tocar la
        //    rotación/escala locales configuradas.
        if (activarAccesoriosMalla)
        {
            // Prioridad de activación visual:
            //   1) SkinRoot (si existe): activar SOLO la jerarquía visual dentro de él
            //      (colosus, raqueta y accesorios), nunca mallas externas.
            //   2) modeloBlender (si no hay SkinRoot o el modelo está directo).
            //   3) raquetaTransform (situación degenerada sin modelo de Blender).
            Transform baseActivar = skinRoot != null ? skinRoot : (modeloBlender != null ? modeloBlender : raquetaTransform);
            if (baseActivar != null)
            {
                if (!baseActivar.gameObject.activeSelf)
                {
                    baseActivar.gameObject.SetActive(true);
                    Debug.Log("[BossColossus] Malla visual de la raqueta reactivada (estaba inactiva al iniciar).");
                }

                Renderer rendVisual = baseActivar.GetComponent<Renderer>();
                if (rendVisual != null && !rendVisual.enabled)
                {
                    rendVisual.enabled = true;
                    Debug.Log("[BossColossus] Renderer de la malla visual habilitado (estaba apagado al iniciar).");
                }

                // Accesorios del modelo: activar TODOS los MeshRenderer/SkinnedMeshRenderer
                // hijos del modelo de Blender (goma, mango, decoración), incluso los que
                // vengan en GameObjects desactivados. NO se toca localRotation/localScale.
                MeshRenderer[] mallas = baseActivar.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < mallas.Length; i++)
                {
                    if (mallas[i] == null) continue;
                    if (!mallas[i].gameObject.activeSelf)
                        mallas[i].gameObject.SetActive(true);
                    if (!mallas[i].enabled)
                        mallas[i].enabled = true;
                }

                SkinnedMeshRenderer[] esqueleto = baseActivar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < esqueleto.Length; i++)
                {
                    if (esqueleto[i] == null) continue;
                    if (!esqueleto[i].gameObject.activeSelf)
                        esqueleto[i].gameObject.SetActive(true);
                    if (!esqueleto[i].enabled)
                        esqueleto[i].enabled = true;
                }
            }
        }

        escalaOriginalVisual = raquetaVisualTransform != null ? raquetaVisualTransform.localScale : escalaOriginalRaqueta;

        // ── FIJAR las transformaciones LOCALES del MODELO VISUAL (colosus dentro de
        //    SkinRoot): al emparentar colosus bajo SkinRoot cambian las transformaciones
        //    heredadas; hay que capturar del Inspector la rotación/escala locales del
        //    modelo y re-aplicarlas SIEMPRE para que la raqueta quede erguida. ──
        if (raquetaVisualTransform != null)
        {
            // 1) Rotación LOCAL: respetar la del Editor; si es identidad (no configurada),
            //    aplicar el fallback rotacionVisualFallback (p. ej. Euler(0,-180,180)).
            rotacionInicialVisual = raquetaVisualTransform.localRotation;
            if (EsRotacionDefault(rotacionInicialVisual))
                rotacionInicialVisual = rotacionVisualFallback;

            raquetaVisualTransform.localRotation = rotacionInicialVisual;
            rotacionVisualGuardada = true;

            // 2) Escala LOCAL: si es cero/inválida, aplicar la escala local base de la
            //    escena (escalaVisualFallback, p. ej. -1.081282, -1.392614, -1.022877).
            if (escalaOriginalVisual == Vector3.zero || escalaOriginalVisual.x == 0f ||
                escalaOriginalVisual.y == 0f || escalaOriginalVisual.z == 0f)
            {
                escalaOriginalVisual = escalaVisualFallback;
                raquetaVisualTransform.localScale = escalaVisualFallback;
            }

            Debug.Log($"[BossColossus Debug] Modelo visual '{raquetaVisualTransform.name}' fijado: rot={rotacionInicialVisual.eulerAngles} | escala={escalaOriginalVisual}");
        }

        // Detectar un Animator que pueda controlar la escala (m_LocalScale).
        raquetaAnimator = raquetaTransform.GetComponent<Animator>();
        if (raquetaAnimator == null)
            raquetaAnimator = raquetaTransform.GetComponentInChildren<Animator>(true);
        if (raquetaAnimator == null)
            raquetaAnimator = GetComponent<Animator>();
    }

    /// <summary>
    /// Indica si un Quaternion está en su estado por defecto (identidad o ángulos
    /// cercanos a cero). Se usa para decidir si aplicar el fallback de rotación del
    /// modelo visual (colosus dentro de SkinRoot): si la rotación capturada del
    /// Editor es neutra (no fue configurada), se fuerza rotacionVisualFallback.
    /// </summary>
    private bool EsRotacionDefault(Quaternion rotacion)
    {
        return Quaternion.Angle(rotacion, Quaternion.identity) < 0.1f ||
               (rotacion.eulerAngles.x == 0f && rotacion.eulerAngles.y == 0f && rotacion.eulerAngles.z == 0f);
    }

    /// <summary>
    /// Aplica la escala tanto al transform principal de la raqueta como al
    /// transform que contiene la malla 3D, garantizando que el cambio se refleje
    /// visualmente:
    ///  - Si la malla está en un HIJO del root: el root ya la escala vía lossyScale;
    ///    se restaura el localScale original del hijo para no componer doble escala.
    ///  - Si la malla es un objeto SEPARADO (p. ej. el modelo del jefe): se aplica
    ///    el mismo factor de crecimiento a su localScale.
    /// </summary>
    private void AplicarEscalaVisual(Vector3 nuevaEscala)
    {
        if (raquetaTransform != null)
            raquetaTransform.localScale = nuevaEscala;

        if (!raquetaVisualResuelto)
            ResolverMallaVisual();

        if (raquetaVisualTransform == null || raquetaVisualTransform == raquetaTransform)
            return;

        // Factor de crecimiento aplicado a la raqueta.
        float fx = escalaOriginalRaqueta.x > 0.0001f ? nuevaEscala.x / escalaOriginalRaqueta.x : 1f;
        float fy = escalaOriginalRaqueta.y > 0.0001f ? nuevaEscala.y / escalaOriginalRaqueta.y : 1f;
        float fz = escalaOriginalRaqueta.z > 0.0001f ? nuevaEscala.z / escalaOriginalRaqueta.z : 1f;

        if (raquetaVisualTransform.IsChildOf(raquetaTransform))
        {
            // Hijo del root: no duplicar escala (el root ya propaga lossyScale).
            // Se restaura la escala LOCAL base del modelo y se FIJAN sus transformaciones
            // locales (rotación erguida) para que el crecimiento no aplane ni acueste al hijo.
            raquetaVisualTransform.localScale = escalaOriginalVisual;
            if (rotacionVisualGuardada)
                raquetaVisualTransform.localRotation = rotacionInicialVisual;
        }
        else
        {
            // Visual separado del root: aplicar el mismo factor de crecimiento.
            raquetaVisualTransform.localScale = new Vector3(
                escalaOriginalVisual.x * Mathf.Max(fx, 0.01f),
                escalaOriginalVisual.y * Mathf.Max(fy, 0.01f),
                escalaOriginalVisual.z * Mathf.Max(fz, 0.01f)
            );
            // Mantener la rotación LOCAL erguida del modelo visual.
            if (rotacionVisualGuardada)
                raquetaVisualTransform.localRotation = rotacionInicialVisual;
        }

        // Forzar la actualización de la matriz de transform (lossyScale) de inmediato.
        raquetaTransform.hasChanged = true;
        if (raquetaVisualTransform != raquetaTransform)
            raquetaVisualTransform.hasChanged = true;

        // Re-ocultar la malla default de la raíz tras aplicar la escala: algunos flujos
        // (regeneración, ResetRound) pueden re-habilitar el renderer base de la raíz.
        DesactivarMallaDefault();
    }

    // ──────────────────────────────────────────────
    // Collider independiente (Collider_Raqueta (2) / ColliderSeguidor)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Resuelve el collider independiente (transformColliderRaqueta) si no está
    /// asignado en el Inspector: busca un ColliderSeguidor cuyo objetivo sea la
    /// raqueta de la CPU. Guarda su escala original para poder escalarlo después.
    /// </summary>
    private void GuardarColliderOriginal()
    {
        if (transformColliderRaqueta == null && raquetaTransform != null)
        {
            // Buscar un ColliderSeguidor que siga a la raqueta de la CPU.
            ColliderSeguidor[] seguidores = FindObjectsOfType<ColliderSeguidor>(true);
            for (int i = 0; i < seguidores.Length; i++)
            {
                if (seguidores[i] == null) continue;
                if (seguidores[i].objetivo == raquetaTransform)
                {
                    transformColliderRaqueta = seguidores[i].transform;
                    break;
                }
            }
        }

        if (transformColliderRaqueta != null)
        {
            escalaOriginalCollider = transformColliderRaqueta.localScale;
            escalaOriginalColliderGuardada = true;

            // Autocalcular el RANGO abs interno del collider (para Vector3.Lerp)
            // usando la misma proporción que el rango de la raqueta:
            //   colliderInicial = originalCollider
            //   colliderMaxima  = originalCollider * (escalaMaximaRaqueta / escalaInicialRaqueta)
            // De esta forma la caja de colisiones crece al MISMO ritmo que el visual.
            escalaInicialCollider = escalaOriginalCollider;
            escalaMaximaCollider = new Vector3(
                escalaInicialCollider.x * (escalaInicialRaqueta.x > 0.0001f ? escalaMaximaRaqueta.x / escalaInicialRaqueta.x : 1f),
                escalaInicialCollider.y * (escalaInicialRaqueta.y > 0.0001f ? escalaMaximaRaqueta.y / escalaInicialRaqueta.y : 1f),
                escalaInicialCollider.z * (escalaInicialRaqueta.z > 0.0001f ? escalaMaximaRaqueta.z / escalaInicialRaqueta.z : 1f)
            );
        }
    }

    /// <summary>
    /// Aplica la misma escala proporcional al collider independiente que a la
    /// raqueta, usando la escala acumulada (crecimiento por puntaje). Se llama
    /// tras RecalcularEscalaBasePorPuntaje() y se sincroniza la física de Unity.
    /// </summary>
    private void SincronizarColliderRaqueta()
    {
        if (!escalaBaseCalculada) return;

        if (!escalaOriginalColliderGuardada)
            GuardarColliderOriginal();

        if (transformColliderRaqueta == null) return;

        // Calcular el MISMO progreso t usado para la raqueta (puntos del JUGADOR).
        int puntos = Mathf.Max(0, ObtenerPuntosValidosParaCrecimiento());
        float t = Mathf.Clamp01((float)puntos / (float)Mathf.Max(1, puntosParaEscalaMaxima));

        // Lerp simétrico: el collider independiente interpolado entre su escala
        // inicial y su escala máxima con el mismo t que la raqueta visual. Así la
        // caja de colisiones coincide exactamente con el modelo a cualquier tamaño.
        transformColliderRaqueta.localScale = Vector3.Lerp(escalaInicialCollider, escalaMaximaCollider, t);

        // Sincronizar físicas para actualizar la matriz de colisión de Unity.
        Physics.SyncTransforms();
    }

// ──────────────────────────────────────────────
    // Centrado / bloqueo lateral por tamaño de raqueta
    // ──────────────────────────────────────────────

    /// <summary>
    /// Evalúa si la raqueta debe fijarse al centro de la mesa (bloqueo lateral):
    ///   - Si el jugador ha anotado puntosParaCentrar o más puntos contra Colossus.
    ///   - O si el ancho de la raqueta supera fraccionAnchoParaCentrar del ancho
    ///     de la mesa (default 80%).
    /// Cuando está centrada, fuerza la posición X (o Z según ejeLateralEsX) al
    /// centro de la mesa y fija raquetaCentrada = true para que la raqueta no se
    /// mueva lateralmente al cubrir toda la mesa.
    /// </summary>
    private void EvaluarCentradoRaqueta()
    {
        if (raquetaTransform == null) return;
        if (ObtenerDatosMesa(out Vector3 centroMesa, out Vector3 tamanoMesa) == false)
        {
            raquetaCentrada = false;
            return;
        }

        int puntos = Mathf.Max(0, ObtenerPuntosValidosParaCrecimiento());

        // Eje real del ancho de la raqueta (coherente con el bloqueo lateral):
        // ejeLateralEsX = false → la raqueta se mueve en Z, por lo que su ancho
        // real está en X; ejeLateralEsX = true → el ancho está en Z.
        bool anchoEnX = !ejeLateralEsX;
        float anchoMesa = anchoEnX ? tamanoMesa.x : tamanoMesa.z;
        float anchoRaqueta = anchoEnX ? raquetaTransform.localScale.x : raquetaTransform.localScale.z;

        bool centraPorPuntos = puntos >= puntosParaCentrar;
        bool centraPorAncho = anchoMesa > 0.01f && (anchoRaqueta / anchoMesa) >= fraccionAnchoParaCentrar;

        raquetaCentrada = centraPorPuntos || centraPorAncho;

        if (raquetaCentrada)
        {
            // Fijar la posición lateral (X o Z) al centro de la mesa y bloquear
            // el movimiento lateral (destinoZ de CPUControl se ignora por el propio
            // CPUControl al cubrir toda la mesa; aquí solo se fuerza la posición).
            Vector3 pos = raquetaTransform.position;
            if (ejeLateralEsX)
                pos.x = centroMesa.x;
            else
                pos.z = centroMesa.z;
            raquetaTransform.position = pos;

            Physics.SyncTransforms(); // para que la nueva posición aplique de inmediato

            Debug.Log($"[BossColossus] Raqueta CENTRADA (bloqueo lateral). Puntos={puntos}, Ancho%={anchoRaqueta / Mathf.Max(anchoMesa, 0.0001f):P0}.");
        }
        else
        {
            Debug.Log($"[BossColossus] Raqueta libre. Puntos={puntos}, Ancho%={anchoRaqueta / Mathf.Max(anchoMesa, 0.0001f):P0}.");
        }
    }

    private void DetenerCorrutinasFortaleza()
    {
        StopAllCoroutines();
        estado = EstadoColossus.Normal;
    }

    // ──────────────────────────────────────────────
    // Utilidades de audio y partículas
    // ──────────────────────────────────────────────

    private void ReproducirSonido(AudioClip clip)
    {
        if (clip == null) return;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (audioSource != null)
            audioSource.PlayOneShot(clip);
    }

    /// <summary>
    /// Reproduce un clip de forma SEGURA y 100% en 2D (Spatial Blend = 0), sin
    /// atenuación por distancia, garantizando que se escuche siempre en partidas
    /// 2D/2.5D aunque la raqueta (o el jefe) se desactive o se destruya en el
    /// mismo frame (p. ej. al agotarse las cargas de resistencia o al activarse
    /// la regeneración).
    ///
    /// Evita AudioSource.PlayClipAtPoint porque esa API crea internamente un
    /// AudioSource 3D (Spatial Blend ~1.0) con atenuación por distancia que puede
    /// silenciar por completo los clips en escenas 2D/2.5D.
    ///
    /// Estrategia:
    ///   1) Si hay audioSourceColossus asignado y habilitado -> PlayOneShot 2D.
    ///   2) Si no (objeto destruido/desactivado) -> crea un GameObject temporal
    ///      con AudioSource 2D puro que se autodestruye al terminar el clip.
    /// </summary>
    private void ReproducirSonidoSeguro(AudioClip clip, float volumen = 1f)
    {
        if (clip == null)
        {
            Debug.LogWarning("[BossColossus Audio] Intento de reproducir un sonido pero el AudioClip es NULL. Revisa el Inspector.");
            return;
        }

        Debug.Log($"[BossColossus Audio] Disparando sonido 2D: {clip.name}");

        // Opción 1: Si hay un AudioSource asignado en la escena/objeto, usar PlayOneShot en 2D
        if (audioSourceColossus != null && audioSourceColossus.enabled)
        {
            audioSourceColossus.spatialBlend = 0f; // Forzar modo 2D (sin atenuación por distancia)
            audioSourceColossus.PlayOneShot(clip, volumen);
            return;
        }

        // Opción 2: Si audioSourceColossus es nulo o el objeto se desactiva/destruye,
        // creamos un reproductor temporal 100% 2D que se autodestruye al terminar.
        GameObject sfxGO = new GameObject($"TempSFX_{clip.name}");
        AudioSource aSource = sfxGO.AddComponent<AudioSource>();
        aSource.clip = clip;
        aSource.volume = volumen;
        aSource.spatialBlend = 0f; // 0 = Sonido 2D puro (se escucha en todo el juego)
        aSource.playOnAwake = false;
        aSource.Play();

        // Destruir el GameObject temporal cuando termine de reproducirse el clip
        Destroy(sfxGO, clip.length + 0.1f);
    }

    private void EfectoParticulas(GameObject prefab)
    {
        if (prefab == null) return;

        Transform refTransform = (raquetaTransform != null) ? raquetaTransform : transform;

        // Adelantar el efecto al FRENTE de la raqueta, hacia la mesa/campo del jugador:
        // el eje local forward suele estar invertido, así que se invierte la dirección
        // con -forward * 1.0f para que el efecto salga por la cara frontal expuesta.
        Vector3 dirHaciaAdelante = -refTransform.forward;
        Vector3 dirArriba = refTransform.up;

        // Elevar ligeramente el efecto para alinearlo con el CENTRO de la goma negra
        // de la raqueta (offset vertical dirArriba * 0.5f). Si necesita subir/bajar más,
        // ajustar el multiplicador (0.3f - 0.8f).
        Vector3 posicion = refTransform.position + (dirHaciaAdelante * 1.0f) + (dirArriba * 0.5f);

        GameObject efecto = Instantiate(prefab, posicion, refTransform.rotation);

        // Escalar el efecto dinámicamente en proporción al tamaño actual de la
        // raqueta/jefe (crece con cada punto/fase de Colossus).
        efecto.transform.localScale *= CalcularFactorEscalaJefe();

        // Si el efecto es un Particle System, forzar una duración CORTA de 0.5 segundos
        // para que el efecto sea dinámico y no se quede estancado en pantalla.
        ParticleSystem ps = efecto.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            // ParticleSystem.main devuelve la estructura MainModule POR VALOR (CS1612):
            // usar una variable local intermedia para modificar startLifetime.
            var mainModule = ps.main;
            mainModule.startLifetime = 0.5f; // Duración corta y dinámica (0.5s)
        }

        // Limpieza rápida del GameObject temporal del efecto (0.6s).
        Destroy(efecto, 0.6f);
    }

    /// <summary>
    /// Devuelve el factor de escala relativo del jefe/raqueta respecto a su tamaño
    /// base: 1.0 en el tamaño inicial y > 1 conforme Colossus crece con los puntos
    /// (escalaInicialRaqueta → escalaMaximaRaqueta). Se usa para escalar los efectos
    /// visuales (onda de choque, partículas) en proporción al tamaño actual del jefe.
    /// </summary>
    private float CalcularFactorEscalaJefe()
    {
        // La raqueta es el objeto que realmente crece: su localScale es un valor
        // ABSOLUTO interpolado entre escalaInicialRaqueta y escalaMaximaRaqueta.
        // El ratio entre la escala actual y la base es el multiplicador del efecto.
        if (raquetaTransform != null && escalaOriginalGuardada && Mathf.Abs(escalaOriginalRaqueta.x) > 0.0001f)
        {
            return Mathf.Max(1f, raquetaTransform.localScale.x / Mathf.Abs(escalaOriginalRaqueta.x));
        }

        // Fallback: escala del propio jefe (si el diseñador la ajusta en el Inspector).
        return Mathf.Max(1f, Mathf.Abs(transform.localScale.x));
    }

    /// <summary>
    /// Sobrescribe PuedeResistir de RaquetaGolpe para que la raqueta de Colossus
    /// aguante los golpes veloces (modo épico/ultra) MIENTRAS tenga cargas de
    /// inmunidad/resistencia (rebotesResistenciaRestantes > 0).
    /// PingPongBall consulta raquetaGolpe.PuedeResistir(currentSpeed) ANTES del
    /// impacto (HandleRacketCollision): si no se sobrescribiera, la lógica
    /// genérica destruiría la raqueta colosal de inmediato al comparar la
    /// velocidad directa de la pelota contra velocidadDestruccion, sin consumir
    /// las cargas de inmunidad.
    /// </summary>
    public override bool PuedeResistir(float velocidadPelota)
    {
        // Si el combate no está activo, mantener el comportamiento regular.
        if (!combateActivo)
            return base.PuedeResistir(velocidadPelota);

        // 1. Si la pelota viene a velocidad NORMAL (por debajo del umbral de
        //    shockwave, p. ej. < 100f en el saque o en peloteos lentos), la
        //    raqueta del jefe SIEMPRE aguanta: ni consume cargas de inmunidad
        //    ni se destruye. La mecánica de durabilidad/rotura queda reservada
        //    exclusivamente para los intercambios a alta velocidad (>= 100f).
        if (velocidadPelota < umbralVelocidadShockwave)
        {
            return true;
        }

        // 2. Solo si está en modo velocidad/Shockwave (>= 100f), consume
        //    resistencia: UNA carga por impacto de alta velocidad.
        if (rebotesResistenciaRestantes > 0)
        {
            rebotesResistenciaRestantes--; // Consumir una carga
            golpeAbsorbidoEnEsteImpacto = true; // Evitar doble consumo si luego se llama a RegistrarGolpeEnRaqueta

            // Efecto visual de inmunidad en escena (si está asignado) + partículas.
            if (efectoInmunidad != null) efectoInmunidad.Play();
            EfectoParticulas(prefabParticulasFortaleza); // Efecto visual de impacto absorbido

            // Reproducir sonido de absorción/fortaleza de Colossus de forma segura
            // (AudioSource temporal 3D: no se corta aunque la raqueta se destruya).
            ReproducirSonidoSeguro(sonidoAbsorcionFortaleza);

            Debug.Log($"[BossColossus] Impacto a alta velocidad ({velocidadPelota:F1}) ABSORBIDO. Cargas restantes: {rebotesResistenciaRestantes}");

            // Si fue el último consumo, la inmunidad se considera agotada.
            if (rebotesResistenciaRestantes == 0)
            {
                estado = EstadoColossus.Normal;
                AplicarEscalaBaseAcumulada();
                Debug.Log("[BossColossus] Inmunidad agotada. La próxima rotura será definitiva (sin cargas).");
            }

            return true;
        }

        // 3. Se agotan las cargas en modo velocidad (>= 100f): la raqueta se
        //    rompe (punto para el jugador). Sonido de rotura (seguro, 3D,
        //    independiente del estado del objeto) + destrucción visual de la
        //    raqueta CPU (Destruir) para que la regeneración posterior funcione.
        ReproducirSonidoSeguro(sonidoRupturaRaqueta);

        if (gameManager != null && gameManager.golpeRaquetaCPU != null)
            gameManager.golpeRaquetaCPU.ForzarDestruccion();
        return false;
    }

    // ──────────────────────────────────────────────
    // Puntos de Colossus — avance de fase
    // ──────────────────────────────────────────────

    /// <summary>
    /// Invocado por GameManager (RegisterPoint, evento de punto del JUGADOR) cuando
    /// el jugador logra anotarle un punto a Colossus. Solo entonces el jefe crece:
    /// avanza de fase, recalcula la escala base (Vector3.Lerp por puntos del
    /// jugador), rearma la inmunidad por puntos y re-evalúa el centrado.
    /// Cuando anota la CPU (CpuScore++) NO se invoca: Colossus no se refuerza
    /// con los puntos del rival.
    /// </summary>
    public override void OnPuntoDelJugador()
    {
        // Sonido de cambio de fase / crecimiento colosal al recibir un punto del JUGADOR.
        ReproducirSonidoSeguro(sonidoCambioDeFase);

        Debug.Log($"[BossColossus] Recibió un punto del JUGADOR en Fase {faseActual}. Escala y resistencia aumentadas.");
        AvanzarFase();

        // El crecimiento es permanente: al recibir un punto del jugador, recalcular
        // la escala base acumulada, rearmar la inmunidad con N = nuevo puntaje del
        // JUGADOR y re-evaluar el centrado (bloqueo lateral) de la raqueta.
        RecalcularEscalaBasePorPuntaje();
        ReiniciarInmunidad();
        EvaluarCentradoRaqueta();
    }

    /// <summary>Alias por compatibilidad (antiguos nombres del evento).</summary>
    public void OnJefeAnotaPunto()
    {
        OnPuntoDelJugador();
    }

    /// <summary>Alias por compatibilidad (antiguos nombres del evento).</summary>
    public void OnColossusAnotaPunto()
    {
        OnPuntoDelJugador();
    }

    // ──────────────────────────────────────────────
    // Limpieza al destruir el objeto
    // ──────────────────────────────────────────────

    private void OnDestroy()
    {
        // Destruir la instancia dinámica de la skin si existe (evita basura en memoria)
        if (instanciaSkinActual != null)
        {
            Destroy(instanciaSkinActual);
            instanciaSkinActual = null;
        }
    }
}