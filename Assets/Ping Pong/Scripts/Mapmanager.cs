using UnityEngine;

/// <summary>
/// Maneja la selección e instanciación de mapas.
/// Cada mapa tiene su propia escala y posición configurables desde el Inspector.
/// </summary>
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    [System.Serializable]
    public class ConfigMapa
    {
        public string     nombre;
        public GameObject prefab;
        public Vector3    posicion = Vector3.zero;
        public Vector3    escala   = Vector3.one;
        public Vector3    rotacion = Vector3.zero;
        [Tooltip("Skybox material para este mapa (opcional)")]
        public Material   skybox;
        [Tooltip("Velocidad de rotación del skybox (0 = sin movimiento)")]
        public float      velocidadSkybox = 0f;
    }

    [Header("Mapas disponibles")]
    public ConfigMapa mapaHabitacion = new ConfigMapa
    {
        nombre   = "Habitación Vacía",
        posicion = new Vector3(-14f, 44.2f, 163.4f),
        escala   = new Vector3(348f, 94f, 6f),
        rotacion = new Vector3(0f, 0f, 0f)
    };

    public ConfigMapa mapaHabitacionJavi = new ConfigMapa
    {
        nombre   = "Habitacion de javi",
        posicion = Vector3.zero,
        escala   = Vector3.one,
        rotacion = Vector3.zero
    };

    public ConfigMapa mapaInfinito = new ConfigMapa
    {
        nombre   = "Mapa Infinito",
        posicion = Vector3.zero,
        escala   = Vector3.one,
        rotacion = Vector3.zero
    };

    public ConfigMapa mapaNube = new ConfigMapa
    {
        nombre   = "Mapa Nube",
        posicion = Vector3.zero,
        escala   = Vector3.one,
        rotacion = Vector3.zero
    };

    public ConfigMapa mapaSpace = new ConfigMapa
    {
        nombre   = "Space",
        posicion = new Vector3(-44.5f, -21.7f, 1.5f),
        escala   = new Vector3(0.8f, 0.8f, 0.8f),
        rotacion = new Vector3(0f, -90f, 0f)
    };

    public ConfigMapa mapaHielo = new ConfigMapa
    {
        nombre   = "Hielo",
        posicion = new Vector3(-10.8f, -64.8f, -7.6f),
        escala   = new Vector3(6f, 6f, 6f),
        rotacion = new Vector3(0f, 180f, 0f)
    };

    public ConfigMapa mapaLava = new ConfigMapa
    {
        nombre   = "Lava",
        posicion = new Vector3(0.9f, -3.7f, -3.3f),
        escala   = new Vector3(8f, 8f, 8.5f),
        rotacion = new Vector3(0f, 180f, 0f)
    };

    public ConfigMapa mapaPractica = new ConfigMapa
    {
        nombre   = "Habitación Práctica",
        posicion = new Vector3(0f, 0f, 0f),
        escala   = Vector3.one,
        rotacion = Vector3.zero
    };

    // --- Estado interno ---
    private GameObject mapaActual;
    private Transform mapaContenedor;
    private ConfigMapa mapaActualConfig;
    private int        mapaSeleccionado = 0;

    // -------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        mapaContenedor = new GameObject("MapaContenedor").transform;
        mapaContenedor.SetParent(null, false);
        mapaContenedor.position = Vector3.zero;
        mapaContenedor.rotation = Quaternion.identity;
        mapaContenedor.localScale = Vector3.one;
        CleanExistingSceneMaps();
    }

    // -------------------------------------------------------
    private float anguloSkybox = 0f;

    void Update()
    {
        // Rotar skybox del mapa actual si tiene velocidad
        if (RenderSettings.skybox != null)
        {
            ConfigMapa config = ObtenerConfigActual();
            Debug.Log($"[Skybox] config={config?.nombre} vel={config?.velocidadSkybox} angulo={anguloSkybox}");

            if (config != null && config.velocidadSkybox != 0f)
            {
                anguloSkybox = Mathf.Repeat(anguloSkybox + config.velocidadSkybox * Time.deltaTime, 360f);
                RenderSettings.skybox.SetFloat("_Rotation", anguloSkybox);
            }
        }
    }

    ConfigMapa ObtenerConfigActual()
    {
        return mapaSeleccionado switch
        {
            0 => mapaHabitacion,
            1 => mapaHabitacionJavi,
            2 => mapaInfinito,
            3 => mapaNube,
            4 => mapaSpace,
            5 => mapaHielo,
            6 => mapaLava,
            7 => mapaPractica,
            _ => null
        };
    }

    // -------------------------------------------------------
    public void SeleccionarHabitacion() { mapaSeleccionado = 0; InstanciarMapa(mapaHabitacion); }
    public void SeleccionarHabitacionJavi() { mapaSeleccionado = 1; InstanciarMapa(mapaHabitacionJavi); }
    public void SeleccionarInfinito() { mapaSeleccionado = 2; InstanciarMapa(mapaInfinito); }
    public void SeleccionarNube() { mapaSeleccionado = 3; InstanciarMapa(mapaNube); }
    public void SeleccionarSpace()      { mapaSeleccionado = 4; InstanciarMapa(mapaSpace); }
    public void SeleccionarHielo()      { mapaSeleccionado = 5; InstanciarMapa(mapaHielo); }
    public void SeleccionarLava()       { mapaSeleccionado = 6; InstanciarMapa(mapaLava); }
    public void SeleccionarPractica()   { mapaSeleccionado = 7; InstanciarMapa(mapaPractica); }

    // -------------------------------------------------------
    void InstanciarMapa(ConfigMapa config)
    {
        if (config == null)
        {
            Debug.LogWarning("[MapManager] Configuración de mapa nula");
            return;
        }

        // Asegurarse de que cualquier mapa colocado manualmente en escena se limpie antes de crear uno nuevo.
        CleanExistingSceneMaps();

        if (mapaActualConfig != null && mapaActualConfig == config && mapaActual != null)
        {
            Debug.Log($"[MapManager] El mapa {config.nombre} ya está activo, no se instancia de nuevo.");
            return;
        }

        if (mapaActual != null)
            Destroy(mapaActual);

        mapaActual = config.prefab != null ? Instantiate(config.prefab, mapaContenedor) : CrearMapaProcedural(config);

        if (mapaActual == null)
        {
            Debug.LogWarning($"[MapManager] No se pudo crear el mapa: {config.nombre}");
            return;
        }

        mapaActual.transform.SetParent(mapaContenedor, false);
        mapaActual.transform.position = config.posicion;
        mapaActual.transform.localScale = config.escala;
        mapaActual.transform.eulerAngles = config.rotacion;
        mapaActualConfig = config;

        // Cambiar skybox si el mapa tiene uno asignado
        if (config.skybox != null)
        {
            // Crear instancia del material para poder modificarlo en runtime
            Material skyboxInstancia = new Material(config.skybox);
            RenderSettings.skybox = skyboxInstancia;
            anguloSkybox = 0f; // resetear rotación al cambiar mapa
        }

        Debug.Log($"[MapManager] Mapa cargado: {config.nombre}");
    }

    // Busca en la escena objetos raíz que coincidan con nombres de prefabs de mapa
    // y los elimina para evitar mapas duplicados colocados manualmente.
    void CleanExistingSceneMaps()
    {
        // Reunir nombres de prefabs y nombres de configuraciones de mapa configurados
        string[] mapNames = new string[]
        {
            mapaHabitacion.prefab != null ? mapaHabitacion.prefab.name : null,
            mapaHabitacionJavi.prefab != null ? mapaHabitacionJavi.prefab.name : null,
            mapaInfinito.prefab != null ? mapaInfinito.prefab.name : null,
            mapaNube.prefab != null ? mapaNube.prefab.name : null,
            mapaSpace.prefab != null ? mapaSpace.prefab.name : null,
            mapaHielo.prefab != null ? mapaHielo.prefab.name : null,
            mapaLava.prefab != null ? mapaLava.prefab.name : null,
            mapaPractica.prefab != null ? mapaPractica.prefab.name : null,
            mapaHabitacion.nombre,
            mapaHabitacionJavi.nombre,
            mapaInfinito.nombre,
            mapaNube.nombre,
            mapaSpace.nombre,
            mapaHielo.nombre,
            mapaLava.nombre,
            mapaPractica.nombre
        };

        var allObjects = FindObjectsOfType<GameObject>();
        foreach (var go in allObjects)
        {
            if (go == null) continue;
            if (go == this.gameObject) continue;
            if (mapaContenedor != null && go.transform.IsChildOf(mapaContenedor)) continue;
            if (go.transform.IsChildOf(this.transform)) continue;

            string lower = go.name.ToLower();
            if (lower.Contains("camera") || lower.Contains("ui") || lower.Contains("canvas") || lower.Contains("manager") || lower.Contains("eventsystem") || lower.Contains("volcan") || lower.Contains("shadow") || lower.Contains("preview"))
                continue;

            bool matched = false;
            foreach (var name in mapNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                string lowerName = name.ToLower();
                if (lower.Contains(lowerName) || lowerName.Contains(lower))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched) continue;

            Debug.Log($"[MapManager] Eliminando mapa existente en escena: {go.name}");
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }

    GameObject CrearMapaProcedural(ConfigMapa config)
    {
        GameObject raiz = new GameObject(config?.nombre ?? "Mapa Procedural");
        raiz.transform.position = Vector3.zero;

        switch (config?.nombre)
        {
            case "Habitacion de javi":
                CrearPlano(raiz, new Vector3(20f, 1f, 20f), new Vector3(0f, -0.5f, 0f), Color.gray);
                CrearCaja(raiz, new Vector3(0.5f, 3f, 20f), new Vector3(-10f, 1.5f, 0f), Color.white);
                CrearCaja(raiz, new Vector3(0.5f, 3f, 20f), new Vector3(10f, 1.5f, 0f), Color.white);
                CrearCaja(raiz, new Vector3(20f, 3f, 0.5f), new Vector3(0f, 1.5f, -10f), Color.white);
                CrearCaja(raiz, new Vector3(20f, 3f, 0.5f), new Vector3(0f, 1.5f, 10f), Color.white);
                CrearCaja(raiz, new Vector3(2f, 0.4f, 2f), new Vector3(0f, 0.2f, 0f), Color.yellow);
                break;

            case "Mapa Infinito":
                CrearPlano(raiz, new Vector3(80f, 1f, 80f), new Vector3(0f, -0.5f, 0f), new Color(0.2f, 0.4f, 0.7f));
                for (int i = 0; i < 12; i++)
                {
                    float x = (i - 6) * 6f;
                    CrearCaja(raiz, new Vector3(2f, 2f, 2f), new Vector3(x, 1f, 0f), Color.cyan);
                }
                break;

            case "Mapa Nube":
                CrearPlano(raiz, new Vector3(60f, 1f, 60f), new Vector3(0f, -0.5f, 0f), new Color(0.85f, 0.95f, 1f));
                for (int i = 0; i < 6; i++)
                {
                    float x = (i - 2.5f) * 4f;
                    CrearEsfera(raiz, new Vector3(2f, 2f, 2f), new Vector3(x, 2f, 0f), Color.white);
                }
                break;

            default:
                CrearPlano(raiz, new Vector3(20f, 1f, 20f), new Vector3(0f, -0.5f, 0f), Color.gray);
                break;
        }

        return raiz;
    }

    void CrearPlano(GameObject padre, Vector3 escala, Vector3 posicion, Color color)
    {
        GameObject plano = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plano.transform.SetParent(padre.transform, false);
        plano.transform.localScale = escala;
        plano.transform.localPosition = posicion;

        Renderer renderer = plano.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
    }

    void CrearCaja(GameObject padre, Vector3 escala, Vector3 posicion, Color color)
    {
        GameObject caja = GameObject.CreatePrimitive(PrimitiveType.Cube);
        caja.transform.SetParent(padre.transform, false);
        caja.transform.localScale = escala;
        caja.transform.localPosition = posicion;

        Renderer renderer = caja.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
    }

    void CrearEsfera(GameObject padre, Vector3 escala, Vector3 posicion, Color color)
    {
        GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera.transform.SetParent(padre.transform, false);
        esfera.transform.localScale = escala;
        esfera.transform.localPosition = posicion;

        Renderer renderer = esfera.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
    }

    // -------------------------------------------------------
    public int GetMapaSeleccionado() => mapaSeleccionado;

    
   
}