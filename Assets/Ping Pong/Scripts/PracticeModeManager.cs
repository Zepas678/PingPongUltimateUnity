using UnityEngine;

public class PracticeModeManager : MonoBehaviour
{
    public static PracticeModeManager Instance { get; private set; }

    [Header("Referencias (asignar en escena)")]
    public PingPongBall ball;
    public GameObject wallPrefab; // Prefab opcional de muro
    public Transform wallSpawnTransform;
    public RaquetaGolpe playerRaqueta;

    [Header("Física práctica")]
    public float gravity = 9.81f;

    [Header("Muro")]
    public bool wallPerfectBounce = true;

    [HideInInspector]
    public bool IsPracticeMode = true;

    private GameObject spawnedWall;
    private bool       spawnedWallOwned = false;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;
    }

    void Start()
    {
        EnterPracticeMode();
    }

    public void EnterPracticeMode()
    {
        IsPracticeMode = true;

        // Aplicar gravedad inicial
        Physics.gravity = new Vector3(0f, -gravity, 0f);

        // Desactivar CPU y otros controles relacionados
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.cpu != null)
            {
                GameManager.Instance.cpu.enabled = false;
                var cpuAnim = GameManager.Instance.cpu.GetComponent<RaquetaAnimacion>();
                if (cpuAnim != null)
                    cpuAnim.StopAllCoroutines();
                GameManager.Instance.cpu.gameObject.SetActive(false);
            }
            if (GameManager.Instance.cpuControl != null)
            {
                GameManager.Instance.cpuControl.enabled = false;
                GameManager.Instance.cpuControl.gameObject.SetActive(false);
            }
            if (GameManager.Instance.controlJugador2 != null)
            {
                GameManager.Instance.controlJugador2.enabled = false;
                GameManager.Instance.controlJugador2.gameObject.SetActive(false);
            }
            if (GameManager.Instance.golpeRaquetaCPU != null)
                GameManager.Instance.golpeRaquetaCPU.esJugador = false;
        }

        // Instanciar muro si tenemos prefab o si se necesita uno nuevo.
        if (spawnedWall == null && wallSpawnTransform != null)
        {
            PracticeWall existingWall = FindExistingPracticeWall();
            if (existingWall != null)
            {
                spawnedWall = existingWall.gameObject;
                spawnedWallOwned = false;
            }
            else if (wallPrefab != null)
            {
                spawnedWall = Instantiate(wallPrefab, wallSpawnTransform.position, wallSpawnTransform.rotation);
                spawnedWallOwned = true;
            }
            else
            {
                spawnedWall = CreatePracticeWall();
                spawnedWallOwned = true;
            }

            if (spawnedWall != null)
            {
                var pm = spawnedWall.GetComponent<PracticeWall>();
                if (pm != null)
                    pm.randomBounce = !wallPerfectBounce;
            }
        }

        // Añadir listener a la pelota para estadísticas
        if (ball == null && GameManager.Instance != null)
        {
            ball = GameManager.Instance.ball;
        }

        if (ball != null)
        {
            if (ball.gameObject.GetComponent<PracticeBallListener>() == null)
                ball.gameObject.AddComponent<PracticeBallListener>();

            ball.CancelPendingLaunch();
            ball.ResetBall();
        }

        // Reset combo tracking
        ComboManager.Instance?.ResetSessionMax();
    }

    void OnDestroy()
    {
        IsPracticeMode = false;

        // Restaurar estado del juego al salir del modo práctica
        // PERO solo si NO estamos entrando a una partida PvP.
        // El estado de CPU/P2 lo maneja GameManager, no PracticeModeManager.
        if (GameManager.Instance != null && !GameManager.Instance.EsModoPvP)
        {
            if (GameManager.Instance.cpu != null)
            {
                GameManager.Instance.cpu.enabled = true;
                GameManager.Instance.cpu.gameObject.SetActive(true);
            }
            if (GameManager.Instance.cpuControl != null)
            {
                GameManager.Instance.cpuControl.enabled = true;
                GameManager.Instance.cpuControl.gameObject.SetActive(true);
            }
            if (GameManager.Instance.controlJugador2 != null)
            {
                GameManager.Instance.controlJugador2.enabled = false;
                GameManager.Instance.controlJugador2.gameObject.SetActive(true);
            }
            if (GameManager.Instance.golpeRaquetaCPU != null)
                GameManager.Instance.golpeRaquetaCPU.esJugador = false;
            if (GameManager.Instance.cpu != null)
                GameManager.Instance.cpu.SetDificultad(GameManager.Instance.dificultadSeleccionada);
        }

        if (spawnedWall != null && spawnedWallOwned)
            Destroy(spawnedWall);

        Physics.gravity = new Vector3(0f, -9.81f, 0f);
    }

    void Update()
    {
        // Mantener gravedad en tiempo real
        Physics.gravity = new Vector3(0f, -gravity, 0f);
    }

    public void SetWallRandomness(bool random)
    {
        wallPerfectBounce = !random;
        if (spawnedWall != null)
        {
            var pm = spawnedWall.GetComponent<PracticeWall>();
            if (pm != null) pm.randomBounce = random;
        }
    }

    PracticeWall FindExistingPracticeWall()
    {
        if (wallSpawnTransform != null)
        {
            var existing = wallSpawnTransform.GetComponentInChildren<PracticeWall>();
            if (existing != null)
                return existing;
        }

        var all = FindObjectsOfType<PracticeWall>();
        foreach (var pm in all)
        {
            if (pm != null && pm.gameObject != this.gameObject)
                return pm;
        }

        return null;
    }

    public void SetPlayerRacketForce(float mult)
    {
        if (playerRaqueta != null)
            playerRaqueta.multiplicadorGolpe = mult;
    }

    public void SetBallInitialSpeed(float val)
    {
        if (ball != null) ball.initialSpeed = val;
    }

    public void SetBallMaxSpeed(float val)
    {
        if (ball != null) ball.maxSpeed = val;
    }

    public void ResetPracticeSession()
    {
        EnterPracticeMode();
        ComboManager.Instance?.ResetSessionMax();
        ComboManager.Instance?.RomperComboPorPunto();

        var listener = ball != null ? ball.GetComponent<PracticeBallListener>() : null;
        if (listener != null) listener.ResetCounters();

        if (ball != null)
            ball.ResetBall();

        RestorePracticeRackets();
    }

    void RestorePracticeRackets()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.golpeRaquetaJugador?.Regenerar();
        GameManager.Instance.golpeRaquetaCPU?.Regenerar();
    }

    GameObject CreatePracticeWall()
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "PracticeWall";
        wall.transform.position = wallSpawnTransform != null ? wallSpawnTransform.position : new Vector3(14f, 5f, 0f);
        wall.transform.rotation = wallSpawnTransform != null ? wallSpawnTransform.rotation : Quaternion.identity;
        wall.transform.localScale = new Vector3(1f, 6f, 20f);

        var collider = wall.GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = false;

        wall.AddComponent<PracticeWall>();
        return wall;
    }
}
