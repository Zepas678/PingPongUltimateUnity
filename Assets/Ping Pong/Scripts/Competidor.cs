/// <summary>
/// Representa un competidor dentro del torneo.
/// </summary>
[System.Serializable]
public class Competidor
{
    public string nombre;
    public bool esJugador;
    public DificultadCPU dificultad;
    /// <summary>Índice de skin (-1 = skin por defecto / sin asignar).</summary>
    public int skinID = -1;

    public Competidor(string nombre, bool esJugador, DificultadCPU dificultad = DificultadCPU.Facil)
    {
        this.nombre = nombre;
        this.esJugador = esJugador;
        this.dificultad = dificultad;
        this.skinID = -1;
    }

    public override string ToString()
    {
        return string.IsNullOrEmpty(nombre) ? "Sin nombre" : nombre;
    }
}