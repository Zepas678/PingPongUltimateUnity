using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Administra un torneo de eliminación directa de 16 participantes.
/// 1 jugador humano + 15 CPUs.
/// No depende de UI, TextMeshPro ni botones.
/// </summary>
public class TournamentManager
{
    // ─── Nombres de los 15 CPUs ───
    private static readonly string[] NombresCPU = new string[]
    {
        "Shadow", "Clavito", "Nova", "Vortex", "Titan",
        "Phantom", "Zero", "Eclipse", "El bot", "Storm",
        "Javi", "Pablo777", "Venom", "Frost", "Cyber"
    };

    // ─── Listas de partidos por ronda ───
    public List<Competidor> Competidores { get; private set; }
    public List<Match> Octavos { get; private set; }
    public List<Match> Cuartos { get; private set; }
    public List<Match> Semifinales { get; private set; }
    public Match Final { get; private set; }

    // ─── Estado interno ───
    public enum Ronda { Octavos, Cuartos, Semifinal, Final, Terminado }
    public Ronda RondaActual { get; private set; }
    public Competidor Campeon { get; private set; }

    // ─── Constructor ───
    public TournamentManager()
    {
        CrearNuevoTorneo();
    }

    // ─── CrearNuevoTorneo ───
    /// <summary>
    /// Crea un nuevo torneo: genera los 16 competidores (1 humano + 15 CPUs),
    /// mezcla los CPUs y genera los octavos.
    /// </summary>
    public void CrearNuevoTorneo()
    {
        // 1. Crear lista de 16 competidores
        Competidores = new List<Competidor>(16);

        // Jugador humano siempre en primer lugar
        Competidores.Add(new Competidor("Jugador", esJugador: true, DificultadCPU.Facil));

        // Crear los 15 CPUs
        for (int i = 0; i < NombresCPU.Length; i++)
        {
            Competidores.Add(new Competidor(NombresCPU[i], esJugador: false, DificultadCPU.Facil));
        }

        // 2. Mezclar los CPUs (posiciones 1 a 15)
        MezclarCPUs();

        // 3. Asignar dificultades por ronda a cada CPU
        AsignarDificultadesPorRonda();

        // 4. Inicializar listas de partidos
        Octavos = new List<Match>(8);
        Cuartos = new List<Match>(4);
        Semifinales = new List<Match>(2);
        Final = null;

        // 5. Generar octavos
        RondaActual = Ronda.Octavos;
        GenerarOctavos();

        Debug.Log("[TournamentManager] Torneo creado. Bracket:");
        foreach (var c in Competidores)
        {
            Debug.Log($"  {c.nombre} | Jugador: {c.esJugador} | Dificultad: {c.dificultad}");
        }
        Debug.Log($"[TournamentManager] Octavos generados: {Octavos.Count} partidos.");
    }

    // ─── MezclarCPUs ───
    /// <summary>
    /// Mezcla aleatoriamente los 15 CPUs (posiciones 1 a 15 de la lista).
    /// </summary>
    public void MezclarCPUs()
    {
        // Mezclar desde índice 1 hasta 15 (dejando al jugador en 0)
        int inicio = 1;
        int cantidad = Competidores.Count - inicio; // 15

        for (int i = 0; i < cantidad; i++)
        {
            int idxA = inicio + i;
            int idxB = inicio + Random.Range(i, cantidad);
            var temp = Competidores[idxA];
            Competidores[idxA] = Competidores[idxB];
            Competidores[idxB] = temp;
        }

        Debug.Log("[TournamentManager] CPUs mezclados.");
    }

