/// <summary>
/// Representa un partido individual dentro del torneo.
/// </summary>
[System.Serializable]
public class Match
{
    public Competidor jugadorA;
    public Competidor jugadorB;
    public Competidor ganador;
    public bool jugado;

    public Match(Competidor jugadorA, Competidor jugadorB)
    {
        this.jugadorA = jugadorA;
        this.jugadorB = jugadorB;
        this.ganador = null;
        this.jugado = false;
    }

    public override string ToString()
    {
        string resultado = jugado ? $"{jugadorA?.nombre ?? "?"} vs {jugadorB?.nombre ?? "?"} -> Gana: {ganador?.nombre ?? "?"}"
                                  : $"{jugadorA?.nombre ?? "?"} vs {jugadorB?.nombre ?? "?"} [Pendiente]";
        return resultado;
    }
}