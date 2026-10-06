using UnityEngine;

/// <summary>
/// Rota el objeto de preview de la raqueta en la cámara secundaria.
/// Coloca este script en Preview_Raqueta_3D.
/// La rotación usa Time.unscaledDeltaTime para no pausarse con Time.timeScale
/// (tienda / submenús pausan el fondo pero el preview sigue girando).
/// </summary>
public class PreviewRotador : MonoBehaviour
{
    [Header("Rotación")]
    public float velocidadRotacion = 45f;
    public Vector3 orientacionInicial = new Vector3(90f, 180f, 0f);
    [Tooltip("Si es true, rota con tiempo no escalado (sigue girando con Time.timeScale = 0).")]
    public bool usarTiempoNoEscalado = true;

    private GameObject modeloActual;

    void Update()
    {
        if (modeloActual == null) return;
        float dt = usarTiempoNoEscalado ? Time.unscaledDeltaTime : Time.deltaTime;
        // Fallback: si el tiempo escalado está pausado, usar no escalado para no congelar.
        if (!usarTiempoNoEscalado && dt <= 0f) dt = Time.unscaledDeltaTime;
        modeloActual.transform.Rotate(Vector3.up, velocidadRotacion * dt, Space.World);
    }

    /// <summary>
    /// Instancia el prefab de la skin seleccionada frente a la cámara de preview.
    /// </summary>
    public void MostrarPrefab(GameObject prefab)
    {
        if (modeloActual != null)
            Destroy(modeloActual);

        if (prefab == null) return;

        modeloActual = Instantiate(prefab, transform.position, Quaternion.identity, transform);

        modeloActual.transform.localScale    = new Vector3(72f, 22f, 293f);
        modeloActual.transform.localPosition = Vector3.zero;
        modeloActual.transform.localRotation = Quaternion.Euler(-90f, 0f, 90f);

        // Desactivar componentes innecesarios en el preview
        foreach (var comp in modeloActual.GetComponentsInChildren<MonoBehaviour>())
            comp.enabled = false;

        Debug.Log($"[Preview] Mostrando: {prefab.name}");
    }
}