    // ─── AsignarDificultadesPorRonda ───
    /// <summary>
    /// Asigna la dificultad a cada CPU según la ronda en la que potencialmente
    /// se enfrentarían al jugador en un bracket estándar.
    /// Octavos = Fácil, Cuartos = Media, Semifinal = Difícil, Final = Inhumano.
    /// </summary>
    private void AsignarDificultadesPorRonda()
    {
        // Bracket estándar con seeds 1-16.
        // Jugador es seed 1 (índice 0).
        // Tras mezclar, asignamos según la posición relativa en el bracket:
        //   - Oponente directo del jugador en octavos (seed 16, índice 15) -> Fácil
        //   - Potencial oponente en cuartos (semillas 8-9, índices 7-8) -> Media
        //   - Potencial oponente en semifinal (semillas 4-5, 12-13) -> Difícil
        //   - Resto (lado lejano del bracket) -> Inhumano

        // Por defecto todo el lado lejano es Inhumano
        for (int i = 1; i < Competidores.Count; i++)
        {
            Competidores[i].dificultad = DificultadCPU.Inhumano;
        }

        // Oponente directo del jugador en octavos (índice 15) -> Fácil
        Competidores[15].dificultad = DificultadCPU.Facil;

        // Potencial oponente en cuartos (índices 7 y 8) -> Media
        Competidores[7].dificultad = DificultadCPU.Media;
        Competidores[8].dificultad = DificultadCPU.Media;

        // Potencial oponente en semifinal (índices 3, 4, 11, 12) -> Difícil
        Competidores[3].dificultad = DificultadCPU.Dificil;
        Competidores[4].dificultad = DificultadCPU.Dificil;
        Competidores[11].dificultad = DificultadCPU.Dificil;
        Competidores[12].dificultad = DificultadCPU.Dificil;
    }

    // ─── GenerarOctavos ───
    /// <summary>
    /// Genera los 8 partidos de octavos emparejando 1vs16, 2vs15, etc.
    /// </summary>
    public void GenerarOctavos()
    {
        Octavos.Clear();

        // Bracket estándar: seed 1 vs seed 16, seed 2 vs seed 15, etc.
        Octavos.Add(new Match(Competidores[0], Competidores[15])); // Partido 1: Jugador vs seed 16
        Octavos.Add(new Match(Competidores[7], Competidores[8]));  // Partido 2: seed 8 vs seed 9
        Octavos.Add(new Match(Competidores[3], Competidores[12])); // Partido 3: seed 4 vs seed 13
        Octavos.Add(new Match(Competidores[4], Competidores[11])); // Partido 4: seed 5 vs seed 12
        Octavos.Add(new Match(Competidores[1], Competidores[14])); // Partido 5: seed 2 vs seed 15
        Octavos.Add(new Match(Competidores[6], Competidores[9]));  // Partido 6: seed 7 vs seed 10
        Octavos.Add(new Match(Competidores[2], Competidores[13])); // Partido 7: seed 3 vs seed 14
        Octavos.Add(new Match(Competidores[5], Competidores[10])); // Partido 8: seed 6 vs seed 11

        Debug.Log("[TournamentManager] Octavos generados:");
        for (int i = 0; i < Octavos.Count; i++)
        {
            Debug.Log($"  Partido {i + 1}: {Octavos[i]}");
        }
    }

    // ─── RegistrarGanador ───
    /// <summary>
    /// Registra al ganador de un partido específico.
    /// Si todos los partidos de la ronda actual están jugados, avanza automáticamente.
    /// </summary>
    public void RegistrarGanador(Match match, Competidor ganador)
    {
        if (match == null || ganador == null)
        {
            Debug.LogError("[TournamentManager] Match o ganador inválido.");
            return;
        }

        if (match.jugado)
        {
            Debug.LogWarning("[TournamentManager] Este partido ya fue jugado.");
            return;
        }

        if (ganador != match.jugadorA && ganador != match.jugadorB)
        {
            Debug.LogError($"[TournamentManager] '{ganador.nombre}' no es participante de este partido.");
            return;
        }

        match.ganador = ganador;
        match.jugado = true;

        Debug.Log($"[TournamentManager] Partido registrado: {match}");

        // Verificar si la ronda actual está completa y avanzar
        switch (RondaActual)
        {
            case Ronda.Octavos:
                if (Octavos.TrueForAll(m => m.jugado))
                    AvanzarRonda();
                break;

            case Ronda.Cuartos:
                if (Cuartos.TrueForAll(m => m.jugado))
                    AvanzarRonda();
                break;

            case Ronda.Semifinal:
                if (Semifinales.TrueForAll(m => m.jugado))
                    AvanzarRonda();
                break;

            case Ronda.Final:
                if (Final != null && Final.jugado)
                {
                    Campeon = Final.ganador;
                    RondaActual = Ronda.Terminado;
                    Debug.Log($"[TournamentManager] ¡CAMPEÓN DEL TORNEO: {Campeon.nombre}!");
                }
                break;
        }
    }

