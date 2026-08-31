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

    // ─── Evento para notificar a la UI ───
    /// <summary>Se dispara cuando se registra un resultado o cambia la ronda.</summary>
    public event System.Action OnTorneoActualizado;

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
        // 0. Limpiar estado del torneo anterior antes de generar uno nuevo
        if (Octavos != null) Octavos.Clear();
        if (Cuartos != null) Cuartos.Clear();
        if (Semifinales != null) Semifinales.Clear();
        Final = null;
        Campeon = null;
        RondaActual = Ronda.Octavos;

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

        // 4. Asignar skinID secuencial a cada CPU (0 a 14, en el orden que quedaron tras mezclar)
        for (int i = 1; i < Competidores.Count; i++)
        {
            Competidores[i].skinID = i - 1;
            Debug.Log($"[TournamentManager] CPU '{Competidores[i].nombre}' -> skinID={Competidores[i].skinID}");
        }

        // 5. Inicializar listas de partidos
        Octavos = new List<Match>(8);
        Cuartos = new List<Match>(4);
        Semifinales = new List<Match>(2);
        Final = null;

        // 6. Generar octavos
        RondaActual = Ronda.Octavos;
        GenerarOctavos();

        // Notificar a la UI
        OnTorneoActualizado?.Invoke();

        Debug.Log("[TournamentManager] Torneo creado. Bracket:");
        foreach (var c in Competidores)
        {
            Debug.Log($"  {c.nombre} | skinID: {c.skinID}");
        }
        Debug.Log($"[TournamentManager] Octavos generados: {Octavos.Count} partidos.");
    }

    // ─── MezclarCPUs ───
    /// <summary>
    /// Mezcla aleatoriamente los 15 CPUs (posiciones 1 a 15 de la lista).
    /// </summary>
    public void MezclarCPUs()
    {
        int inicio = 1;
        int cantidad = Competidores.Count - inicio;

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
    private void AsignarDificultadesPorRonda()
    {
        for (int i = 1; i < Competidores.Count; i++)
        {
            Competidores[i].dificultad = DificultadCPU.Inhumano;
        }

        Competidores[15].dificultad = DificultadCPU.Facil;
        Competidores[7].dificultad = DificultadCPU.Media;
        Competidores[8].dificultad = DificultadCPU.Media;
        Competidores[3].dificultad = DificultadCPU.Dificil;
        Competidores[4].dificultad = DificultadCPU.Dificil;
        Competidores[11].dificultad = DificultadCPU.Dificil;
        Competidores[12].dificultad = DificultadCPU.Dificil;
    }

    // ─── GenerarOctavos ───
    public void GenerarOctavos()
    {
        Octavos.Clear();

        Octavos.Add(new Match(Competidores[0], Competidores[15]));
        Octavos.Add(new Match(Competidores[7], Competidores[8]));
        Octavos.Add(new Match(Competidores[3], Competidores[12]));
        Octavos.Add(new Match(Competidores[4], Competidores[11]));
        Octavos.Add(new Match(Competidores[1], Competidores[14]));
        Octavos.Add(new Match(Competidores[6], Competidores[9]));
        Octavos.Add(new Match(Competidores[2], Competidores[13]));
        Octavos.Add(new Match(Competidores[5], Competidores[10]));

        Debug.Log("[TournamentManager] Octavos generados:");
        for (int i = 0; i < Octavos.Count; i++)
        {
            Debug.Log($"  Partido {i + 1}: {Octavos[i]}");
        }
    }

    // ─── RegistrarGanador ───
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

        SimularPartidosCPU();

        OnTorneoActualizado?.Invoke();

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

        OnTorneoActualizado?.Invoke();
    }

    // ─── SimularPartidosCPU ───
    private void SimularPartidosCPU()
    {
        List<Match> partidosRonda = ObtenerPartidosRondaActual();
        if (partidosRonda == null) return;

        foreach (var m in partidosRonda)
        {
            if (m == null || m.jugado) continue;
            if (m.jugadorA == null || m.jugadorB == null) continue;
            if (m.jugadorA.esJugador || m.jugadorB.esJugador) continue;

            m.ganador = Random.Range(0, 2) == 0 ? m.jugadorA : m.jugadorB;
            m.jugado = true;

            Debug.Log($"[TournamentManager] CPU simulado: {m}");
        }
    }

    // ─── Helpers ───
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

    public Ronda ObtenerRondaActual() => RondaActual;

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