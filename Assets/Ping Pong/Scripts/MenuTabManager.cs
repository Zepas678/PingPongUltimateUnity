using UnityEngine;
using UnityEngine.UI;

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
        // Asegurar que los botones de la sección Habilidades tengan fondo visible
        // aunque nunca se active esa pestaña (evita texto "flotante" sin contenedor gráfico).
        AsegurarFondoBotonesHabilidades();
    }

    void LateUpdate()
    {
        // Reparar cada frame el fondo del botón HAB, ya que scripts externos
        // (MainMenuBackgroundManager.ApagarPorNombre("Habilidad")) pueden
        // deshabilitar su componente Image en cualquier transición de escena/partida.
        AsegurarFondoBotonesHabilidades();
    }

    /// <summary>
    /// Dale un color de fondo visible a los botones dentro de seccionHabilidades
    /// que carezcan de Sprite asignado en su Image. Evita que el texto "HAB" quede
    /// flotando sin contenedor gráfico. También asegura que el componente Image
    /// esté habilitado (puede ser desactivado por MainMenuBackgroundManager al
    /// buscar objetos con "Habilidad" en el nombre).
    /// </summary>
    public void AsegurarFondoBotonesHabilidades()
    {
        if (seccionHabilidades == null) return;
        var btns = seccionHabilidades.GetComponentsInChildren<Button>(true);
        foreach (var b in btns)
        {
            if (b == null) continue;
            var img = b.GetComponent<Image>();
            if (img != null)
            {
                img.enabled = true;
                if (img.sprite == null)
                {
                    img.color = new Color(0.2f, 0.6f, 1f, 1f);
                }
            }
        }
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

    /// <summary>Muestra la pestaña de habilidades y asegura visibilidad del botón HAB.</summary>
    public void MostrarHabilidades()
    {
        if (seccionSkins != null) seccionSkins.SetActive(false);
        if (seccionMapas != null) seccionMapas.SetActive(false);
        if (seccionHabilidades != null) seccionHabilidades.SetActive(true);
        if (previewSkin3D != null) previewSkin3D.SetActive(false);

        AsegurarFondoBotonesHabilidades();
    }
}
