using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// Maneja la selección, activación e instanciación de mapas.
///
/// Cada escenario conserva SIEMPRE el transform con el que fue creado: si el mapa ya
/// existe como GameObject en la escena se activa/desactiva ese mismo objeto, y si hay
/// que instanciar su prefab se copia tal cual (sin sobrescribir posición, rotación ni
/// escala). Así la escala dinámica del Inspector no deforma los mapas.
/// </summary>
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    /// <summary>
    /// Nombre del mapa que se usa cuando un jefe no tiene mapa asignado o cuando
    /// el mapa indicado no existe. Se resuelve con la búsqueda tolerante por nombre.
    /// </summary>
    public const string MAPA_POR_DEFECTO = "Mapa Nube";

    /// <summary>
    /// Índice interno (ver ConfigPorIndice) del mapa "Mapa Infinito", cuyo escenario
    /// es el prefab "habitacion_Infinito Variant". Es el escenario FIJO del jefe Mirage.
    /// </summary>
    public const int INDICE_MAPA_INFINITO = 2;

    /// <summary>
    /// Nombre del escenario FIJO del jefe Mirage ("habitacion_Infinito Variant").
    /// SeleccionarMapaBoss() lo fuerza cuando el jefe Mirage no trae un mapa propio
    /// (BossData.mapaAsociado / nombreMapa vacíos o sin coincidencia), de modo que su
    /// combate nunca acabe en el mapa por defecto.
    /// </summary>
    public const string MAPA_JEFE_MIRAGE = "habitacion_Infinito Variant";

    // ─── Transform EXACTO del escenario del jefe Mirage ("habitacion_Infinito Variant") ───
    /// <summary>
    /// Posición (en coordenadas de MUNDO) con la que se coloca el escenario
    /// "habitacion_Infinito Variant" al instanciarlo. Coincide con el valor autorizado
    /// en el Inspector (MapManager.mapaInfinito.posicion y BossData[Mirage].mapaAsociado.posicion).
    /// El prefab de ese escenario es una VARIANTE cuya raíz está guardada en
    /// (-572.3073, 0, 878.2168), así que sin este forzado el mapa aparece lejísimos del
    /// centro de la mesa.
    /// </summary>
    public static readonly Vector3 POSICION_MAPA_INFINITO = new Vector3(-20.3f, 40f, 163.4f);

    /// <summary>Rotación (grados de MUNDO) del escenario "habitacion_Infinito Variant".</summary>
    public static readonly Vector3 ROTACION_MAPA_INFINITO = new Vector3(0f, 0f, 0f);

    /// <summary>Escala del escenario "habitacion_Infinito Variant" (348, 94, 6).</summary>
    public static readonly Vector3 ESCALA_MAPA_INFINITO = new Vector3(348f, 94f, 6f);

    [System.Serializable]
    public class ConfigMapa
    {
        public string     nombre;
        public GameObject prefab;
        [Tooltip("Posición (MUNDO) con la que se coloca el mapa al instanciarlo. Se aplica explícitamente después de emparentarlo a MapaContenedor. Si se deja en (0,0,0) con escala 1 y rotación 0 se conserva el transform del prefab.")]
        public Vector3    posicion = Vector3.zero;
        [Tooltip("Escala del mapa al instanciarlo (348, 94, 6) para los escenarios tipo habitación. Si se deja en (1,1,1) se conserva la del prefab.")]
        public Vector3    escala   = Vector3.one;
        [Tooltip("Rotación (grados de MUNDO) del mapa al instanciarlo. Si se deja en (0,0,0) se conserva la del prefab.")]
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
        // Valores EXACTOS del escenario "habitacion_Infinito Variant": su prefab (variante)
        // guarda la raíz en (-572.3, 0, 878.2), así que el transform correcto se aplica
        // al instanciar (ver InstanciarMapa / AplicarTransformDeConfig).
        posicion = new Vector3(-20.3f, 40f, 163.4f),
        escala   = new Vector3(348f, 94f, 6f),
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
    /// <summary>
    /// true si el mapa activo es un GameObject que YA existía en la escena
    /// (preconfigurado a mano). En ese caso solo se activa/desactiva: nunca se
    /// destruye ni se le modifica el transform.
    /// </summary>
    private bool       mapaActualEsDeEscena = false;

    /// <summary>
    /// Mapa forzado por el jefe ACTIVO (null = sin forzado). Hoy solo lo usa Mirage
    /// ("habitacion_Infinito Variant"). Evita que un BossData ANÓNIMO —el que llevan
    /// los MonoBehaviour de jefe colocados en la escena, que normalmente está VACÍO—
    /// arrastre el escenario al mapa por defecto durante el combate de un jefe con
    /// escenario FIJO (era el motivo de que Mirage acabara en "Mapa Nube"/MapaNubesV11).
    /// Se limpia en cuanto se selecciona el mapa de otro jefe.
    /// </summary>
    private string mapaForzadoJefeActivo;

    // -------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        CrearContenedorDeMapas();

        // Los mapas colocados a mano en la escena NO se destruyen: solo se desactivan
        // (se reactivarán al seleccionar su mapa). Así cada escenario conserva el
        // transform con el que fue configurado en la escena.
        DesactivarMapasDeEscenaExcepto(null);
    }

    /// <summary>
    /// Crea (si no existe) el GameObject contenedor vacío donde MapManager instancia
    /// los mapas. Se crea en Awake y también de forma perezosa antes de instanciar un
    /// mapa: si por orden de ejecución no existiera todavía, el mapa acabaría suelto
    /// en la raíz de la escena y podría quedar fuera de la vista o ser desactivado por
    /// la limpieza de escenarios.
    /// </summary>
    void CrearContenedorDeMapas()
    {
        if (mapaContenedor != null) return;

        mapaContenedor = new GameObject("MapaContenedor").transform;
        mapaContenedor.SetParent(null, false);
        mapaContenedor.position = Vector3.zero;
        mapaContenedor.rotation = Quaternion.identity;
        mapaContenedor.localScale = Vector3.one;
        mapaContenedor.gameObject.SetActive(true);
    }

    // -------------------------------------------------------
    private float anguloSkybox = 0f;

    void Update()
    {
        // Rotar el skybox del mapa actual si tiene velocidad.
        // (Se eliminó el Debug.Log por frame: generaba spam constante en consola.)
        if (RenderSettings.skybox == null) return;

        ConfigMapa config = ObtenerConfigActual();
        if (config == null || config.velocidadSkybox == 0f) return;

        anguloSkybox = Mathf.Repeat(anguloSkybox + config.velocidadSkybox * Time.deltaTime, 360f);
        RenderSettings.skybox.SetFloat("_Rotation", anguloSkybox);
    }

    /// <summary>
    /// Configuración del mapa que está activo ahora mismo.
    /// Se prioriza mapaActualConfig para que funcione también con mapas EXTERNOS
    /// (por ejemplo el mapa de un jefe cargado desde BossData.mapaAsociado) y solo
    /// se cae al índice clásico cuando no hay mapa instanciado.
    /// </summary>
    ConfigMapa ObtenerConfigActual()
    {
        if (mapaActualConfig != null) return mapaActualConfig;

        if (mapaSeleccionado < 0 || mapaSeleccionado > 7) return null;
        return ConfigPorIndice(mapaSeleccionado);
    }

    /// <summary>
    /// Devuelve la configuración de mapa correspondiente a un índice (0-7), o null
    /// si el índice está fuera de rango. Búsqueda por índice segura: nunca lanza
    /// excepción ni devuelve una configuración equivocada (y no escribe log, para
    /// poder usarse por frame sin ensuciar la consola).
    /// </summary>
    public ConfigMapa ConfigPorIndice(int indice)
    {
        switch (indice)
        {
            case 0: return mapaHabitacion;
            case 1: return mapaHabitacionJavi;
            case 2: return mapaInfinito;
            case 3: return mapaNube;
            case 4: return mapaSpace;
            case 5: return mapaHielo;
            case 6: return mapaLava;
            case 7: return mapaPractica;
            default: return null;
        }
    }

    // ─── Lista de mapas DISPONIBLES (mapasDisponibles) ───
    /// <summary>
    /// Lista de mapas DISPONIBLES en este MapManager: las configuraciones 0-7 del
    /// Inspector, sin nulos ni repetidos. Es la lista que consulta el forzado del
    /// escenario de Mirage para localizar la configuración de "Mapa Infinito"
    /// (nombre "Mapa Infinito" o prefab "habitacion_Infinito Variant") y, si esa
    /// configuración no estuviera, para cargar el PRIMER mapa disponible como
    /// respaldo: así la escena nunca se queda sin escenario.
    /// </summary>
    public List<ConfigMapa> MapasDisponibles()
    {
        List<ConfigMapa> lista = new List<ConfigMapa>();

        for (int i = 0; i < 8; i++)
        {
            ConfigMapa config = ConfigPorIndice(i);
            if (config == null || lista.Contains(config)) continue;
            lista.Add(config);
        }

        return lista;
    }

    /// <summary>
    /// Configuración de "Mapa Infinito" dentro de la lista mapasDisponibles.
    /// Se localiza por el nombre configurado del mapa ("Mapa Infinito") o por el
    /// nombre de su prefab ("habitacion_Infinito Variant"), con la comparación
    /// tolerante de NormalizarNombreMapa (ignora mayúsculas, espacios, guiones y
    /// tildes). Devuelve null si esa configuración no existe en la lista.
    /// </summary>
    public ConfigMapa ObtenerConfigMapaInfinito()
    {
        const string claveNombre  = "mapainfinito";
        const string clavePrefab  = "habitacioninfinitovariant";
        const string clavePrefab2 = "habitacioninfinito";

        foreach (ConfigMapa config in MapasDisponibles())
        {
            if (NormalizarNombreMapa(config.nombre) == claveNombre) return config;

            if (config.prefab == null) continue;

            string clave = NormalizarNombreMapa(config.prefab.name);
            if (clave == clavePrefab || clave == clavePrefab2) return config;
        }

        return null;
    }
    // ─── Transform de los mapas al instanciarlos ───
    /// <summary>
    /// Devuelve true si la configuración corresponde al escenario del jefe Mirage
    /// ("habitacion_Infinito Variant"): por nombre configurado ("Mapa Infinito"), por
    /// el nombre de su prefab o por cualquiera de los alias ("infinito", "habitacion_infinito").
    /// </summary>
    public static bool EsConfigDelMapaInfinito(ConfigMapa config)
    {
        if (config == null) return false;

        string clave = NormalizarNombreMapa(config.nombre);
        if (clave == "mapainfinito" || clave == "habitacioninfinito" ||
            clave == "habitacioninfinitovariant" || clave == "infinito")
            return true;

        if (config.prefab != null)
        {
            string clavePrefab = NormalizarNombreMapa(config.prefab.name);
            if (clavePrefab == "habitacioninfinitovariant" || clavePrefab == "habitacioninfinito" ||
                clavePrefab == "mapainfinito" || clavePrefab == "infinito")
                return true;
        }

        if (ALIAS_INDICE_MAPA.TryGetValue(clave, out int indice))
            return indice == INDICE_MAPA_INFINITO;

        return false;
    }

    /// <summary>
    /// true si la configuración trae un transform autorizado explícitamente en el
    /// Inspector (posición, rotación o escala distintos de los neutros). En ese caso
    /// ese transform MANDA sobre el del prefab.
    /// </summary>
    public static bool TieneTransformExplicito(ConfigMapa config) =>
        config != null &&
        (config.posicion != Vector3.zero ||
         config.rotacion != Vector3.zero ||
         config.escala   != Vector3.one);

    /// <summary>
    /// Aplica al mapa recién instanciado el transform de su configuración, asignando
    /// POSICIÓN y ROTACIÓN de MUNDO (no locales) inmediatamente después de emparentarlo
    /// a MapaContenedor: así el mapa NO hereda ningún desplazamiento del contenedor.
    ///
    /// Reglas:
    ///   • Mapa Infinito (Mirage): se fuerzan SIEMPRE las constantes exactas
    ///     POSICION/ROTACION/ESCALA_MAPA_INFINITO cuando la configuración no trae sus
    ///     propios valores (y los del Inspector cuando sí los trae, que son los mismos
    ///     por diseño).
    ///   • Resto de mapas: se aplica el transform del Inspector si es explícito; si es
    ///     neutro (0/1), se conserva el transform del prefab (comportamiento clásico).
    /// </summary>
    void AplicarTransformDeConfig(GameObject mapa, ConfigMapa config)
    {
        if (mapa == null || config == null) return;

        bool esInfinito = EsConfigDelMapaInfinito(config);

        Vector3 posicion = config.posicion;
        Vector3 rotacion = config.rotacion;
        Vector3 escala   = config.escala;

        if (!TieneTransformExplicito(config))
        {
            if (!esInfinito) return; // sin transform explícito: se conserva el del prefab

            // Configuración neutra para el escenario Infinito: se usan los valores EXACTOS.
            posicion = POSICION_MAPA_INFINITO;
            rotacion = ROTACION_MAPA_INFINITO;
            escala   = ESCALA_MAPA_INFINITO;
        }

        AplicarTransformForzado(mapa, posicion, rotacion, escala,
            esInfinito ? $"escenario Infinito ({MAPA_JEFE_MIRAGE})" : $"config '{config.nombre}'");
    }

    /// <summary>
    /// Fuerza posición (MUNDO), rotación (MUNDO) y escala de un mapa creado por
    /// MapManager. El contenedor se normaliza a identidad para que la escala asignada
    /// sea exactamente la escala mundial pedida.
    /// </summary>
    void AplicarTransformForzado(GameObject mapa, Vector3 posicion, Vector3 rotacion, Vector3 escala, string motivo)
    {
        if (mapa == null)
        {
            Debug.LogError($"[MapManager] AplicarTransformForzado: mapa nulo ({motivo}).");
            return;
        }

        // El mapa cuelga del contenedor pero sin heredar su transform (worldPositionStays
        // = false); la posición y la rotación se asignan como MUNDO justo después.
        if (mapaContenedor != null) mapa.transform.SetParent(mapaContenedor, false);

        // El contenedor es un objeto interno de MapManager: se garantiza en identidad para
        // que la escala local asignada equivalga a la escala mundial exacta.
        if (mapaContenedor != null &&
            (mapaContenedor.position != Vector3.zero ||
             mapaContenedor.rotation != Quaternion.identity ||
             mapaContenedor.localScale != Vector3.one))
        {
            Debug.LogWarning($"[MapManager] MapaContenedor no estaba en identidad (pos={mapaContenedor.position} rot={mapaContenedor.eulerAngles} escala={mapaContenedor.localScale}); se normaliza para aplicar el transform exacto ({motivo}).");
            mapaContenedor.position   = Vector3.zero;
            mapaContenedor.rotation   = Quaternion.identity;
            mapaContenedor.localScale = Vector3.one;
        }

        mapa.transform.position    = posicion;  // MUNDO exacto (evita el desplazamiento por herencia)
        mapa.transform.eulerAngles = rotacion;  // MUNDO exacto
        mapa.transform.localScale  = escala;    // el contenedor está en identidad

        Physics.SyncTransforms();

        Debug.Log($"[MapManager] Transform aplicado al mapa '{mapa.name}' ({motivo}): posicion={mapa.transform.position} rotacion={mapa.transform.eulerAngles} escala={mapa.transform.lossyScale}");
    }

    /// <summary>
    /// Reafirma el transform EXACTO del escenario del jefe Mirage sobre el mapa activo.
    /// Se llama tras cargar/forzar el escenario Infinito, de modo que aunque el mapa ya
    /// estuviera cargado quede siempre en (-20.3, 40, 163.4) / rotación 0 / escala (348, 94, 6).
    /// </summary>
    /// <returns>true si el transform pudo aplicarse.</returns>
    public bool ForzarTransformDelMapaInfinito()
    {
        if (mapaActual == null)
        {
            Debug.LogError("[MapManager] No se puede forzar el transform del escenario Infinito: no hay mapa activo (mapaActual = null).");
            return false;
        }

        // Invariante de MapManager: a los mapas colocados a mano en la escena NUNCA se les
        // toca el transform (se respeta el que tienen configurado en la escena).
        if (mapaActualEsDeEscena)
        {
            Debug.LogWarning($"[MapManager] El mapa activo '{mapaActual.name}' es un objeto de la ESCENA: se respeta su transform original y no se fuerza el del escenario Infinito.");
            return false;
        }

        AplicarTransformForzado(mapaActual, POSICION_MAPA_INFINITO, ROTACION_MAPA_INFINITO, ESCALA_MAPA_INFINITO,
            $"forzado final del jefe Mirage ({MAPA_JEFE_MIRAGE})");
        return true;
    }


    // -------------------------------------------------------
    // ─── Selección de mapa por índice (botones del Inspector) ───
    //     Todos delegan en SeleccionarMapaPorIndice(), que valida el índice,
    //     guarda la selección e instancia el mapa correspondiente.
    public void SeleccionarHabitacion()     { SeleccionarMapaPorIndice(0); }
    public void SeleccionarHabitacionJavi() { SeleccionarMapaPorIndice(1); }
    public void SeleccionarInfinito()       { SeleccionarMapaPorIndice(2); }
    public void SeleccionarNube()           { SeleccionarMapaPorIndice(3); }
    public void SeleccionarSpace()          { SeleccionarMapaPorIndice(4); }
    public void SeleccionarHielo()          { SeleccionarMapaPorIndice(5); }
    public void SeleccionarLava()           { SeleccionarMapaPorIndice(6); }
    public void SeleccionarPractica()       { SeleccionarMapaPorIndice(7); }

    /// <summary>
    /// Selecciona e instancia un mapa por su índice
    /// (0 = Habitación Vacía ... 7 = Habitación Práctica).
    /// Los índices fuera de rango se rechazan con un aviso, sin excepciones.
    /// </summary>
    /// <returns>true si el mapa quedó activo; false si el índice no es válido.</returns>
    public bool SeleccionarMapaPorIndice(int indice)
    {
        ConfigMapa config = ConfigPorIndice(indice);
        if (config == null)
        {
            Debug.LogWarning($"[MapManager] SeleccionarMapaPorIndice: índice fuera de rango: {indice}");
            return false;
        }

        mapaSeleccionado = indice;
        InstanciarMapa(config);
        return mapaActual != null;
    }

    // ─── Utilidades de nombre de mapa ───
    /// <summary>
    /// Normaliza el nombre de un mapa para compararlo de forma tolerante:
    /// elimina tildes, mayúsculas, espacios, guiones y guiones bajos.
    /// Así "Mapa Nube", " MapaNube ", "mapa_nube", "mapa-nube" y "MAPANUBE"
    /// producen la misma clave. Evita que una diferencia de espacios o de
    /// acentos impida encontrar el escenario.
    /// </summary>
    public static string NormalizarNombreMapa(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return string.Empty;

        // Descomponer en carácter base + marca diacrítica para poder quitar la tilde.
        string descompuesto = nombre.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder(descompuesto.Length);

        foreach (char c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(c) || c == '_' || c == '-') continue;
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Alias explícitos de textos de mapa -> índice interno (ConfigPorIndice).
    /// Cubren los casos en los que el texto NO coincide literalmente con el nombre
    /// configurado ni con el nombre del prefab de esa configuración (por ejemplo
    /// porque el prefab aún no está asignado en el Inspector, o porque el escenario
    /// se llama como su archivo de prefab: "habitacion_Infinito Variant", "MapaNubesV11",
    /// "Mapa de lavaV1 Variant"). Las claves ya vienen normalizadas con NormalizarNombreMapa().
    /// </summary>
    static readonly Dictionary<string, int> ALIAS_INDICE_MAPA = new Dictionary<string, int>
    {
        // ── Mapa Infinito (índice 2) ──
        { "habitacioninfinitovariant", INDICE_MAPA_INFINITO }, // escenario "habitacion_Infinito Variant"
        { "habitacioninfinito",        INDICE_MAPA_INFINITO }, // "habitacion_infinito"
        { "mapainfinito",              INDICE_MAPA_INFINITO }, // "mapa infinito" / nombre configurado "Mapa Infinito"
        { "infinito",                  INDICE_MAPA_INFINITO }, // "infinito"

        // ── Mapa Nube (índice 3) ──
        { "mapanubesv11", 3 }, // escenario "MapaNubesV11"
        { "mapanubev11",  3 },
        { "mapanubes",    3 },

        // ── Mapa de Lava (índice 6) ──
        { "mapadelavav1variant", 6 }, // escenario "Mapa de lavaV1 Variant"
        { "mapadelava",          6 },
        { "lavav1variant",       6 }
    };

    /// <summary>
    /// Resuelve el índice (0-7) del mapa cuyo nombre configurado —o cuyo nombre de
    /// prefab— coincide con el nombre indicado, y consulta los alias explícitos
    /// (ALIAS_INDICE_MAPA) cuando no hay coincidencia directa.
    /// Devuelve -1 si no hay ninguna coincidencia.
    /// </summary>
    public int IndiceDeMapaPorNombre(string nombre)
    {
        string clave = NormalizarNombreMapa(nombre);
        if (clave.Length == 0) return -1;

        // Sufijo de duplicado de Unity: "Mapa Infinito (1)" -> "mapainfinito".
        int parentesis = clave.IndexOf('(');
        if (parentesis > 0) clave = clave.Substring(0, parentesis);

        for (int i = 0; i < 8; i++)
        {
            ConfigMapa config = ConfigPorIndice(i);
            if (config == null) continue;

            // Coincidencia por el nombre configurado del mapa...
            if (NormalizarNombreMapa(config.nombre) == clave) return i;

            // ...o por el nombre del prefab asignado a esa configuración.
            if (config.prefab != null && NormalizarNombreMapa(config.prefab.name) == clave) return i;
        }

        // Alias explícitos (Infinito, Nubes, Lava...): textos de escenario conocidos que
        // no coinciden con ningún nombre de mapa configurado ni con el de su prefab.
        if (ALIAS_INDICE_MAPA.TryGetValue(clave, out int indiceAlias)) return indiceAlias;

        return -1;
    }

    // ─── Selección de mapa por nombre (para jefes) ───
    /// <summary>
    /// Devuelve true si el jefe indicado es Mirage, comparando por nombre e ignorando
    /// mayúsculas/minúsculas, espacios y tildes ("Mirage", "mirage", " JEFE MIRAGE ").
    /// Se usa para asignarle su escenario FIJO ("habitacion_Infinito Variant") cuando
    /// su BossData no trae un mapa propio que resuelva.
    /// </summary>
    public static bool EsJefeMirage(BossData jefe) =>
        jefe != null &&
        !string.IsNullOrWhiteSpace(jefe.nombre) &&
        jefe.nombre.Trim().ToLowerInvariant().Contains("mirage");

    /// <summary>
    /// Selecciona un mapa por su nombre, reutilizando los métodos de selección existentes.
    /// </summary>
    /// <param name="nombre">Nombre del mapa (ej: "Mapa Nube").</param>
    /// <returns>true si el mapa fue encontrado y seleccionado; false si no existe.</returns>
    public bool SeleccionarMapaPorNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            Debug.LogWarning("[MapManager] SeleccionarMapaPorNombre: nombre de mapa vacío o nulo.");
            return false;
        }

        // Búsqueda TOLERANTE por nombre: se ignoran mayúsculas/minúsculas, espacios
        // sobrantes, guiones y tildes, y también se acepta el nombre del prefab
        // asignado a cada configuración. Antes se usaba un switch estricto que
        // fallaba con cualquier variación mínima del texto (espacio final, tilde...).
        int indice = IndiceDeMapaPorNombre(nombre);
        if (indice >= 0)
        {
            Debug.Log($"[MapManager] 2. SeleccionarMapaPorNombre(\"{nombre}\") -> mapa #{indice}");
            return SeleccionarMapaPorIndice(indice);
        }

        Debug.LogWarning("[MapManager] No existe un mapa con el nombre: " + nombre);
        return false;
    }

    /// <summary>
    /// Selecciona y activa un mapa a partir de su configuración (ConfigMapa).
    ///
    /// IMPORTANTE: si la configuración trae un PREFAB asignado, éste se instancia
    /// DIRECTAMENTE (con su propia posición, escala, rotación y skybox), SIN buscar
    /// nada por nombre. La búsqueda por nombre queda solo como respaldo para
    /// configuraciones sin prefab (mapas internos/procedurales).
    /// Así el mapa de un jefe (BossData.mapaAsociado) se carga siempre que tenga
    /// prefab, aunque su nombre no coincida con ningún mapa interno.
    /// </summary>
    /// <param name="config">Configuración del mapa (nombre, prefab, posición, etc.).</param>
    /// <returns>true si el mapa quedó activo; false si no se pudo cargar.</returns>
    public bool SeleccionarMapa(ConfigMapa config)
    {
        if (config == null)
        {
            Debug.LogWarning("[MapManager] SeleccionarMapa: ConfigMapa nulo.");
            return false;
        }

        // ─ 1. Prefab explícito: instanciarlo directamente, sin buscar por nombre. ──
        if (config.prefab != null)
        {
            if (string.IsNullOrEmpty(config.nombre)) config.nombre = config.prefab.name;

            Debug.Log($"[MapManager] SeleccionarMapa: usando la configuración '{config.nombre}' (prefab '{config.prefab.name}').");
            InstanciarMapa(config);
            SincronizarIndiceConConfig(config);
            return mapaActual != null;
        }

        // ── 2. Sin prefab: resolver por nombre entre los mapas internos. ──
        if (!string.IsNullOrEmpty(config.nombre))
        {
            int indice = IndiceDeMapaPorNombre(config.nombre);
            if (indice >= 0)
            {
                Debug.Log($"[MapManager] SeleccionarMapa: config '{config.nombre}' sin prefab -> mapa interno #{indice}.");
                return SeleccionarMapaPorIndice(indice);
            }

            //  3. Sin prefab y con nombre desconocido: mapa procedural con esos datos. ─
            Debug.LogWarning($"[MapManager] SeleccionarMapa: '{config.nombre}' no tiene prefab ni mapa interno equivalente. Generando mapa procedural.");
            InstanciarMapa(config);
            mapaSeleccionado = -1; // mapa externo/procedural: no corresponde a ningún índice
            return mapaActual != null;
        }

        Debug.LogWarning("[MapManager] SeleccionarMapa: ConfigMapa sin nombre ni prefab.");
        return false;
    }

    /// <summary>
    /// Sincroniza el índice interno (mapaSeleccionado) cuando la configuración activada
    /// coincide con uno de los mapas internos. Si es una configuración externa (jefe),
    /// deja el índice en -1: así ObtenerConfigActual() usa mapaActualConfig y no el
    /// índice clásico (que ya no representaría al mapa realmente activo).
    /// </summary>
    void SincronizarIndiceConConfig(ConfigMapa config)
    {
        if (config == null)
        {
            mapaSeleccionado = -1;
            return;
        }

        for (int i = 0; i < 8; i++)
        {
            if (ConfigPorIndice(i) == config)
            {
                mapaSeleccionado = i;
                return;
            }
        }

        mapaSeleccionado = -1;
    }

    /// <summary>
    /// Fuerza el escenario FIJO del jefe Mirage ("habitacion_Infinito Variant").
    /// Se usa desde SeleccionarMapaBoss() (prioridad ABSOLUTA, antes de cualquier otra
    /// evaluación) y desde BossManager.StartBossBattle() cuando el jefe es Mirage.
    ///
    /// Flujo directo y seguro (sin depender de alias ni de índices):
    ///   1. Localiza el ConfigMapa de la lista mapasDisponibles() cuyo nombre sea
    ///      "Mapa Infinito" o cuyo prefab sea "habitacion_Infinito Variant" e invoca
    ///      InstanciarMapa(config) DIRECTAMENTE con esa configuración.
    ///   2. Si esa configuración NO está en la lista, lo indica con Debug.LogError
    ///      (referencia nula) y continúa con los respaldos.
    ///   3. Respaldos: prefab del BossData del jefe, resolución por nombre/alias, índice
    ///      interno del Mapa Infinito.
    ///   4. Respaldo FINAL garantizado: carga el PRIMER mapa disponible de la lista en
    ///      lugar de dejar la escena vacía.
    ///
    /// Deja marcado el forzado del jefe activo para que un BossData anónimo posterior
    /// (el del MonoBehaviour del jefe en la escena, normalmente VACÍO) NO pueda
    /// devolver el escenario al mapa por defecto ("Mapa Nube" / MapaNubesV11).
    /// </summary>
    /// <param name="jefe">BossData del jefe seleccionado (opcional). Se usa para
    /// aprovechar el prefab de su mapaAsociado si la lista no lo tuviera asignado.</param>
    /// <returns>true si quedó activo un mapa; false solo si no había NINGÚN mapa cargable.</returns>
    public bool ForzarMapaJefeMirage(BossData jefe = null)
    {
        Debug.Log("[MapManager] Boss Mirage detectado. Forzando carga de 'habitacion_Infinito Variant'.");

        // Marcar el forzado del jefe ACTIVO antes de intentar cargar: si el prefab del
        // mapa Infinito no estuviera asignado, el fallback por defecto tampoco deberá
        // aplicarse durante el combate de Mirage.
        mapaForzadoJefeActivo = MAPA_JEFE_MIRAGE;

        List<ConfigMapa> disponibles = MapasDisponibles();

        // ── 1. ConfigMapa del Mapa Infinito tomada DIRECTAMENTE de mapasDisponibles. ──
        ConfigMapa configInfinito = ObtenerConfigMapaInfinito();

        if (configInfinito == null)
        {
            Debug.LogError(
                $"[MapManager] La lista mapasDisponibles ({disponibles.Count} mapas) NO contiene la configuración del Mapa Infinito: " +
                $"ningún mapa se llama 'Mapa Infinito' ni tiene asignado el prefab '{MAPA_JEFE_MIRAGE}'. " +
                "Asigna ese prefab a la configuración 'Mapa Infinito' (o dale ese nombre) en el Inspector de MapManager. " +
                "Se intentará con los respaldos.");
        }
        else
        {
            Debug.Log(
                $"[MapManager] Config del Mapa Infinito encontrada en mapasDisponibles: nombre='{configInfinito.nombre}'" +
                $" | prefab='{(configInfinito.prefab != null ? configInfinito.prefab.name : "SIN PREFAB (se generará procedural)")}'.");
        }

        // ── 2. InstanciarMapa DIRECTAMENTE con esa configuración (sin alias ni índices). ──
        if (configInfinito != null && InstanciarMapa(configInfinito))
        {
            SincronizarIndiceConConfig(configInfinito);

            // Reafirmación FINAL del transform: el escenario Infinito queda EXACTAMENTE en
            // (-20.3, 40, 163.4) / rotación 0 / escala (348, 94, 6), aunque su config traiga
            // otros valores o el mapa ya estuviera cargado.
            ForzarTransformDelMapaInfinito();

            LogEstadoMapaActivo("forzado Mirage (config de mapasDisponibles)");
            return true;
        }

        if (configInfinito != null)
        {
            Debug.LogWarning("[MapManager] La configuración del Mapa Infinito existe pero no se pudo instanciar. Se intentará con los respaldos.");
        }

        // ── 3. Respaldo: prefab del propio jefe (la vía que ya funcionaba antes). ──
        if (jefe != null && jefe.mapaAsociado != null && jefe.mapaAsociado.prefab != null)
        {
            Debug.Log($"[MapManager] Respaldo: usando el prefab '{jefe.mapaAsociado.prefab.name}' del mapaAsociado del jefe '{jefe.nombre}'.");

            if (SeleccionarMapa(jefe.mapaAsociado))
            {
                ForzarTransformDelMapaInfinito();
                LogEstadoMapaActivo("respaldo con el prefab del BossData");
                return true;
            }
        }

        // ── 4. Respaldo: resolución tolerante por nombre/alias y por índice interno. ──
        if (configInfinito == null)
        {
            if (SeleccionarMapaPorNombre(MAPA_JEFE_MIRAGE))
            {
                ForzarTransformDelMapaInfinito();
                LogEstadoMapaActivo("respaldo por nombre/alias");
                return true;
            }

            Debug.LogWarning($"[MapManager] No se pudo resolver '{MAPA_JEFE_MIRAGE}' por nombre; usando el mapa interno #{INDICE_MAPA_INFINITO} ('Mapa Infinito').");

            if (SeleccionarMapaPorIndice(INDICE_MAPA_INFINITO))
            {
                ForzarTransformDelMapaInfinito();
                LogEstadoMapaActivo("respaldo por índice interno");
                return true;
            }
        }

        // ── 5. Respaldo FINAL: el primer mapa disponible — la escena nunca queda vacía. ──
        return CargarPrimerMapaDisponible("respaldo del forzado de Mirage");
    }

    /// <summary>
    /// Respaldo final: carga el PRIMER mapa disponible de la lista mapasDisponibles()
    /// que tenga algo con lo que crear un escenario (prefab o nombre conocido).
    /// Existe para que un fallo al forzar el escenario de un jefe nunca deje la escena
    /// sin mapa visible.
    /// </summary>
    /// <param name="motivo">Texto para el log (de dónde viene el respaldo).</param>
    /// <returns>true si algún mapa quedó activo; false si no había ninguno cargable.</returns>
    bool CargarPrimerMapaDisponible(string motivo)
    {
        foreach (ConfigMapa config in MapasDisponibles())
        {
            if (config.prefab == null && string.IsNullOrWhiteSpace(config.nombre)) continue;

            Debug.LogWarning($"[MapManager] Cargando el PRIMER mapa disponible de mapasDisponibles ('{config.nombre}') como respaldo ({motivo}).");

            if (InstanciarMapa(config))
            {
                SincronizarIndiceConConfig(config);
                LogEstadoMapaActivo($"{motivo} -> primer mapa disponible");
                return true;
            }
        }

        Debug.LogError($"[MapManager] No se pudo cargar NINGÚN mapa de mapasDisponibles ({motivo}): la escena quedará vacía. Revisa que las configuraciones de mapa de MapManager tengan prefab asignado en el Inspector.");
        return false;
    }

    /// <summary>
    /// Imprime el estado del mapa activo después de una carga/forzado: nombre del
    /// objeto, si está activo en la jerarquía, si cuelga del contenedor, cuántos
    /// renderers tiene y su transform. Sirve para diagnosticar de inmediato el caso
    /// "no aparece ningún mapa" (mapaActual nulo o desactivado).
    /// </summary>
    void LogEstadoMapaActivo(string motivo)
    {
        if (mapaActual == null)
        {
            Debug.LogError($"[MapManager] Estado del mapa tras '{motivo}': mapaActual = NULL (no se cargó ningún escenario).");
            return;
        }

        Debug.Log(
            $"[MapManager] Estado del mapa tras '{motivo}': objeto='{mapaActual.name}'" +
            $" activoSelf={mapaActual.activeSelf} activoEnJerarquia={mapaActual.activeInHierarchy}" +
            $" esDeEscena={mapaActualEsDeEscena} esHijoDeContenedor={(mapaContenedor != null && mapaActual.transform.IsChildOf(mapaContenedor))}" +
            $" renderers={mapaActual.GetComponentsInChildren<Renderer>(true).Length}" +
            $" pos={mapaActual.transform.position} escala={mapaActual.transform.lossyScale}");
    }

    /// <summary>
    /// Activa el escenario (mapa) del jefe de forma 100% centralizada, guiado por
    /// los datos del jefe (BossData.mapaAsociado o BossData.nombreMapa).
    /// Es responsable de cargar/activar el mapa para cualquier jefe sin hardcodear
    /// nombres dentro de los scripts de los jefes. Si el mapa ya está activo,
    /// no se reinstancia (comportamiento idempotente de InstanciarMapa).
    /// </summary>
    /// <param name="jefe">BossData del jefe seleccionado para la batalla.</param>
    /// <returns>true si se activó un mapa; false si no se pudo cargar ninguno.</returns>
    /// <remarks>Los jefes con escenario FIJO (Mirage -> "habitacion_Infinito Variant")
    /// lo reciben aquí aunque su BossData no traiga un mapa propio válido.</remarks>
    public bool SeleccionarMapaBoss(BossData jefe)
    {
        if (jefe == null)
        {
            Debug.LogWarning("[MapManager] SeleccionarMapaBoss: BossData nulo.");
            return false;
        }

        Debug.Log(
            $"[MapManager] SeleccionarMapaBoss: jefe='{jefe.nombre}'" +
            $" | mapaAsociado={(jefe.mapaAsociado != null ? (string.IsNullOrEmpty(jefe.mapaAsociado.nombre) ? "(sin nombre)" : jefe.mapaAsociado.nombre) : "ninguno")}" +
            $" | prefabAsociado={(jefe.mapaAsociado != null && jefe.mapaAsociado.prefab != null ? jefe.mapaAsociado.prefab.name : "ninguno")}" +
            $" | nombreMapa='{(string.IsNullOrEmpty(jefe.nombreMapa) ? "ninguno" : jefe.nombreMapa)}'");

        // ── FORZADO ESTRICTO DEL ESCENARIO DE MIRAGE (prioridad ABSOLUTA) ──
        // Esta comprobación va ANTES de cualquier otra evaluación (mapaAsociado,
        // nombreMapa, alias o mapa por defecto) para que el jefe Mirage NUNCA pueda
        // combatir en otro escenario ni acabar en "Mapa Nube" (MapaNubesV11), aunque
        // su BossData traiga otro mapa asignado.
        // ForzarMapaJefeMirage() localiza el ConfigMapa de la lista mapasDisponibles
        // (nombre "Mapa Infinito" / prefab "habitacion_Infinito Variant") e invoca
        // InstanciarMapa(config) directamente; si esa configuración no estuviera, emite
        // Debug.LogError y cae al primer mapa disponible en vez de dejar la escena vacía.
        if (EsJefeMirage(jefe) || (jefe.nombre != null && jefe.nombre.ToLowerInvariant().Contains("mirage")))
            return ForzarMapaJefeMirage(jefe);

        // Cualquier otro jefe limpia el forzado del jefe anterior.
        mapaForzadoJefeActivo = null;

        // 1) Mapa asociado al jefe (BossData.mapaAsociado). Si trae prefab se
        //    instancia directamente, sin búsquedas por texto.
        if (jefe.mapaAsociado != null)
        {
            if (SeleccionarMapa(jefe.mapaAsociado))
            {
                // El mapa resultante conserva el transform de su propio prefab (o de la
                // instancia ya colocada en la escena): no se sobrescribe nada.
                return true;
            }

            // No se aborta: se intenta el siguiente camino en lugar de devolver
            // false sin cargar nada (que dejaba el escenario anterior activo).
            Debug.LogWarning($"[MapManager] No se pudo activar el mapa asociado de '{jefe.nombre}'. Se intentará por nombre.");
        }

        // 2) Mapa por nombre (BossData.nombreMapa), con búsqueda tolerante.
        if (!string.IsNullOrWhiteSpace(jefe.nombreMapa))
        {
            if (SeleccionarMapaPorNombre(jefe.nombreMapa))
                return true;

            Debug.LogWarning($"[MapManager] No existe el mapa '{jefe.nombreMapa}' del jefe '{jefe.nombre}'.");
        }

        // 3) NOTA: el forzado del escenario FIJO de Mirage ("habitacion_Infinito
        //    Variant") se realiza al INICIO de este método (ForzarMapaJefeMirage), con
        //    prioridad absoluta, para que no lo pueda saltar ninguna otra evaluación
        //    previa (mapaAsociado / nombreMapa / mapa por defecto).

        // 4) Si hay un jefe con escenario FIJO en curso (p. ej. Mirage) y llega un
        //    BossData ANÓNIMO —el de su MonoBehaviour en la escena, que suele estar
        //    VACÍO— NO se aplica el mapa por defecto: se reafirma el escenario del jefe
        //    activo. Sin esta guardia, BossController.IniciarCombate() volvía a llamar
        //    aquí con datos vacíos y recargaba "Mapa Nube" (MapaNubesV11) justo después
        //    de haber cargado "habitacion_Infinito Variant".
        if (!string.IsNullOrWhiteSpace(mapaForzadoJefeActivo))
        {
            Debug.Log($"[MapManager] BossData anónimo ('{jefe.nombre}') durante el combate de un jefe con escenario fijo; se reafirma '{mapaForzadoJefeActivo}' en lugar de '{MAPA_POR_DEFECTO}'.");

            // Reafirmar es forzar de nuevo: pasa por el flujo directo (config de
            // mapasDisponibles + respaldos), así este camino tampoco puede dejar la
            // escena sin mapa.
            if (ForzarMapaJefeMirage(jefe)) return true;

            Debug.LogWarning($"[MapManager] No se pudo reafirmar '{mapaForzadoJefeActivo}' por nombre; usando el mapa interno #{INDICE_MAPA_INFINITO} ('Mapa Infinito').");

            if (SeleccionarMapaPorIndice(INDICE_MAPA_INFINITO)) return true;

            return CargarPrimerMapaDisponible($"reafirmación fallida de '{mapaForzadoJefeActivo}'");
        }

        // 5) El jefe no tiene ningún mapa válido y no hay forzado activo: mapa por defecto.
        Debug.Log($"[MapManager] SeleccionarMapaBoss: '{jefe.nombre}' sin mapa válido, usando '{MAPA_POR_DEFECTO}'.");

        if (SeleccionarMapaPorNombre(MAPA_POR_DEFECTO)) return true;

        // Ni siquiera el mapa por defecto se pudo cargar: se usa el primero disponible
        // para que la escena nunca se quede vacía.
        Debug.LogWarning($"[MapManager] No se pudo cargar el mapa por defecto '{MAPA_POR_DEFECTO}'; usando el primer mapa disponible.");
        return CargarPrimerMapaDisponible("mapa por defecto no disponible");
    }

    // ─── Selección aleatoria de mapa (para torneo) ───
    private static int ultimoMapaTorneo = -1;

    /// <summary>
    /// Selecciona un mapa aleatorio (índice 0-6, excluye práctica).
    /// No repite el mismo mapa de la última llamada.
    /// </summary>
    /// <returns>El índice del mapa seleccionado (0-6).</returns>
    public int SeleccionarMapaAleatorio()
    {
        int nuevoMapa;
        do
        {
            nuevoMapa = Random.Range(0, 7);
        } while (nuevoMapa == ultimoMapaTorneo && ultimoMapaTorneo >= 0);

        int anterior = ultimoMapaTorneo;
        ultimoMapaTorneo = nuevoMapa;

        // Selección por índice: valida el rango, guarda la selección y —antes de
        // instanciar el nuevo mapa— limpia el escenario anterior.
        SeleccionarMapaPorIndice(nuevoMapa);

        Debug.Log($"[MapManager] Mapa torneo: {nuevoMapa} (anterior: {(anterior >= 0 ? anterior.ToString() : "ninguno")})");
        return nuevoMapa;
    }

    // -------------------------------------------------------
    /// <summary>
    /// Activa (o instancia) la configuración de mapa indicada, garantizando que nunca
    /// queden dos escenarios visibles a la vez.
    /// Orden de resolución:
    ///   1. Si la misma configuración ya está activa, no se recarga.
    ///   2. Si el mapa YA existe como GameObject en la escena, se activa/desactiva ese
    ///      objeto (sin instanciar clones) y se conserva su transform tal cual.
    ///   3. Si no existe en la escena, se instancia su prefab SIN sobrescribir
    ///      posición, rotación ni escala: la copia mantiene el transform del prefab.
    /// El mapa anterior se destruye solo si lo creó MapManager; si era un objeto de la
    /// escena, únicamente se desactiva.
    /// </summary>
    /// <returns>true si el mapa indicado quedó cargado y activo; false si no se pudo.</returns>
    bool InstanciarMapa(ConfigMapa config)
    {
        if (config == null)
        {
            Debug.LogWarning("[MapManager] Configuración de mapa nula");
            return false;
        }

        // El contenedor se crea en Awake; se asegura aquí (creación perezosa) para que
        // el mapa nunca se instancie suelto en la raíz de la escena.
        CrearContenedorDeMapas();

        // ── 1. Idempotencia: si la MISMA configuración ya está activa, no recargar. ──
        if (mapaActual != null && MismaConfig(mapaActualConfig, config))
        {
            // Reactivar por si algo lo desactivó. No se modifica su transform.
            if (!mapaActual.activeSelf) mapaActual.SetActive(true);

            AplicarSkybox(config);
            mapaActualConfig = config;

            // El escenario Infinito (jefe Mirage) debe quedar SIEMPRE en su transform exacto,
            // aunque el mapa ya viniera cargado de una selección anterior. Si el mapa es un
            // objeto de la escena se respeta su transform (invariante de MapManager).
            if (!mapaActualEsDeEscena && EsConfigDelMapaInfinito(config))
                AplicarTransformDeConfig(mapaActual, config);

            Debug.Log($"[MapManager] El mapa {config.nombre} ya está activo, no se instancia de nuevo.");
            return true;
        }

        // ── 2. ¿Ese mapa ya está colocado en la escena como GameObject? ──
        //    Si es así se reutiliza el objeto de la escena (activar/desactivar), sin
        //    instanciar ningún clon ni modificar su transform: conserva exactamente la
        //    posición, la rotación y la escala configuradas en la escena.
        GameObject mapaDeEscena = BuscarMapaEnEscena(config);

        // ── 3. Liberar el mapa anterior: se destruye solo si lo creó MapManager; si es
        //    un objeto de la escena, únicamente se desactiva. ──
        LimpiarMapaActual();

        if (mapaDeEscena != null)
        {
            // Dejar visible solo el mapa pedido (los demás mapas de escena se apagan).
            DesactivarMapasDeEscenaExcepto(mapaDeEscena);

            mapaActual           = mapaDeEscena;
            mapaActualEsDeEscena = true;
            mapaActualConfig     = config;

            // El objeto de escena reutilizado debe quedar VISIBLE: si estaba desactivado
            // (o colgado de algo desactivado) se reactiva, porque si no la escena
            // quedaría sin mapa visible.
            if (!mapaActual.activeSelf) mapaActual.SetActive(true);

            Physics.SyncTransforms();
            AplicarSkybox(config);

            // Un mapa colocado a mano en la escena NO se recoloca: se respeta su transform.
            if (EsConfigDelMapaInfinito(config))
                Debug.LogWarning($"[MapManager] '{mapaActual.name}' es el escenario Infinito colocado en la ESCENA: se usa su transform original (no se aplica posicion/escala de la config).");

            Debug.Log($"[MapManager] Mapa de escena activado: '{mapaActual.name}' (transform original: pos={mapaActual.transform.position} rot={mapaActual.transform.eulerAngles} escala={mapaActual.transform.lossyScale})");
            return true;
        }

        // ── 4. No está en la escena: instanciar el prefab en el transform de su configuración. ──
        //    El prefab de un mapa puede tener guardada una raíz que no corresponde con su
        //    sitio de juego (p. ej. "habitacion_Infinito Variant" está en -572.3, 0, 878.2),
        //    así que el transform del Inspector (config.posicion / rotacion / escala) MANDA:
        //    se asigna en coordenadas de MUNDO justo después de instanciar y de emparentar
        //    el clon a MapaContenedor, para que la herencia del padre no desplace el mapa.
        DesactivarMapasDeEscenaExcepto(null);

        mapaActual = config.prefab != null
            ? Instantiate(config.prefab, mapaContenedor)
            : CrearMapaProcedural(config);

        if (mapaActual == null)
        {
            Debug.LogWarning($"[MapManager] No se pudo crear el mapa: {config.nombre}");
            mapaActualConfig = null;
            return false;
        }

        // La raíz instanciada debe quedar ACTIVA: si el prefab trae su raíz desactivada
        // (m_IsActive = 0), Instantiate la copiaría apagada y la escena se vería vacía.
        if (!mapaActual.activeSelf) mapaActual.SetActive(true);

        mapaActualEsDeEscena = false;
        mapaActualConfig     = config;

        // Transform EXACTO e inmediato (en MUNDO) tras instanciar y emparentar el clon.
        AplicarTransformDeConfig(mapaActual, config);

        // Sincronizar los colliders del escenario recién cargado (mesa, paredes, etc.).
        // (AplicarTransformForzado ya lo hace; se mantiene por seguridad cuando el
        //  transform es neutro y se conserva el del prefab.)
        Physics.SyncTransforms();

        AplicarSkybox(config);

        Debug.Log($"[MapManager] Mapa cargado: {config.nombre} (origen: {(config.prefab != null ? "prefab " + config.prefab.name : "procedural")}, transform del prefab: pos={mapaActual.transform.position} rot={mapaActual.transform.eulerAngles} escala={mapaActual.transform.lossyScale})");
        return true;
    }

    /// <summary>
    /// Libera el mapa activo para que no queden dos escenarios a la vez.
    /// Si el mapa lo creó MapManager (prefab instanciado o procedural) se desactiva y se
    /// destruye; si es un GameObject preconfigurado de la escena, SOLO se desactiva
    /// (nunca se destruye ni se le cambia el transform). Es público para que otros
    /// sistemas puedan forzar la limpieza del escenario (p. ej. al terminar un combate
    /// de jefe).
    /// </summary>
    public void LimpiarMapaActual()
    {
        if (mapaActual != null)
        {
            string nombreAnterior = mapaActual.name;

            // Desactivar primero: Destroy() se aplica al final del frame y durante
            // ese frame el mapa viejo seguiría visible y colisionando.
            mapaActual.SetActive(false);

            if (mapaActualEsDeEscena)
            {
                Debug.Log($"[MapManager] Mapa de escena desactivado: {nombreAnterior}");
            }
            else
            {
                if (Application.isPlaying) Destroy(mapaActual);
                else DestroyImmediate(mapaActual);

                Debug.Log($"[MapManager] Mapa anterior eliminado: {nombreAnterior}");
            }
        }

        mapaActual           = null;
        mapaActualEsDeEscena = false;
        mapaActualConfig     = null;
    }

    // ─── Escenarios que ya existen en la escena ───
    /// <summary>
    /// Busca en la escena el GameObject que representa el mapa de la configuración
    /// indicada: compara, de forma tolerante, el nombre de la configuración y el de su
    /// prefab (también con los objetos desactivados). Devuelve null si ese mapa no está
    /// colocado en la escena, en cuyo caso se instanciará su prefab.
    /// Permite reutilizar escenarios preconfigurados en vez de crear clones.
    /// </summary>
    GameObject BuscarMapaEnEscena(ConfigMapa config)
    {
        if (config == null) return null;

        List<string> clavesConfig = new List<string>();
        AgregarClavesDeConfig(config, clavesConfig);
        if (clavesConfig.Count == 0) return null;

        foreach (GameObject go in MapasDeEscena())
        {
            if (go != null && CoincideNombreMapa(go.name, clavesConfig)) return go;
        }

        return null;
    }

    /// <summary>
    /// Todos los GameObjects de la escena que parecen un mapa colocado a mano: su
    /// nombre coincide con el de alguna configuración/prefab de mapa conocido y no
    /// pertenece a la jerarquía de MapManager ni a cámaras, UI, etc.
    /// Se incluyen los objetos desactivados y se descartan los assets del proyecto
    /// (prefabs) comprobando que el objeto viva en una escena cargada.
    /// </summary>
    List<GameObject> MapasDeEscena()
    {
        List<string> claves = ClavesDeMapasConocidos();
        List<GameObject> resultado = new List<GameObject>();

        Transform[] todos = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < todos.Length; i++)
        {
            Transform t = todos[i];
            if (t == null) continue;

            GameObject go = t.gameObject;
            if (go == null || go == this.gameObject) continue;

            // Descartar assets del proyecto (prefabs) y escenas que no están cargadas.
            if (!go.scene.IsValid() || !go.scene.isLoaded) continue;

            // Descartar la propia jerarquía de MapManager y los mapas que él instancia.
            if (go.transform.IsChildOf(this.transform)) continue;
            if (mapaContenedor != null &&
                (go.transform == mapaContenedor || go.transform.IsChildOf(mapaContenedor))) continue;

            if (EsNombreIgnoradoDeEscena(go.name)) continue;
            if (!CoincideNombreMapa(go.name, claves)) continue;

            resultado.Add(go);
        }

        return resultado;
    }

    /// <summary>
    /// Deja activo solo el mapa de escena indicado y desactiva los demás
    /// (con null se desactivan todos). Nunca destruye objetos ni modifica transforms:
    /// solo cambia activeSelf, así cada escenario conserva su configuración original y
    /// nunca se ven dos escenarios a la vez.
    /// </summary>
    void DesactivarMapasDeEscenaExcepto(GameObject mapaActivo)
    {
        foreach (GameObject go in MapasDeEscena())
        {
            if (go == null) continue;

            bool debeEstarActivo = go == mapaActivo;
            if (go.activeSelf != debeEstarActivo) go.SetActive(debeEstarActivo);
        }
    }

    void AplicarSkybox(ConfigMapa config)
    {
        if (config == null || config.skybox == null) return;

        // Crear instancia del material para poder modificarlo en runtime.
        RenderSettings.skybox = new Material(config.skybox);
        anguloSkybox = 0f; // resetear rotación al cambiar mapa
    }

    /// <summary>
    /// Indica si dos configuraciones de mapa representan el mismo escenario (mismo
    /// prefab y mismo nombre). Sirve para no reinstanciar un mapa que ya está activo,
    /// aunque la configuración venga de otra referencia (por ejemplo
    /// BossData.mapaAsociado frente al ConfigMapa propio de MapManager).
    /// </summary>
    static bool MismaConfig(ConfigMapa a, ConfigMapa b)
    {
        if (a == null || b == null) return false;
        if (ReferenceEquals(a, b)) return true;

        return a.prefab != null && a.prefab == b.prefab &&
               NormalizarNombreMapa(a.nombre) == NormalizarNombreMapa(b.nombre);
    }

    /// <summary>
    /// Nombres que nunca deben considerarse mapas de escena (cámaras, UI, managers,
    /// previews de skins, decoración como volcanes/skybox, etc.). Evita tocar objetos
    /// ajenos al escenario cuando se activan/desactivan mapas.
    /// </summary>
    static bool EsNombreIgnoradoDeEscena(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return true;

        string lower = nombre.ToLowerInvariant();
        return lower.Contains("camera") || lower.Contains("ui") || lower.Contains("canvas") ||
               lower.Contains("manager") || lower.Contains("eventsystem") || lower.Contains("volcan") ||
               lower.Contains("shadow") || lower.Contains("preview") || lower.Contains("card") ||
               lower.Contains("panel") || lower.Contains("titulo") || lower.Contains("contenedor") ||
               lower.Contains("skybox");
    }

    /// <summary>
    /// Nombres normalizados de todos los mapas conocidos: los de las 8 configuraciones
    /// del Inspector (nombre y prefab) más los nombres de prefab de los escenarios
    /// actuales del juego. Sirve para reconocer mapas ya colocados en la escena.
    /// </summary>
    List<string> ClavesDeMapasConocidos(ConfigMapa configExtra = null)
    {
        List<string> claves = new List<string>();

        for (int i = 0; i < 8; i++)
        {
            AgregarClavesDeConfig(ConfigPorIndice(i), claves);
        }

        AgregarClavesDeConfig(configExtra, claves);

        // Nombres de prefab usados por los escenarios del juego: permiten reconocer el
        // mapa de la escena aunque todavía no esté asignado en el Inspector.
        string[] nombresConocidos =
        {
            "habitacion_Infinito Variant",
            "MapaNubesV11",
            "Mapa de lavaV1 Variant"
        };

        for (int i = 0; i < nombresConocidos.Length; i++)
        {
            string clave = NormalizarNombreMapa(nombresConocidos[i]);
            if (clave.Length > 0 && !claves.Contains(clave)) claves.Add(clave);
        }

        return claves;
    }

    /// <summary>
    /// Añade a la lista las claves normalizadas del nombre de la configuración y del
    /// nombre de su prefab, sin duplicados.
    /// </summary>
    static void AgregarClavesDeConfig(ConfigMapa config, List<string> claves)
    {
        if (config == null || claves == null) return;

        string claveConfig = NormalizarNombreMapa(config.nombre);
        if (claveConfig.Length > 0 && !claves.Contains(claveConfig)) claves.Add(claveConfig);

        if (config.prefab != null)
        {
            string clavePrefab = NormalizarNombreMapa(config.prefab.name);
            if (clavePrefab.Length > 0 && !claves.Contains(clavePrefab)) claves.Add(clavePrefab);
        }
    }

    /// <summary>
    /// Comprueba si el nombre de un objeto de la escena coincide EXACTAMENTE con uno
    /// de los nombres de mapa conocidos, comparando sobre el nombre normalizado y
    /// descartando el sufijo de duplicado que añade Unity (" (1)", "(Clone)"...).
    /// Se sustituyó la antigua comparación por "Contains" bidireccional, que podía
    /// borrar objetos de la escena ajenos al mapa.
    /// </summary>
    static bool CoincideNombreMapa(string nombreObjeto, List<string> clavesNormalizadas)
    {
        if (clavesNormalizadas == null || clavesNormalizadas.Count == 0) return false;

        string normalizado = NormalizarNombreMapa(nombreObjeto);
        if (normalizado.Length == 0) return false;

        // Quitar el sufijo de duplicado de Unity ("Mapa Nube (1)" -> "mapanube").
        int parentesis = normalizado.IndexOf('(');
        if (parentesis > 0) normalizado = normalizado.Substring(0, parentesis);

        for (int i = 0; i < clavesNormalizadas.Count; i++)
        {
            if (clavesNormalizadas[i] == normalizado) return true;
        }

        return false;
    }

    /// <summary>
    /// Crea un escenario básico por código cuando la configuración no tiene prefab.
    /// La raíz se cuelga del contenedor conservando su transform (el contenedor está en
    /// identidad, así que el mapa queda tal cual se define aquí abajo).
    /// </summary>
    GameObject CrearMapaProcedural(ConfigMapa config)
    {
        GameObject raiz = new GameObject(config?.nombre ?? "Mapa Procedural");
        raiz.transform.position = Vector3.zero;
        raiz.transform.rotation = Quaternion.identity;
        raiz.transform.localScale = Vector3.one;

        // Mantener el mapa dentro del contenedor sin alterar su transform.
        if (mapaContenedor != null) raiz.transform.SetParent(mapaContenedor, true);

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