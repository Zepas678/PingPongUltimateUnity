using UnityEngine;
using System.Collections;

/// <summary>
/// Jefe Zeus: controla los rayos.
/// Fase 3: detecta cuándo la pelota se dirige hacia el jugador y ya cruzó
/// el centro de la mesa, instancia un rayo que sigue a la pelota durante
/// un tiempo de carga, y al completarse teletransporta la pelota delante
/// del jugador sin modificar su velocidad.
/// La detección usa referencias de la mesa, no la posición del modelo del jefe.
/// </summary>
public class BossZeus : BossController
{
    /// <summary>Instancia única referenciada por PingPongBall para notificar los golpes de CPU.</summary>
    public static BossZeus Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    // ── AudioSource que reproduce el sonido de la habilidad ──
    private AudioSource audioSource;

    /// <summary>
    /// Reproduce sonidoRayo de forma segura (PlayOneShot). Si sonidoRayo es null
    /// o no hay AudioSource, no hace nada y no produce errores.
    /// </summary>
    private void ReproducirSonidoRayo()
    {
        if (sonidoRayo == null) return;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (audioSource != null)
            audioSource.PlayOneShot(sonidoRayo);
    }

    [Header("Configuración de Zeus")]
    [Tooltip("Tiempo entre ataques de rayo.")]
    public float tiempoEntreAtaques = 3f;

    [Tooltip("Distancia frente al jugador donde aparece el rayo.")]
    public float distanciaFrenteJugador = 2f;

    [Tooltip("Prefab del rayo que se instancia al activar la habilidad. Debe contener un LineRenderer.")]
    public GameObject efectoRayoPrefab;

    [Tooltip("Tiempo de carga del rayo antes de teletransportar la pelota.")]
    public float tiempoCargaRayo = 0.6f;

    [Tooltip("Sonido del rayo (se asignará después).")]
    public AudioClip sonidoRayo;

    [Header("Teletransporte")]
    [Tooltip("Offset en el espacio de la mesa: X = transversal (izquierda/derecha), Y = altura, Z = longitudinal (frente/atrás).")]
    public Vector3 offsetTeletransporte;

    [Header("Efectos de teletransporte")]
    [Tooltip("Modo épico: reduce la espera de la estrella a 1 segundo.")]
    public bool esModoEpico;

    [Tooltip("Prefab de la estrella que aparece en la posición destino ANTES del teletransporte.")]
    public GameObject prefabEstrella;

    [Tooltip("Prefab del efecto de aparición que se muestra DESPUÉS del teletransporte.")]
    public GameObject prefabAparicion;

    [Header("Referencias de la mesa")]
    [Tooltip("Punto central de la mesa. La detección usa este punto, no la posición del modelo del jefe.")]
    public Transform centroMesa;

    [Tooltip("Punto de referencia del lado del jugador.")]
    public Transform ladoJugador;

    [Tooltip("Punto de referencia del lado de la CPU (opcional).")]
    public Transform ladoCPU;

    // ── Estado interno ──
    /// <summary>Estados posibles de la habilidad de Zeus.</summary>
    private enum EstadoZeus
    {
        Listo,
        Recargando
    }

    /// <summary>Estado actual de la habilidad.</summary>
    private EstadoZeus estado = EstadoZeus.Listo;

    /// <summary>Momento (Time.time) del último ataque/detección registrada.</summary>
    private float ultimoAtaqueTiempo = -Mathf.Infinity;

    // ── Estado del rayo ──
    /// <summary>Instancia activa del rayo.</summary>
    private GameObject rayoInstanciado;

    /// <summary>LineRenderer del rayo instanciado.</summary>
    private LineRenderer lineRendererRayo;

    /// <summary>Indica si el rayo está en su fase de carga.</summary>
    private bool rayoActivo = false;

    /// <summary>Momento (Time.time) en que se instanció el rayo.</summary>
    private float rayoInicioTiempo = 0f;

    // ── Configuración del rayo zigzag ──
    /// <summary>Cantidad de puntos del LineRenderer (P0=Zeus, P7=Pelota).</summary>
    private const int PUNTOS_RAYO = 8;

    /// <summary>Amplitud del desplazamiento lateral del zigzag.</summary>
    private float amplitudZigzag = 2.5f;

    /// <summary>Intervalo en segundos para regenerar el patrón del rayo.</summary>
    private float intervaloRegeneracion = 0.05f;

    /// <summary>Momento (Time.time) del último patrón generado.</summary>
    private float ultimoPatronTiempo = 0f;

    /// <summary>Desplazamientos laterales actuales de los puntos intermedios.</summary>
    private Vector3[] desplazamientosZigzag;

    // ── Fase de finalización del rayo ──
    /// <summary>Duración breve en la que el rayo permanece visible tras el teletransporte.</summary>
    private const float TIEMPO_FINALIZAR_RAYO = 0.1f;

    /// <summary>Indica si el rayo está en la breve fase posterior al teletransporte.</summary>
    private bool finalizandoRayo = false;

    /// <summary>Momento (Time.time) en que comenzó la fase de finalización.</summary>
    private float finalizarRayoTiempo = 0f;

    /// <summary>Protección: impide iniciar múltiples secuencias de teletransporte.</summary>
    private bool teletransporteEnProgreso = false;

    /// <summary>Indica que la estrella ya está visible esperando el SEGUNDO golpe real de la CPU.</summary>
    private bool estrellaEsperandoGolpe = false;

    /// <summary>Instancia de la estrella de la Fase 1, creada en la posición REAL.</summary>
    private GameObject estrellaPendiente = null;

    /// <summary>Posición REAL del teletransporte, calculada en la Fase 1 y reutilizada en la Fase 2.</summary>
    private Vector3 nuevaPosicionPendiente;

    // ── Diagnóstico temporal ──
    /// <summary>Solo para diagnóstico: expone estrellaEsperandoGolpe a otros scripts.</summary>
    public bool EstrellaEsperandoGolpeDiag => estrellaEsperandoGolpe;

    /// <summary>Solo para diagnóstico: expone teletransporteEnProgreso a otros scripts.</summary>
    public bool TeletransporteEnProgresoDiag => teletransporteEnProgreso;

    /// <summary>Flanco: la pelota ya venía viajando hacia el jugador en el frame anterior.</summary>
    private bool pelotaYaViajabaHaciaJugador = false;

    // ── Diagnóstico temporal: seguimiento post-teletransporte ──
    /// <summary>Posición destino del último teletransporte (para seguimiento).</summary>
    private Vector3 ultimaPosicionTeleDiag;

    /// <summary>Frames restantes de seguimiento de posición tras el teletransporte.</summary>
    private int framesSegumientoDiag = 0;

