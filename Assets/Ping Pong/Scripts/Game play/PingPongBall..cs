using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class PingPongBall : MonoBehaviour
{
    [Header("Velocidad horizontal")]
    public float initialSpeed        = 8f;
    public float speedIncreasePerHit = 0.5f;
    public float maxSpeed            = 25f;

    [Header("Rebote en mesa")]
    public float racketLaunchUpward  = 6f;
    public float minLaunchUpward     = 1.5f;
    public float maxHeightAboveTable = 12f;
    public float tableY              = 17.6f;

    [Header("Aleatoriedad")]
    [Range(0f, 0.5f)] public float wallRandomness     = 0.15f;
    [Range(0f, 1f)]   public float racketAngleStrength = 0.6f;

    [Header("Límites del mapa")]
    public float outOfBoundsX = 65f;
    public float outOfBoundsY = 0f;

    [Header("Trail")]
    [Tooltip("Velocidad a partir de la cual el trail se pone naranja")]
    public float velocidadTrailNaranja = 20f;

    [Header("Shockwave")]
    public ShockwaveEffect shockwave;
    public float velocidadShockwave = 110f;
    private bool shockwaveActivado = false;

    [Header("Sonidos")]
    public AudioClip soundTableBounce;      // Sonido cuando rebota en la tabla
    public AudioClip soundRacketHit;        // Sonido normal (0-109)
    public AudioClip soundRacketHitMedium;  // Sonido épico (110-160)
    public AudioClip soundRacketHitUltra;   // Sonido ultra (160-220)
    public AudioClip soundRacketHitHigh;    // Sonido cuando raqueta golpea a 220+ velocidad
    public AudioClip[] soundWallBounce;     // 5 sonidos aleatorios para pared lateral
    public AudioClip soundHighSpeed;        // Sonido cuando alcanza 110 de velocidad

    // --- Estado interno ---
    private Rigidbody     rb;
    private float         currentSpeed;
    private GameObject    lastHitObject;
    private float         directionX;
    private bool          pointRegistered   = false;
    private float         nextServeDirectionX = 0f;
    private TrailRenderer trail;
    private AudioSource   audioSource;
    private bool          highSpeedSoundPlayed = false;
    [SerializeField]
    private GameObject shieldMarkPrefab;

    // -------------------------------------------------------
    void Start()
    {
        rb    = GetComponent<Rigidbody>();
        trail = GetComponent<TrailRenderer>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        
        rb.useGravity             = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints            = RigidbodyConstraints.FreezeRotationX
                                  | RigidbodyConstraints.FreezeRotationY
                                  | RigidbodyConstraints.FreezeRotationZ;
        currentSpeed      = initialSpeed;
        rb.linearVelocity = Vector3.zero;
        rb.useGravity     = false;

        CargarClipsPorDefecto();

        CongelarPelota();
    }

    // -------------------------------------------------------
    void LaunchBall()
    {
        pointRegistered = false;
        rb.isKinematic  = false; // Reactivar física
        rb.useGravity   = true;

        if (nextServeDirectionX == 0f)
            directionX = (Random.value > 0.5f) ? 1f : -1f;
        else
            directionX = nextServeDirectionX;

        nextServeDirectionX = 0f;

        // Sin desviación en Z durante el saque para que vaya directo
        Vector3 dir = new Vector3(directionX, 0f, 0f).normalized;
        Vector3 vel = dir * currentSpeed;
        vel.y       = racketLaunchUpward;
        rb.linearVelocity = vel;
    }

    // -------------------------------------------------------
    void OnCollisionEnter(Collision collision)
    {
        GameObject hit = collision.gameObject;
        if (hit == lastHitObject) return;
        lastHitObject = hit;

        if      (hit.CompareTag("Racket"))   HandleRacketCollision(collision);
        else if (hit.CompareTag("WallSide"))
                HandleSideWallCollision(collision);
        else if (hit.CompareTag("Table"))    HandleTableBounce(collision);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("WallEnd"))
            HandleEndWallTrigger(other.transform.position.x);
    }

    // -------------------------------------------------------
    void HandleRacketCollision(Collision collision)
    {
        // Capturar la dirección X de la pelota ANTES de aplicar el rebote/golpe.
        // La CPU cambia la dirección a +X al golpear, por lo que esta es la
        // dirección REAL con la que la pelota llegó a la raqueta.
        float velocidadXAntesDelGolpe = rb.linearVelocity.x;

        currentSpeed = Mathf.Min(currentSpeed + speedIncreasePerHit, maxSpeed);
        directionX  *= -1f;

        Vector3 racketCenter   = collision.collider.bounds.center;
        float hitOffsetZ       = transform.position.z - racketCenter.z;
        float racketHalfZ      = collision.collider.bounds.extents.z;
        float normalizedOffset = (racketHalfZ > 0f) ? (hitOffsetZ / racketHalfZ) : 0f;

        float angleZ = normalizedOffset * racketAngleStrength;
        angleZ      += Random.Range(-0.1f, 0.1f);
        angleZ       = Mathf.Clamp(angleZ, -0.75f, 0.75f);

        // Buscar RaquetaGolpe — puede estar en el objeto o referenciado via ColliderSeguidor
        RaquetaGolpe raquetaGolpe = collision.gameObject.GetComponent<RaquetaGolpe>();
        if (raquetaGolpe == null)
        {
            ColliderSeguidor seguidor = collision.gameObject.GetComponent<ColliderSeguidor>();
            if (seguidor != null) raquetaGolpe = seguidor.GetRaquetaGolpe();
        }
        Debug.Log($"[Colision] speed={currentSpeed:F1} | raquetaGolpe={raquetaGolpe != null}");

        // Determinar si el impacto rompe la raqueta, consultando a la raqueta
        // (RaquetaGolpe.PuedeResistir). En combate contra Colossus, si la raqueta
        // que recibe el golpe es la del CPU y el jefe está activo, se consulta el
        // override BossColossus.PuedeResistir: mientras tenga cargas de inmunidad/
        // resistencia (rebotesResistenciaRestantes > 0) consume UNA carga y aguanta
        // el impacto en modo épico; solo romperá cuando las cargas lleguen a 0.
        bool esRaquetaJugador1 = false;
        if (GameManager.Instance != null)
            esRaquetaJugador1 = (raquetaGolpe == GameManager.Instance.golpeRaquetaJugador);

        bool puedeResistir = true;
        if (raquetaGolpe != null)
        {
            // Si es la raqueta del CPU y hay un BossColossus activo, usar su override.
            if (!esRaquetaJugador1 && raquetaGolpe.esJugador == false)
            {
                BossColossus[] colossusEnEscena = FindObjectsOfType<BossColossus>(true);
                BossColossus colossusActivo = null;
                for (int i = 0; i < colossusEnEscena.Length; i++)
                {
                    BossColossus c = colossusEnEscena[i];
                    if (c != null && c.enabled && c.combateActivo) { colossusActivo = c; break; }
                }
                if (colossusActivo != null)
                    puedeResistir = colossusActivo.PuedeResistir(currentSpeed);
                else
                    puedeResistir = raquetaGolpe.PuedeResistir(currentSpeed);
            }
            else
            {
                puedeResistir = raquetaGolpe.PuedeResistir(currentSpeed);
            }
        }

        if (raquetaGolpe != null && !puedeResistir)
        {
            // Determinar quién anota según qué raqueta se rompió.
            // En VS CPU: si se rompe la raqueta J1 -> punto para CPU.
            // En PvP:      si se rompe la raqueta J1 -> punto para J2.
            //              si se rompe la raqueta J2 -> punto para J1.
            // Comparamos contra las referencias de GameManager en lugar de raquetaGolpe.esJugador,
            // porque en PvP ambas raquetas tienen esJugador = true.
            bool esRaquetaJugadorUno = (GameManager.Instance != null &&
                                      raquetaGolpe == GameManager.Instance.golpeRaquetaJugador);
            // playerScored = true  cuando NO se rompió la raqueta del jugador 1
            //              = false cuando SÍ se rompió la raqueta del jugador 1 (anota el oponente)
            ForzarRespawn(!esRaquetaJugadorUno);
            GameManager.Instance?.RegenerarRaquetas(0.35f);
            return;
        }

        // Reproducir sonido de golpe de raqueta según la velocidad
        if (currentSpeed >= 200f)
            ReproducirSonido(soundRacketHitHigh);
        else if (currentSpeed >= 160f)
            ReproducirSonido(soundRacketHitUltra);
        else if (currentSpeed >= 110f)
            ReproducirSonido(soundRacketHitMedium);
        else
            ReproducirSonido(soundRacketHit);

        // Golpe potenciado del jugador
        if (raquetaGolpe != null && raquetaGolpe.esJugador)
            currentSpeed = Mathf.Min(currentSpeed * raquetaGolpe.multiplicadorGolpe, maxSpeed);

        // Registrar combo — quién golpeó y a qué velocidad
        bool fueGolpeJugador = raquetaGolpe != null && raquetaGolpe.esJugador;
        ComboManager.Instance?.RegistrarGolpe(fueGolpeJugador, currentSpeed);

        // ── Notificación de golpe de CPU al jefe activo ──
        // Se elimina la llamada directa hardcodeada a BossZeus. En su lugar se
        // detecta el jefe activo en el momento del golpe:
        //   1) BossZeus:      existe, enabled y combateActivo == true → ActivarHabilidadDesdeGolpeCPU()
        //   2) BossColossus:  existe, enabled y combateActivo == true → ActivarHabilidadDesdeGolpeCPU()
        //   3) Cualquier otro BossController activo (compatibilidad genérica).
        string jefeNotificado = null;
        if (!fueGolpeJugador)
            jefeNotificado = NotificarGolpeJefeActivoCPU(velocidadXAntesDelGolpe, currentSpeed);

        // ── DIAGNÓSTICO TEMPORAL: cada golpe de raqueta ──
        BossZeus bossZeusDiag = FindObjectOfType<BossZeus>();
        bool zeusActivo = bossZeusDiag != null && bossZeusDiag.enabled && bossZeusDiag.combateActivo;
        Debug.Log($"[DIAG GOLPE] raqueta='{collision.gameObject.name}' | fueGolpeJugador={fueGolpeJugador} | X_ANTES={velocidadXAntesDelGolpe:F2} | X_DESPUES={rb.linearVelocity.x:F2} | velocidad={rb.linearVelocity} | jefeActivo={jefeNotificado ?? "NINGUNO"} | bossZeusActivo={zeusActivo} | estrellaEsperando={(zeusActivo ? bossZeusDiag.EstrellaEsperandoGolpeDiag : false)} | teleTP={(zeusActivo ? bossZeusDiag.TeletransporteEnProgresoDiag : false)}");

        // Animación de golpe — solo si el jugador presionó el botón
        // Para la CPU siempre anima (ya lo controla CPUControl)
        bool debeAnimar = true;
        if (raquetaGolpe != null && raquetaGolpe.esJugador)
            debeAnimar = raquetaGolpe.BotonPresionado; // solo si presionó J

        if (debeAnimar)
        {
            RaquetaAnimacion anim = collision.gameObject.GetComponent<RaquetaAnimacion>();
            if (anim == null)
            {
                ColliderSeguidor seg = collision.gameObject.GetComponent<ColliderSeguidor>();
                seg?.TriggerGolpe();
            }
            else anim.AnimarGolpe();
        }

        Vector3 dir = new Vector3(directionX, 0f, angleZ).normalized;
        Vector3 vel = dir * currentSpeed;

        float speedRatio    = Mathf.Clamp01((currentSpeed - initialSpeed) / (maxSpeed - initialSpeed));
        float dynamicUpward = Mathf.Lerp(racketLaunchUpward, minLaunchUpward, speedRatio);

        // A velocidades muy altas (>50% del max) reducir aún más el upward
        if (currentSpeed > maxSpeed * 0.5f)
        {
            float highSpeedRatio = Mathf.Clamp01((currentSpeed - maxSpeed * 0.5f) / (maxSpeed * 0.5f));
            dynamicUpward = Mathf.Lerp(dynamicUpward, 0f, highSpeedRatio);
        }

        // Si la pelota ya está alta, empujar hacia abajo
        float heightAboveTable = transform.position.y - tableY;
        if (heightAboveTable > maxHeightAboveTable * 0.5f)
            dynamicUpward = Mathf.Min(dynamicUpward, -1f);

        vel.y = Mathf.Clamp(dynamicUpward, -8f, racketLaunchUpward);
        rb.linearVelocity = vel;
    }

    // -------------------------------------------------------
    /// <summary>
    /// Detecta el jefe activo en el momento del golpe de CPU y notifica su
    /// habilidad/golpe específico. Un jefe se considera "activo" si su componente
    /// está habilitado (enabled) y su combate está en curso (combateActivo).
    /// Orden de detección:
    ///   1) BossZeus      → ActivarHabilidadDesdeGolpeCPU(velocidadXAntes)
    ///   2) BossColossus  → ActivarHabilidadDesdeGolpeCPU(velocidadActual)
    ///   3) Otro BossController activo (compatibilidad genérica).
    /// </summary>
    /// <param name="velocidadXAntesDelGolpe">Dirección X de la pelota ANTES del golpe (la usa Zeus).</param>
    /// <param name="velocidadActual">Velocidad actual de la pelota (la usa Colossus / otros).</param>
    /// <returns>Nombre del jefe notificado, o null si no hay ningún jefe activo.</returns>
    private string NotificarGolpeJefeActivoCPU(float velocidadXAntesDelGolpe, float velocidadActual)
    {
        // 1. BossZeus: existe, está habilitado y su combate está en curso.
        BossZeus[] zeusEnEscena = FindObjectsOfType<BossZeus>(true);
        for (int i = 0; i < zeusEnEscena.Length; i++)
        {
            BossZeus z = zeusEnEscena[i];
            if (z == null) continue;
            if (!z.enabled || !z.combateActivo) continue;

            z.ActivarHabilidadDesdeGolpeCPU(velocidadXAntesDelGolpe);
            return z.gameObject.name;
        }

        // 2. BossColossus: existe, está habilitado y su combate está en curso.
        BossColossus[] colossusEnEscena = FindObjectsOfType<BossColossus>(true);
        for (int i = 0; i < colossusEnEscena.Length; i++)
        {
            BossColossus c = colossusEnEscena[i];
            if (c == null) continue;
            if (!c.enabled || !c.combateActivo) continue;

            c.ActivarHabilidadDesdeGolpeCPU(velocidadActual);
            return c.gameObject.name;
        }

        // 3. Compatibilidad genérica: cualquier otro BossController activo.
        BossController[] controllers = FindObjectsOfType<BossController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            BossController bc = controllers[i];
            if (bc == null) continue;
            if (bc is BossZeus || bc is BossColossus) continue; // ya gestionados
            if (!bc.enabled || !bc.combateActivo) continue;

            // La clase base BossController no define una API de golpe de CPU; se
            // podría extender aquí en el futuro. Por ahora solo se registra.
            Debug.Log($"[Boss] Jefe activo (genérico) '{bc.gameObject.name}' — sin habilidad de golpe CPU específica.");
            return bc.gameObject.name;
        }

        // Sin jefe activo.
        return null;
    }
    // -------------------------------------------------------
    void HandleSideWallCollision(Collision collision)
    {
        Vector3 vel = rb.linearVelocity;

        float newZ = -vel.z + Random.Range(-wallRandomness, wallRandomness);

        if (Mathf.Abs(newZ) < 0.05f)
        newZ = 0.05f * (newZ >= 0f ? 1f : -1f);

        Vector3 h = new Vector3(vel.x, 0f, newZ);
        if (h.sqrMagnitude < 0.01f)
            h = new Vector3(0.5f, 0f, newZ >= 0f ? 0.5f : -0.5f);

        h = h.normalized * currentSpeed;
        rb.position += collision.contacts[0].normal * 0.02f;
        rb.WakeUp();
        rb.linearVelocity = new Vector3(h.x, vel.y, h.z);

        // Reproducir sonido aleatorio de rebote en pared
        if (soundWallBounce != null && soundWallBounce.Length > 0)
        {
            int randomIndex = Random.Range(0, soundWallBounce.Length);
            ReproducirSonido(soundWallBounce[randomIndex]);
        }

        // Crear la marca en el punto de impacto
        if (shieldMarkPrefab != null)
        {
        ContactPoint contact = collision.contacts[0];

        GameObject mark = Instantiate(
        shieldMarkPrefab,
        contact.point + contact.normal * 0.01f,
        Quaternion.LookRotation(contact.normal)
        );

        StartCoroutine(FadeAndDestroy(mark, 1f));
        }
    }

    void HandleTableBounce(Collision collision)
    {
        Vector3 vel = rb.linearVelocity;
        Vector3 h   = new Vector3(vel.x, 0f, vel.z);
        if (h.magnitude < currentSpeed * 0.8f)
        {
            h = h.normalized * currentSpeed;
            rb.linearVelocity = new Vector3(h.x, vel.y, h.z);
        }

        // Reproducir sonido de rebote en tabla
        ReproducirSonido(soundTableBounce);
    }

    // -------------------------------------------------------
    // Llamado desde HandleRacketCollision cuando la raqueta se destruye
    public void ForzarRespawn(bool playerScored)
    {
        if (pointRegistered) return;
        pointRegistered = true;

        Vector3 posCongelada = transform.position;

        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity      = false;
        rb.isKinematic     = true;
        transform.position = posCongelada;

        nextServeDirectionX = playerScored ? -1f : 1f;

        // En modo práctica no registrar puntos: resetear la pelota en su lugar
        bool isPractice = PracticeModeManager.Instance != null && PracticeModeManager.Instance.IsPracticeMode;
        if (GameManager.Instance != null && !isPractice)
            GameManager.Instance.RegisterPoint(playerScored, currentSpeed);
        else
        {
            ComboManager.Instance?.RomperComboPorPunto();
            ResetBall();
            GameManager.Instance?.RegenerarRaquetas(0.35f);
        }
    }

    void HandleEndWallTrigger(float wallX)
    {
        if (pointRegistered) return;
        pointRegistered = true;

        Vector3 posCongelada = transform.position;

        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity      = false;
        rb.isKinematic     = true; // Bloqueo total — nada puede mover la pelota
        transform.position = posCongelada;

        bool playerScored   = wallX < 0f;
        nextServeDirectionX = playerScored ? -1f : 1f;

        Debug.Log(playerScored
            ? $"[PingPong] PUNTO JUGADOR (pared X={wallX:F1})"
            : $"[PingPong] PUNTO CPU     (pared X={wallX:F1})");

        bool isPractice2 = PracticeModeManager.Instance != null && PracticeModeManager.Instance.IsPracticeMode;
        if (GameManager.Instance != null && !isPractice2)
            GameManager.Instance.RegisterPoint(playerScored, currentSpeed);
        else
        {
            ComboManager.Instance?.RomperComboPorPunto();
            ResetBall();
            GameManager.Instance?.RegenerarRaquetas(0.35f);
        }
    }

    void CargarClipsPorDefecto()
    {
        soundTableBounce = ResolverClip(soundTableBounce, "Assets/Ping Pong/Sonidos/Pelota/impactMetal_000.ogg");
        soundRacketHit = ResolverClip(soundRacketHit, "Assets/Ping Pong/Sonidos/Pelota/impactMetal_001.ogg");
        soundRacketHitMedium = ResolverClip(soundRacketHitMedium, "Assets/Ping Pong/Sonidos/Pelota/impactMetal_002.ogg");
        soundRacketHitUltra = ResolverClip(soundRacketHitUltra, "Assets/Ping Pong/Sonidos/Pelota/impactMetal_003.ogg");
        soundRacketHitHigh = ResolverClip(soundRacketHitHigh, "Assets/Ping Pong/Sonidos/Pelota/dragon-studio-nuclear-explosion-386181.mp3");
        soundHighSpeed = ResolverClip(soundHighSpeed, "Assets/Ping Pong/Sonidos/Pelota/forceField_004.ogg");
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

    // -------------------------------------------------------
    void Update()
    {
        UpdateVisuals();

        if (pointRegistered) return;

        if (transform.position.y < outOfBoundsY)
        {
            Debug.Log($"[PingPong] Pelota bajo el mapa (Y={transform.position.y:F1})");
            pointRegistered = true;
            ResetBall();
        }
    }

    // -------------------------------------------------------
    public void ResetBall()
    {
        Debug.Log($"[ResetBall] Llamado | gameOver={GameManager.Instance?.IsGameOver} | stack={new System.Diagnostics.StackTrace()}");
        // Resetear bandera de punto para poder reactivar la física tras el reinicio.
        pointRegistered = false;

        // Resetear velocidad ANTES de poner isKinematic = true
        if (!rb.isKinematic)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        CancelPendingLaunch();

        rb.isKinematic = true; // Bloqueo total mientras se reposiciona
        rb.useGravity  = false;
        currentSpeed   = initialSpeed;
        lastHitObject  = null;

        transform.position = new Vector3(0f, tableY + 1f, 0f);
        rb.position         = transform.position;
        Physics.SyncTransforms();

        Invoke(nameof(LaunchBall), 0.5f);
    }

    void MoveToCenter()
    {
        rb.isKinematic = true; // Mantener bloqueada hasta el lanzamiento
        transform.position = new Vector3(0f, tableY + 1f, 0f);
    }

    public void CancelPendingLaunch()
    {
        CancelInvoke(nameof(LaunchBall));
        CancelInvoke(nameof(MoveToCenter));
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // -------------------------------------------------------
    void FixedUpdate()
    {
        if (rb.linearVelocity.sqrMagnitude < 0.01f) return;

        Vector3 vel = rb.linearVelocity;
        Vector3 h   = new Vector3(vel.x, 0f, vel.z);

        if (h.magnitude > 0.1f && Mathf.Abs(h.magnitude - currentSpeed) > 0.5f)
        {
            h = h.normalized * currentSpeed;
            rb.linearVelocity = new Vector3(h.x, vel.y, h.z);
        }

        float height = transform.position.y - tableY;

        if (height > maxHeightAboveTable && vel.y > 0f)
        {
            float pushDown = (height - maxHeightAboveTable) * 5f;
            pushDown       = Mathf.Clamp(pushDown, 2f, 20f);
            rb.AddForce(Vector3.down * pushDown, ForceMode.Acceleration);
        }

        // Clamp Y más agresivo a alta velocidad
        float speedFactor  = Mathf.Clamp01(currentSpeed / maxSpeed);
        float maxYVelocity = Mathf.Lerp(racketLaunchUpward + 2f, 3f, speedFactor);
        if (Mathf.Abs(rb.linearVelocity.y) > maxYVelocity)
        {
            Vector3 clamped = rb.linearVelocity;
            clamped.y       = Mathf.Sign(clamped.y) * maxYVelocity;
            rb.linearVelocity = clamped;
        }
    }

    // -------------------------------------------------------
    void UpdateVisuals()
    {
        if (trail == null) return;
        bool rapido = currentSpeed >= velocidadTrailNaranja;
        trail.startColor = rapido ? new Color(1f, 0.5f, 0f) : Color.white;
        trail.endColor   = rapido ? new Color(1f, 0.3f, 0f, 0f) : new Color(1f, 1f, 1f, 0f);

        // Shockwave: se activa UNA vez al cruzar la velocidad umbral
        if (currentSpeed >= velocidadShockwave)
        {
            if (!shockwaveActivado)
            {
                shockwaveActivado = true;
                shockwave?.Reproducir(transform.position);
                // Reproducir sonido de alta velocidad
                ReproducirSonido(soundHighSpeed);
            }
        }
        else
        {
            shockwaveActivado = false; // resetear para la próxima vez que acelere
        }
    }

    // -------------------------------------------------------
    void OnDrawGizmos()
    {
        if (rb == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, rb.linearVelocity.normalized * 2f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(
            new Vector3(transform.position.x, tableY + maxHeightAboveTable, transform.position.z),
            new Vector3(0.3f, 0.05f, 0.3f));
    }

    IEnumerator FadeAndDestroy(GameObject mark, float duration)
    {
        SpriteRenderer sr = mark.GetComponent<SpriteRenderer>();

        if (sr == null)
        {
            Destroy(mark);
            yield break;
        }

        Vector3 finalScale = mark.transform.localScale;
        mark.transform.localScale = finalScale * 0.7f;

        Color originalColor = sr.color;

        float popTime = 0.12f;
        float fadeTime = 0.8f;
        float visibleTime = duration - popTime - fadeTime;

        // POP
        float t = 0f;
        while (t < popTime)
        {
            t += Time.deltaTime;

            float p = Mathf.SmoothStep(0f, 1f, t / popTime);

            mark.transform.localScale = Vector3.Lerp(
                finalScale * 0.7f,
                finalScale,
                p
            );

            yield return null;
        }

        mark.transform.localScale = finalScale;

        // Espera
        yield return new WaitForSeconds(visibleTime);

        // Fade
        t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;

            Color c = originalColor;
            c.a = Mathf.Lerp(1f, 0f, t / fadeTime);

            sr.color = c;

            yield return null;
        }

        Destroy(mark);
    }
    // Función para reproducir sonidos
    void ReproducirSonido(AudioClip clip)
{
    if (audioSource != null && clip != null)
        audioSource.PlayOneShot(clip);
}

    public void CongelarPelota()
{
    CancelPendingLaunch();

    rb.isKinematic = true;
    rb.useGravity = false;
    rb.linearVelocity = Vector3.zero;
    rb.angularVelocity = Vector3.zero;

    transform.position = new Vector3(0f, tableY + 1f, 0f);
}
}