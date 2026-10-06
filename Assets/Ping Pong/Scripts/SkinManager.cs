using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Maneja la selección de skins de raqueta.
/// Al confirmar, aplica el material a la raqueta del jugador y vuelve al menú.
/// </summary>
public class SkinManager : MonoBehaviour
{
    public static SkinManager Instance { get; private set; }

    [Header("Referencias UI")]
    public Transform  contenedorBotones;
    public RawImage   previewSkin;
    public GameObject botonSkinPrefab;

    [Header("Preview 3D")]
    public PreviewRotador previewRotador;

    [Header("Tienda / Compra de skins")]
    [Tooltip("Botón principal del panel derecho (BUY / EQUIP / EQUIPPED).")]
    public UnityEngine.UI.Button botonAccionSkin;
    [Tooltip("Texto del botón de acción. Si se deja vacío se busca en los hijos del botón.")]
    public TMPro.TMP_Text textoBotonAccionSkin;
    [Tooltip("Texto inferior con el precio (ej: $300 Pollocoins). Visible solo si la skin está bloqueada.")]
    public TMPro.TMP_Text textoPrecioSkin;
    [Tooltip("Precio por defecto si la lista de precios es más corta que la de skins.")]
    public int precioSkinPorDefecto = 100;
    [Tooltip("Un precio por skin (índice = skin). Ej: [0, 100, 200...]. El 0 = gratis.")]
    public System.Collections.Generic.List<int> preciosSkins = new System.Collections.Generic.List<int>();
    [Tooltip("Índices desbloqueados por defecto (ej: 0). El resto se compra.")]
    public System.Collections.Generic.List<int> skinsDesbloqueadasPorDefecto = new System.Collections.Generic.List<int>() { 0 };

    [Header("Raqueta del jugador")]
    public Renderer rendererRaquetaJugador;

    [Header("Skins disponibles")]
    [Tooltip("Arrastra aquí todos los prefabs de skins en orden")]
    public List<GameObject> skinsPrefabs = new List<GameObject>();

    // --- Estado interno ---
    private int          skinSeleccionada    = 0;
    // Qué índice quedó aplicado a la raqueta (EQUIPPED).
    private int          skinEquipada        = -1;
    private Material     materialSeleccionado;
    private List<Button> botonesSkins        = new List<Button>();

    /// <summary>Clave PlayerPrefs donde persiste la skin equipada.</summary>
    public const string CLAVE_SKIN_EQUIPADA = "SKIN_RAQUETA_EQUIPADA";

    // -------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // NO regenerar: los botones ya existen colocados a mano en la escena.
        // Solo se enlazan los Button hijos del contenedor en orden (0, 1, 2... N).
        VincularBotonesManuales();

