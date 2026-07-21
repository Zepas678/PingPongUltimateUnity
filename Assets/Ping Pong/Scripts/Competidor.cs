/// <summary>
/// Representa un competidor dentro del torneo.
/// </summary>
[System.Serializable]
public class Competidor
{
    public string nombre;
    public bool esJugador;
    public DificultadCPU dificultad;

    public Competidor(string nombre, bool esJugador, DificultadCPU dificultad = DificultadCPU.Facil)
    {
        this.nombre = nombre;
        this.esJugador = esJugador;
        this.dificultad = dificultad;
    }

    public override string ToString()
    {
        return string.IsNullOrEmpty(nombre) ? "Sin nombre" : nombre;
    }
}