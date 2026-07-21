using UnityEngine;

/// <summary>
/// Rota el objeto de preview de la raqueta en la cámara secundaria.
/// Coloca este script en Preview_Raqueta_3D.
/// </summary>
public class PreviewRotador : MonoBehaviour
{
    [Header("Rotación")]
    public float velocidadRotacion = 45f;
    public Vector3 orientacionInicial = new Vector3(90f, 180f, 0f);

    private GameObject modeloActual;

    void Update()
    {
        if (modeloActual != null)
            modeloActual.transform.Rotate(Vector3.up, velocidadRotacion * Time.deltaTime, Space.World);
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