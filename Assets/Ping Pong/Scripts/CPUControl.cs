using UnityEngine;

public class CPUControl : MonoBehaviour
{
    [Header("Referencia")]
    public Transform pelota;

    [Header("Posiciones en Z")]
    public float zIzquierda = 20f;
    public float zCentro    = 0f;
    public float zDerecha   = -20f;

    [Header("Dificultad actual")]
    public Dificultad dificultadActual = Dificultad.Facil;

    private float velocidadMovimiento;
    private float velocidadMinFallo;
    private float velocidadMaxFallo;
    private float falloMaximo;

    private float          destinoZ;
    private bool           fallando     = false;
    private Rigidbody      rbPelota;
    private bool           modoSaque    = true;
    private RaquetaAnimacion animacion;
    private RaquetaGolpe   raquetaGolpe;
    private float          lastDestinoZ = 999f;

    private float probGolpeInicial;
    private float probGolpeRapido;
    private float velocidadGolpe    = 50f;
    private float tiempoUltimoGolpe = -999f;
    private float cooldownGolpe     = 0.5f;

    // -------------------------------------------------------
    void Start()
    {
        if (pelota != null)
            rbPelota = pelota.GetComponent<Rigidbody>();

        destinoZ     = zCentro;
        lastDestinoZ = zCentro;
        modoSaque    = true;
        animacion    = GetComponent<RaquetaAnimacion>();
        raquetaGolpe = GetComponent<RaquetaGolpe>();
        SetDificultad(dificultadActual);
    }

    // -------------------------------------------------------
    public void ActivarModoSaque()
    {
        modoSaque = true;
        fallando  = false;
        destinoZ  = zCentro;
        Debug.Log("[CPU] Modo saque activado — sin fallos");
    }

    // -------------------------------------------------------
    public void SetDificultad(Dificultad d)
    {
        dificultadActual = d;

        switch (d)
        {
            case Dificultad.Facil:
                // Un poco fácil
                velocidadMovimiento = 300f;
                velocidadMinFallo   = 50f;
                velocidadMaxFallo   = 100f;
                falloMaximo         = 0.50f;
                probGolpeInicial    = 0.30f;
                probGolpeRapido     = 0.70f;
                break;

            case Dificultad.Dificil:
                // Un poco difícil
                velocidadMovimiento = 450f;
                velocidadMinFallo   = 80f;
                velocidadMaxFallo   = 120f;
                falloMaximo         = 0.08f;
                probGolpeInicial    = 0.55f;
                probGolpeRapido     = 0.95f;
                break;

            case Dificultad.Inhumano:
                // Imposible — reacciona perfectamente, nunca falla, siempre golpea
                velocidadMovimiento = 800f;   // ultrarápido, alcanza cualquier pelota
                velocidadMinFallo   = 999f;   // nunca falla
                velocidadMaxFallo   = 999f;
                falloMaximo         = 0f;     // 0% de fallo
                probGolpeInicial    = 1.00f;  // siempre golpea
                probGolpeRapido     = 1.00f;  // siempre golpea rápido
                break;
        }

        Debug.Log($"[CPU] Dificultad: {d} | Vel: {velocidadMovimiento} | " +
                  $"Fallo: 0%→{falloMaximo*100}% entre vel {velocidadMinFallo}-{velocidadMaxFallo}");
    }

