using UnityEngine;

/// <summary>
/// Representa la información de un jefe dentro del sistema de jefes.
/// Configurable desde el Inspector a través de la lista de BossManager.
/// </summary>
[System.Serializable]
public class BossData
{
    [Header("Identificación")]
    [Tooltip("Identificador único del jefe.")]
    public int id;

    [Tooltip("Nombre del jefe.")]
    public string nombre;

    [Tooltip("Título o apodo del jefe.")]
    public string titulo;

    [Tooltip("Descripción o historia del jefe.")]
    public string descripcion;

    [Header("Apariencia")]
    [Tooltip("Imagen del jefe para la UI.")]
    public Sprite imagen;

    [Tooltip("Color de tema que identifica al jefe.")]
    public Color colorTema;

    [Header("Estado")]
    [Tooltip("Nivel de dificultad del jefe.")]
    public int dificultad;

    [Tooltip("Indica si el jefe está desbloqueado.")]
    public bool desbloqueado;

    [Tooltip("Indica si el jefe ya fue derrotado.")]
    public bool derrotado;

    [Header("Mapa")]
    [Tooltip("Nombre del mapa de combate de este jefe. Debe coincidir con un mapa configurado en MapManager.")]
    public string nombreMapa;

    [Tooltip("Configuración de mapa asociada (opcional). Si se asigna, StartBossBattle la usará en lugar de nombreMapa.")]
    public MapManager.ConfigMapa mapaAsociado;

    /// <summary>
    /// Constructor por defecto requerido por Unity para poder crear
    /// y editar elementos de la lista desde el Inspector.
    /// </summary>
    public BossData() { }

    public BossData(int id, string nombre, string titulo, string descripcion, Sprite imagen, Color colorTema, int dificultad, bool desbloqueado, bool derrotado)
    {
        this.id = id;
        this.nombre = nombre;
        this.titulo = titulo;
        this.descripcion = descripcion;
        this.imagen = imagen;
        this.colorTema = colorTema;
        this.dificultad = dificultad;
        this.desbloqueado = desbloqueado;
        this.derrotado = derrotado;
    }
}