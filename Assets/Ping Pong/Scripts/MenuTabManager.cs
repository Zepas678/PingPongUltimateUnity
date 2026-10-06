using UnityEngine;

/// <summary>
/// Gestiona el cambio entre secciones (pestañas) dentro del Panel_Skins.
/// Asignar al GameObject Panel_Skins y arrastrar las secciones en el Inspector.
/// Los botones Tab_ButtonSkins / Tab_ButtonMapas llaman a MostrarSkins() / MostrarMapas().
/// </summary>
public class MenuTabManager : MonoBehaviour
{
    [Header("Paneles de Secciones")]
    [Tooltip("Sección de skins (cuadrícula de botones)")]
    public GameObject seccionSkins;
    [Tooltip("Sección de mapas (carrusel Panel_Mapas)")]
    public GameObject seccionMapas;
    [Tooltip("Sección de habilidades (opcional, reservado)")]
    public GameObject seccionHabilidades;

    [Header("Vista previa 3D")]
    [Tooltip("Objeto de la vista previa 3D de la skin (se muestra solo en la pestaña Skins)")]
    public GameObject previewSkin3D;

    void Start()
    {
        // Al iniciar, mostramos la sección de Skins por defecto
        MostrarSkins();
    }

    public void MostrarSkins()
    {
        if (seccionSkins != null) seccionSkins.SetActive(true);
        if (seccionMapas != null) seccionMapas.SetActive(false);
        if (seccionHabilidades != null) seccionHabilidades.SetActive(false);
        if (previewSkin3D != null) previewSkin3D.SetActive(true);
    }

    public void MostrarMapas()
    {
        if (seccionSkins != null) seccionSkins.SetActive(false);
        if (seccionMapas != null) seccionMapas.SetActive(true);
        if (seccionHabilidades != null) seccionHabilidades.SetActive(false);
        if (previewSkin3D != null) previewSkin3D.SetActive(false);
        // Resetear la tienda de mapas: índice 0 (Space) + preview + botón correctos.
        // No toca el carrusel del menú de selección (MapManager.indiceCarrusel intacto).
        MapManager.Instance?.AlAbrirTiendaMapas();
    }

    /// <summary>Reservado para la futura pestaña de habilidades.</summary>
    public void MostrarHabilidades()
    {
        if (seccionSkins != null) seccionSkins.SetActive(false);
        if (seccionMapas != null) seccionMapas.SetActive(false);
        if (seccionHabilidades != null) seccionHabilidades.SetActive(true);
        if (previewSkin3D != null) previewSkin3D.SetActive(false);
    }
}