    // ──────────────────────────────────────────────
    void Update()
    {
        // ── Seguimiento temporal post-teletransporte ──
        // Registra si rb.position se mantiene en nuevaPosicion o lo mueven.
        if (framesSegumientoDiag > 0)
        {
            framesSegumientoDiag--;
            Rigidbody rbDiag = ball != null ? ball.GetComponent<Rigidbody>() : null;
            Debug.Log($"[BossZeus] DIAG SEGUIMIENTO: frame={Time.frameCount} | rb.position={(rbDiag != null ? rbDiag.position.ToString() : "N/A")} | nuevaPosicion={ultimaPosicionTeleDiag} | coincide={(rbDiag != null && rbDiag.position == ultimaPosicionTeleDiag)} | velocidad={(rbDiag != null ? rbDiag.linearVelocity.ToString() : "N/A")}");
        }

        // Solo actuar durante un combate activo
        if (!combateActivo) return;

        // Sin pelota no se puede mantener el rayo ni detectar
        if (ball == null) return;

        // Fase de finalización: mantener el rayo visible brevemente tras el teletransporte
        if (finalizandoRayo)
        {
            ActualizarRayoFinalizacion();
            return;
        }

        // Si el rayo está cargando, actualizarlo y esperar
        if (rayoActivo)
        {
            ActualizarRayo();
            return;
        }

        // Sin jugador referenciado no se puede detectar
        if (jugador == null) return;

        DetectarPelotaHaciaJugador();
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Comprueba si la pelota se dirige hacia el jugador y ya cruzó
    /// aproximadamente el centro de la mesa. Usa la posición real del
    /// jugador y la dirección de la pelota en lugar de un eje fijo.
    /// </summary>
    private void DetectarPelotaHaciaJugador()
    {
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb == null) return;

        // Referencias de la mesa necesarias para la detección
        if (centroMesa == null || ladoJugador == null) return;

        // Eje de la mesa: desde el centro hacia el lado del jugador.
        // Usa referencias de la mesa, no la posición del modelo del jefe.
        Vector3 desdeCentroHaciaJugador = (ladoJugador.position - centroMesa.position);
        if (desdeCentroHaciaJugador.sqrMagnitude < 0.0001f) return;
        Vector3 ejeMesa = desdeCentroHaciaJugador.normalized;

        // Dirección actual de la pelota
        Vector3 velocidad = rb.linearVelocity;
        if (velocidad.sqrMagnitude < 0.0001f) return;
        Vector3 direccionPelota = velocidad.normalized;

        // 0) Condición obligatoria de dirección: Zeus solo puede iniciar la habilidad
        //    cuando la pelota viaja hacia el lado positivo (X > 0, hacia el Player).
        //    Si la pelota va hacia la CPU (X <= 0), la habilidad no comienza.
        if (rb.linearVelocity.x <= 0f)
        {
            pelotaYaViajabaHaciaJugador = false;
            return;
        }

        // 1) ¿La pelota se dirige hacia el lado del jugador?
        //    Producto punto > 0 significa que la velocidad apunta hacia el lado del jugador.
        bool pelotaHaciaJugador = Vector3.Dot(direccionPelota, ejeMesa) > 0f;

        if (!pelotaHaciaJugador)
        {
            pelotaYaViajabaHaciaJugador = false;
            return;
        }

        // 2) ¿Ya cruzó aproximadamente el centro de la mesa?
        //    Proyección de la pelota relativa al centro de la mesa.
        //    > 0 significa que ya está del lado del jugador.
        float proyeccionDesdeCentro = Vector3.Dot(ball.transform.position - centroMesa.position, ejeMesa);
        if (proyeccionDesdeCentro < 0f)
        {
            // Aún no cruzó el centro: no es una aproximación válida todavía.
            pelotaYaViajabaHaciaJugador = false;
            return;
        }

        // 2b) FLANCO de dirección: solo permitir UNA activación por cada nueva
        //     trayectoria válida hacia el jugador. La marca se pone DESPUÉS de
        //     verificar que cruzó el centro, para no consumir la oportunidad antes.
        if (pelotaYaViajabaHaciaJugador)
            return;
        pelotaYaViajabaHaciaJugador = true;

        // 3) Estado de la habilidad
        if (estado == EstadoZeus.Recargando)
        {
            // Cooldown efectivo según el modo (mismos valores que en CASO 1 de
            // ActivarHabilidadDesdeGolpeCPU — objetivo: 3-4 TP reales por ronda).
            // - Normal: 30% del cooldown base (≈1.0 s).
            // - Épico: 60% del cooldown base (≈1.8 s).
            float cooldownEfectivo = esModoEpico ? tiempoEntreAtaques * 0.6f : tiempoEntreAtaques * 0.3f;

            // Si ya pasó el tiempo de recarga, volver a estar Listo
            if (Time.time >= ultimoAtaqueTiempo + cooldownEfectivo)
                estado = EstadoZeus.Listo;

            return;
        }

        // 4) La habilidad ya NO se activa aquí.
        //    Ahora se activa únicamente desde ActivarHabilidadDesdeGolpeCPU(),
        //    cuando la raqueta de la CPU golpea realmente la pelota.
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Teletransporta la pelota utilizando su Rigidbody,
    /// conservando completamente su velocidad y dirección.
    /// La posición final se calcula con los ejes de la mesa
    /// (centroMesa, ladoJugador, ladoCPU), nunca con la rotación del jugador.
    /// </summary>
    private void EjecutarTeletransporte()
    {
        // ── Diagnóstico: ¿cuántas veces se ejecuta por habilidad? ──
        Debug.Log($"[BossZeus] DIAG: EjecutarTeletransporte() frame={Time.frameCount} | time={Time.time:F3} | estado={estado} | rayoActivo={rayoActivo}");

        if (ball == null) return;

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb == null) return;

        // Calcular la posición destino UNA sola vez con la fórmula actual
        Vector3 nuevaPosicion = CalcularPosicionTeletransporte();

        // Mover mediante el Rigidbody para mantener una interacción
        // correcta con la física.
        rb.position = nuevaPosicion;

        // ── Diagnóstico DESPUÉS del teletransporte ──
        Debug.Log($"[BossZeus] DIAG DESPUES: frame={Time.frameCount} | rb.position={rb.position} | ball.position={ball.transform.position} | ball.localPosition={ball.transform.localPosition}");

        // NO se modifica rb.linearVelocity: la pelota conserva exactamente
        // la misma velocidad y dirección que tenía antes del teletransporte.

        Debug.Log($"[BossZeus] Teletransporte ejecutado. Destino: {nuevaPosicion}");
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Calcula la posición destino del teletransporte usando EXACTAMENTE la fórmula
    /// actual (zonas izquierda/centro/derecha sobre el eje transversal).
    /// Se calcula UNA sola vez y se reutiliza para la estrella y el teletransporte.
    /// </summary>
    private Vector3 CalcularPosicionTeletransporte()
    {
        // Referencias de la mesa necesarias para calcular la posición
        if (centroMesa == null || ladoJugador == null || ladoCPU == null)
        {
            Debug.LogWarning("[BossZeus] Faltan referencias de la mesa para el teletransporte.");
            return ball != null ? ball.transform.position : Vector3.zero;
        }

        // Eje longitudinal de la mesa: desde el lado CPU hacia el lado jugador.
        Vector3 ejeLongitudinal = (ladoJugador.position - ladoCPU.position);
        if (ejeLongitudinal.sqrMagnitude < 0.0001f) return ball.transform.position;
        ejeLongitudinal.Normalize();

        // Eje transversal de la mesa (ancho, corresponde a Z).
        Vector3 ejeTransversal = Vector3.Cross(ejeLongitudinal, Vector3.up);
        if (ejeTransversal.sqrMagnitude < 0.0001f) return ball.transform.position;
        ejeTransversal.Normalize();

        // A) Posición longitudinal (X = largo de la mesa)
        Vector3 posicionLongitudinal = ladoJugador.position
                                     + ejeLongitudinal * distanciaFrenteJugador
                                     + ejeLongitudinal * offsetTeletransporte.z;

        // B) Posición lateral (Z = izquierda/centro/derecha)
        float desplazamientoLateral = CalcularDesplazamientoLateral(ejeTransversal);

        // ── ELECCIÓN DE ZONA EVITANDO LA TRAYECTORIA ACTUAL ──
        // Proyectamos la velocidad de la pelota sobre el eje transversal (Z):
        //   - componente claramente negativa → viaja hacia IZQUIERDA (zona 0)
        //   - componente claramente positiva → viaja hacia DERECHA (zona 2)
        //   - componente ~0 (pelota en -X/+X sin deriva lateral) → CENTRO (zona 1)
        // La estrella NUNCA se coloca en la zona hacia la que la pelota se dirige;
        // se elige aleatoriamente entre las otras dos zonas para que el
        // teletransporte impulse la pelota hacia una dirección distinta.
        Rigidbody rbSel = ball != null ? ball.GetComponent<Rigidbody>() : null;
        float componenteTransversal = (rbSel != null)
            ? Vector3.Dot(rbSel.linearVelocity, ejeTransversal)
            : 0f;

        const float umbralZona = 0.1f;
        int zonaPelota;
        if (componenteTransversal < -umbralZona) zonaPelota = 0;      // izquierda
        else if (componenteTransversal > umbralZona) zonaPelota = 2;  // derecha
        else zonaPelota = 1;                                         // centro

        int zona = Random.Range(0, 3);
        while (zona == zonaPelota) // no elegir la dirección por la que viene (va) la pelota
            zona = Random.Range(0, 3);

        Vector3 posicionLateral;
        if (zona == 0)
            posicionLateral = centroMesa.position - ejeTransversal * desplazamientoLateral;
        else if (zona == 2)
            posicionLateral = centroMesa.position + ejeTransversal * desplazamientoLateral;
        else
            posicionLateral = centroMesa.position;

        // C) Altura (Y)
        float altura = centroMesa.position.y + offsetTeletransporte.y;

        // Posición final combinada
        Vector3 nuevaPosicion = new Vector3(
            posicionLongitudinal.x,
            altura,
            posicionLateral.z
        );

        // ── GARANTIZAR QUE LA NUEVA POSICIÓN SEA DIFERENTE ──
        // Si la posición calculada está demasiado cerca de la posición actual
        // de la pelota, cambiar a la zona opuesta:
        //   - izquierda -> derecha
        //   - derecha   -> izquierda
        //   - centro    -> elegir aleatoriamente izquierda o derecha (NUNCA centro)
        const float distanciaMinima = 5f;
        Vector3 posicionActual = ball != null ? ball.transform.position : Vector3.zero;
        if (Vector3.Distance(nuevaPosicion, posicionActual) < distanciaMinima)
        {
            if (zona == 0)
                posicionLateral = centroMesa.position + ejeTransversal * desplazamientoLateral;
            else if (zona == 2)
                posicionLateral = centroMesa.position - ejeTransversal * desplazamientoLateral;
            else // zona == 1 (centro): elegir aleatoriamente izquierda o derecha
                posicionLateral = (Random.value < 0.5f)
                    ? centroMesa.position - ejeTransversal * desplazamientoLateral
                    : centroMesa.position + ejeTransversal * desplazamientoLateral;

            nuevaPosicion = new Vector3(
                posicionLongitudinal.x,
                altura,
                posicionLateral.z
            );
        }

        // ── Diagnóstico ANTES del teletransporte ──
        Rigidbody rbVel = ball != null ? ball.GetComponent<Rigidbody>() : null;
        Debug.Log($"[BossZeus] DIAG ANTES: frame={Time.frameCount} | ball.antes={ball.transform.position} | velocidad={(rbVel != null ? rbVel.linearVelocity.ToString() : "N/A")} | ladoJugador={ladoJugador.position} | ladoCPU={ladoCPU.position} | centroMesa={centroMesa.position} | ejeLongitudinal={ejeLongitudinal} | ejeTransversal={ejeTransversal} | posLongitudinal={posicionLongitudinal} | posLateral={posicionLateral} | altura={altura} | nuevaPosicion={nuevaPosicion} | zona={zona}");

        return nuevaPosicion;
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Calcula el desplazamiento lateral máximo seguro para el teletransporte,
    /// usando el ancho real de la mesa (objeto "Tablanube" y su BoxCollider).
    /// Usa el 35% de la mitad del ancho para mantener la pelota dentro de los límites.
    /// </summary>
    private float CalcularDesplazamientoLateral(Vector3 ejeTransversal)
    {
        // Buscar específicamente el objeto "Tablanube" (la mesa del mapa de nubes).
        GameObject tablanube = GameObject.Find("Tablanube");
        if (tablanube != null)
        {
            BoxCollider boxCollider = tablanube.GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                // bounds.size.z ya incluye la escala del Transform (51 * 0.6929 ≈ 35.34).
                float anchoRealZ = boxCollider.bounds.size.z;
                if (anchoRealZ > 0.01f)
                {
                    // 35% de la mitad del ancho: margen de seguridad dentro de la mesa.
                    return (anchoRealZ / 2f) * 0.35f;
                }
            }
        }

        // Fallback: comportamiento anterior usando el valor del Inspector.
        return Mathf.Abs(offsetTeletransporte.x);
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Instancia el prefab del rayo y comienza la fase de carga.
    /// Si no hay prefab o no tiene LineRenderer, ejecuta el teletransporte inmediato.
    /// </summary>
    private void IniciarRayo()
    {
        // ── Diagnóstico: ¿el prefab es null? ──
        if (efectoRayoPrefab == null)
        {
            Debug.LogWarning("[BossZeus] DIAG: efectoRayoPrefab es NULL. Teletransporte inmediato.");
            EjecutarTeletransporte();
            return;
        }
        Debug.Log($"[BossZeus] DIAG: efectoRayoPrefab asignado: {efectoRayoPrefab.name}");

        // Instanciar el rayo en la posición de Zeus
        rayoInstanciado = Instantiate(efectoRayoPrefab, transform.position, Quaternion.identity);

        // ── Diagnóstico: ¿se instanció correctamente? ──
        Debug.Log($"[BossZeus] DIAG: Rayo instanciado: {(rayoInstanciado != null ? rayoInstanciado.name : "NULL")}");
        Debug.Log($"[BossZeus] DIAG: Instantiate() time={Time.time:F3}");
        Debug.Log($"[BossZeus] DIAG: rayoInstanciado != null: {rayoInstanciado != null}");
        Debug.Log($"[BossZeus] DIAG: activeSelf = {rayoInstanciado.activeSelf}");
        Debug.Log($"[BossZeus] DIAG: activeInHierarchy = {rayoInstanciado.activeInHierarchy}");

        // Obtener el LineRenderer del prefab instanciado
        lineRendererRayo = rayoInstanciado.GetComponent<LineRenderer>();

        // ── Diagnóstico: ¿encontró el LineRenderer? ──
        Debug.Log($"[BossZeus] DIAG: LineRenderer encontrado: {(lineRendererRayo != null ? "SI" : "NO")}");
        if (lineRendererRayo == null)
        {
            Debug.LogWarning("[BossZeus] El prefab del rayo no tiene LineRenderer. Teletransporte inmediato.");
            Destroy(rayoInstanciado);
            rayoInstanciado = null;
            EjecutarTeletransporte();
            return;
        }

        // ── Diagnóstico: confirmar que el prefab instanciado es el correcto ──
        Debug.Log($"[BossZeus] DIAG: efectoRayoPrefab.name = {efectoRayoPrefab.name}");
        Debug.Log($"[BossZeus] DIAG: rayoInstanciado.name = {rayoInstanciado.name}");
        Debug.Log($"[BossZeus] DIAG: lineRendererRayo.name = {lineRendererRayo.name}");
        Debug.Log($"[BossZeus] DIAG: sharedMaterial = {(lineRendererRayo.sharedMaterial != null ? lineRendererRayo.sharedMaterial.name : "NULL")}");
        Debug.Log($"[BossZeus] DIAG: widthMultiplier = {lineRendererRayo.widthMultiplier}");
        Debug.Log($"[BossZeus] DIAG: useWorldSpace = {lineRendererRayo.useWorldSpace}");

        // ── Diagnóstico: componentes del prefab instanciado ──
        Debug.Log($"[BossZeus] DIAG: Componentes — LineRenderer: {(rayoInstanciado.GetComponent<LineRenderer>() != null ? "EXISTE" : "NO")} | AudioSource: {(rayoInstanciado.GetComponent<AudioSource>() != null ? "EXISTE" : "NO")} | ParticleSystem: {(rayoInstanciado.GetComponent<ParticleSystem>() != null ? "EXISTE" : "NO")} | Animator: {(rayoInstanciado.GetComponent<Animator>() != null ? "EXISTE" : "NO")}");

        // Configurar los puntos del rayo zigzag (P0=Zeus, P7=Pelota)
        lineRendererRayo.positionCount = PUNTOS_RAYO;
        desplazamientosZigzag = new Vector3[PUNTOS_RAYO - 2]; // P1 a P6
        GenerarPatronZigzag();
        AplicarPosicionesRayo();

        // ── Diagnóstico: ¿positionCount vale 8? ──
        Debug.Log($"[BossZeus] DIAG: positionCount = {lineRendererRayo.positionCount}");

        // ── Diagnóstico: estado del LineRenderer ──
        Debug.Log($"[BossZeus] DIAG: Posición inicial (GetPosition(0)) = {lineRendererRayo.GetPosition(0)}");
        Debug.Log($"[BossZeus] DIAG: Posición final (GetPosition(1)) = {lineRendererRayo.GetPosition(1)}");
        Debug.Log($"[BossZeus] DIAG: startWidth = {lineRendererRayo.startWidth}");
        Debug.Log($"[BossZeus] DIAG: endWidth = {lineRendererRayo.endWidth}");
        Debug.Log($"[BossZeus] DIAG: enabled = {lineRendererRayo.enabled}");

        rayoActivo = true;
        rayoInicioTiempo = Time.time;
        Debug.Log($"[BossZeus] DIAG: IniciarRayo() fin time={Time.time:F3}");

        // ── Diagnóstico: verificación de posiciones ──
        Debug.Log($"[BossZeus] DIAG: BossZeus.transform.position = {transform.position}");
        Debug.Log($"[BossZeus] DIAG: RayoZeus(Clone).transform.position = {(rayoInstanciado != null ? rayoInstanciado.transform.position.ToString() : "NULL")}");
        Debug.Log($"[BossZeus] DIAG: ball.transform.position = {ball.transform.position}");
        Debug.Log($"[BossZeus] DIAG: P0 (GetPosition(0)) = {lineRendererRayo.GetPosition(0)}");
        Debug.Log($"[BossZeus] DIAG: P7 (GetPosition(7)) = {lineRendererRayo.GetPosition(7)}");
        Debug.Log($"[BossZeus] DIAG: useWorldSpace = {lineRendererRayo.useWorldSpace}");
        Debug.Log($"[BossZeus] DIAG: positionCount = {lineRendererRayo.positionCount}");
        Debug.Log($"[BossZeus] DIAG: BossZeus.transform.position == P0 ? {transform.position == lineRendererRayo.GetPosition(0)}");
        Debug.Log($"[BossZeus] DIAG: ball.transform.position == P7 ? {ball.transform.position == lineRendererRayo.GetPosition(7)}");
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Actualiza el rayo mientras está activo:
    /// el punto 0 sigue a Zeus y el punto 1 sigue a la pelota.
    /// Al cumplirse el tiempo de carga, teletransporta y destruye el rayo.
    /// </summary>
    private void ActualizarRayo()
    {
        // ── Diagnóstico: ¿se ejecuta cada frame? ──
        Debug.Log($"[BossZeus] DIAG: ActualizarRayo() frame={Time.frameCount} | time={Time.time:F3} | rayoActivo={rayoActivo} | lineRenderer={(lineRendererRayo != null ? "OK" : "NULL")} | activeSelf={(rayoInstanciado != null ? rayoInstanciado.activeSelf.ToString() : "N/A")} | activeInHierarchy={(rayoInstanciado != null ? rayoInstanciado.activeInHierarchy.ToString() : "N/A")}");

        // Regenerar el patrón del zigzag periódicamente para dar sensación de electricidad
        if (Time.time >= ultimoPatronTiempo + intervaloRegeneracion)
        {
            GenerarPatronZigzag();
            ultimoPatronTiempo = Time.time;
        }

        // Aplicar las posiciones: P0 sigue a Zeus, P7 sigue a la pelota, P1-P6 zigzag
        if (lineRendererRayo != null)
            AplicarPosicionesRayo();

        // Completada la carga → iniciar secuencia de teletransporte (una sola vez)
        if (Time.time >= rayoInicioTiempo + tiempoCargaRayo)
        {
            Debug.Log($"[BossZeus] DIAG: Carga completada en frame={Time.frameCount} (tiempo={Time.time:F3} >= inicio={rayoInicioTiempo:F3} + carga={tiempoCargaRayo:F3})");

            // Protección: iniciar la secuencia solo una vez (la condición seguirá
            // siendo verdadera mientras la coroutine espera 1 o 2 segundos).
            if (!teletransporteEnProgreso)
            {
                teletransporteEnProgreso = true;

                // Calcular la posición destino UNA sola vez con la fórmula actual
                Vector3 nuevaPosicion = CalcularPosicionTeletransporte();

                // Ocultar el rayo durante la espera de la estrella
                rayoActivo = false;
                if (rayoInstanciado != null)
                {
                    rayoInstanciado.SetActive(false);
                    Debug.Log($"[BossZeus] DIAG RAYO OCULTO: frame={Time.frameCount} | SetActive(false) ejecutado");
                }

                // Iniciar la secuencia: estrella → espera → teletransporte → aparición → rayo
                StartCoroutine(CoroutineSecuenciaTeletransporte(nuevaPosicion));
            }
        }
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Secuencia de teletransporte: muestra la estrella en el destino, espera,
    /// teletransporta con la MISMA posición calculada, muestra la aparición,
    /// reconstruye el rayo y finaliza.
    /// </summary>
    private IEnumerator CoroutineSecuenciaTeletransporte(Vector3 nuevaPosicion)
    {
        // ── Diagnóstico temporal: inicio de la coroutine ──
        Debug.Log($"[BossZeus] DIAG COROUTINE INICIO: frame={Time.frameCount} | nuevaPosicion={nuevaPosicion} | rayoActivo={rayoActivo} | rayoActiveSelf={(rayoInstanciado != null ? rayoInstanciado.activeSelf.ToString() : "N/A")}");

        // La estrella YA fue creada en la Fase 1 (ActivarHabilidad).
        // Aquí solo se referencia para destruirla antes del TP.
        GameObject estrella = estrellaPendiente;
        Debug.Log($"[DIAG BOSSZEUS] COROUTINE ARRANCADA | frame={Time.frameCount} | combateActivo={combateActivo} | estado={estado} | teletransporteEnProgreso={teletransporteEnProgreso} | estrellaPendiente={(estrellaPendiente != null ? "EXISTE" : "NULL")} | nuevaPosicion={nuevaPosicion} | enabled={enabled} | activeInHierarchy={gameObject.activeInHierarchy}");

        // La coroutine SOLO se inicia en la FASE 2 (segundo golpe real de la CPU).
        // Aquí se calcula la espera variable con la velocidad JUSTO después del golpe:
        //   reducida: siempre ≈ 1.1 s (lenta 1.1 s, rápida 1.0 s) para TP más frecuente.
        Rigidbody rbEspera = ball != null ? ball.GetComponent<Rigidbody>() : null;
        if (rbEspera == null)
        {
            Debug.Log("[DIAG BOSSZEUS] SALIDA TEMPRANA: rbEspera NULL al arrancar la coroutine. No habrá TP.");
            // Limpieza completa del estado de la habilidad antes de salir
            teletransporteEnProgreso = false;
            estrellaEsperandoGolpe = false;
            if (estrellaPendiente != null)
            {
                Destroy(estrellaPendiente);
                estrellaPendiente = null;
            }
            yield break;
        }

        // La validación de la dirección X ya se hizo en ActivarHabilidadDesdeGolpeCPU()
        // usando velocidadXAntesDelGolpe < 0f. El +X posterior al golpe de la CPU
        // es el comportamiento normal y esperado, por lo que aquí NO se rechaza.
        float velocidadActual = rbEspera.linearVelocity.magnitude;
        float tVelocidad = Mathf.InverseLerp(ball.initialSpeed, ball.maxSpeed, velocidadActual);
        float espera = Mathf.Lerp(1.1f, 1f, tVelocidad);
        yield return new WaitForSeconds(espera);

        // ── Diagnóstico de fin de espera ──
        Debug.Log($"[BossZeus] DIAG ESPERA TERMINADA (2do golpe): frame={Time.frameCount} | espera={espera:F2} | velocidad={velocidadActual:F1}");

        // ── VERIFICACIÓN MÍNIMA DESPUÉS DE LA ESPERA ──
        Rigidbody rb = ball != null ? ball.GetComponent<Rigidbody>() : null;
        if (rb == null)
        {
            Debug.Log("[DIAG BOSSZEUS] SALIDA TEMPRANA: rb NULL tras la espera. No habrá TP.");
            // Limpieza completa del estado de la habilidad antes de salir
            teletransporteEnProgreso = false;
            estrellaEsperandoGolpe = false;
            if (estrellaPendiente != null)
            {
                Destroy(estrellaPendiente);
                estrellaPendiente = null;
            }
            yield break;
        }

        // Velocidad X ACTUAL leída justo después de WaitForSeconds.
        float velocidadXDespuesDeLaEspera = rb.linearVelocity.x;

        // ── VALIDACIÓN TRAS LA ESPERA ──
        // Cancelación SOLO si el combate terminó o la pelota desapareció durante
        // la espera. Un isKinematic con combateActivo=true y ball!=null NO
        // cancela el TP por sí solo (validación equivalente más amplia).
        if (!combateActivo || ball == null)
        {
            Debug.Log($"[BossZeus] TELETRANSPORTE CANCELADO tras la espera: frame={Time.frameCount} | combateActivo={combateActivo} | ball={(ball != null ? "OK" : "NULL")} | isKinematic={rb.isKinematic} | velocidadXDespuesDeLaEspera={velocidadXDespuesDeLaEspera:F2} | destino descartado={nuevaPosicion}");
            if (estrella != null)
            {
                Destroy(estrella);
                estrella = null;
            }
            estrellaPendiente = null;
            teletransporteEnProgreso = false;
            Debug.Log("[DIAG BOSSZEUS] teletransporteEnProgreso liberado a false (combate inactivo o sin pelota).");
            yield break;
        }

        Debug.Log($"[BossZeus] PASÓ isKinematic | velocidadX={velocidadXDespuesDeLaEspera:F2}");

        // La señal fue el SEGUNDO golpe CPU validado con velocidadXAntesDelGolpe < 0f.
        // La espera se redujo a ≈1.1 s y NO se cancela por dirección: aunque la
        // pelota esté en -X al terminar la espera, el teletransporte se ejecuta
        // y su componente X se fuerza a +X justo antes del salto.

        // ── DESTRUIR LA ESTRELLA DE LA FASE 1 ANTES DEL TP ──
        // Ya NO se espera dirección +X/-X: la señal fue el segundo golpe FÍSICO.
        if (estrella != null)
        {
            Destroy(estrella);
            estrella = null;
        }

        estrellaPendiente = null;
        estrellaEsperandoGolpe = false;
        Debug.Log($"[DIAG BOSSZEUS] PUNTO DE NO RETORNO: estrella consumida, a punto de ejecutar el TP | destino={nuevaPosicion} | velocidadXDespuesDeLaEspera={velocidadXDespuesDeLaEspera:F2}");

        // 3. Teletransportar usando EXACTAMENTE la misma posición calculada.
        //    Antes del salto, la componente X de la velocidad se fuerza a +X
        //    (salida hacia el jugador); Y y Z no se modifican.
        // Protección final: no ejecutar el TP si la ronda terminó durante la espera.
        if (!combateActivo || ball == null)
        {
            Debug.Log("[DIAG BOSSZEUS] TP ABORTADO justo antes del salto: combate inactivo o sin pelota.");
            teletransporteEnProgreso = false;
            yield break;
        }
        Debug.Log($"[BossZeus] EJECUTANDO TELETRANSPORTE | destino={nuevaPosicion} | velocidadX={velocidadXDespuesDeLaEspera:F2}");
        Vector3 velocidad = rb.linearVelocity;
        velocidad.x = Mathf.Abs(velocidad.x);
        rb.linearVelocity = velocidad;
        rb.position = nuevaPosicion;
        Physics.SyncTransforms();
        Debug.Log(
            $"[BossZeus] DIAG TP COMPLETO | " +
            $"rb.position={rb.position} | " +
            $"ball.transform.position={ball.transform.position} | " +
            $"velocidad={rb.linearVelocity}"
        );
        Debug.Log($"[BossZeus] DIAG RB POSITION ASIGNADO: frame={Time.frameCount} | rb.position={rb.position} | nuevaPosicion={nuevaPosicion}");

        // Iniciar seguimiento de posición durante 10 frames para detectar si la mueven
        ultimaPosicionTeleDiag = nuevaPosicion;
        framesSegumientoDiag = 10;

        // 4. Efecto de aparición en la posición final real de la pelota
        if (prefabAparicion != null)
        {
            GameObject aparicion = Instantiate(prefabAparicion, rb.position, Quaternion.identity);

            // Activar explícitamente la instancia (el prefab original está inactivo)
            if (aparicion != null)
            {
                aparicion.SetActive(true);
                Destroy(aparicion, 0.5f); // Auto-destrucción para no acumular
            }

            Debug.Log($"[BossZeus] Aparicion: prefab={prefabAparicion.name} | pos={rb.position} | instancia={(aparicion != null ? "OK" : "NULL")} | activeSelf={(aparicion != null ? aparicion.activeSelf.ToString() : "N/A")} | activeInHierarchy={(aparicion != null ? aparicion.activeInHierarchy.ToString() : "N/A")}");

            // Diagnóstico de ParticleSystem en la instancia
            if (aparicion != null)
            {
                ParticleSystem[] sistemas = aparicion.GetComponentsInChildren<ParticleSystem>(true);
                Debug.Log($"[BossZeus] Aparicion: sistemas encontrados={sistemas.Length}");
                foreach (ParticleSystem sistema in sistemas)
                {
                    if (sistema != null)
                        Debug.Log($"[BossZeus] Aparicion: sistema='{sistema.name}' | active={sistema.gameObject.activeSelf} | isPlaying={sistema.isPlaying}");
                }
            }

            // Diagnóstico visual completo de la aparición
            DiagnosticarEfectoVisual(aparicion, "Aparicion");
        }
        else
        {
            Debug.LogWarning("[BossZeus] prefabAparicion es NULL — no se mostró la aparición.");
        }

        // 5. Crear el rayo AHORA (después del teletransporte) como destello.
        //    IniciarRayo() instancia el prefab y aplica P0-P7 usando la NUEVA
        //    posición de la pelota (rb.position).
        IniciarRayo();
        if (rayoInstanciado != null)
            rayoInstanciado.SetActive(true);
        if (lineRendererRayo != null)
        {
            GenerarPatronZigzag();
            AplicarPosicionesRayo();
        }

        // Reproducir el sonido de la habilidad en el momento en que se crea el
        // efecto del rayo (destello tras el teletransporte). No interfiere con
        // la lógica ni el estado de la habilidad.
        ReproducirSonidoRayo();

        // 6. Fase de finalización: el rayo se muestra solo 0.1s como destello
        rayoActivo = false;
        finalizandoRayo = true;
        finalizarRayoTiempo = Time.time;

        // 7. Liberar el bloqueo para que la habilidad pueda volver a ejecutarse
        teletransporteEnProgreso = false;
        Debug.Log($"[DIAG BOSSZEUS] COROUTINE FIN NORMAL: teletransporteEnProgreso liberado a false | frame={Time.frameCount} | combateActivo={combateActivo} | estado={estado} | estrellaPendiente={(estrellaPendiente != null ? "EXISTE" : "NULL")}");
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Mantiene el rayo visible durante el breve intervalo posterior al teletransporte.
    /// P7 sigue la posición actual de la pelota hasta que se llama a FinalizarRayo().
    /// </summary>
    private void ActualizarRayoFinalizacion()
    {
        // Reconstruir el rayo completo para que todos los puntos (P0-P7)
        // sigan la posición actual de la pelota, no solo P7.
        if (lineRendererRayo != null && ball != null)
            AplicarPosicionesRayo();

        // Pasado el pequeño intervalo, destruir el rayo
        if (Time.time >= finalizarRayoTiempo + TIEMPO_FINALIZAR_RAYO)
        {
            finalizandoRayo = false;
            FinalizarRayo();
        }
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Genera desplazamientos laterales aleatorios para los puntos intermedios P1-P6.
    /// El desplazamiento es perpendicular a la dirección Zeus → pelota.
    /// </summary>
    private void GenerarPatronZigzag()
    {
        if (desplazamientosZigzag == null || ball == null) return;

        // Referencias de la mesa necesarias para el eje transversal
        if (ladoJugador == null || ladoCPU == null) return;

        // Eje longitudinal de la mesa (largo, CPU → jugador)
        Vector3 ejeLongitudinal = (ladoJugador.position - ladoCPU.position);
        if (ejeLongitudinal.sqrMagnitude < 0.0001f) return;
        ejeLongitudinal.Normalize();

        // Eje transversal REAL de la mesa (ancho, corresponde a Z)
        Vector3 ejeTransversal = Vector3.Cross(ejeLongitudinal, Vector3.up);
        if (ejeTransversal.sqrMagnitude < 0.0001f) return;
        ejeTransversal.Normalize();

        // Generar desplazamientos irregulares pero controlados para cada punto intermedio.
        // El rayo debe avanzar desde P0 hasta P7; las desviaciones solo deforman la
        // trayectoria base, sin saltos exagerados ni aspecto desordenado.
        float ultimaDireccion = 1f;
        for (int i = 0; i < desplazamientosZigzag.Length; i++)
        {
            // Alternar dirección irregular: el punto tiene 70% de probabilidad de
            // continuar en la dirección opuesta al anterior, para crear zigzag natural.
            if (i > 0 && Random.value < 0.7f)
                ultimaDireccion = -ultimaDireccion;

            // Amplitud variable: mezcla de tamaño grande/medio/pequeño para
            // que no todos los dobleces parezcan iguales, pero sin caos.
            float factor = Random.Range(0.35f, 1.0f);
            float desplazamiento = ultimaDireccion * amplitudZigzag * factor;

            // Pequeña variación vertical controlada.
            float vertical = Random.Range(-0.4f, 0.4f);

            // El desplazamiento lateral se aplica SOLO sobre el eje transversal (Z),
            // sin alterar arbitrariamente la posición X (largo de la mesa).
            desplazamientosZigzag[i] = ejeTransversal * desplazamiento + Vector3.up * vertical;
        }
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Aplica las posiciones al LineRenderer: P0=Zeus, P7=Pelota, P1-P6=zigzag.
    /// </summary>
    private void AplicarPosicionesRayo()
    {
        if (lineRendererRayo == null || ball == null) return;

        // Usar la posición REAL del Rigidbody (rb.position), que refleja
        // inmediatamente el teletransporte, en lugar de ball.transform.position
        // que puede quedar desactualizado tras rb.position = nuevaPosicion.
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        Vector3 posicionRealPelota = rb != null ? rb.position : ball.transform.position;

        // ── P0: origen del rayo desde cerca del CPU ──
        // Usa ladoCPU como referencia principal y desplaza ligeramente
        // hacia centroMesa para que el rayo parezca salir desde la zona del CPU.
        // Fallback a transform.position si ladoCPU es null.
        Vector3 posicionOrigen;
        if (ladoCPU != null)
        {
            posicionOrigen = ladoCPU.position;

            // Pequeño desplazamiento hacia el centro de la mesa.
            if (centroMesa != null)
            {
                Vector3 haciaCentro = centroMesa.position - posicionOrigen;
                haciaCentro.y = 0f;
                if (haciaCentro.sqrMagnitude > 0.0001f)
                    posicionOrigen += haciaCentro.normalized * 2f;
            }
        }
        else
        {
            posicionOrigen = transform.position; // Fallback original
        }

        // P0 = posición de origen (cerca del CPU)
        lineRendererRayo.SetPosition(0, posicionOrigen);

        // P1-P6 = puntos intermedios con desplazamiento zigzag.
        // Interpolan entre posicionOrigen (CPU) y posicionRealPelota.
        for (int i = 1; i < PUNTOS_RAYO - 1; i++)
        {
            float t = (float)i / (PUNTOS_RAYO - 1); // 0.125, 0.25, ..., 0.75
            Vector3 puntoBase = Vector3.Lerp(posicionOrigen, posicionRealPelota, t);

            // Aplicar el desplazamiento lateral del patrón actual
            Vector3 desplazamiento = (desplazamientosZigzag != null && i - 1 < desplazamientosZigzag.Length)
                ? desplazamientosZigzag[i - 1]
                : Vector3.zero;

            lineRendererRayo.SetPosition(i, puntoBase + desplazamiento);
        }

        // P7 = posición real de la pelota (usando el Rigidbody)
        lineRendererRayo.SetPosition(PUNTOS_RAYO - 1, posicionRealPelota);

        // ── Diagnóstico temporal: comprobar que P0 quedó cerca del CPU ──
        Debug.Log($"[BossZeus] DIAG ORIGEN: ladoCPU.position={(ladoCPU != null ? ladoCPU.position.ToString() : "NULL")} | posicionOrigen={posicionOrigen} | P0={lineRendererRayo.GetPosition(0)} | P7={lineRendererRayo.GetPosition(PUNTOS_RAYO - 1)}");
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Diagnóstico temporal: muestra los componentes visuales de una instancia de efecto.
    /// </summary>
    private void DiagnosticarEfectoVisual(GameObject instancia, string etiqueta)
    {
        if (instancia == null)
        {
            Debug.Log($"[BossZeus] {etiqueta}: instancia NULL");
            return;
        }

        Debug.Log($"[BossZeus] {etiqueta}: localScale={instancia.transform.localScale} | lossyScale={instancia.transform.lossyScale}");

        Renderer[] renderers = instancia.GetComponentsInChildren<Renderer>(true);
        Debug.Log($"[BossZeus] {etiqueta}: Renderers encontrados={renderers.Length}");

        int lineRenderers = 0;
        int trailRenderers = 0;
        int meshRenderers = 0;
        int spriteRenderers = 0;

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;

            Debug.Log($"[BossZeus] {etiqueta}: Renderer name='{r.gameObject.name}' | tipo={r.GetType().Name} | enabled={r.enabled} | activeInHierarchy={r.gameObject.activeInHierarchy} | bounds.center={r.bounds.center} | bounds.size={r.bounds.size}");

            // Diagnóstico específico del SpriteRenderer
            if (r is SpriteRenderer sr)
            {
                Debug.Log($"[BossZeus] {etiqueta}: SpriteRenderer — sprite={(sr.sprite != null ? sr.sprite.name : "NULL")} | color={sr.color} | alpha={sr.color.a} | sortingLayer={sr.sortingLayerName} | sortingOrder={sr.sortingOrder} | maskInteraction={sr.maskInteraction} | flipX={sr.flipX} | flipY={sr.flipY} | drawMode={sr.drawMode} | material={(sr.material != null ? sr.material.name : "NULL")} | pos={sr.transform.position} | rot={sr.transform.rotation.eulerAngles}");
            }

            if (r is LineRenderer) lineRenderers++;
            if (r is TrailRenderer) trailRenderers++;
            if (r is MeshRenderer) meshRenderers++;
            if (r is SpriteRenderer) spriteRenderers++;
        }

        Debug.Log($"[BossZeus] {etiqueta}: LineRenderer={lineRenderers} | TrailRenderer={trailRenderers} | MeshRenderer={meshRenderers} | SpriteRenderer={spriteRenderers}");
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Destruye el rayo instanciado y limpia el estado del mismo.
    /// </summary>
    private void FinalizarRayo()
    {
        // ── Diagnóstico: ¿se destruye antes de tiempo? ──
        Debug.Log($"[BossZeus] DIAG: FinalizarRayo() frame={Time.frameCount} | time={Time.time:F3} | rayoInstanciado={(rayoInstanciado != null ? "EXISTE" : "NULL")} | rayoActivo={rayoActivo} | activeSelf={(rayoInstanciado != null ? rayoInstanciado.activeSelf.ToString() : "N/A")} | activeInHierarchy={(rayoInstanciado != null ? rayoInstanciado.activeInHierarchy.ToString() : "N/A")}");

        if (rayoInstanciado != null)
            Destroy(rayoInstanciado);

        rayoInstanciado = null;
        lineRendererRayo = null;
        rayoActivo = false;
    }

    // ──────────────────────────────────────────────
    public override void IniciarCombate()
    {
        base.IniciarCombate();

        // Permitir el primer ataque inmediatamente al iniciar el combate
        ultimoAtaqueTiempo = -Mathf.Infinity;
        estado = EstadoZeus.Listo;

        // Limpiar cualquier rastro residual de un combate anterior
        finalizandoRayo = false;
        teletransporteEnProgreso = false;
        pelotaYaViajabaHaciaJugador = false;
        estrellaEsperandoGolpe = false;
        if (estrellaPendiente != null)
        {
            Destroy(estrellaPendiente);
            estrellaPendiente = null;
        }
        FinalizarRayo();

        // ── Diagnóstico de referencias ──
        Debug.Log("[BossZeus] 5. IniciarCombate() ejecutado.");
        Debug.Log($"[BossZeus] Referencias — ball: {(ball != null ? "OK" : "NULL")} | jugador: {(jugador != null ? "OK" : "NULL")} | ladoJugador: {(ladoJugador != null ? "OK" : "NULL")} | centroMesa: {(centroMesa != null ? "OK" : "NULL")}");
    }

    // ──────────────────────────────────────────────
    public override void FinalizarCombate()
    {
        base.FinalizarCombate();

        // Cancelar cualquier coroutine pendiente (p. ej. la secuencia de
        // teletransporte esperando por WaitForSeconds) para que no persista
        // tras salir del combate del jefe.
        StopAllCoroutines();

        estado = EstadoZeus.Listo;

        // Limpiar el rayo y la estrella pendiente si siguen activos
        finalizandoRayo = false;
        teletransporteEnProgreso = false;
        estrellaEsperandoGolpe = false;

        if (estrellaPendiente != null)
        {
            Destroy(estrellaPendiente);
            estrellaPendiente = null;
        }

        FinalizarRayo();

        Debug.Log("[BossZeus] FinalizarCombate() ejecutado.");
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// FASE 1: prepara la habilidad. Calcula y guarda la posición REAL del TP,
    /// crea la estrella y queda esperando el SEGUNDO golpe real de la CPU.
    /// NO inicia la coroutine de teletransporte ni ninguna cuenta regresiva.
    /// </summary>
    public override void ActivarHabilidad()
    {
        base.ActivarHabilidad();

        // Asegurar que no haya rayo residual visible
        rayoActivo = false;
        finalizandoRayo = false;
        if (rayoInstanciado != null)
            FinalizarRayo();

        // Protección: no crear una segunda estrella si ya hay habilidad preparada
        if (estrellaEsperandoGolpe)
        {
            Debug.Log("[DIAG BOSSZEUS] ActivarHabilidad BLOQUEADA: estrellaEsperandoGolpe=true.");
            return;
        }

        // Protección: si ya hay un teletransporte en ejecución, no repetir
        if (teletransporteEnProgreso)
        {
            Debug.Log("[DIAG BOSSZEUS] ActivarHabilidad BLOQUEADA: teletransporteEnProgreso=true.");
            return;
        }

        // 1) Calcular y guardar la posición REAL del teletransporte (una sola vez)
        nuevaPosicionPendiente = CalcularPosicionTeletransporte();

        // 2) Crear la estrella en la posición REAL (sin cambios visuales por ahora)
        if (prefabEstrella != null)
        {
            estrellaPendiente = Instantiate(prefabEstrella, nuevaPosicionPendiente, Quaternion.identity);
            if (estrellaPendiente != null)
            {
                estrellaPendiente.SetActive(true);

                // ── Ajuste de apariencia SOLO de la instancia estrella (igual que antes) ──
                estrellaPendiente.transform.localScale = new Vector3(2f, 2f, 2f);
                estrellaPendiente.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                SpriteRenderer srEstrella = estrellaPendiente.GetComponent<SpriteRenderer>();
                if (srEstrella != null)
                    srEstrella.color = new Color(1f, 0.8f, 0f, 1f);
            }

            Debug.Log($"[BossZeus] FASE 1: estrella creada en {nuevaPosicionPendiente}");
        }
        else
        {
            Debug.LogWarning("[BossZeus] prefabEstrella es NULL — no se mostró la estrella de preparación.");
        }

        // 3) La estrella queda esperando el SEGUNDO golpe real de la CPU
        estrellaEsperandoGolpe = true;
        teletransporteEnProgreso = false;
        Debug.Log($"[DIAG BOSSZEUS] FASE 1 PREPARADA | teletransporteEnProgreso=false (liberado) | estrellaEsperandoGolpe=true | estrellaPendiente={(estrellaPendiente != null ? "EXISTE" : "NULL")} | nuevaPosicionPendiente={nuevaPosicionPendiente} | estado={estado}");

        // Cooldown normal/épico (se conserva)
        estado = EstadoZeus.Recargando;
        ultimoAtaqueTiempo = Time.time;

        Debug.Log("[BossZeus] Fase 1 lista: estrella esperando el SEGUNDO golpe de la CPU.");
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Activa la habilidad de BossZeus inmediatamente después de que la raqueta
    /// de la CPU golpea realmente la pelota. Reemplaza la detección por posición
    /// como responsable de iniciar la habilidad.
    /// </summary>
    public void ActivarHabilidadDesdeGolpeCPU(float velocidadXAntesDelGolpe)
    {
        // ── DIAGNÓSTICO TEMPORAL: inicio del método ──
        Rigidbody rbDiag = ball != null ? ball.GetComponent<Rigidbody>() : null;
        Debug.Log($"[DIAG BOSSZEUS] ActivarHabilidadDesdeGolpeCPU() llamado | X_ANTES={velocidadXAntesDelGolpe:F2} | X_DESPUES={(rbDiag != null ? rbDiag.linearVelocity.x.ToString("F2") : "N/A")} | estrellaEsperandoGolpe={estrellaEsperandoGolpe} | teletransporteEnProgreso={teletransporteEnProgreso} | estado={estado} | velocidad={(rbDiag != null ? rbDiag.linearVelocity.ToString() : "N/A")} | combateActivo={combateActivo}");

        Debug.Log($"[DIAG BOSSZEUS] ESTADO DETALLADO | combateActivo={combateActivo} | estado={estado} | estrellaEsperandoGolpe={estrellaEsperandoGolpe} | estrellaPendiente={(estrellaPendiente != null ? "EXISTE:" + estrellaPendiente.name : "NULL")} | teletransporteEnProgreso={teletransporteEnProgreso} | nuevaPosicionPendiente={nuevaPosicionPendiente} | ultimoAtaqueTiempo={ultimoAtaqueTiempo:F2} | Time.time={Time.time:F2}");

        // Solo durante un combate activo
        if (!combateActivo)
        {
            Debug.Log("[DIAG BOSSZEUS] RETORNO: combateActivo=false → golpe ignorado.");
            return;
        }
        if (ball == null)
        {
            Debug.Log("[DIAG BOSSZEUS] RETORNO: ball == null → golpe ignorado.");
            return;
        }

        // ── CASO 2: Ya hay una estrella esperando → este SEGUNDO golpe es la señal ──
        if (estrellaEsperandoGolpe)
        {
            Debug.Log($"[DIAG BOSSZEUS] GOLPE CON ESTRELLA ESPERANDO | X_ANTES={velocidadXAntesDelGolpe:F2} | estrellaEsperandoGolpe={estrellaEsperandoGolpe} | teletransporteEnProgreso={teletransporteEnProgreso} | estado={estado} | combateActivo={combateActivo}");
            // GOLPE DE CPU: se acepta X_ANTES > 0 (la pelota llega hacia la CPU, que está
            // en +X) y se rechaza X_ANTES <= 0. Se usa `velocidadXAntesDelGolpe` (guardada
            // en HandleRacketCollision) en lugar de `rb.linearVelocity.x`, porque la CPU ya
            // cambió la dirección al golpear.
            if (velocidadXAntesDelGolpe <= 0f)
            {
                // Segundo golpe de CPU rechazado: la pelota llegó con X_ANTES <= 0.
                // NO se inicia la coroutine, NO se destruye la estrella,
                // NO se pone estrellaEsperandoGolpe = false, NO se toca teletransporteEnProgreso.
                Debug.Log($"[BossZeus] SEGUNDO GOLPE CPU RECHAZADO: X ANTES DEL GOLPE={velocidadXAntesDelGolpe:F2} <= 0.");
                return;
            }

            // Protección: si ya hay un teletransporte en ejecución, ignorar este golpe.
            if (teletransporteEnProgreso)
            {
                Debug.Log("[BossZeus] Teletransporte ya en progreso. Segundo golpe ignorado.");
                Debug.Log($"[DIAG BOSSZEUS] IGNORADO POR TP EN PROGRESO | teletransporteEnProgreso={teletransporteEnProgreso} | estrellaPendiente={(estrellaPendiente != null ? "EXISTE" : "NULL")} | estado={estado}");
                return;
            }

            // ── SEGUNDO GOLPE VÁLIDO (velocidadXAntesDelGolpe < 0) ──
            Debug.Log($"[BossZeus] SEGUNDO GOLPE CPU ACEPTADO: X ANTES DEL GOLPE={velocidadXAntesDelGolpe:F2}");
            Debug.Log("[BossZeus] Iniciando secuencia de teletransporte...");

            estrellaEsperandoGolpe = false;
            teletransporteEnProgreso = true;

            // Se usa la posición REAL guardada en la Fase 1 (NO se recalcula).
            // La espera variable de 1-2s se calcula DENTRO de la coroutine,
            // justo ahora (velocidad actual después del segundo golpe).
            Debug.Log($"[DIAG BOSSZEUS] PRE-STARTCOROUTINE | teletransporteEnProgreso={teletransporteEnProgreso} | estrellaPendiente={(estrellaPendiente != null ? "EXISTE" : "NULL")} | nuevaPosicionPendiente={nuevaPosicionPendiente} | enabled={enabled} | activeInHierarchy={gameObject.activeInHierarchy} | estado={estado}");
            StartCoroutine(CoroutineSecuenciaTeletransporte(nuevaPosicionPendiente));
            return;
        }

        // ── CASO 1: No hay estrella esperando → PREPARAR la Fase 1 ──
        // Estado de la habilidad (cooldown)
        if (estado == EstadoZeus.Recargando)
        {
            // Cooldown efectivo según el modo (objetivo: 3-4 TP reales por ronda).
            // Se mide desde la PREPARACIÓN de la Fase 1; el ciclo completo
            // (prep → 2º golpe → espera ≈1.1 s) ya consume varios segundos,
            // así que este cooldown solo asegura una separación mínima.
            // - Normal: 30% del cooldown base (≈1.0 s con tiempoEntreAtaques=3).
            // - Épico: 60% del cooldown base (≈1.8 s). Antes 150% bloqueaba ciclos.
            float cooldownEfectivo = esModoEpico ? tiempoEntreAtaques * 0.6f : tiempoEntreAtaques * 0.3f;

            // Si ya pasó el tiempo de recarga, volver a estar Listo
            if (Time.time >= ultimoAtaqueTiempo + cooldownEfectivo)
            {
                Debug.Log($"[DIAG BOSSZEUS] COOLDOWN SUPERADO: estado→Listo | ultimoAtaqueTiempo={ultimoAtaqueTiempo:F2} | cooldownEfectivo={cooldownEfectivo:F2} | Time.time={Time.time:F2}");
                estado = EstadoZeus.Listo;
            }
            else
            {
                Debug.Log($"[DIAG BOSSZEUS] COOLDOWN ACTIVO: no se prepara nueva Fase 1 | Time.time={Time.time:F2} | ultimoAtaqueTiempo={ultimoAtaqueTiempo:F2} | cooldownEfectivo={cooldownEfectivo:F2} | faltan={(ultimoAtaqueTiempo + cooldownEfectivo - Time.time):F2}s");
                return; // Cooldown aún no terminado: no preparar
            }
        }

        // Preparar la habilidad: estrella visible, posición guardada, esperando 2do golpe
        Debug.Log($"[DIAG BOSSZEUS] CASO 1: preparando nueva Fase 1 | estado={estado} | ultimoAtaqueTiempo={ultimoAtaqueTiempo:F2} | Time.time={Time.time:F2}");
        ActivarHabilidad();
    }

    // ──────────────────────────────────────────────
    public override void DesactivarHabilidad()
    {
        base.DesactivarHabilidad();
        Debug.Log("[BossZeus] DesactivarHabilidad() ejecutado.");
    }
}
