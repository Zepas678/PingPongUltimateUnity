/// <summary>
/// Representa un partido individual dentro del torneo.
/// Almacena los dos competidores enfrentados y, una vez jugado, el ganador.
/// </summary>
[System.Serializable]
public class TournamentMatch
{
    /// <summary>Primer competidor del partido.</summary>
    public Competidor jugadorA;

    /// <summary>Segundo competidor del partido.</summary>
    public Competidor jugadorB;

    /// <summary>Ganador del partido (null si aún no se ha jugado).</summary>
    public Competidor ganador;

    // -------------------------------------------------------
    /// <summary>
    /// Constructor para crear un enfrentamiento entre dos competidores.
    /// </summary>
    /// <param name="jugadorA">Primer competidor.</param>
    /// <param name="jugadorB">Segundo competidor.</param>
    public TournamentMatch(Competidor jugadorA, Competidor jugadorB)
    {
        this.jugadorA = jugadorA;
        this.jugadorB = jugadorB;
        this.ganador  = null;
    }

    // -------------------------------------------------------
    /// <summary>
    /// Indica si el partido ya se ha jugado (tiene un ganador asignado).
    /// </summary>
    public bool EstaJugado => ganador != null;

    // -------------------------------------------------------
    /// <summary>
    /// Representación en string para depuración.
    /// </summary>
    public override string ToString()
    {
        string resultado = EstaJugado ? $"{jugadorA?.nombre ?? "?"} vs {jugadorB?.nombre ?? "?"} → Gana: {ganador?.nombre ?? "?"}"
                                      : $"{jugadorA?.nombre ?? "?"} vs {jugadorB?.nombre ?? "?"} [Pendiente]";
        return resultado;
    }
}