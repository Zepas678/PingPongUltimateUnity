using UnityEngine;
using TMPro;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Maneja el contador de combos — rebotes consecutivos cuando la pelota
/// supera la velocidad épica (110). Combo separado para jugador y CPU.
/// </summary>
public class ComboManager : MonoBehaviour
{
    public static ComboManager Instance { get; private set; }

    [Header("UI")]
    public GameObject contenedorCombo;
    public TMP_Text   textoCombo;

    [Header("Sonidos")]
    public AudioClip sonidoCombo;

    [Header("Configuración")]
    [Tooltip("Velocidad mínima para que un golpe cuente como combo")]
    public float velocidadCombo = 110f;

    [Tooltip("Segundos que se muestra el resultado final antes de desaparecer")]
    public float duracionResultadoFinal = 1.5f;

    // --- Estado interno ---
    private int  comboJugador = 0;
    private int  comboCPU     = 0;
    private bool comboActivo  = false;
    private AudioSource audioSource;
    // Max combo alcanzado en la sesión (solo jugador)
    private int maxComboJugadorSession = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (sonidoCombo == null)
            sonidoCombo = CargarClipPorDefecto("Assets/Ping Pong/Sonidos/Raqueta/explosionCrunch_004.ogg");
    }

    void Start()
    {
        if (contenedorCombo != null)
            contenedorCombo.SetActive(false);
    }

    // -------------------------------------------------------
    /// <summary>
    /// Llamar cada vez que la raqueta golpea la pelota.
    /// esJugador: true si fue el jugador, false si fue la CPU.
    /// velocidadPelota: velocidad actual de la pelota en ese golpe.
    /// </summary>
    public void RegistrarGolpe(bool esJugador, float velocidadPelota)
    {
        if (velocidadPelota < velocidadCombo)
        {
            // Golpe normal — si había un combo activo, se rompe
            if (comboActivo)
                FinalizarCombo();
            return;
        }

        // Golpe épico — sumar al combo correspondiente
        comboActivo = true;
        CancelInvoke(nameof(FinalizarCombo)); // cancelar cualquier cierre pendiente

        if (esJugador)
            comboJugador++;
        else
            comboCPU++;

        // Actualizar máximo de sesión
        if (esJugador && comboJugador > maxComboJugadorSession)
            maxComboJugadorSession = comboJugador;

        ActualizarUI();
        MostrarContenedor();
        ReproducirSonido(sonidoCombo);
    }

    // -------------------------------------------------------
    /// <summary>
    /// Llamar cuando alguien anota un punto — rompe el combo inmediatamente
    /// y muestra el resultado final brevemente.
    /// </summary>
    public void RomperComboPorPunto()
    {
        if (!comboActivo) return;
        FinalizarCombo();
    }

    // -------------------------------------------------------
    void FinalizarCombo()
    {
        comboActivo = false;
        ActualizarUI(); // mostrar el resultado final una última vez

        // Esperar antes de ocultar y resetear
        Invoke(nameof(OcultarYResetear), duracionResultadoFinal);
    }

    void OcultarYResetear()
    {
        if (contenedorCombo != null)
            contenedorCombo.SetActive(false);

        comboJugador = 0;
        comboCPU     = 0;
    }

    /// <summary>
    /// Obtiene el combo actual del jugador.
    /// </summary>
    public int CurrentComboJugador => comboJugador;

    /// <summary>
    /// Obtiene el máximo combo logrado por el jugador en la sesión.
    /// </summary>
    public int MaxComboJugadorSession => maxComboJugadorSession;

    /// <summary>
    /// Resetea el máximo de combo de la sesión.
    /// </summary>
    public void ResetSessionMax()
    {
        maxComboJugadorSession = 0;
    }

    // -------------------------------------------------------
    AudioClip CargarClipPorDefecto(string ruta)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
#else
        return null;
#endif
    }

    void ReproducirSonido(AudioClip clip)
    {
        if (clip == null) return;

        if (audioSource != null)
            audioSource.PlayOneShot(clip);
        else
            AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : transform.position);
    }

    void MostrarContenedor()
    {
        if (contenedorCombo != null)
            contenedorCombo.SetActive(true);
    }

    void ActualizarUI()
    {
        if (textoCombo == null) return;
        textoCombo.text = $"¡COMBO!!\nx{comboJugador} / x{comboCPU}\nPLAYER / CPU";
    }
}
