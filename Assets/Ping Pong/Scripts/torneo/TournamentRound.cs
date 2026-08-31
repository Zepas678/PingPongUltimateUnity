/// <summary>
/// Enumeración que define las rondas de un torneo de eliminación directa.
/// </summary>
public enum TournamentRound
{
    /// <summary>Octavos de final (16 participantes → 8 partidos).</summary>
    Octavos,

    /// <summary>Cuartos de final (8 participantes → 4 partidos).</summary>
    Cuartos,

    /// <summary>Semifinal (4 participantes → 2 partidos).</summary>
    Semifinal,

    /// <summary>Final (2 participantes → 1 partido).</summary>
    Final,

    /// <summary>El torneo ha terminado y hay un campeón.</summary>
    Terminado
}