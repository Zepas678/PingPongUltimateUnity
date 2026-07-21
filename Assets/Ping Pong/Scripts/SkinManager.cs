using UnityEngine;
using UnityEngine.UI;
using TMPro;
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

    [Header("Raqueta del jugador")]
    public Renderer rendererRaquetaJugador;

    [Header("Skins disponibles")]
    [Tooltip("Arrastra aquí todos los prefabs de skins en orden")]
    public List<GameObject> skinsPrefabs = new List<GameObject>();

    // --- Estado interno ---
    private int          skinSeleccionada    = 0;
    private Material     materialSeleccionado;
    private List<Button> botonesSkins        = new List<Button>();

    // -------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        GenerarBotones();
        SeleccionarSkin(0);
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
    }

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

        Debug.Log($"[SkinManager] Skin seleccionada: {skinsPrefabs[index].name}");
    }

    // -------------------------------------------------------
    /// Aplica el material a la raqueta del jugador y vuelve al menú
    public void ConfirmarSkin()
    {
        if (rendererRaquetaJugador != null && materialSeleccionado != null)
        {
            rendererRaquetaJugador.material = materialSeleccionado;
            Debug.Log($"[SkinManager] Skin aplicada: {materialSeleccionado.name}");
        }

        // Volver al menú principal
        UIManager.Instance?.OnClickVolverAlMenu();
    }

    // -------------------------------------------------------
    public Material GetMaterialSeleccionado() => materialSeleccionado;
}