        // Seleccionar la primera skin por defecto
        if (botonesSkins.Count > 0)
        {
            SeleccionarSkin(0);
        }
    }

    /// <summary>
    /// Busca los Button hijos de contenedorBotones (en orden de jerarquía),
    /// los guarda en botonesSkins y les asigna el listener OnClick con su índice.
    /// No destruye ni instancia nada: respeta los botones manuales de la escena.
    /// </summary>
    public void VincularBotonesManuales()
    {
        botonesSkins.Clear();
        if (contenedorBotones == null) return;

        // Cargar botones manuales presentes en el contenedor
        Button[] btns = contenedorBotones.GetComponentsInChildren<Button>(true);

        for (int i = 0; i < btns.Length; i++)
        {
            int index = i;
            Button btn = btns[i];
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => SeleccionarSkin(index));
            botonesSkins.Add(btn);
        }
    }

    // -------------------------------------------------------
    void GenerarBotones()
    {
        foreach (Transform hijo in contenedorBotones)
            Destroy(hijo.gameObject);

        botonesSkins.Clear();

        for (int i = 0; i < skinsPrefabs.Count; i++)
        {
            if (skinsPrefabs[i] == null) continue;

            int index = i;

            GameObject btnObj = Instantiate(botonSkinPrefab, contenedorBotones);
            btnObj.name = $"BtnSkin_{i}";

            // Color del botón según el material del prefab
            Renderer rend = skinsPrefabs[i].GetComponentInChildren<Renderer>();
            if (rend != null && rend.sharedMaterial != null)
            {
                Image imgBtn = btnObj.GetComponent<Image>();
                if (imgBtn != null)
                {
                    Material mat   = rend.sharedMaterial;
                    Color colorBtn = Color.white;
                    if (mat.HasProperty("_BaseColor"))
                        colorBtn = mat.GetColor("_BaseColor");
                    else if (mat.HasProperty("_Color"))
                        colorBtn = mat.GetColor("_Color");
                    imgBtn.color = colorBtn;
                }
            }

            // Nombre del prefab en el botón
            TMP_Text texto = btnObj.GetComponentInChildren<TMP_Text>();
            if (texto != null)
                texto.text = skinsPrefabs[i].name.Replace("Raqueta", "");

            // Click
            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => SeleccionarSkin(index));
                botonesSkins.Add(btn);
            }
        }

        // Cuadrícula siempre arriba al (re)generar: evita scroll aleatorio del GridLayout.
        ResetearScrollArriba();
    }

    /// <summary>
    /// Posiciona la cuadrícula en la parte superior: resetea el Content a
    /// anchoredPosition cero y el ScrollRect a verticalNormalizedPosition = 1.
    /// Se difiere al final del frame para que el layout ya esté calculado.
    /// </summary>
    public void ResetearScrollArriba()
    {
        if (contenedorBotones == null) return;
        if (contenedorBotones is RectTransform rectContenedor)
        {
            rectContenedor.anchoredPosition = Vector2.zero;
        }
        StartCoroutine(ResetearScrollArribaDiferido());
    }

    IEnumerator ResetearScrollArribaDiferido()
    {
        yield return new WaitForEndOfFrame();
        if (contenedorBotones == null) yield break;

        if (contenedorBotones is RectTransform rectContenedor)
        {
            rectContenedor.anchoredPosition = Vector2.zero;
        }

        ScrollRect scroll = contenedorBotones.GetComponentInParent<ScrollRect>();
        if (scroll != null)
        {
            scroll.verticalNormalizedPosition = 1f;
            scroll.horizontalNormalizedPosition = 0f;
        }
    }

    /// <summary>
    /// Llamar al abrir el panel de skins para garantizar que la cuadrícula
    /// arranque arriba (2 columnas de 150x150 con desplazamiento continuo).
    /// Además refresca el botón BUY/EQUIP con el saldo actual.
    /// </summary>
    void OnEnable()
    {
        ResetearScrollArriba();
        ActualizarBotonAccionSkin();
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnPollocoinsChanged -= RefrescarBotonSkinPorSaldo;
            EconomyManager.Instance.OnPollocoinsChanged += RefrescarBotonSkinPorSaldo;
        }
    }

    void OnDisable()
    {
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnPollocoinsChanged -= RefrescarBotonSkinPorSaldo;
    }

    void RefrescarBotonSkinPorSaldo(int _) => ActualizarBotonAccionSkin();

    // -------------------------------------------------------
    public void SeleccionarSkin(int index)
    {
        if (index < 0 || index >= skinsPrefabs.Count) return;

        skinSeleccionada = index;

        // Obtener material del prefab seleccionado
        Renderer rend = skinsPrefabs[index].GetComponentInChildren<Renderer>();
        if (rend != null)
            materialSeleccionado = rend.sharedMaterial;

        // Resaltar botón seleccionado
        for (int i = 0; i < botonesSkins.Count; i++)
        {
            ColorBlock cb      = botonesSkins[i].colors;
            cb.normalColor     = (i == index)
                ? new Color(0.3f, 0.8f, 0.3f)
                : new Color(0.2f, 0.2f, 0.2f);
            botonesSkins[i].colors = cb;
        }

        // Mostrar modelo 3D en el preview
        previewRotador?.MostrarPrefab(skinsPrefabs[index]);
        ActualizarBotonAccionSkin();
    }

    // ─── Tienda de skins: BUY / EQUIP / EQUIPPED ───
    string ClaveSkin(int index) => "SKIN_DESBLOQUEADA_" + index;

    /// <summary>Precio de la skin (lista preciosSkins o precioSkinPorDefecto).</summary>
    public int PrecioSkin(int index)
    {
        if (index < 0) return precioSkinPorDefecto;
        if (preciosSkins != null && index < preciosSkins.Count) return preciosSkins[index];
        return precioSkinPorDefecto;
    }

    /// <summary>¿Desbloqueada? Por defecto, gratis (precio 0) o PlayerPrefs.</summary>
    public bool SkinDesbloqueada(int index)
    {
        if (skinsDesbloqueadasPorDefecto != null && skinsDesbloqueadasPorDefecto.Contains(index)) return true;
        if (PrecioSkin(index) <= 0) return true;
        return PlayerPrefs.GetInt(ClaveSkin(index), 0) == 1;
    }

    void DesbloquearSkin(int index)
    {
        PlayerPrefs.SetInt(ClaveSkin(index), 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Botón principal del panel derecho:
    /// a) Bloqueado -&gt; "BUY" + "$XXX Pollocoins". b) Desbloqueado -&gt; "EQUIP". c) Equipado -&gt; "EQUIPPED".
    /// </summary>
    public void ActualizarBotonAccionSkin()
    {
        TMPro.TMP_Text texto = textoBotonAccionSkin;
        if (texto == null && botonAccionSkin != null)
            texto = botonAccionSkin.GetComponentInChildren<TMPro.TMP_Text>(true);
        int precio = PrecioSkin(skinSeleccionada);

        if (!SkinDesbloqueada(skinSeleccionada))
        {
            if (texto != null) texto.text = "BUY";
            if (textoPrecioSkin != null) textoPrecioSkin.text = "$" + precio + " Pollocoins";
            if (botonAccionSkin != null)
                botonAccionSkin.interactable = EconomyManager.Instance != null
                    && EconomyManager.Instance.TieneSuficiente(precio);
            return;
        }

        if (textoPrecioSkin != null) textoPrecioSkin.text = "";
        if (skinEquipada == skinSeleccionada)
        {
            if (texto != null) texto.text = "EQUIPPED";
            if (botonAccionSkin != null) botonAccionSkin.interactable = false;
        }
        else
        {
            if (texto != null) texto.text = "EQUIP";
            if (botonAccionSkin != null) botonAccionSkin.interactable = true;
        }
    }

    /// <summary>Acción del botón BUY / EQUIP. Conectar a su OnClick. RETURN es botón aparte.</summary>
    public void OnClickAccionSkin()
    {
        if (!SkinDesbloqueada(skinSeleccionada))
        {
            if (EconomyManager.Instance == null) return;
            if (EconomyManager.Instance.ConsumirPollocoins(PrecioSkin(skinSeleccionada)))
            {
                DesbloquearSkin(skinSeleccionada);
                ActualizarBotonAccionSkin();
            }
            return;
        }
        EquiparSkinSeleccionada();
    }

    void EquiparSkinSeleccionada()
    {
        // 1) Estado interno + persistencia inmediata (misma sesión, sin reiniciar).
        skinEquipada = skinSeleccionada;
        PlayerPrefs.SetInt(CLAVE_SKIN_EQUIPADA, skinEquipada);
        PlayerPrefs.Save();
        // 2) Aplicación instantánea: preview 3D + raqueta activa de la escena.
        AplicarSkinEquipadaConLog(rendererRaquetaJugador);
        // Notificar a la UI / GameManager en tiempo real (misma sesión).
        NotificarSkinEquipada(skinEquipada);
        ActualizarBotonAccionSkin();
    }

    /// <summary>Evento: se dispara al equipar una skin (índice como parámetro).</summary>
    public static event System.Action<int> OnSkinEquipada;

    /// <summary>Notifica a los suscriptores (UI/GameManager) el cambio de skin en tiempo real.</summary>
    public static void NotificarSkinEquipada(int indice)
    {
        OnSkinEquipada?.Invoke(indice);
    }

    /// <summary>Índice equipado guardado en PlayerPrefs (0 por defecto).</summary>
    public static int LeerSkinEquipada()
    {
        return PlayerPrefs.GetInt(CLAVE_SKIN_EQUIPADA, 0);
    }

    /// <summary>
    /// Aplica la skin equipada (PlayerPrefs) al Renderer indicado.
    /// Asigna material Y sharedMaterial, con log [Skin Debug].
    /// Llamar al arrancar el partido sobre la raqueta 3D del jugador.
    /// Devuelve true si aplicó un material válido.
    /// </summary>
    public bool AplicarSkinEquipada(Renderer destino)
    {
        return AplicarSkinEquipadaConLog(destino);
    }

    /// <summary>
    /// Versión con diagnóstico: aplica material + sharedMaterial e imprime
    /// [Skin Debug] con el material y el GameObject destino.
    /// Si el destino es null, intenta usar rendererRaquetaJugador.
    /// </summary>
    public bool AplicarSkinEquipadaConLog(Renderer destino)
    {
        if (destino == null) destino = rendererRaquetaJugador;
        if (destino == null) return false;
        int indice = Mathf.Clamp(LeerSkinEquipada(), 0, Mathf.Max(0, skinsPrefabs.Count - 1));
        if (skinsPrefabs == null || indice < 0 || indice >= skinsPrefabs.Count) return false;
        if (skinsPrefabs[indice] == null) return false;
        Renderer rend = skinsPrefabs[indice].GetComponentInChildren<Renderer>();
        Material mat = rend != null ? rend.sharedMaterial : null;
        if (mat == null) return false;
        // Asignar ambas propiedades: material (instancia en juego) y
        // sharedMaterial (visible de inmediato aunque el objeto se instancie tarde).
        destino.material = mat;
        destino.sharedMaterial = mat;
        // TODO DEBUG-SKIN: log temporal de diagnóstico. Quitar al estabilizar.
        Debug.Log($"[Skin Debug] Aplicando material {mat.name} al Renderer: {destino.gameObject.name}");
        skinEquipada = indice;
        skinSeleccionada = indice;
        materialSeleccionado = mat;
        return true;
    }

    // -------------------------------------------------------
    /// Aplica el material a la raqueta del jugador y vuelve al menú
    public void ConfirmarSkin()
    {
        EquiparSkinSeleccionada();
        if (rendererRaquetaJugador != null && materialSeleccionado != null)
        {
            rendererRaquetaJugador.material = materialSeleccionado;
        }

        // Volver al menú principal
        UIManager.Instance?.OnClickVolverAlMenu();
    }

    // -------------------------------------------------------
    public Material GetMaterialSeleccionado() => materialSeleccionado;

    // -------------------------------------------------------
    /// <summary>
    /// Devuelve el material de la skin en el índice indicado.
    /// Reutiliza la misma lógica de SeleccionarSkin() sin cambiar la skin activa.
    /// Útil para aplicar skins a la raqueta del CPU en modo torneo.
    /// </summary>
    public Material GetSkinMaterial(int index)
    {
        if (index < 0 || index >= skinsPrefabs.Count) return null;
        Renderer rend = skinsPrefabs[index].GetComponentInChildren<Renderer>();
        return rend != null ? rend.sharedMaterial : null;
    }

    // -------------------------------------------------------
    /// <summary>
    /// Devuelve el prefab completo de skin en el índice indicado.
    /// Útil para instanciar el modelo completo (mesh + materiales + jerarquía)
    /// en la raqueta del CPU durante el modo torneo.
    /// </summary>
    public GameObject GetSkinPrefab(int index)
    {
        if (index < 0 || index >= skinsPrefabs.Count) return null;
        return skinsPrefabs[index];
    }
}