    // -------------------------------------------------------
    void Update()
    {
        Debug.Log("CPU Update");
        if (pelota == null) return;
        if (raquetaGolpe != null && raquetaGolpe.EstaDestruida) return;

        float velocidadPelota     = rbPelota != null ? rbPelota.linearVelocity.magnitude : 0f;
        bool  pelotaVieniaHaciaCPU = rbPelota != null && rbPelota.linearVelocity.x < 0f;

        // Bug 2 fix: desactivar modo saque solo cuando la pelota está MUY cerca de la CPU
        // Usar posición X de la CPU como referencia en vez de -5 fijo
        float posXCPU = transform.position.x;
        if (modoSaque && pelotaVieniaHaciaCPU && pelota.position.x < posXCPU + 15f)
        {
            modoSaque = false;
            Debug.Log("[CPU] Modo saque desactivado");
        }

        // Durante saque o pelota yendo al jugador → centro, nunca falla
        if (modoSaque || !pelotaVieniaHaciaCPU)
        {
            destinoZ = zCentro;
            fallando = false;
        }
        else
        {
            float probFallo = CalcularProbFallo(velocidadPelota);

            if (!fallando)
            {
                if (Random.value < probFallo * Time.deltaTime * 3f)
                {
                    fallando = true;
                    float zonaCorrecta = CalcularZonaObjetivo();
                    destinoZ = (zonaCorrecta == zIzquierda) ? zDerecha : zIzquierda;
                    Debug.Log($"[CPU] Falla | vel={velocidadPelota:F1} | prob={probFallo*100:F1}%");
                }
                else
                {
                    destinoZ = CalcularZonaObjetivo();
                }
            }
        }

        // Animación de movimiento
        if (!Mathf.Approximately(destinoZ, lastDestinoZ))
        {
            lastDestinoZ = destinoZ;
            if (destinoZ > 5f)       animacion?.AnimarMovimiento(1);
            else if (destinoZ < -5f) animacion?.AnimarMovimiento(-1);
            else                     animacion?.AnimarMovimiento(0);
        }

        // Bug 1 fix: golpe CPU solo cuando velocidad supera umbral
        float distanciaX = Mathf.Abs(pelota.position.x - transform.position.x);
        bool  pelotaCerca = distanciaX < 8f;

        if (!modoSaque && pelotaVieniaHaciaCPU && pelotaCerca && raquetaGolpe != null)
        {
            // Modo épico (110-160): siempre golpea rápido
            bool esVelocidadEpica = velocidadPelota >= 110f && velocidadPelota < 160f;
            bool esVelocidadUltra  = velocidadPelota >= 160f && velocidadPelota < 220f;
            
            bool velocidadSuficiente = velocidadPelota >= velocidadGolpe;
            bool puedeGolpearLento   = dificultadActual != Dificultad.Facil;

            float prob = 0f;
            if (esVelocidadEpica || esVelocidadUltra)
            {
                // Modo épico/ultra: golpear SIEMPRE y rápido
                prob = 1.00f;
            }
            else if (velocidadSuficiente)
            {
                prob = probGolpeRapido;
            }
            else if (puedeGolpearLento)
            {
                prob = probGolpeInicial;
            }

            bool cooldownOk = Time.time - tiempoUltimoGolpe > cooldownGolpe;
            if (prob > 0f && cooldownOk && Random.value < prob)
            {
                tiempoUltimoGolpe = Time.time;
                // Pasar velocidad actual para que RaquetaGolpe decida si animar
                raquetaGolpe.ActivarGolpeCPU(velocidadPelota);
            }
        }

        if (!pelotaCerca && raquetaGolpe != null)
            raquetaGolpe.ResetGolpe();

        // Mover
        Vector3 objetivo = new Vector3(transform.position.x, transform.position.y, destinoZ);
        transform.position = Vector3.MoveTowards(transform.position, objetivo,
                                                  velocidadMovimiento * Time.deltaTime);
    }

    // -------------------------------------------------------
    float CalcularProbFallo(float velocidad)
    {
        if (velocidad <= velocidadMinFallo) return 0f;
        float t = Mathf.InverseLerp(velocidadMinFallo, velocidadMaxFallo, velocidad);
        return Mathf.Lerp(0f, falloMaximo, t);
    }

    float CalcularZonaObjetivo()
    {
        if (pelota.position.z > 5f)  return zIzquierda;
        if (pelota.position.z < -5f) return zDerecha;
        return zCentro;
    }

    void OnEnable()
    {
    Debug.Log("[S/ CPUControl] OnEnable");
    Debug.Log(new System.Diagnostics.StackTrace(true).ToString());
    }

void OnDisable()
    {   
    Debug.Log("[S/ CPUControl] OnDisable");
    Debug.Log(new System.Diagnostics.StackTrace(true).ToString());
    }
}