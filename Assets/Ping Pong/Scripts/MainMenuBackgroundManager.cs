using UnityEngine;

/// <summary>
/// Fondo dinámico del menú: partido CPU vs CPU en cámara cinemática,
/// con mapa aleatorio de fondo. Ver métodos EntrarAlMenuPrincipal /
/// SalirDelDemoHaciaPartida / PausarFondoSubmenu / ReanudarFondoSubmenu.
/// </summary>
public class MainMenuBackgroundManager : MonoBehaviour
{
    public static MainMenuBackgroundManager Instance { get; private set; }

    [Header("Cámaras")]
    public Camera gameplayCamera;
    public Camera menuCamera;
    public bool autoPosicionarMenuCamera = true;
    public Vector3 menuCameraOffset = new Vector3(-55f, 45f, 55f);

    [Header("Demo CPU vs CPU")]
    public Dificultad dificultadDemo = Dificultad.Facil;
    public bool mapaAleatorioAlEntrar = true;
    public float retrasoSaqueDemo = 1.0f;

    [Header("Pausa en submenús")]
    public bool pausarEnSubmenus = true;

    public bool DemoActivo { get; private set; }

    float timeScalePrevio = 1f;
    int submenusAbiertos;
    Coroutine corrutinaSaqueDemo;

    bool habiaCpuEnabled = true;
    bool habiaCpuControlEnabled = true;
    bool habiaP2Enabled;
    bool habiaRaquetaCPU;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Carga inmediata del fondo: el mapa aleatorio se genera en Awake para que
        // la mesa exista desde el primer frame (sin esperar a Start ni al usuario).
        CargaInicialFondo();
    }

    void Start()
    {
        ResolverCamaras();
        // Reintentar la entrada completa al menú (cámaras + demo + saque).
        // Si UIManager aún no está listo, reintentar unos frames más.
        if (UIManager.Instance != null)
            EntrarAlMenuPrincipal();
        else
            StartCoroutine(ReintentarEntradaMenu());
    }

    /// <summary>Carga inmediata del mapa aleatorio de fondo (sin esperar a Start).</summary>
    void CargaInicialFondo()
    {
        if (MapManager.Instance != null)
            MapManager.Instance.MostrarMapaAleatorioDeFondo();
    }

    System.Collections.IEnumerator ReintentarEntradaMenu()
    {
        int intentos = 0;
        while (UIManager.Instance == null && intentos < 120)
        {
            intentos++;
            yield return null;
        }
        if (UIManager.Instance != null)
        {
            if (UIManager.Instance.panelMenuPrincipal == null
                || UIManager.Instance.panelMenuPrincipal.activeInHierarchy)
                EntrarAlMenuPrincipal();
        }
        else
        {
            // Sin UIManager: al menos encender cámaras + demo directamente.
            EntrarAlMenuPrincipalSinUI();
        }
    }

    /// <summary>Entrada al demo sin depender de UIManager (fallback).</summary>
    void EntrarAlMenuPrincipalSinUI()
    {
        ResolverCamaras();
        GameManager gm = GM();
        if (gm == null) return;
        GuardarEstadoDemo(gm);
        if (mapaAleatorioAlEntrar && MapManager.Instance != null
            && MapManager.Instance.MapaActual == null)
            MapManager.Instance.MostrarMapaAleatorioDeFondo();
        if (gameplayCamera != null) gameplayCamera.enabled = false;
        if (menuCamera != null) menuCamera.enabled = true;
        ActivarDemoCPU(gm);
        if (corrutinaSaqueDemo != null) StopCoroutine(corrutinaSaqueDemo);
        corrutinaSaqueDemo = StartCoroutine(SaqueDemoDiferido());
        DemoActivo = true;
        submenusAbiertos = 0;
    }

    void GuardarEstadoDemo(GameManager gm)
    {
        habiaCpuEnabled = gm.cpu != null ? gm.cpu.enabled : true;
        habiaCpuControlEnabled = gm.cpuControl != null ? gm.cpuControl.enabled : true;
        habiaP2Enabled = gm.controlJugador2 != null && gm.controlJugador2.enabled;
        habiaRaquetaCPU = gm.golpeRaquetaCPU != null && gm.golpeRaquetaCPU.esJugador;
    }

    // Guardado de jefes/habilidades para restaurar al salir del demo.
    readonly System.Collections.Generic.List<MonoBehaviour> jefesApagados = new System.Collections.Generic.List<MonoBehaviour>();
    BossManager bossManagerRef;
    bool bossManagerEstabaActivo;

    /// <summary>
    /// Desactiva temporalmente BossManager, controladores de jefes (Zeus, etc.)
    /// y habilidades especiales para que el demo CPU vs CPU no dispare efectos
    /// ni ataques mágicos de fondo. Se restauran en SalirDelDemoHaciaPartida().
    /// </summary>
    void DesactivarJefesYHabilidades()
    {
        jefesApagados.Clear();
        bossManagerRef = BossManager.Instance != null ? BossManager.Instance : FindObjectOfType<BossManager>();
        if (bossManagerRef != null)
        {
            bossManagerEstabaActivo = bossManagerRef.isActiveAndEnabled;
            // Apagar el combate activo del jefe si lo hay.
            if (bossManagerRef.JefeActivo != null)
            {
                try { bossManagerRef.JefeActivo.FinalizarCombate(); } catch (System.Exception) { }
                bossManagerRef.JefeActivo = null;
            }
            bossManagerRef.enabled = false;
        }
        // Apagar controladores de jefes por tipo (incluye inactivos en escena).
        ApagarComponentesDeTipo<BossZeus>();
        ApagarComponentesDeTipo<BossColossus>();
        ApagarComponentesDeTipo<BossMirage>();
        ApagarComponentesDeTipo<BossController>();
        // Habilidades especiales comunes (por nombre, sin acoplar tipos).
        ApagarPorNombre("Zeus");
        ApagarPorNombre("Boss");
        ApagarPorNombre("Habilidad");
        ApagarPorNombre("Skill");
        ApagarPorNombre("PowerUp");
        ApagarPorNombre("EfectoJefe");
    }

    void ApagarComponentesDeTipo<T>() where T : MonoBehaviour
    {
        T[] todos;
        try { todos = Resources.FindObjectsOfTypeAll<T>(); }
        catch (System.Exception) { return; }
        if (todos == null) return;
        foreach (T c in todos)
        {
            if (c == null || !c.gameObject.scene.isLoaded) continue;
            // No apagar el piloto del demo ni este manager.
            if (c is DemoCpuPiloto || c is MainMenuBackgroundManager) continue;
            if (c.enabled)
            {
                c.enabled = false;
                jefesApagados.Add(c);
            }
        }
    }

    void ApagarPorNombre(string fragmento)
    {
        GameObject[] gos;
        try { gos = Resources.FindObjectsOfTypeAll<GameObject>(); }
        catch (System.Exception) { return; }
        if (gos == null) return;
        foreach (GameObject go in gos)
        {
            if (go == null || !go.scene.isLoaded) continue;
            if (!go.name.ToLowerInvariant().Contains(fragmento.ToLowerInvariant())) continue;
            if (go == gameObject) continue;
            MonoBehaviour[] comps = go.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour m in comps)
            {
                if (m == null || m is MainMenuBackgroundManager || m is DemoCpuPiloto) continue;
                // No apagar el GameManager/UIManager/MapManager/Economy.
                if (m is GameManager || m is UIManager || m is MapManager || m is EconomyManager) continue;
                if (m.enabled && !jefesApagados.Contains(m))
                {
                    m.enabled = false;
                    jefesApagados.Add(m);
                }
            }
        }
    }

    /// <summary>
    /// Restaura jefes/habilidades apagados al salir del demo (PLAY).
    /// Si el panel de jefes está abierto, reactiva BossManager para que el botón
    /// COMBATIR y la interfaz respondan sin reiniciar el juego.
    /// </summary>
    void RestaurarJefesYHabilidades()
    {
        foreach (MonoBehaviour m in jefesApagados)
        {
            if (m != null) m.enabled = true;
        }
        jefesApagados.Clear();
        if (bossManagerRef != null)
            bossManagerRef.enabled = bossManagerEstabaActivo;
        // Reactivar BossManager si el submenú de jefes está visible (COMBATIR).
        ReactivarBossManagerSiPanelJefesAbierto();
        bossManagerRef = null;
    }

    /// <summary>
    /// Si el panel de jefes está abierto (UIManager.panelBosses activo), reactiva
    /// BossManager y sus componentes para que COMBATIR responda sin reiniciar.
    /// Llamar también al abrir el panel de jefes (ver AbrirPanelJefes).
    /// </summary>
    public void ReactivarBossManagerSiPanelJefesAbierto()
    {
        UIManager ui = UIManager.Instance;
        if (ui == null || ui.panelBosses == null || !ui.panelBosses.activeInHierarchy) return;
        ReactivarBossManager();
    }

    /// <summary>
    /// Reactiva BossManager y sus componentes (abrir submenú/pantalla de jefes).
    /// Restaura la lista de apagados que pertenezcan a jefes y fuerza enabled.
    /// </summary>
    public void ReactivarBossManager()
    {
        BossManager bm = bossManagerRef != null ? bossManagerRef
            : (BossManager.Instance != null ? BossManager.Instance : FindObjectOfType<BossManager>());
        if (bm == null) return;
        for (int i = jefesApagados.Count - 1; i >= 0; i--)
        {
            MonoBehaviour m = jefesApagados[i];
            if (m == null) { jefesApagados.RemoveAt(i); continue; }
            if (m is BossManager || m is BossController || m is BossZeus || m is BossColossus || m is BossMirage)
            {
                m.enabled = true;
                jefesApagados.RemoveAt(i);
            }
        }
        bm.enabled = true;
        bossManagerRef = bm;
        bossManagerEstabaActivo = true;
    }

    /// <summary>
    /// Llamar al abrir el panel/submenú de jefes: reactiva BossManager para que
    /// el botón COMBATIR funcione aunque el fondo demo lo haya apagado.
    /// Conectar al botón que abre el menú de jefes.
    /// </summary>
    public void AbrirPanelJefes()
    {
        // Salir de la pausa de fondo si la hay (el menú de jefes es interactivo).
        ReanudarFondoSubmenu();
        ReactivarBossManager();
    }

    void ResolverCamaras()
    {
        if (gameplayCamera == null && Camera.main != null)
            gameplayCamera = Camera.main;
        if (menuCamera == null)
        {
            GameObject go = new GameObject("MenuCamera");
            go.transform.SetParent(transform, false);
            menuCamera = go.gameObject.AddComponent<Camera>();
            menuCamera.enabled = false;
            if (gameplayCamera != null)
            {
                menuCamera.fieldOfView = gameplayCamera.fieldOfView;
                menuCamera.nearClipPlane = gameplayCamera.nearClipPlane;
                menuCamera.farClipPlane = gameplayCamera.farClipPlane;
            }
        }
        if (autoPosicionarMenuCamera && menuCamera != null)
        {
            Vector3 centro = new Vector3(0f, 17.6f, 0f);
            menuCamera.transform.position = centro + menuCameraOffset;
            menuCamera.transform.LookAt(centro);
        }
    }

    GameManager GM() => GameManager.Instance;

    /// <summary>Activa el fondo demo: mapa aleatorio, MenuCamera y CPU vs CPU.</summary>
    public void EntrarAlMenuPrincipal()
    {
        ResolverCamaras();
        GameManager gm = GM();
        if (gm == null) return;
        GuardarEstadoDemo(gm);
        // Jefes/habilidades OFF durante el demo: sin magias de fondo.
        DesactivarJefesYHabilidades();
        if (mapaAleatorioAlEntrar && MapManager.Instance != null)
            MapManager.Instance.MostrarMapaAleatorioDeFondo();
        if (gameplayCamera != null) gameplayCamera.enabled = false;
        if (autoPosicionarMenuCamera && menuCamera != null)
        {
            Vector3 centro = new Vector3(0f, 17.6f, 0f);
            menuCamera.transform.position = centro + menuCameraOffset;
            menuCamera.transform.LookAt(centro);
        }
        if (menuCamera != null) menuCamera.enabled = true;
        ActivarDemoCPU(gm);
        if (corrutinaSaqueDemo != null) StopCoroutine(corrutinaSaqueDemo);
        corrutinaSaqueDemo = StartCoroutine(SaqueDemoDiferido());
        DemoActivo = true;
        submenusAbiertos = 0;
    }

    /// <summary>Sale del demo (PLAY): vuelve a GameplayCamera y restaura controles.</summary>
    public void SalirDelDemoHaciaPartida()
    {
        GameManager gm = GM();
        if (corrutinaSaqueDemo != null) { StopCoroutine(corrutinaSaqueDemo); corrutinaSaqueDemo = null; }
        if (pausarEnSubmenus) Time.timeScale = timeScalePrevio > 0f ? timeScalePrevio : 1f;
        submenusAbiertos = 0;
        DemoActivo = false;
        // Restaurar jefes/habilidades para la partida real.
        RestaurarJefesYHabilidades();
        if (menuCamera != null) menuCamera.enabled = false;
        if (gameplayCamera == null && Camera.main != null) gameplayCamera = Camera.main;
        if (gameplayCamera != null) gameplayCamera.enabled = true;
        if (gm != null) RestaurarControles(gm);
    }

    System.Collections.IEnumerator SaqueDemoDiferido()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, retrasoSaqueDemo));
        GameManager gm = GM();
        if (DemoActivo && gm != null && gm.ball != null)
            gm.ball.ResetBall();
        corrutinaSaqueDemo = null;
    }

    void ActivarDemoCPU(GameManager gm)
    {
        if (gm.golpeRaquetaJugador != null) gm.golpeRaquetaJugador.esJugador = false;
        if (gm.golpeRaquetaCPU != null) gm.golpeRaquetaCPU.esJugador = false;
        var controlJ1 = gm.golpeRaquetaJugador != null
            ? gm.golpeRaquetaJugador.GetComponent<RaquetaControl>() : null;
        if (controlJ1 != null) controlJ1.enabled = false;
        if (gm.controlJugador2 != null) gm.controlJugador2.enabled = false;
        if (gm.cpu != null) { gm.cpu.enabled = true; gm.cpu.SetDificultad((Dificultad)(int)dificultadDemo); }
        if (gm.cpuControl != null) gm.cpuControl.enabled = true;
        if (gm.golpeRaquetaJugador != null)
        {
            var piloto = gm.golpeRaquetaJugador.GetComponent<DemoCpuPiloto>();
            if (piloto == null) piloto = gm.golpeRaquetaJugador.gameObject.AddComponent<DemoCpuPiloto>();
            piloto.enabled = true;
            piloto.dificultadDemo = dificultadDemo;
            if (gm.cpu != null) piloto.pelota = gm.cpu.pelota;
            if (piloto.pelota == null && gm.ball != null) piloto.pelota = gm.ball.transform;
        }
    }

    void RestaurarControles(GameManager gm)
    {
        if (gm.golpeRaquetaJugador != null)
        {
            var piloto = gm.golpeRaquetaJugador.GetComponent<DemoCpuPiloto>();
            if (piloto != null) Destroy(piloto);
            var controlJ1 = gm.golpeRaquetaJugador.GetComponent<RaquetaControl>();
            if (controlJ1 != null) controlJ1.enabled = true;
            gm.golpeRaquetaJugador.esJugador = true;
        }
        if (gm.golpeRaquetaCPU != null) gm.golpeRaquetaCPU.esJugador = habiaRaquetaCPU;
        if (gm.cpu != null) gm.cpu.enabled = habiaCpuEnabled;
        if (gm.cpuControl != null) gm.cpuControl.enabled = habiaCpuControlEnabled;
        if (gm.controlJugador2 != null) gm.controlJugador2.enabled = habiaP2Enabled;
    }

    /// <summary>Al abrir Shop/Options/Help: congela la simulación de fondo.</summary>
    public void PausarFondoSubmenu()
    {
        if (!DemoActivo || !pausarEnSubmenus) return;
        submenusAbiertos++;
        if (submenusAbiertos == 1) { timeScalePrevio = Time.timeScale; Time.timeScale = 0f; }
    }

    /// <summary>Al cerrar el submenú: reanuda el movimiento.</summary>
    public void ReanudarFondoSubmenu()
    {
        if (!DemoActivo || !pausarEnSubmenus) return;
        submenusAbiertos = Mathf.Max(0, submenusAbiertos - 1);
        if (submenusAbiertos == 0) Time.timeScale = timeScalePrevio > 0f ? timeScalePrevio : 1f;
    }
}

/// <summary>
/// Piloto CPU temporal para la pala del jugador durante el demo.
/// </summary>
public class DemoCpuPiloto : MonoBehaviour
{
    public Transform pelota;
    public Dificultad dificultadDemo = Dificultad.Facil;
    float velocidad = 300f;
    RaquetaGolpe golpe;
    void Awake() { golpe = GetComponent<RaquetaGolpe>(); }
    void OnEnable()
    {
        switch (dificultadDemo)
        {
            case Dificultad.Facil: velocidad = 300f; break;
            case Dificultad.Media: velocidad = 380f; break;
            case Dificultad.Dificil: velocidad = 450f; break;
            case Dificultad.Inhumano: velocidad = 650f; break;
        }
    }
    void Update()
    {
        if (golpe != null && golpe.EstaDestruida) return;
        if (pelota == null) return;
        Vector3 o = new Vector3(transform.position.x, transform.position.y, pelota.position.z);
        transform.position = Vector3.MoveTowards(transform.position, o, velocidad * Time.deltaTime);
    }
}
