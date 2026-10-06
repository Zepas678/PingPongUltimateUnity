using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Maneja la selección, activación e instanciación de mapas.
///
/// Cada escenario conserva SIEMPRE el transform con el que fue creado: si el mapa ya
/// existe como GameObject en la escena se activa/desactiva ese mismo objeto, y si hay
/// que instanciar su prefab se copia tal cual (sin sobrescribir posición, rotación ni
/// escala). Así la escala dinámica del Inspector no deforma los mapas.
/// </summary>
/// <summary>Cómo se desbloquea un mapa: con Pollocoins o derrotando a un jefe (reclamo gratis en tienda).</summary>
public enum TipoDesbloqueoMapa
{
    Pollocoins,
    Jefe
}

/// <summary>
/// Estado de un mapa en la tienda (Opción 2: reclamar en tienda).
/// </summary>
public enum EstadoDesbloqueoMapa
{
    /// <summary>Comprado/reclamado o gratis por defecto.</summary>
    Desbloqueado,
    /// <summary>Se compra con Pollocoins.</summary>
    BloqueadoPollocoins,
    /// <summary>Requiere jefe y aún no fue derrotado.</summary>
    BloqueadoJefe,
    /// <summary>Jefe derrotado, pendiente de reclamar gratis (costo 0).</summary>
    PendienteReclamar
}

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
        [Tooltip("Sprite de vista previa de este mapa (se muestra en Preview_Mapa_Grande al enfocarlo)")]
        public Sprite     spriteImagen;

        [Header("Tienda / Economía")]
        [Tooltip("Precio del mapa en monedas")]
        public int        precio = 100;
        [Tooltip("Si es true, el mapa está disponible desde el inicio sin comprar")]
        public bool       desbloqueadoPorDefecto = false;
        [Tooltip("Identificador único persistente. Ej: mapa_space, mapa_habitacion. Si se deja vacío se genera desde el nombre.")]
        public string     idUnico;

        [Header("Desbloqueo por jefe (Opción 2: reclamar en tienda)")]
        [Tooltip("Cómo se desbloquea este mapa: con Pollocoins o derrotando a un jefe.")]
        public TipoDesbloqueoMapa tipoDesbloqueo = TipoDesbloqueoMapa.Pollocoins;
        [Tooltip("Si tipoDesbloqueo = Jefe: id del jefe requerido (ej: zeus, colossus, mirage). Debe coincidir con el id usado en JEFE_DERROTADO_[id].")]
        public string     idJefeRequerido;
        [Tooltip("Nombre visible del jefe para el texto 'Derrota a [Nombre]' (si se deja vacío se usa idJefeRequerido).")]
        public string     nombreJefeRequerido;

        /// <summary>Clave PlayerPrefs donde se guarda el desbloqueo de este mapa.</summary>
        public string ClaveDesbloqueo => "MAP_DESBLOQUEADO_" + IdUnicoEfectivo;

        /// <summary>
        /// Id efectivo: idUnico si está asignado, si no el nombre normalizado.
        /// Nunca vacío: si ambos faltan usa "mapa_sin_id" para no colisionar con "".
        /// </summary>
        public string IdUnicoEfectivo
        {
            get
            {
                if (!string.IsNullOrEmpty(idUnico)) return idUnico;
                string generado = MapManager.NormalizarNombreMapa(nombre);
                return string.IsNullOrEmpty(generado) ? "mapa_sin_id" : generado;
            }
        }

        /// <summary>
        /// ¿Está desbloqueado? true si la casilla 'desbloqueadoPorDefecto' es true,
        /// o si PlayerPrefs.GetInt("MAP_DESBLOQUEADO_" + idUnico, 0) == 1.
        /// </summary>
        public bool EstaDesbloqueado()
        {
            if (desbloqueadoPorDefecto) return true;
            return PlayerPrefs.GetInt(ClaveDesbloqueo, 0) == 1;
        }

        /// <summary>
        /// Desbloquea el mapa: PlayerPrefs.SetInt("MAP_DESBLOQUEADO_" + idUnico, 1) + Save().
        /// </summary>
        public void DesbloquearMapa()
        {
            PlayerPrefs.SetInt(ClaveDesbloqueo, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Re-bloquea el mapa (solo editor/debug): borra la clave de PlayerPrefs.
        /// No toca desbloqueadoPorDefecto.
        /// </summary>
        public void BloquearMapaDebug()
        {
            PlayerPrefs.DeleteKey(ClaveDesbloqueo);
            PlayerPrefs.Save();
        }

        /// <summary>Nombre visible del jefe requerido (nombreJefeRequerido o idJefeRequerido).</summary>
        public string NombreJefeVisible =>
            !string.IsNullOrEmpty(nombreJefeRequerido) ? nombreJefeRequerido : (idJefeRequerido ?? "");

        /// <summary>
        /// Estado del mapa en la tienda (Opción 2):
        /// DESBLOQUEADO si PlayerPrefs MAP_DESBLOQUEADO_[id] == 1 o desbloqueadoPorDefecto;
        /// PENDIENTE_RECLAMAR si requiere jefe, el jefe fue derrotado
        /// (PlayerPrefs JEFE_DERROTADO_[idJefe] == 1) y aún no se reclamó;
        /// BLOQUEADO_JEFE si requiere jefe no derrotado;
        /// si no, BLOQUEADO (compra con Pollocoins).
        /// </summary>
        public EstadoDesbloqueoMapa ObtenerEstadoDesbloqueo()
        {
            if (EstaDesbloqueado()) return EstadoDesbloqueoMapa.Desbloqueado;
            if (tipoDesbloqueo == TipoDesbloqueoMapa.Jefe)
            {
                if (MapManager.JefeDerrotado(idJefeRequerido))
                    return EstadoDesbloqueoMapa.PendienteReclamar;
                return EstadoDesbloqueoMapa.BloqueadoJefe;
            }
            return EstadoDesbloqueoMapa.BloqueadoPollocoins;
        }
    }

    [Header("Mapas disponibles")]
    public ConfigMapa mapaHabitacion = new ConfigMapa
    {
        nombre   = "Habitación Vacía",
        posicion = new Vector3(-14f, 44.2f, 163.4f),
        escala   = new Vector3(348f, 94f, 6f),
        rotacion = new Vector3(0f, 0f, 0f),
        idUnico  = "mapa_habitacion",
        desbloqueadoPorDefecto = true
    };

    public ConfigMapa mapaHabitacionJavi = new ConfigMapa
    {
        nombre   = "Habitacion de javi",
        posicion = Vector3.zero,
        escala   = Vector3.one,
        rotacion = Vector3.zero,
        idUnico  = "mapa_habitacion_javi"
    };

    public ConfigMapa mapaInfinito = new ConfigMapa
    {
        nombre   = "Mapa Infinito",
        // Valores EXACTOS del escenario "habitacion_Infinito Variant": su prefab (variante)
        // guarda la raíz en (-572.3, 0, 878.2), así que el transform correcto se aplica
        // al instanciar (ver InstanciarMapa / AplicarTransformDeConfig).
        posicion = new Vector3(-20.3f, 40f, 163.4f),
        escala   = new Vector3(348f, 94f, 6f),
        rotacion = Vector3.zero,
        idUnico  = "mapa_infinito",
        tipoDesbloqueo = TipoDesbloqueoMapa.Jefe,
        idJefeRequerido = "mirage",
        nombreJefeRequerido = "Mirage"
    };

    public ConfigMapa mapaNube = new ConfigMapa
    {
        nombre   = "Mapa Nube",
        posicion = Vector3.zero,
        escala   = Vector3.one,
        rotacion = Vector3.zero,
        idUnico  = "mapa_nube"
    };

    public ConfigMapa mapaSpace = new ConfigMapa
    {
        nombre   = "Space",
        posicion = new Vector3(-44.5f, -21.7f, 1.5f),
        escala   = new Vector3(0.8f, 0.8f, 0.8f),
        rotacion = new Vector3(0f, -90f, 0f),
        idUnico  = "mapa_space",
        precio   = 670
    };

    public ConfigMapa mapaHielo = new ConfigMapa
    {
        nombre   = "Hielo",
        posicion = new Vector3(-10.8f, -64.8f, -7.6f),
        escala   = new Vector3(6f, 6f, 6f),
        rotacion = new Vector3(0f, 180f, 0f),
        idUnico  = "mapa_hielo",
        tipoDesbloqueo = TipoDesbloqueoMapa.Jefe,
        idJefeRequerido = "zeus",
        nombreJefeRequerido = "Zeus"
    };

    public ConfigMapa mapaLava = new ConfigMapa
    {
        nombre   = "Lava",
        posicion = new Vector3(0.9f, -3.7f, -3.3f),
        escala   = new Vector3(8f, 8f, 8.5f),
        rotacion = new Vector3(0f, 180f, 0f),
        idUnico  = "mapa_lava",
        tipoDesbloqueo = TipoDesbloqueoMapa.Jefe,
        idJefeRequerido = "colossus",
        nombreJefeRequerido = "Colossus"
    };

    public ConfigMapa mapaPractica = new ConfigMapa
    {
        nombre   = "Habitación Práctica",
        posicion = new Vector3(0f, 0f, 0f),
        escala   = Vector3.one,
        // Rotación neutra (0,0,0): el mapa NO debe aparecer vertical/parado.
        rotacion = Vector3.zero,
        idUnico  = "mapa_practica",
        desbloqueadoPorDefecto = true
    };

    // --- Carrusel UI (Panel_Mapas) ---
    [Header("Carrusel UI (Panel_Mapas)")]
    [Tooltip("ScrollRect del objeto Contenedor_Mapas que contiene las Card_*")]
    public ScrollRect scrollMapas;
    [Tooltip("Content del ScrollRect (contenedor de las Card_*)")]
    public RectTransform contentMapas;
    [Tooltip("Flecha izquierda (Btn_FlechaIzquierda)")]
    public Button btnFlechaIzquierda;
    [Tooltip("Flecha derecha (Btn_FlechaDerecha)")]
    public Button btnFlechaDerecha;
    [Tooltip("Vista previa circular del menú principal (Preview_Mapa_Grande). Si se deja vacío se busca solo.")]
    [SerializeField] public Image previewMapaGrande;
    [Tooltip("Candado UI sobre la preview del carrusel (Panel_Mapas). Visible cuando el mapa enfocado está BLOQUEADO (Pollocoins o Jefe).")]
    public UnityEngine.UI.Image candadoPreviewCarrusel;
    [Tooltip("Texto UI informativo sobre la preview del carrusel. Muestra la razón de bloqueo: 'Bloqueado: Cómpralo en la tienda' o 'Bloqueado: Derrota a [Jefe]'. Oculto si desbloqueado.")]
    public TMPro.TMP_Text textoBloqueoCarrusel;
    [Tooltip("Panel/sombra UI superpuesto a la preview del carrusel para oscurecerla cuando está BLOQUEADO (tono oscuro semi-transparente). Opcional.")]
    public UnityEngine.UI.Image overlayOscuroCarrusel;
    [Tooltip("Color de la preview (Preview_Mapa_Grande) cuando el mapa está BLOQUEADO (oscurecido).")]
    public Color colorPreviewBloqueada = new Color(0.35f, 0.35f, 0.35f, 1f);
    [Tooltip("Vista previa de la TIENDA (Preview_MapShop). Si se deja vacía se busca sola.")]
    [SerializeField] public Image previewMapShop;
    [Tooltip("Alias del campo anterior: Image circular donde se muestra el sprite del mapa activo.")]
    public Image previewMapImage
    {
        get => previewMapaGrande;
        set => previewMapaGrande = value;
    }
    [Tooltip("Sprites de vista previa, uno por mapa (índice 0-7). Se usa el del mapa enfocado.")]
    public List<Sprite> previewsPorMapa = new List<Sprite>();
    [Tooltip("Array alternativo de sprites (índice = mapa 0-7). Se sincroniza con la lista anterior.")]
    public Sprite[] imagenesMapas = new Sprite[0];
    [Tooltip("Mapeo slot de TIENDA -> índice real de mapa 0-7. Posición = slot en la tienda, valor = mapa real. Ej: [4,...] si el slot 0 es Space. Si se deja vacío se autodetecta por sprite y si no hay coincidencia se usa el mismo índice.")]
    public System.Collections.Generic.List<int> mapaIndicePorSlotTienda = new System.Collections.Generic.List<int>();
    [Tooltip("Índice del mapa enfocado en el carrusel (0 = primero)")]
    public int indiceCarrusel = 0;
    [Tooltip("Índice INDEPENDIENTE de la tienda (no se mezcla con el carrusel del menú). 0 = primer elemento de imagenesMapas (Space).")]
    public int indiceTiendaMapa = 0;
    [Tooltip("Duración del desplazamiento suave del carrusel")]
    public float duracionScroll = 0.35f;
    [Header("Efecto escala carrusel")]
    [Tooltip("Si está activo, las tarjetas laterales se achican y la central queda en tamaño 1")]
    public bool activarEscalaCarrusel = true;
    [Tooltip("Escala de las tarjetas laterales (0.6 = 60%). La central siempre es 1.0")]
    [Range(0.3f, 1f)] public float escalaMinimaTarjeta = 0.6f;

    [Header("Tienda / Compra de mapas")]
    [Tooltip("Botón de confirmar/comprar mapa (cambia su texto según estado).")]
    public Button botonConfirmarMapa;
    [Tooltip("Texto del botón de confirmar/comprar (TMP). Si se deja vacío se busca en los hijos del botón.")]
    public TMPro.TMP_Text textoBotonConfirmarMapa;
    [Tooltip("Texto inferior con el precio (ej: $300 Pollocoins). Visible solo si el mapa está bloqueado.")]
    public TMPro.TMP_Text textoPrecioMapa;

    [Header("Tienda: reclamo por jefe (Opción 2)")]
    [Tooltip("OBSOLETO: la tienda ya no muestra candados (solo precio/texto requisito). Se conserva por compatibilidad y se fuerza apagado.")]
    [System.Obsolete("La tienda ya no usa candados: solo precio o texto 'Derrota a [Jefe]'.")]
    public UnityEngine.UI.Image iconoCandadoTienda;
    [Tooltip("OBSOLETO: la tienda ya no usa candados. Se conserva por compatibilidad.")]
    [System.Obsolete("La tienda ya no usa candados.")]
    public GameObject candadoTiendaGameObject;
    [Tooltip("Color de destacado del botón cuando está PENDIENTE_RECLAMAR (¡RECLAMAR!).")]
    public Color colorBotonReclamar = new Color(0.3f, 0.85f, 0.35f);
    [Tooltip("Sonido de confirmación al reclamar (opcional).")]
    public AudioClip sonidoReclamarMapa;
    [Tooltip("Efecto visual al reclamar (opcional, se instancia en el botón).")]
    public GameObject efectoReclamarPrefab;

    [Header("Botón de la TIENDA (opcional)")]
    [Tooltip("Botón BUY/EQUIP exclusivo de la tienda. Si se deja vacío se reutiliza botonConfirmarMapa.")]
    public Button botonComprarTienda;
    [Tooltip("Texto del botón de la tienda. Si se deja vacío se busca en sus hijos.")]
    public TMPro.TMP_Text textoBotonComprarTienda;
    [Tooltip("Texto de precio de la tienda. Si se deja vacío se reutiliza textoPrecioMapa.")]
    public TMPro.TMP_Text textoPrecioTienda;

    Coroutine corrutinaScroll;

    // --- Estado interno ---
    private GameObject mapaActual;
    /// <summary>Lectura pública del mapa activo (expuesta para MainMenuBackgroundManager).</summary>
    public GameObject MapaActual => mapaActual;
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
        // Efecto carrusel: la tarjeta central se ve grande, las laterales se achican.
        ActualizarEscalaTarjetas();

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
    public void SeleccionarHabitacionJavi() { SeleccionarMapaPorIndice(1); ForzarPreviewMapa(1, 1); }
    public void SeleccionarInfinito()       { SeleccionarMapaPorIndice(2); }
    public void SeleccionarNube()           { SeleccionarMapaPorIndice(3); }
    public void SeleccionarSpace()          { SeleccionarMapaPorIndice(4); ForzarPreviewMapa(4, 0); }
    public void SeleccionarHielo()          { SeleccionarMapaPorIndice(5); }
    public void SeleccionarLava()           { SeleccionarMapaPorIndice(6); }
    public void SeleccionarPractica()       { SeleccionarMapaPorIndice(7); }

    /// <summary>
    /// Fuerza la vista previa circular (previewMapaGrande / previewMapImage) al Sprite
    /// del mapa indicado. Prioridad: 1) ConfigMapa[indiceMapa].spriteImagen,
    /// 2) previewsPorMapa / imagenesMapas en indiceMapa, 3) índice alternativo
    /// (p. ej. Space pide imagenesMapas[0], Habitación Javi pide imagenesMapas[1]).
    /// Si nada es válido, cae a ActualizarPreview().
    /// </summary>
    public void ForzarPreviewMapa(int indiceMapa, int indiceAlternativo = -1)
    {
        ResolverPreviewMapaGrande();

        // Tienda: sprite propio/config/global, con alternativo explícito si se pide.
        Sprite spriteTienda = ObtenerSpriteMapa(indiceMapa);
        if (spriteTienda == null && indiceAlternativo >= 0)
            spriteTienda = SpriteDeListaAlternativa(indiceAlternativo);
        if (previewMapShop != null && spriteTienda != null)
            previewMapShop.sprite = spriteTienda;

        // Menú: ConfigMapa.spriteImagen -> previewsPorMapa (+ alternativo), SIN imagenesMapas.
        Sprite spriteMenu = ObtenerSpriteMenuPrincipal(indiceMapa);
        if (spriteMenu == null && indiceAlternativo >= 0)
            spriteMenu = SpriteMenuAlternativo(indiceAlternativo);
        if (previewMapaGrande != null && spriteMenu != null)
            previewMapaGrande.sprite = spriteMenu;

        if ((previewMapaGrande != null && spriteMenu == null)
            || (previewMapShop != null && spriteTienda == null))
            ActualizarPreview();
    }

    /// <summary>Sprite de respaldo para la tienda: imagenesMapas -> previewsPorMapa.</summary>
    Sprite SpriteDeListaAlternativa(int indice)
    {
        if (imagenesMapas != null && indice >= 0 && indice < imagenesMapas.Length
            && imagenesMapas[indice] != null)
            return imagenesMapas[indice];
        if (previewsPorMapa != null && indice >= 0 && indice < previewsPorMapa.Count
            && previewsPorMapa[indice] != null)
            return previewsPorMapa[indice];
        return null;
    }

    /// <summary>Sprite de respaldo para el MENÚ: SOLO previewsPorMapa (sin imagenesMapas).</summary>
    Sprite SpriteMenuAlternativo(int indice)
    {
        if (previewsPorMapa != null && indice >= 0 && indice < previewsPorMapa.Count
            && previewsPorMapa[indice] != null)
            return previewsPorMapa[indice];
        return null;
    }

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
        // Mantener el carrusel enfocado en el mismo mapa (sin animar: la animación la hace IrAlMapaCarrusel).
        indiceCarrusel = Mathf.Clamp(indice, 0, Mathf.Max(0, ObtenerTotalCarrusel() - 1));
        // Actualizar explícitamente el Image de vista previa con el sprite del mapa activo.
        ResolverPreviewMapaGrande();
        SincronizarPreviewConMapa(config);
        ActualizarFlechas();
        // Integrar economía: el botón pasa a BUY / EQUIP / EQUIPPED según estado.
        ActualizarBotonCompraMapa();
        ActualizarEstadoSeleccionMapa();
        SuscribirSaldoTienda();
        return mapaActual != null;
    }

    // ─── Economía / Tienda de mapas ───
    /// <summary>
    /// Mantiene el texto de saldo de la TIENDA actualizado vía OnPollocoinsChanged.
    /// El TMP permanente vive en EconomyManager.textoShopPollocoins; aquí solo se
    /// fuerza el repintado inicial por si la tienda se abrió después del Start.
    /// </summary>
    void SuscribirSaldoTienda()
    {
        if (EconomyManager.Instance == null) return;
        var shop = EconomyManager.Instance.textoShopPollocoins;
        if (shop != null)
            shop.text = EconomyManager.Instance.ObtenerPollocoins().ToString();
        // El botón BUY/EQUIP depende del saldo: refrescarlo ante cada cambio.
        EconomyManager.Instance.OnPollocoinsChanged -= RefrescarBotonPorSaldo;
        EconomyManager.Instance.OnPollocoinsChanged += RefrescarBotonPorSaldo;
    }

    void OnDisable()
    {
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnPollocoinsChanged -= RefrescarBotonPorSaldo;
    }

    void RefrescarBotonPorSaldo(int _) { ActualizarBotonCompraMapa(); ActualizarBotonCompraTienda(); ActualizarEstadoSeleccionMapa(); }

    /// <summary>Config del mapa enfocado/seleccionado (carrusel del MENÚ, no la tienda).</summary>
    public ConfigMapa ConfigMapaActivo() => ConfigPorIndice(indiceCarrusel);

    /// <summary>Config del mapa enfocado en la TIENDA (slot -> índice real). Ver definición real abajo.</summary>
    public ConfigMapa ObtenerConfigTiendaActiva() => ConfigTiendaActiva();

    // ─── Candado + botón SELECCIONAR/JUGAR del carrusel (Panel_Mapas) ───
    /// <summary>
    /// Sincroniza el carrusel de SELECCIÓN (panelMapas) con el bloqueo del mapa.
    /// BLOQUEADO (Pollocoins o Jefe):
    ///   a) candado visible en el centro sobre la imagen del mapa,
    ///   b) preview oscurecida (color + overlay semi-transparente),
    ///   c) texto informativo con la razón ("Cómpralo en la tienda" / "Derrota a [Jefe]"),
    ///   d) clic en la tarjeta NO inicia partida (OnClick/AlPulsarBotonMapa bloqueado)
    ///      y botón SELECCIONAR/JUGAR deshabilitado ("BLOQUEADO").
    /// DESBLOQUEADO: oculta candado/texto, restaura brillo y permite jugar.
    /// Se llama al navegar (SincronizarCarruselConSeleccion / IrAlMapaCarrusel /
    /// SeleccionarMapaPorIndice) y ante cambios de saldo.
    /// </summary>
    public void ActualizarEstadoSeleccionMapa()
    {
        ConfigMapa config = ConfigMapaActivo();
        EstadoDesbloqueoMapa estado = config != null
            ? config.ObtenerEstadoDesbloqueo()
            : EstadoDesbloqueoMapa.BloqueadoPollocoins;
        bool bloqueado = estado != EstadoDesbloqueoMapa.Desbloqueado;

        ActualizarCandadoCarrusel(bloqueado);
        ActualizarOscurecidoCarrusel(config, estado, bloqueado);
        ActualizarTextoBloqueoCarrusel(config, estado, bloqueado);

        // Botón SELECCIONAR/JUGAR: Comparte visual con botonConfirmarMapa cuando no
        // hay botón exclusivo de selección. Si el mapa está bloqueado muestra
        // "BLOQUEADO" y se deshabilita; si no, delega al estado BUY/EQUIP/EQUIPPED.
        if (botonSeleccionMapa == null)
        {
            ActualizarBotonCompraMapa();
            if (bloqueado && botonConfirmarMapa != null)
            {
                botonConfirmarMapa.interactable = false;
                TMPro.TMP_Text t = textoBotonConfirmarMapa;
                if (t == null)
                    t = botonConfirmarMapa.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (t != null) t.text = "BLOQUEADO";
            }
            return;
        }

        TMPro.TMP_Text textoSel = textoBotonSeleccionMapa;
        if (textoSel == null)
            textoSel = botonSeleccionMapa.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (bloqueado)
        {
            if (textoSel != null)
                textoSel.text = estado == EstadoDesbloqueoMapa.BloqueadoJefe
                    ? "Derrota a " + config.NombreJefeVisible
                    : "BLOQUEADO";
            botonSeleccionMapa.interactable = false;
        }
        else
        {
            if (textoSel != null) textoSel.text = textoSeleccionMapaDesbloqueado;
            botonSeleccionMapa.interactable = true;
        }
    }

    /// <summary>
    /// b) Oscurece la preview cuando está BLOQUEADO (color oscuro + overlay
    /// semi-transparente). DESBLOQUEADO: restaura el color/brillo normal (blanco).
    /// Guarda el color original la primera vez para restaurarlo exacto.
    /// </summary>
    void ActualizarOscurecidoCarrusel(ConfigMapa config, EstadoDesbloqueoMapa estado, bool bloqueado)
    {
        if (!colorPreviewOriginalGuardado && previewMapaGrande != null)
        {
            colorPreviewOriginal = previewMapaGrande.color;
            colorPreviewOriginalGuardado = true;
        }

        if (previewMapaGrande != null)
            previewMapaGrande.color = bloqueado ? colorPreviewBloqueada : colorPreviewOriginal;

        if (overlayOscuroCarrusel != null)
        {
            overlayOscuroCarrusel.gameObject.SetActive(bloqueado);
            overlayOscuroCarrusel.enabled = bloqueado;
        }
    }

    Color colorPreviewOriginal = Color.white;
    bool colorPreviewOriginalGuardado = false;

    /// <summary>
    /// c) Texto informativo sobre la preview con la razón de bloqueo:
    /// tienda -> "Bloqueado: Cómpralo en la tienda";
    /// jefe -> "Bloqueado: Derrota a [Nombre]" (ej. Zeus).
    /// DESBLOQUEADO: oculta el texto.
    /// </summary>
    void ActualizarTextoBloqueoCarrusel(ConfigMapa config, EstadoDesbloqueoMapa estado, bool bloqueado)
    {
        if (textoBloqueoCarrusel == null)
        {
            // Auto-búsqueda: hijo de la preview o de su padre.
            Transform raiz = previewMapaGrande != null ? previewMapaGrande.transform : null;
            if (raiz != null)
            {
                string[] candidatos = { "TextoBloqueo", "TextoBloqueoCarrusel", "TextoCandado", "BloqueoTexto", "InfoBloqueo" };
                Transform t = null;
                foreach (string n in candidatos)
                {
                    t = raiz.Find(n);
                    if (t != null) break;
                }
                if (t == null && raiz.parent != null)
                {
                    foreach (string n in candidatos)
                    {
                        t = raiz.parent.Find(n);
                        if (t != null) break;
                    }
                }
                if (t != null)
                    textoBloqueoCarrusel = t.GetComponent<TMPro.TMP_Text>();
            }
        }

        if (textoBloqueoCarrusel == null) return;

        if (!bloqueado)
        {
            textoBloqueoCarrusel.gameObject.SetActive(false);
            return;
        }

        string mensaje;
        if (estado == EstadoDesbloqueoMapa.BloqueadoJefe)
            mensaje = "Bloqueado: Derrota a " + (config != null ? config.NombreJefeVisible : "");
        else
            mensaje = "Bloqueado: Cómpralo en la tienda";

        textoBloqueoCarrusel.text = mensaje;
        textoBloqueoCarrusel.gameObject.SetActive(true);
    }

    /// <summary>
    /// a) Muestra/oculta el candado sobre la preview del carrusel (Panel_Mapas).
    /// Usa SetActive + enabled como en la tienda. Si no hay Image asignada en
    /// candadoPreviewCarrusel, busca un hijo "Candado/Lock" de la preview.
    /// </summary>
    void ActualizarCandadoCarrusel(bool mostrar)
    {
        if (candadoPreviewCarrusel == null)
        {
            // Auto-búsqueda: hijo de la preview grande o de su padre.
            Transform raiz = null;
            if (previewMapaGrande != null) raiz = previewMapaGrande.transform;
            if (raiz == null && previewMapShop != null) raiz = previewMapShop.transform.parent;
            if (raiz != null)
            {
                Transform t = raiz.Find("Candado");
                if (t == null) t = raiz.Find("CandadoCarrusel");
                if (t == null) t = raiz.Find("CandadoPreview");
                if (t == null) t = raiz.Find("Lock");
                if (t == null && raiz.parent != null)
                {
                    t = raiz.parent.Find("Candado");
                    if (t == null) t = raiz.parent.Find("CandadoCarrusel");
                    if (t == null) t = raiz.parent.Find("Lock");
                }
                if (t != null)
                    candadoPreviewCarrusel = t.GetComponent<UnityEngine.UI.Image>();
            }
        }

        if (candadoPreviewCarrusel != null)
        {
            candadoPreviewCarrusel.gameObject.SetActive(mostrar);
            candadoPreviewCarrusel.enabled = mostrar;
        }
    }

    [Header("Carrusel: botón SELECCIONAR/JUGAR (Panel_Mapas)")]
    [Tooltip("Botón SELECCIONAR/JUGAR del carrusel. Si se deja vacío se reutiliza botonConfirmarMapa con texto BLOQUEADO.")]
    public Button botonSeleccionMapa;
    [Tooltip("Texto del botón SELECCIONAR/JUGAR. Si se deja vacío se busca en sus hijos.")]
    public TMPro.TMP_Text textoBotonSeleccionMapa;
    [Tooltip("Texto del botón cuando el mapa está desbloqueado.")]
    public string textoSeleccionMapaDesbloqueado = "JUGAR";

    /// <summary>
    /// d) Puerta de selección: indica si el mapa del carrusel puede iniciarse.
    /// Devuelve false si el mapa enfocado está BLOQUEADO (Pollocoins o Jefe);
    /// en ese caso el clic en la tarjeta/preview NO debe iniciar la partida.
    /// Usar antes de confirmar mapa o llamar a UIManager para jugar.
    /// </summary>
    public bool PuedeJugarMapaEnfocado()
    {
        ConfigMapa config = ConfigMapaActivo();
        return config != null && config.ObtenerEstadoDesbloqueo() == EstadoDesbloqueoMapa.Desbloqueado;
    }

    /// <summary>¿Está desbloqueado el mapa enfocado? (alias de PuedeJugarMapaEnfocado).</summary>
    public bool MapaActivoDesbloqueado() => PuedeJugarMapaEnfocado();

    /// <summary>
    /// Botón principal del panel derecho del MENÚ (no la tienda).
    /// Cambia según el estado del mapa activo del carrusel:
    /// a) Bloqueado -&gt; "BUY" + precio "$XXX Pollocoins" (interactuable solo con saldo).
    /// b) Desbloqueado -&gt; "EQUIP".
    /// c) Equipado (mapa 3D activo) -&gt; "EQUIPPED".
    /// Llamar al seleccionar o cambiar de mapa en el menú.
    /// </summary>
    public void ActualizarBotonCompraMapa()
    {
        ConfigMapa config = ConfigMapaActivo();
        TMPro.TMP_Text texto = textoBotonConfirmarMapa;
        if (texto == null && botonConfirmarMapa != null)
            texto = botonConfirmarMapa.GetComponentInChildren<TMPro.TMP_Text>(true);
        TMPro.TMP_Text textoPrecio = textoPrecioMapa;

        if (config == null)
        {
            if (botonConfirmarMapa != null) botonConfirmarMapa.interactable = false;
            if (texto != null) texto.text = "EQUIP";
            if (textoPrecio != null) textoPrecio.text = "";
            return;
        }

        if (!config.EstaDesbloqueado())
        {
            // a) Bloqueado -> BUY + "$XXX Pollocoins".
            if (texto != null) texto.text = "BUY";
            if (textoPrecio != null) textoPrecio.text = "$" + config.precio + " Pollocoins";
            if (botonConfirmarMapa != null)
            {
                bool puedePagar = EconomyManager.Instance != null
                    && EconomyManager.Instance.TieneSuficiente(config.precio);
                botonConfirmarMapa.interactable = puedePagar;
            }
            return;
        }

        if (textoPrecio != null) textoPrecio.text = "";
        bool esEquipado = mapaActual != null && mapaSeleccionado == indiceCarrusel;
        if (esEquipado)
        {
            // c) Equipado -> EQUIPPED (no necesita pulsarse).
            if (texto != null) texto.text = "EQUIPPED";
            if (botonConfirmarMapa != null) botonConfirmarMapa.interactable = false;
        }
        else
        {
            // b) Desbloqueado pero no equipado -> EQUIP.
            if (texto != null) texto.text = "EQUIP";
            if (botonConfirmarMapa != null) botonConfirmarMapa.interactable = true;
        }
    }

    /// <summary>
    /// Acción del botón BUY / EQUIP del MENÚ (no la tienda).
    /// Mismo flujo blindado que la tienda: verifica saldo, consume, desbloquea
    /// (PlayerPrefs) y refresca a EQUIP. Si ya está desbloqueado, equipa.
    /// Conectar al OnClick de botonConfirmarMapa. La tienda usa OnClickComprarTienda.
    /// RETURN es un botón aparte (UIManager).
    /// </summary>
    public void OnClickConfirmarOComprarMapa()
    {
        ConfigMapa config = ConfigMapaActivo();
        if (config == null || EconomyManager.Instance == null) return;

        if (config.EstaDesbloqueado())
        {
            // EQUIP: la selección ya activó el 3D; solo refrescar a EQUIPPED.
            SeleccionarMapaPorIndice(indiceCarrusel);
            return;
        }

        if (!EconomyManager.Instance.TieneSuficiente(config.precio))
        {
            ActualizarBotonCompraMapa();
            return;
        }

        if (!EconomyManager.Instance.ConsumirPollocoins(config.precio))
        {
            ActualizarBotonCompraMapa();
            return;
        }

        config.DesbloquearMapa();
        EconomyManager.Instance.NotificarCambioPollocoins();
        ActualizarBotonCompraMapa();
    }

    // ─── Botón BUY / UNLOCKED / RECLAMAR de la TIENDA (índice independiente) ───
    /// <summary>
    /// Botón de la TIENDA usando EXCLUSIVAMENTE ConfigTiendaActiva().
    /// - Bloqueado Pollocoins: "BUY" + "$XXX Pollocoins", interactuable con saldo.
    /// - BLOQUEADO_JEFE: botón deshabilitado + texto "Derrota a [Jefe]" + candado.
    /// - PENDIENTE_RECLAMAR: botón verde destacado "¡RECLAMAR!" (costo 0).
    /// - Desbloqueado: "UNLOCKED", precio vacío, interactable = false.
    /// Reutiliza los componentes del menú si no hay botón exclusivo de tienda.
    /// Además refresca los candados de TODAS las miniaturas del carrusel/tienda.
    /// </summary>
    public void ActualizarBotonCompraTienda()
    {
        ActualizarBotonCompraTiendaInterno();
        ActualizarCandadosMiniaturasCarrusel();
    }

    /// <summary>Lógica del botón principal de la tienda (no toca miniaturas).</summary>
    void ActualizarBotonCompraTiendaInterno()
    {
        // ÚNICA fuente: el ConfigMapa real del slot de tienda enfocado.
        ConfigMapa config = ConfigTiendaActiva();
        Button boton = botonComprarTienda != null ? botonComprarTienda : botonConfirmarMapa;
        TMPro.TMP_Text texto = textoBotonComprarTienda;
        if (texto == null && boton != null)
            texto = boton.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (texto == null) texto = textoBotonConfirmarMapa;
        TMPro.TMP_Text textoPrecio = textoPrecioTienda != null ? textoPrecioTienda : textoPrecioMapa;

        // Candado solo en BLOQUEADO_JEFE.
        EstadoDesbloqueoMapa estado = config != null
            ? config.ObtenerEstadoDesbloqueo()
            : EstadoDesbloqueoMapa.BloqueadoPollocoins;
        // Candado UI (Image/Sprite, NO prefab 3D): visible solo en BLOQUEADO_JEFE.
        ActualizarCandadoTienda(estado == EstadoDesbloqueoMapa.BloqueadoJefe, boton);
        RestaurarColorBotonTienda(boton);

        if (config == null)
        {
            if (boton != null) boton.interactable = false;
            if (texto != null) texto.text = "BUY";
            if (textoPrecio != null) textoPrecio.text = "";
            return;
        }

        switch (estado)
        {
            case EstadoDesbloqueoMapa.BloqueadoJefe:
                // Requiere jefe no derrotado: deshabilitado + "Derrota a [Jefe]".
                if (texto != null) texto.text = "Derrota a " + config.NombreJefeVisible;
                if (textoPrecio != null) textoPrecio.text = "";
                if (boton != null) boton.interactable = false;
                return;

            case EstadoDesbloqueoMapa.PendienteReclamar:
                // Jefe derrotado: reclamar gratis, botón verde destacado.
                if (texto != null) texto.text = "¡RECLAMAR!";
                if (textoPrecio != null) textoPrecio.text = "$0 Pollocoins";
                if (boton != null)
                {
                    boton.interactable = true;
                    AplicarColorReclamar(boton);
                }
                return;

            case EstadoDesbloqueoMapa.Desbloqueado:
                // Desbloqueado: UNLOCKED, sin precio y sin interacción.
                if (texto != null) texto.text = "UNLOCKED";
                if (textoPrecio != null) textoPrecio.text = "";
                if (boton != null) boton.interactable = false;
                return;

            default:
                // Bloqueado por Pollocoins: BUY + precio, según saldo.
                if (texto != null) texto.text = "BUY";
                if (textoPrecio != null) textoPrecio.text = "$" + config.precio + " Pollocoins";
                if (boton != null)
                    boton.interactable = EconomyManager.Instance != null
                        && EconomyManager.Instance.TieneSuficiente(config.precio);
                return;
        }
    }

    Color colorOriginalBotonTienda = Color.white;
    bool colorOriginalGuardado = false;

    /// <summary>Oscurecido aplicado a las miniaturas BLOQUEADAS del carrusel.</summary>
    public Color colorMiniaturaBloqueada = new Color(0.45f, 0.45f, 0.45f, 1f);

    /// <summary>
    /// 2) Candados en las miniaturas pequeñas del carrusel: recorre CADA tarjeta
    /// de contentMapas y contentTiendaMapas, revisa su estado por índice y:
    /// - BLOQUEADO: candado visible en la tarjeta + miniatura oscurecida.
    /// - DESBLOQUEADO: candado oculto + brillo normal.
    /// </summary>
    public void ActualizarCandadosMiniaturasCarrusel()
    {
        // Solo carrusel de SELECCIÓN: la tienda no usa candados (solo precio/texto).
        ActualizarCandadosMiniaturasEn(contentMapas, true);
        // Limpieza: por si quedaron candados creados en sesiones previas en la tienda.
        LimpiarCandadosTienda();
    }

    /// <summary>Vincula slots del carrusel y refresca sus candados.</summary>
    public void VincularSlotsCarrusel()
    {
        VincularBotonesMapasManuales();
        ActualizarCandadosMiniaturasCarrusel();
    }

    void ActualizarCandadosMiniaturasEn(RectTransform content, bool indiceDirecto)
    {
        if (content == null) return;
        for (int slot = 0; slot < content.childCount; slot++)
        {
            Transform tarjeta = content.GetChild(slot);
            if (tarjeta == null) continue;
            int indiceMapa = indiceDirecto ? Mathf.Clamp(slot, 0, 7) : IndiceMapaRealDesdeSlotTienda(slot);
            ConfigMapa config = ConfigPorIndice(indiceMapa);
            EstadoDesbloqueoMapa estado = config != null ? config.ObtenerEstadoDesbloqueo() : EstadoDesbloqueoMapa.BloqueadoPollocoins;
            ActualizarCandadoMiniatura(tarjeta, estado != EstadoDesbloqueoMapa.Desbloqueado);
        }
    }

    /// <summary>
    /// Limpia candados residuales en la TIENDA: oculta (sin destruir) cualquier
    /// Image "Candado"/"Lock" en contentTiendaMapas y en la preview lateral
    /// previewMapShop. La tienda informa el bloqueo SOLO con precio/texto
    /// ("$XXX Pollocoins" / "Derrota a [Jefe]"), sin iconos de candado.
    /// No toca el carrusel de selección (sus candados se gestionan aparte).
    /// </summary>
    void LimpiarCandadosTienda()
    {
        // 1) Tarjetas de la tienda (Content propio, si existe y es distinto).
        if (contentTiendaMapas != null && contentTiendaMapas != contentMapas)
        {
            for (int i = 0; i < contentTiendaMapas.childCount; i++)
            {
                Transform tarjeta = contentTiendaMapas.GetChild(i);
                if (tarjeta == null) continue;
                UnityEngine.UI.Image candado = BuscarCandadoEnTarjeta(tarjeta);
                if (candado != null)
                {
                    candado.enabled = false;
                    if (candado.gameObject.name == "Candado")
                        candado.gameObject.SetActive(false);
                }
            }
        }

        // 2) Preview lateral de la tienda.
        if (previewMapShop != null)
        {
            Transform raiz = previewMapShop.transform.parent != null
                ? previewMapShop.transform.parent
                : previewMapShop.transform;
            string[] nombres = { "Candado", "CandadoTienda", "CandadoPreview", "CandadoShop", "Lock" };
            foreach (string n in nombres)
            {
                Transform t = previewMapShop.transform.Find(n);
                if (t == null && previewMapShop.transform.parent != null)
                    t = previewMapShop.transform.parent.Find(n);
                if (t == null && raiz != null && t != raiz) t = raiz.Find(n);
                if (t != null)
                {
                    UnityEngine.UI.Image img = t.GetComponent<UnityEngine.UI.Image>();
                    if (img != null) img.enabled = false;
                    if (t.gameObject.name == "Candado")
                        t.gameObject.SetActive(false);
                }
            }
        }

        // 3) Icono de candado del botón de la tienda (referencia directa, obsoleta).
#pragma warning disable 0618
        if (iconoCandadoTienda != null)
        {
            iconoCandadoTienda.enabled = false;
            if (iconoCandadoTienda.gameObject.name == "Candado")
                iconoCandadoTienda.gameObject.SetActive(false);
        }
        if (candadoTiendaGameObject != null && candadoTiendaGameObject.name == "Candado")
            candadoTiendaGameObject.SetActive(false);
#pragma warning restore 0618
    }

    /// <summary>Busca el candado (hijo "Candado"/"Lock") en una tarjeta pequeña.</summary>
    UnityEngine.UI.Image BuscarCandadoEnTarjeta(Transform tarjeta)
    {
        string[] nombres = { "Candado", "CandadoMini", "CandadoTarjeta", "Lock", "IconoCandado" };
        foreach (string n in nombres)
        {
            Transform t = tarjeta.Find(n);
            if (t != null)
            {
                UnityEngine.UI.Image img = t.GetComponent<UnityEngine.UI.Image>();
                if (img != null) return img;
            }
        }
        UnityEngine.UI.Image[] imagenes = tarjeta.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        if (imagenes != null)
        {
            foreach (UnityEngine.UI.Image img in imagenes)
            {
                if (img == null) continue;
                string n = img.gameObject.name.ToLowerInvariant();
                if (n.Contains("candado") || n.Contains("lock")) return img;
            }
        }
        return null;
    }

    /// <summary>Crea el candado centrado en la tarjeta si no existe.</summary>
    UnityEngine.UI.Image CrearCandadoEnTarjeta(Transform tarjeta)
    {
        Sprite spriteCandado = null;
        if (iconoCandadoTienda != null) spriteCandado = iconoCandadoTienda.sprite;
        if (spriteCandado == null && candadoPreviewCarrusel != null) spriteCandado = candadoPreviewCarrusel.sprite;
        GameObject go = new GameObject("Candado", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(tarjeta, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(64f, 64f);
        go.transform.SetAsLastSibling();
        UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
        if (spriteCandado != null) img.sprite = spriteCandado;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Activa/oculta el candado de una tarjeta y oscurece su foto.</summary>
    void ActualizarCandadoMiniatura(Transform tarjeta, bool bloqueado)
    {
        if (tarjeta == null) return;
        UnityEngine.UI.Image candado = BuscarCandadoEnTarjeta(tarjeta);
        if (candado == null && bloqueado)
            candado = CrearCandadoEnTarjeta(tarjeta);
        if (candado != null)
        {
            candado.gameObject.SetActive(bloqueado);
            candado.enabled = bloqueado;
        }
        UnityEngine.UI.Image[] imagenes = tarjeta.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        if (imagenes == null) return;
        foreach (UnityEngine.UI.Image img in imagenes)
        {
            if (img == null || img == candado) continue;
            string n = img.gameObject.name.ToLowerInvariant();
            if (n.Contains("candado") || n.Contains("lock")) continue;
            if (img.sprite == null) continue;
            img.color = bloqueado ? colorMiniaturaBloqueada : Color.white;
            break;
        }
    }

    void AplicarColorReclamar(Button boton)
    {
        if (boton == null || boton.image == null) return;
        if (!colorOriginalGuardado)
        {
            colorOriginalBotonTienda = boton.image.color;
            colorOriginalGuardado = true;
        }
        boton.image.color = colorBotonReclamar;
    }

    void RestaurarColorBotonTienda(Button boton)
    {
        if (boton == null || boton.image == null || !colorOriginalGuardado) return;
        if (boton.image.color == colorBotonReclamar)
            boton.image.color = colorOriginalBotonTienda;
    }

    /// <summary>
    /// TIENDA SIN CANDADOS: la tienda informa el bloqueo SOLO con precio o texto
    /// de requisito ("$XXX Pollocoins" / "Derrota a [Jefe]"). Este método apaga
    /// cualquier candado residual en tarjetas y preview lateral y ya no activa
    /// ninguno. No destruye objetos (solo .enabled=false / SetActive(false)).
    /// El carrusel de selección conserva sus propios candados (otra lógica).
    /// </summary>
    void ActualizarCandadoTienda(bool mostrar, Button boton)
    {
        // Intencionalmente se ignora 'mostrar': en tienda nunca se enciende.
        LimpiarCandadosTienda();
        // Garantía extra: si quedó cacheada una referencia directa, forzar apagado.
#pragma warning disable 0618
        if (iconoCandadoTienda != null)
        {
            iconoCandadoTienda.enabled = false;
            iconoCandadoTienda.gameObject.SetActive(false);
        }
        if (candadoTiendaGameObject != null)
            candadoTiendaGameObject.SetActive(false);
#pragma warning restore 0618
    }

    /// <summary>
    /// Acción del botón de la TIENDA sobre el mapa enfocado en la tienda.
    /// - PENDIENTE_RECLAMAR ("¡RECLAMAR!"): costo 0, otorga el mapa
    ///   (PlayerPrefs MAP_DESBLOQUEADO_[id] = 1), pasa a DESBLOQUEADO + efecto/sonido.
    /// - Bloqueado Pollocoins ("BUY"): flujo compra a-e (verifica, consume, desbloquea).
    /// - BLOQUEADO_JEFE o DESBLOQUEADO: no hace nada (botón deshabilitado).
    /// Conectar al OnClick del botón de compra de la tienda.
    /// </summary>
    public void OnClickComprarTienda()
    {
        // ConfigMapa real del slot de tienda enfocado.
        ConfigMapa config = ConfigTiendaActiva();
        if (config == null) return;

        EstadoDesbloqueoMapa estado = config.ObtenerEstadoDesbloqueo();

        // Ya desbloqueado o jefe sin derrotar: nada que hacer.
        if (estado == EstadoDesbloqueoMapa.Desbloqueado
            || estado == EstadoDesbloqueoMapa.BloqueadoJefe)
        {
            ActualizarBotonCompraTienda();
            return;
        }

        // ¡RECLAMAR!: jefe derrotado, costo 0 Pollocoins (sin EconomyManager).
        if (estado == EstadoDesbloqueoMapa.PendienteReclamar)
        {
            config.DesbloquearMapa();
            ReproducirEfectoReclamo();
            EconomyManager.Instance?.NotificarCambioPollocoins();
            ActualizarBotonCompraTienda(); // -> UNLOCKED
            return;
        }

        // BUY con Pollocoins: requiere EconomyManager.
        if (EconomyManager.Instance == null) return;

        // a) Verificación explícita de saldo antes de cobrar.
        if (!EconomyManager.Instance.TieneSuficiente(config.precio))
        {
            ActualizarBotonCompraTienda(); // sigue en BUY, no interactuable
            return;
        }

        // b) Descontar el dinero inmediatamente (false si no alcanzó).
        if (!EconomyManager.Instance.ConsumirPollocoins(config.precio))
        {
            ActualizarBotonCompraTienda();
            return;
        }

        // c) Desbloquear (PlayerPrefs) y verificar persistencia.
        config.DesbloquearMapa();
        if (!config.EstaDesbloqueado())
        {
            Debug.LogWarning("[MapManager] La compra no persistió el desbloqueo de " + config.nombre);
            return;
        }

        // d) Refresco instantáneo del saldo.
        EconomyManager.Instance.NotificarCambioPollocoins();

        // e) Botón a UNLOCKED, sin precio y sin interacción.
        ActualizarBotonCompraTienda();
    }

    // ─── Derrotas de jefes (PlayerPrefs JEFE_DERROTADO_[id]) ───
    /// <summary>Clave PlayerPrefs de derrota de jefe.</summary>
    public static string ClaveJefeDerrotado(string idJefe) =>
        "JEFE_DERROTADO_" + (string.IsNullOrEmpty(idJefe) ? "desconocido" : idJefe.Trim().ToLowerInvariant());

    /// <summary>¿Fue derrotado el jefe indicado? (PlayerPrefs JEFE_DERROTADO_[id] == 1).</summary>
    public static bool JefeDerrotado(string idJefe)
    {
        if (string.IsNullOrEmpty(idJefe)) return false;
        return PlayerPrefs.GetInt(ClaveJefeDerrotado(idJefe), 0) == 1;
    }

    /// <summary>
    /// Marca al jefe como derrotado: PlayerPrefs JEFE_DERROTADO_[idJefe] = 1 + Save().
    /// Llamar al ganar una partida contra un jefe (ver BossManager.RegistrarVictoriaJefe).
    /// Refresca el botón de la tienda por si el mapa pendiente pasa a ¡RECLAMAR!.
    /// </summary>
    public void MarcarJefeDerrotado(string idJefe)
    {
        if (string.IsNullOrEmpty(idJefe)) return;
        PlayerPrefs.SetInt(ClaveJefeDerrotado(idJefe), 1);
        PlayerPrefs.Save();
        ActualizarBotonCompraTienda();
    }

    /// <summary>
    /// Efecto/sonido de confirmación al reclamar un mapa por jefe.
    /// Instancia efectoReclamarPrefab en el botón y reproduce sonidoReclamarMapa si existen.
    /// </summary>
    void ReproducirEfectoReclamo()
    {
        Button boton = botonComprarTienda != null ? botonComprarTienda : botonConfirmarMapa;
        if (efectoReclamarPrefab != null && boton != null)
            Instantiate(efectoReclamarPrefab, boton.transform);
        if (sonidoReclamarMapa != null)
            AudioSource.PlayClipAtPoint(sonidoReclamarMapa, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
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
                indiceCarrusel = Mathf.Clamp(i, 0, Mathf.Max(0, ObtenerTotalCarrusel() - 1));
                ActualizarPreview();
                ActualizarFlechas();
                ActualizarEstadoSeleccionMapa();
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

    /// <summary>
    /// Instancia/activa un mapa aleatorio SOLO como fondo visual del menú.
    /// Excluye el mapa de práctica (id/nombre con "practica"): NUNCA es fondo.
    /// NO cambia mapaSeleccionado/indiceCarrusel: la elección del jugador queda intacta.
    /// Útil para MainMenuBackgroundManager (fondo dinámico CPU vs CPU).
    /// Devuelve el índice mostrado (0-7) o mapaSeleccionado si no hay candidato.
    /// </summary>
    public int MostrarMapaAleatorioDeFondo()
    {
        System.Collections.Generic.List<int> candidatos = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 8; i++)
        {
            ConfigMapa c = ConfigPorIndice(i);
            if (c == null || EsMapaPractica(c)) continue;
            candidatos.Add(i);
        }
        if (candidatos.Count == 0) return mapaSeleccionado;
        int indice = candidatos[UnityEngine.Random.Range(0, candidatos.Count)];
        ConfigMapa config = ConfigPorIndice(indice);
        if (config == null) return mapaSeleccionado;
        InstanciarMapa(config);
        return indice;
    }

    /// <summary>
    /// true si el config es el mapa de práctica (id o nombre con "practica").
    /// El mapa de práctica nunca debe ser fondo aleatorio del menú.
    /// </summary>
    public static bool EsMapaPractica(ConfigMapa config)
    {
        if (config == null) return false;
        string id = (config.idUnico ?? "").ToLowerInvariant();
        string nombre = NormalizarNombreMapa(config.nombre);
        return id.Contains("practica") || id.Contains("mapa_practica")
            || nombre.Contains("practica") || nombre.Contains("mapapractica");
    }

    /// <summary>
    /// Instancia/activa el mapa indicado SOLO como fondo visual (sin tocar la selección).
    /// </summary>
    public int MostrarMapaDeFondo(int indice)
    {
        ConfigMapa config = ConfigPorIndice(indice);
        if (config == null) return mapaSeleccionado;
        InstanciarMapa(config);
        return indice;
    }

    // -------------------------------------------------------
    public int GetMapaSeleccionado() => mapaSeleccionado;

    // =======================================================
    // ─── Carrusel Panel_Mapas (ScrollRect + flechas) ───
    //     Contenedor_Mapas (ScrollRect) con Card_* en Content,
    //     Btn_FlechaIzquierda / Btn_FlechaDerecha y la vista
    //     previa circular Preview_Mapa_Grande.
    // =======================================================
    void Start()
    {
        ResolverPreviewMapaGrande();
        VincularBotonesMapasManuales();
        indiceCarrusel = Mathf.Clamp(indiceCarrusel, 0, Mathf.Max(0, ObtenerTotalCarrusel() - 1));
        ActualizarCarruselUI(false);
        ActualizarBotonCompraMapa();
        SuscribirSaldoTienda();
    }

    /// <summary>
    /// Cada vez que el objeto se activa (p. ej. al abrir Panel_Mapas), el menú abre
    /// siempre centrado en el mapa 0 (Habitación): fuerza indiceCarrusel = 0 y
    /// recentra sin animación. El layout aún no está calculado en este punto, así que
    /// el centrado real se difiere al final del frame (ver CentrarAlActivar()).
    /// También re-vincula los botones manuales de las Card_* por si la jerarquía cambió.
    /// </summary>
    void OnEnable()
    {
        ResolverPreviewMapaGrande();
        VincularBotonesMapasManuales();
        indiceCarrusel = 0;
        if (corrutinaScroll != null) { StopCoroutine(corrutinaScroll); corrutinaScroll = null; }
        // Difere: espera 1 frame a que Unity calcule tamaños/posiciones del layout.
        StartCoroutine(CentrarAlActivar());
    }

    /// <summary>
    /// Auto-referencia de los Image de vista previa si las casillas del Inspector
    /// están vacías:
    /// - previewMapaGrande -> buscar "Preview_Mapa_Grande"
    /// - previewMapShop -> buscar "Preview_MapShop" o "Preview_Map"
    /// Nota: GameObject.Find solo encuentra objetos ACTIVOS en la jerarquía; si
    /// están dentro de un panel desactivado, asigna las casillas a mano.
    /// </summary>
    public void ResolverPreviewMapaGrande()
    {
        if (previewMapaGrande == null)
            previewMapaGrande = BuscarImageEnEscena("Preview_Mapa_Grande");
        if (previewMapShop == null)
            previewMapShop = BuscarImageEnEscena("Preview_MapShop")
                          ?? BuscarImageEnEscena("Preview_Map");
    }

    /// <summary>Busca un GameObject por nombre y devuelve su Image (o la de sus hijos).</summary>
    static Image BuscarImageEnEscena(string nombreObjeto)
    {
        GameObject go = GameObject.Find(nombreObjeto);
        if (go == null) return null;
        Image img = go.GetComponent<Image>();
        if (img == null) img = go.GetComponentInChildren<Image>(true);
        return img;
    }

    /// <summary>
    /// Aplica el sprite a AMBAS vistas previas (menú principal + tienda) si no son nulas.
    /// </summary>
    public void AplicarSpritePreviews(Sprite spriteSeleccionado)
    {
        if (spriteSeleccionado == null) return;
        if (previewMapaGrande != null) previewMapaGrande.sprite = spriteSeleccionado;
        if (previewMapShop != null) previewMapShop.sprite = spriteSeleccionado;
    }

    /// <summary>
    /// Espera al final del frame (layout ya calculado) y luego centra la tarjeta 0
    /// sin animación + refresca preview y flechas. Garantiza apertura sin desalineación.
    /// </summary>
    System.Collections.IEnumerator CentrarAlActivar()
    {
        yield return new WaitForEndOfFrame();
        indiceCarrusel = 0;
        // Sin animar: posicionamiento instantáneo para evitar barrido visible al abrir.
        ActualizarCarruselUI(false);
    }

    /// <summary>
    /// Vincula los botones manuales de las Card_* del MENÚ dentro de contentMapas
    /// (índice de jerarquía = índice de mapa 0..N) a AlPulsarBotonMapa, y las
    /// tarjetas de la TIENDA (contentTiendaMapas) a OnClickBotonTienda(slot).
    /// Cada handler solo toca su propio estado: menú (indiceCarrusel/previewMapaGrande)
    /// o tienda (indiceTiendaMapa/previewMapShop). Se llama en Start() y OnEnable().
    /// </summary>
    public void VincularBotonesMapasManuales()
    {
        // CASO A: tienda con Content propio -> cada Content con su handler.
        if (contentTiendaMapas != null && contentTiendaMapas != contentMapas)
        {
            if (contentMapas != null)
            {
                Button[] btnsMenu = contentMapas.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < btnsMenu.Length; i++)
                {
                    int index = i;
                    Button btn = btnsMenu[i];
                    if (btn == null) continue;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => AlPulsarBotonMapa(btn, index));
                }
            }
            VincularBotonesTienda();
            return;
        }

        // CASO B (actual): un solo Content compartido menú+tienda.
        // Cada tarjeta actualiza AMBOS estados por separado: menú (3D + grande)
        // y tienda (slot + shop). Así el OnClick sirve a las dos vistas sin mezclar índices.
        if (contentMapas != null)
        {
            Button[] btns = contentMapas.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < btns.Length; i++)
            {
                int index = i;
                Button btn = btns[i];
                if (btn == null) continue;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    AlPulsarBotonMapa(btn, index); // menú: 3D + previewMapaGrande
                    OnClickBotonTienda(index);     // tienda: slot + previewMapShop
                });
            }
        }
    }

    /// <summary>
    /// Vincula las tarjetas de la TIENDA a OnClickBotonTienda(slot): cada tarjeta
    /// actualiza ÚNICAMENTE indiceTiendaMapa + previewMapShop + botón de tienda.
    /// Itera TODOS los Button hijos del Content de tienda (Nube, Lava, Infinito,
    /// etc.): cada slot toma su estado (BUY / Derrota a [Jefe] / ¡RECLAMAR! /
    /// UNLOCKED) y su candado vía ActualizarBotonCompraTienda().
    /// Si contentTiendaMapas está vacío se usa contentMapas como respaldo, pero con
    /// handler de tienda (no toca el menú).
    /// </summary>
    public void VincularBotonesTienda()
    {
        RectTransform contentTienda = contentTiendaMapas != null ? contentTiendaMapas : null;
        // Si la tienda tiene su propio Content, vincularlo con handler de tienda.
        if (contentTienda != null && contentTienda != contentMapas)
        {
            VincularSlotsTienda(contentTienda);
        }
        // Si NO hay Content separado, no se re-vincula aquí: el menú ya vinculó
        // contentMapas a AlPulsarBotonMapa. La tienda se opera por slots vía
        // OnClickBotonTienda(slot) llamado desde botones dedicados de la UI.
    }

    /// <summary>
    /// Vincula cada slot (Button hijo) del Content de tienda dado a
    /// OnClickBotonTienda(slot). Tras vincular, pinta CADA tarjeta con el sprite
    /// de SU slot (imagenesMapas[slot]) para que Nube/Lava/Infinito y el resto
    /// muestren su imagen correcta, y refresca el botón con el estado del slot 0.
    /// Devuelve cuántos slots vinculó.
    /// </summary>
    public int VincularSlotsTienda(RectTransform contentTienda)
    {
        if (contentTienda == null) return 0;
        Button[] btns = contentTienda.GetComponentsInChildren<Button>(true);
        int vinculados = 0;
        for (int i = 0; i < btns.Length; i++)
        {
            int slot = i;
            Button btn = btns[i];
            if (btn == null) continue;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnClickBotonTienda(slot));
            // Pintar la tarjeta con el sprite de SU slot (no el de otro mapa).
            PintarTarjetaTienda(btn.transform, slot);
            vinculados++;
        }
        // Refrescar con el slot actual para que cada tarjeta tome su estado
        // (BUY / Derrota a [Jefe] / ¡RECLAMAR! / UNLOCKED).
        ActualizarBotonCompraTienda();
        return vinculados;
    }

    /// <summary>
    /// Pinta la tarjeta de tienda con el Sprite EXACTO de SU slot según el
    /// ConfigMapa del mapa real: SOLO config.spriteImagen (fuente de verdad).
    /// Ya no se usa imagenesMapas[slot] como respaldo para pintar tarjetas, para
    /// evitar que un orden distinto del array muestre la imagen de otro mapa.
    /// La primera Image no-candado de la tarjeta recibe el sprite.
    /// Si el ConfigMapa no tiene sprite, deja la imagen intacta (la asignada en escena).
    /// </summary>
    void PintarTarjetaTienda(Transform tarjeta, int slot)
    {
        if (tarjeta == null) return;
        // Fuente de verdad: el ConfigMapa real del slot.
        int indiceReal = IndiceMapaRealDesdeSlotTienda(slot);
        ConfigMapa config = ConfigPorIndice(indiceReal);
        Sprite spriteCorrecto = config != null ? config.spriteImagen : null;
        if (spriteCorrecto == null) return;
        Image[] imagenes = tarjeta.GetComponentsInChildren<Image>(true);
        if (imagenes == null) return;
        foreach (Image img in imagenes)
        {
            if (img == null) continue;
            string n = img.gameObject.name.ToLowerInvariant();
            if (n.Contains("candado") || n.Contains("lock")) continue;
            if (img.sprite == spriteCorrecto && img.color == Color.white) return;
            img.sprite = spriteCorrecto;
            img.color = Color.white;
            return;
        }
    }

    /// <summary>
    /// Handler del clic en una tarjeta/botón de la TIENDA: actualiza ÚNICAMENTE
    /// el estado de la tienda (indiceTiendaMapa) y refresca previewMapShop con el
    /// mapa de esa tarjeta, más el botón BUY/EQUIP/EQUIPPED.
    /// NO toca el carrusel del menú (indiceCarrusel) ni el mapa 3D activo.
    /// Para comprar/equipar el mapa enfocado de la tienda usar OnClickComprarTienda().
    /// </summary>
    public void OnClickBotonTienda(int index)
    {
        indiceTiendaMapa = Mathf.Clamp(index, 0, Mathf.Max(0, ObtenerTotalTienda() - 1));
        ResolverPreviewMapaGrande();
        ActualizarPreviewTienda();
        ActualizarBotonCompraTienda();
    }

    /// <summary>
    /// Handler del clic en una tarjeta/botón del carrusel del MENÚ: selecciona el
    /// mapa 3D, copia el Sprite visible del botón a previewMapaGrande y refresca
    /// carrusel + botón del menú. NO toca la tienda (indiceTiendaMapa intacto).
    /// d) Si el mapa está BLOQUEADO no inicia partida: solo enfoca la tarjeta
    /// (candado + texto + oscurecido) para informar el bloqueo.
    /// </summary>
    void AlPulsarBotonMapa(Button btn, int index)
    {
        int total = ObtenerTotalCarrusel();
        int clamped = Mathf.Clamp(index, 0, Mathf.Max(0, total - 1));
        ConfigMapa destino = ConfigPorIndice(clamped);
        bool bloqueado = destino == null
            || destino.ObtenerEstadoDesbloqueo() != EstadoDesbloqueoMapa.Desbloqueado;

        if (bloqueado)
        {
            // d) Bloqueado: NO instanciar 3D ni iniciar partida. Solo enfocar la
            // tarjeta para mostrar candado + texto + oscurecido.
            indiceCarrusel = clamped;
            ActualizarPreviewMenu();
            ActualizarCarruselUI(true);
            ActualizarEstadoSeleccionMapa();
            return;
        }

        // 1) Asignar el mapa seleccionado (activa/instancia el escenario 3D).
        // El índice ya fue validado arriba (clamped) y el mapa está desbloqueado.
        SeleccionarMapaPorIndice(clamped);

        // 2) Obtener el Image dentro de ese botón (o sus hijos).
        //    Se prefiere un Image hijo (foto de la tarjeta) sobre el fondo del propio botón.
        Image imgBoton = null;
        if (btn != null)
        {
            Image[] imagenes = btn.GetComponentsInChildren<Image>(true);
            if (imagenes != null && imagenes.Length > 0)
            {
                // Primera imagen con sprite válido que NO sea el fondo del botón.
                foreach (Image img in imagenes)
                {
                    if (img != null && img.sprite != null && img.gameObject != btn.gameObject)
                    { imgBoton = img; break; }
                }
                // Si solo existe la del propio botón, usarla como respaldo.
                if (imgBoton == null)
                {
                    foreach (Image img in imagenes)
                    {
                        if (img != null && img.sprite != null)
                        { imgBoton = img; break; }
                    }
                }
            }
        }

        // 3) Si el botón tiene un Sprite válido, asignarlo directo al MENÚ
        //    (la tienda es independiente: no se toca aquí).
        ResolverPreviewMapaGrande();
        if (imgBoton != null && imgBoton.sprite != null)
        {
            if (previewMapaGrande != null)
                previewMapaGrande.sprite = imgBoton.sprite;
        }
        else
        {
            // Sin sprite en el botón: resolver solo el menú (no la tienda).
            ActualizarPreviewMenu();
        }

        // Mantener carrusel (scroll + flechas) en sincronía con el mapa elegido.
        // El estado candado/texto/oscurecido/botón ya quedó vía SeleccionarMapaPorIndice.
        indiceCarrusel = Mathf.Clamp(clamped, 0, Mathf.Max(0, ObtenerTotalCarrusel() - 1));
        ActualizarFlechas();
        CentrarTarjetaActiva(true);
        ActualizarEstadoSeleccionMapa();
    }

    /// <summary>Total de tarjetas del carrusel: hijos del Content si existe, si no los mapas disponibles.</summary>
    public int ObtenerTotalCarrusel()
    {
        if (contentMapas != null && contentMapas.childCount > 0)
            return contentMapas.childCount;
        int n = MapasDisponibles().Count;
        return Mathf.Max(n, 1);
    }

    /// <summary>Flecha derecha: avanza una tarjeta (llamar desde OnClick de Btn_FlechaDerecha).</summary>
    public void SiguienteMapa() => IrAlMapaCarrusel(indiceCarrusel + 1);

    /// <summary>Flecha izquierda: retrocede una tarjeta (llamar desde OnClick de Btn_FlechaIzquierda).</summary>
    public void MapaAnterior() => IrAlMapaCarrusel(indiceCarrusel - 1);

    /// <summary>
    /// Enfoca la tarjeta indicada: limita el índice, ACTIVA E INSTANCIA
    /// inmediatamente el mapa 3D correspondiente mediante SeleccionarMapaPorIndice()
    /// (escena e interfaz quedan 100% en sincronía), actualiza DE INMEDIATO
    /// previewMapaGrande con el sprite del mapa activo, desplaza el ScrollRect con
    /// suavizado y refresca preview + flechas.
    /// También se usa al hacer clic en una Card_* (pasar su índice).
    /// </summary>
    public void IrAlMapaCarrusel(int indice)
    {
        int total = ObtenerTotalCarrusel();
        indice = Mathf.Clamp(indice, 0, total - 1);
        indiceCarrusel = indice;

        // Activar/instanciar el escenario 3D correspondiente de inmediato.
        SeleccionarMapaPorIndice(indice);

        // Actualización inmediata del menú principal (no espera al layout/scroll).
        // La tienda es independiente: no se toca al navegar el menú.
        ActualizarPreviewMenu();

        ActualizarCarruselUI(true);
    }

    /// <summary>
    /// Sincroniza el carrusel cuando el mapa se eligió por otra vía (boss, torneo, etc.).
    /// Además refresca el candado + botón SELECCIONAR/JUGAR del mapa enfocado.
    /// </summary>
    public void SincronizarCarruselConSeleccion()
    {
        int total = ObtenerTotalCarrusel();
        if (mapaSeleccionado >= 0 && mapaSeleccionado < total)
            indiceCarrusel = mapaSeleccionado;
        ActualizarCarruselUI(false);
        ActualizarEstadoSeleccionMapa();
    }

    /// <summary>
    /// Refresca scroll + preview + flechas + candados de miniaturas.
    /// Sin animación si animar = false.
    /// </summary>
    public void ActualizarCarruselUI(bool animar)
    {
        ActualizarPreview();
        ActualizarFlechas();
        ActualizarCandadosMiniaturasCarrusel();
        CentrarTarjetaActiva(animar);
    }

    /// <summary>
    /// Desplaza el ScrollRect para centrar la Card_* activa.
    /// Calcula la posición destino por PÍXELES EXACTOS (mundo) en lugar de confiar
    /// solo en indice/(total-1): mide cuánto le falta a la tarjeta para llegar al
    /// centro del viewport y lo convierte a posición normalizada 0-1.
    /// </summary>
    public void CentrarTarjetaActiva(bool animar)
    {
        if (scrollMapas == null) return;
        int total = ObtenerTotalCarrusel();
        if (total <= 1) { scrollMapas.horizontalNormalizedPosition = 0f; return; }

        float destino = CalcularDestinoScrollExacto();
        destino = Mathf.Clamp01(destino);

        if (!animar || !gameObject.activeInHierarchy)
        {
            if (corrutinaScroll != null) { StopCoroutine(corrutinaScroll); corrutinaScroll = null; }
            scrollMapas.horizontalNormalizedPosition = destino;
            return;
        }

        if (corrutinaScroll != null) StopCoroutine(corrutinaScroll);
        corrutinaScroll = StartCoroutine(AnimarScroll(destino));
    }

    /// <summary>
    /// Calcula la posición normalizada destino para que la tarjeta enfocada quede
    /// centrada en el viewport, usando posiciones de MUNDO (píxeles exactos).
    /// Independiente de pivots/anchors/spacing del Content: funciona con cualquier layout.
    /// Si algo no es medible, cae al reparto uniforme indice/(total-1).
    /// </summary>
    float CalcularDestinoScrollExacto()
    {
        int total = ObtenerTotalCarrusel();
        float respaldo = total > 1 ? (float)indiceCarrusel / (float)(total - 1) : 0f;

        if (scrollMapas == null || contentMapas == null) return respaldo;
        if (indiceCarrusel < 0 || indiceCarrusel >= contentMapas.childCount) return respaldo;

        RectTransform viewport = scrollMapas.viewport != null
            ? scrollMapas.viewport
            : scrollMapas.GetComponent<RectTransform>();
        RectTransform card = contentMapas.GetChild(indiceCarrusel) as RectTransform;
        if (viewport == null || card == null) return respaldo;

        float anchoVista = viewport.rect.width;
        float anchoContenido = contentMapas.rect.width;
        if (anchoVista <= 0f || anchoContenido <= 0f) return respaldo;

        float escalaVista = Mathf.Abs(viewport.lossyScale.x) > 0.0001f ? Mathf.Abs(viewport.lossyScale.x) : 1f;
        float escalaContenido = Mathf.Abs(contentMapas.lossyScale.x) > 0.0001f ? Mathf.Abs(contentMapas.lossyScale.x) : 1f;
        float vistaMundo = anchoVista * escalaVista;
        float contenidoMundo = anchoContenido * escalaContenido;
        float desplazableMundo = contenidoMundo - vistaMundo;
        if (desplazableMundo <= 0.01f) return 0f;

        // Centro real de la tarjeta y del viewport en coordenadas de MUNDO.
        Vector3 centroTarjetaMundo = card.TransformPoint(card.rect.center);
        Vector3 centroVistaMundo = viewport.TransformPoint(viewport.rect.center);

        // Cuánto debe moverse la tarjeta (mundo) para quedar centrada.
        float deltaMundo = centroVistaMundo.x - centroTarjetaMundo.x;

        // Convertir ese desplazamiento en mundo a delta normalizado 0-1.
        float actual = scrollMapas.horizontalNormalizedPosition;
        float destino = actual - deltaMundo / desplazableMundo;
        return Mathf.Clamp01(destino);
    }

    private IEnumerator AnimarScroll(float targetPos)
    {
        if (scrollMapas == null) yield break;

        float tiempo = 0f;
        float duracion = Mathf.Max(0.05f, duracionScroll);
        float inicioPos = scrollMapas.horizontalNormalizedPosition;

        while (tiempo < duracion)
        {
            if (scrollMapas == null) yield break;
            tiempo += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(tiempo / duracion);
            // Interpola suavemente la posición del ScrollRect
            scrollMapas.horizontalNormalizedPosition = Mathf.Lerp(inicioPos, targetPos, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        if (scrollMapas != null)
            scrollMapas.horizontalNormalizedPosition = targetPos;
        corrutinaScroll = null;
    }

    /// <summary>
    /// Cambia los Sprites de las previews separando fuentes e índices:
    /// - previewMapShop (tienda) usa indiceTiendaMapa -> índice real de mapa.
    /// - previewMapaGrande (menú) usa indiceCarrusel (no toca la tienda).
    /// Al cambiar de mapa activo o hacer clic en uno, este método sincroniza ambas.
    /// </summary>
    public void ActualizarPreview()
    {
        ActualizarPreviewTienda();
        ActualizarPreviewMenu();
    }

    /// <summary>
    /// Actualiza SOLO previewMapaGrande (menú de selección de nivel) con el sprite
    /// del mapa activo: ConfigMapa.spriteImagen -> previewsPorMapa[indice] ->
    /// sprite de la Card_* enfocada. Nunca usa imagenesMapas (fuente de tienda).
    /// Se llama de forma inmediata al navegar el carrusel.
    /// </summary>
    public void ActualizarPreviewMenu()
    {
        ResolverPreviewMapaGrande();
        if (previewMapaGrande == null) return;

        Sprite spriteMenu = ObtenerSpriteMenuPrincipal(indiceCarrusel);

        // Respaldo: copiar el Sprite de la Card_* enfocada.
        if (spriteMenu == null
            && contentMapas != null && indiceCarrusel >= 0 && indiceCarrusel < contentMapas.childCount)
        {
            Transform card = contentMapas.GetChild(indiceCarrusel);
            Image img = card != null ? card.GetComponentInChildren<Image>() : null;
            if (img != null && img.sprite != null && img != previewMapaGrande && img != previewMapShop)
                spriteMenu = img.sprite;
        }

        if (spriteMenu != null)
            previewMapaGrande.sprite = spriteMenu;
    }

    /// <summary>
    /// Actualiza SOLO previewMapShop (tarjeta lateral de la tienda):
    /// 1) config.spriteImagen del mapa real del SLOT enfocado (fuente de verdad);
    /// 2) fallback a la lista global del Inspector (imagenesMapas[slot] y luego
    ///    previewsPorMapa[indiceReal]) si el config no trae sprite.
    /// La imagen solo se habilita si hay sprite válido (nunca recuadro blanco).
    /// Usa indiceTiendaMapa (NO el carrusel del menú).
    /// </summary>
    public void ActualizarPreviewTienda()
    {
        ResolverPreviewMapaGrande();
        if (previewMapShop == null) return;
        int slot = IndiceTiendaClampeado();
        int indiceReal = IndiceMapaRealDesdeSlotTienda(slot);
        ConfigMapa config = ConfigPorIndice(indiceReal);
        // 1) Sprite del ConfigMapa.
        Sprite spriteTienda = config != null ? config.spriteImagen : null;
        // 2) Fallbacks globales del Inspector (mismo slot / mismo mapa real).
        if (spriteTienda == null
            && imagenesMapas != null && slot >= 0 && slot < imagenesMapas.Length)
            spriteTienda = imagenesMapas[slot];
        if (spriteTienda == null
            && previewsPorMapa != null && indiceReal >= 0 && indiceReal < previewsPorMapa.Count)
            spriteTienda = previewsPorMapa[indiceReal];
        if (spriteTienda == null) return;
        if (previewMapShop.sprite != spriteTienda)
            previewMapShop.sprite = spriteTienda;
        if (previewMapShop.color != Color.white)
            previewMapShop.color = Color.white;
        // Habilitar la imagen solo si hay sprite válido (evita recuadro blanco).
        if (!previewMapShop.enabled)
            previewMapShop.enabled = true;
        if (!previewMapShop.gameObject.activeSelf)
            previewMapShop.gameObject.SetActive(true);
    }

    /// <summary>Total de elementos de la tienda (slots de tarjetas si hay Content, si no imagenesMapas, si no 8).</summary>
    public int ObtenerTotalTienda()
    {
        if (contentTiendaMapas != null && contentTiendaMapas.childCount > 0)
            return contentTiendaMapas.childCount;
        if (imagenesMapas != null && imagenesMapas.Length > 0)
            return imagenesMapas.Length;
        return 8;
    }

    /// <summary>Content de la TIENDA (contenedor de sus tarjetas). Si se deja vacío se reutiliza contentMapas.</summary>
    [Tooltip("Content de la TIENDA (sus tarjetas). Si se deja vacío se usa el mismo contentMapas del menú.")]
    public RectTransform contentTiendaMapas;

    /// <summary>Content efectivo de la tienda (el propio o el del menú como respaldo).</summary>
    RectTransform ContentTiendaEfectivo() => contentTiendaMapas != null ? contentTiendaMapas : contentMapas;

    /// <summary>
    /// Convierte un slot de tienda (posición de tarjeta) al índice REAL de mapa 0-7:
    /// 1) mapaIndicePorSlotTienda[slot] si está configurado, 2) autodetección por
    /// NOMBRE de tarjeta o por sprite propio de ConfigMapa/previewsPorMapa,
    /// 3) respaldo: slot 0 -> Space (4), resto mismo índice.
    /// IMPORTANTE: imagenesMapas está ordenado por SLOT de tienda (slot 0 = Space),
    /// NO por índice de mapa, así que su posición NO se usa como índice de mapa.
    /// </summary>
    public int IndiceMapaRealDesdeSlotTienda(int slot)
    {
        // 1) Mapeo manual del Inspector (más fiable).
        if (mapaIndicePorSlotTienda != null && slot >= 0 && slot < mapaIndicePorSlotTienda.Count)
        {
            int mapeado = mapaIndicePorSlotTienda[slot];
            if (mapeado >= 0 && mapeado < 8) return mapeado;
        }

        // 2) Autodetección por tarjeta de la tienda (nombre o sprite).
        RectTransform contentTienda = ContentTiendaEfectivo();
        if (contentTienda != null && slot >= 0 && slot < contentTienda.childCount)
        {
            Transform tarjeta = contentTienda.GetChild(slot);
            if (tarjeta != null)
            {
                // 2a) Por nombre de la tarjeta (Card_Space, space, etc.).
                string nombreTarjeta = NormalizarNombreMapa(tarjeta.name);
                for (int i = 0; i < 8; i++)
                {
                    ConfigMapa c = ConfigPorIndice(i);
                    if (c == null || string.IsNullOrEmpty(c.nombre)) continue;
                    string nombreMapa = NormalizarNombreMapa(c.nombre);
                    if (!string.IsNullOrEmpty(nombreTarjeta) && !string.IsNullOrEmpty(nombreMapa)
                        && (nombreTarjeta.Contains(nombreMapa) || nombreMapa.Contains(nombreTarjeta)))
                        return i;
                }

                // 2b) Por sprite propio de algún ConfigMapa.
                Image img = tarjeta.GetComponentInChildren<Image>(true);
                Sprite spriteTarjeta = img != null ? img.sprite : null;
                if (spriteTarjeta != null)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        ConfigMapa c = ConfigPorIndice(i);
                        if (c != null && c.spriteImagen == spriteTarjeta) return i;
                    }
                    // 2c) Por previewsPorMapa (orden = mapa 0-7).
                    if (previewsPorMapa != null)
                    {
                        for (int i = 0; i < previewsPorMapa.Count && i < 8; i++)
                            if (previewsPorMapa[i] == spriteTarjeta) return i;
                    }
                }
            }
        }

        // 3) Respaldo: slot 0 es Space (mapa 4); resto mismo índice.
        if (slot == 0) return 4;
        return Mathf.Clamp(slot, 0, 7);
    }

    /// <summary>Config del mapa enfocado en la TIENDA (slot -> índice real de mapa).</summary>
    public ConfigMapa ConfigTiendaActiva() => ConfigPorIndice(IndiceMapaRealDesdeSlotTienda(IndiceTiendaClampeado()));

    /// <summary>indiceTiendaMapa limitado al rango válido de la tienda.</summary>
    public int IndiceTiendaClampeado()
    {
        return Mathf.Clamp(indiceTiendaMapa, 0, Mathf.Max(0, ObtenerTotalTienda() - 1));
    }

    /// <summary>
    /// Resetea la tienda a su estado inicial: indiceTiendaMapa = 0 (Space),
    /// preview de tienda con imagenesMapas[0] y botón BUY/EQUIP/EQUIPPED correcto.
    /// Re-vincula los slots (Nube, Lava, Infinito, etc.) por si se agregaron
    /// tarjetas nuevas en el Content de la tienda.
    /// Llamar al abrir la pestaña MAP de la tienda (OnEnable / AlAbrirTiendaMapas).
    /// NO toca el carrusel del menú (indiceCarrusel intacto).
    /// </summary>
    public void AlAbrirTiendaMapas()
    {
        indiceTiendaMapa = 0;
        ResolverPreviewMapaGrande();
        // Re-vincular slots nuevos (si Content propio) antes de refrescar estados.
        if (contentTiendaMapas != null && contentTiendaMapas != contentMapas)
            VincularSlotsTienda(contentTiendaMapas);
        ActualizarPreviewTienda();
        ActualizarBotonCompraTienda();
    }

    /// <summary>
    /// Devuelve el Sprite de TIENDA para un SLOT de tienda (posición en la lista).
    /// Usa el array imagenesMapas EN ORDEN DE SLOT: imagenesMapas[slot].
    /// (imagenesMapas está ordenado por slot: slot 0 = Space, NO por índice de mapa.)
    /// Si el slot no tiene sprite, cae al mapa real del slot (config/listas).
    /// </summary>
    public Sprite ObtenerSpriteTiendaPorSlot(int slot)
    {
        if (imagenesMapas != null && slot >= 0 && slot < imagenesMapas.Length
            && imagenesMapas[slot] != null)
        {
            return imagenesMapas[slot];
        }
        // Respaldo: sprite del mapa real asociado a ese slot.
        return ObtenerSpriteMapa(IndiceMapaRealDesdeSlotTienda(slot));
    }

    /// <summary>
    /// Devuelve el Sprite correspondiente al mapa indicado (ÍNDICE DE MAPA 0-7).
    /// Prioridad: 1) ConfigMapa.spriteImagen, 2) previewsPorMapa[indice].
    /// NOTA: imagenesMapas NO se usa aquí porque está ordenado por SLOT de tienda,
    /// no por índice de mapa (usar ObtenerSpriteTiendaPorSlot para slots).
    /// Esta es una fuente auxiliar de la TIENDA (previewMapShop) por mapa real.
    /// </summary>
    public Sprite ObtenerSpriteMapa(int indice)
    {
        // 1) Sprite propio de la configuración del mapa.
        ConfigMapa config = ConfigPorIndice(indice);
        if (config != null && config.spriteImagen != null)
            return config.spriteImagen;

        // 2) Lista global asignada en el Inspector (índice = mapa).
        if (previewsPorMapa != null && indice >= 0 && indice < previewsPorMapa.Count
            && previewsPorMapa[indice] != null)
        {
            return previewsPorMapa[indice];
        }

        return null;
    }

    /// <summary>
    /// Devuelve el Sprite del MENÚ de selección de nivel (previewMapaGrande):
    /// DIRECTAMENTE ConfigMapa.spriteImagen -> previewsPorMapa[indice].
    /// Sin pasar por imagenesMapas (esa es fuente exclusiva de la tienda).
    /// </summary>
    public Sprite ObtenerSpriteMenuPrincipal(int indice)
    {
        // 1) Sprite propio de la configuración del mapa.
        ConfigMapa config = ConfigPorIndice(indice);
        if (config != null && config.spriteImagen != null)
            return config.spriteImagen;

        // 2) Lista global asignada en el Inspector (índice = mapa).
        if (previewsPorMapa != null && indice >= 0 && indice < previewsPorMapa.Count
            && previewsPorMapa[indice] != null)
        {
            return previewsPorMapa[indice];
        }

        return null;
    }

    /// <summary>
    /// Sincroniza el Image de vista previa del MENÚ con el mapa activo.
    /// Usa la fuente del menú (ConfigMapa.spriteImagen -> previewsPorMapa).
    /// La tienda NO se toca aquí (tiene su propio índice y refresco).
    /// </summary>
    public void SincronizarPreviewConMapaActivo()
    {
        ConfigMapa config = ConfigPorIndice(mapaSeleccionado);
        SincronizarPreviewConMapa(config);
    }

    /// <summary>Sincroniza SOLO previewMapaGrande (menú) con el ConfigMapa indicado.</summary>
    public void SincronizarPreviewConMapa(ConfigMapa mapaSeleccionado)
    {
        ResolverPreviewMapaGrande();
        int indice = IndiceDeConfig(mapaSeleccionado);
        if (indice < 0 && mapaSeleccionado != null && mapaSeleccionado.spriteImagen != null)
        {
            if (previewMapaGrande != null) previewMapaGrande.sprite = mapaSeleccionado.spriteImagen;
            return;
        }
        Sprite spriteMenu = indice >= 0 ? ObtenerSpriteMenuPrincipal(indice) : null;
        if (previewMapaGrande != null && spriteMenu != null)
            previewMapaGrande.sprite = spriteMenu;
        else
            ActualizarPreviewMenu();
    }

    /// <summary>Devuelve el índice 0-7 del ConfigMapa indicado, o -1 si no pertenece a la lista.</summary>
    int IndiceDeConfig(ConfigMapa config)
    {
        if (config == null) return -1;
        for (int i = 0; i < 8; i++)
        {
            if (ConfigPorIndice(i) == config) return i;
        }
        return -1;
    }

    /// <summary>Apaga la flecha izquierda en el índice 0 y la derecha en el último.</summary>
    public void ActualizarFlechas()
    {
        int total = ObtenerTotalCarrusel();
        if (btnFlechaIzquierda != null) btnFlechaIzquierda.interactable = indiceCarrusel > 0;
        if (btnFlechaDerecha != null) btnFlechaDerecha.interactable = indiceCarrusel < total - 1;
    }

    /// <summary>
    /// Efecto carrusel: escala cada tarjeta según su distancia al centro del
    /// viewport, medida en World Space (GetWorldCorners). 1.0 en el centro,
    /// escalaMinimaTarjeta en los bordes. Se llama cada frame desde Update().
    /// </summary>
    void ActualizarEscalaTarjetas()
    {
        if (!activarEscalaCarrusel) return;
        if (contentMapas == null || scrollMapas == null) return;

        // Obtener la posición X del centro del Viewport en coordenadas de pantalla
        RectTransform viewportRect = scrollMapas.viewport != null
            ? scrollMapas.viewport
            : scrollMapas.GetComponent<RectTransform>();
        if (viewportRect == null) return;

        Vector3[] viewportCorners = new Vector3[4];
        viewportRect.GetWorldCorners(viewportCorners);
        float centroViewportX = (viewportCorners[0].x + viewportCorners[2].x) / 2f;
        float anchoViewport = Mathf.Abs(viewportCorners[2].x - viewportCorners[0].x);
        if (anchoViewport <= 0.0001f) return;

        foreach (Transform child in contentMapas)
        {
            RectTransform childRect = child as RectTransform;
            if (childRect == null || !child.gameObject.activeSelf) continue;

            // Obtener el centro X de la tarjeta en coordenadas de pantalla
            Vector3[] childCorners = new Vector3[4];
            childRect.GetWorldCorners(childCorners);
            float centroTarjetaX = (childCorners[0].x + childCorners[2].x) / 2f;

            // Distancia absoluta al centro del Viewport
            float distancia = Mathf.Abs(centroViewportX - centroTarjetaX);

            // Normalizar la distancia (0 en el centro, 1 en los bordes del Viewport)
            float factor = Mathf.Clamp01(distancia / (anchoViewport / 2f));

            // Interpolar escala entre 1.0 y escalaMinimaTarjeta (0.6)
            float escala = Mathf.Lerp(1f, escalaMinimaTarjeta, factor);
            childRect.localScale = new Vector3(escala, escala, 1f);
        }
    }

   
   
}