    // ─── AvanzarRonda ───
    /// <summary>
    /// Avanza a la siguiente ronda usando los ganadores de la ronda actual.
    /// </summary>
    public void AvanzarRonda()
    {
        switch (RondaActual)
        {
            case Ronda.Octavos:
                var ganadoresOctavos = ObtenerGanadores(Octavos);
                if (ganadoresOctavos.Count != 8) return;

                Cuartos.Clear();
                for (int i = 0; i < ganadoresOctavos.Count; i += 2)
                    Cuartos.Add(new Match(ganadoresOctavos[i], ganadoresOctavos[i + 1]));

                RondaActual = Ronda.Cuartos;
                Debug.Log("[TournamentManager] Avanzando a Cuartos de final.");
                break;

            case Ronda.Cuartos:
                var ganadoresCuartos = ObtenerGanadores(Cuartos);
                if (ganadoresCuartos.Count != 4) return;

                Semifinales.Clear();
                for (int i = 0; i < ganadoresCuartos.Count; i += 2)
                    Semifinales.Add(new Match(ganadoresCuartos[i], ganadoresCuartos[i + 1]));

                RondaActual = Ronda.Semifinal;
                Debug.Log("[TournamentManager] Avanzando a Semifinales.");
                break;

            case Ronda.Semifinal:
                var ganadoresSemis = ObtenerGanadores(Semifinales);
                if (ganadoresSemis.Count != 2) return;

                Final = new Match(ganadoresSemis[0], ganadoresSemis[1]);
                RondaActual = Ronda.Final;
                Debug.Log("[TournamentManager] Avanzando a la Final.");
                break;
        }
    }

    // ─── ObtenerGanadores (helper) ───
    private List<Competidor> ObtenerGanadores(List<Match> partidos)
    {
        var ganadores = new List<Competidor>();
        foreach (var m in partidos)
        {
            if (m.jugado && m.ganador != null)
                ganadores.Add(m.ganador);
        }
        return ganadores;
    }

    // ─── ObtenerRondaActual ───
    public Ronda ObtenerRondaActual()
    {
        return RondaActual;
    }

    // ─── ObtenerPartidoDelJugador ───
    /// <summary>
    /// Devuelve el partido de la ronda actual donde participa el jugador humano.
    /// Retorna null si no hay partido pendiente del jugador.
    /// </summary>
    public Match ObtenerPartidoDelJugador()
    {
        List<Match> partidosRonda = ObtenerPartidosRondaActual();
        if (partidosRonda == null) return null;

        foreach (var match in partidosRonda)
        {
            if (match != null && !match.jugado &&
                ((match.jugadorA != null && match.jugadorA.esJugador) ||
                 (match.jugadorB != null && match.jugadorB.esJugador)))
            {
                return match;
            }
        }

        return null;
    }

    // ─── ObtenerPartidosRondaActual ───
    /// <summary>
    /// Devuelve la lista de partidos de la ronda actual.
    /// </summary>
    public List<Match> ObtenerPartidosRondaActual()
    {
        switch (RondaActual)
        {
            case Ronda.Octavos:    return Octavos;
            case Ronda.Cuartos:    return Cuartos;
            case Ronda.Semifinal:  return Semifinales;
            case Ronda.Final:      return Final != null ? new List<Match> { Final } : null;
            default:               return null;
        }
    }

    // ─── ObtenerDificultadParaRondaActual ───
    /// <summary>
    /// Devuelve la dificultad que debería tener el CPU según la ronda actual.
    /// Octavos = Fácil, Cuartos = Media, Semifinal = Difícil, Final = Inhumano.
    /// </summary>
    public DificultadCPU ObtenerDificultadParaRondaActual()
    {
        switch (RondaActual)
        {
            case Ronda.Octavos:    return DificultadCPU.Facil;
            case Ronda.Cuartos:    return DificultadCPU.Media;
            case Ronda.Semifinal:  return DificultadCPU.Dificil;
            case Ronda.Final:      return DificultadCPU.Inhumano;
            default:               return DificultadCPU.Facil;
        }
    }

    // ─── ObtenerResumen ───
    /// <summary>
    /// Devuelve un resumen del estado actual del torneo.
    /// </summary>
    public string ObtenerResumen()
    {
        string res = $"=== TORNEO ===\nRonda: {RondaActual}\n";

        if (RondaActual == Ronda.Terminado)
        {
            res += $"Campeón: {Campeon?.nombre ?? "N/A"}";
            return res;
        }

        var partidos = ObtenerPartidosRondaActual();
        if (partidos != null)
        {
            res += $"Partidos: {partidos.Count}\n";
            foreach (var m in partidos)
            {
                res += $"  {m}\n";
            }
        }

        return res;
    }
}