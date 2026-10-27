# Documentación de Scripts - Ping Pong Ultimate Unity

Este documento recopila y describe **todos los scripts C# del proyecto**, organizados por categoría funcional. Cada entrada incluye un resumen de qué hace el script, sus responsabilidades principales, sus campos y métodos clave, y notas de implementación relevantes.

> **Nota:** Los scripts de ejemplo de TextMesh Pro (paquete estándar de Unity) se mencionan al final por completitud pero no se detallan, pues no forman parte de la lógica del juego.

---

## Índice

1. [Economía y Sistema de Tienda](#1-economía-y-sistema-de-tienda)
2. [Gameplay y Lógica de Partida](#2-gameplay-y-lógica-de-partida)
3. [Jugadores y Raquetas](#3-jugadores-y-raquetas)
4. [Skins y Personalización](#4-skins-y-personalización)
5. [Mapas y Escenarios](#5-mapas-y-escenarios)
6. [Sistema de Jefes (Bosses)](#6-sistema-de-jefes-bosses)
7. [Torneo](#7-torneo)
8. [Modo Práctica](#8-modo-práctica)
9. [Interfaz de Usuario (UI)](#9-interfaz-de-usuario-ui)
10. [Menú Principal y Navegación](#10-menú-principal-y-navegación)
11. [Efectos Visuales y Partículas](#11-efectos-visuales-y-partículas)
12. [Utilidades y Scripts Auxiliares](#12-utilidades-y-scripts-auxiliares)
13. [Scripts de Editor y Herramientas](#13-scripts-de-editor-y-herramientas)
14. [Scripts de TextMesh Pro (Ejemplos)](#14-scripts-de-textmesh-pro-ejemplos)

---

# 1. Economía y Sistema de Tienda

## EconomyManager.cs

**Ruta:** `Assets/Ping Pong/Scripts/EconomyManager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)  
**Clave persistente:** `POLLOCOINS_JUGADOR` (nueva) y `MONEDAS_JUGADOR` (legada, migración automática)

Gestiona toda la economía del juego (monedas "Pollocoins"). Persiste el saldo en PlayerPrefs con migración automática desde la clave legada. Expone eventos `OnPollocoinsChanged` y `OnCoinsChanged` para que la UI se actualice sola.

**Funcionamiento principal:**
- Al iniciar (`Awake`), migra el saldo legacy si es necesario y pinta el saldo en la tienda y el widget animado.
- `AgregarPollocoins` / `ConsumirPollocoins` actualizan PlayerPrefs y disparan eventos.
- Incluye notificación flotante `+X Pollocoins`, widget con conteo rápido (`easeOutCubic`) y slide in/out, y secuencia post-partida que muestra el widget, anima el conteo, espera ~3.5s y lo retrae (salvo que la tienda esté activa).
- Define recompensas configurables por dificultad CPU, ronda de torneo y jefe vencido.

**Campos principales:** `monedasIniciales`, `textoShopPollocoins`, `panelNotificacionPollocoins`, `widgetPollocoins`, `textoWidgetPollocoins`, `duracionConteoWidget`, `duracionVisibleWidget`, `widgetOffsetOcultoX`, recompensas CPU/torneo/jefes.

**Métodos principales:** `ObtenerPollocoins`, `AgregarPollocoins`, `ConsumirPollocoins`, `TieneSuficiente`, `MostrarNotificacionPollocoins`, `MostrarRecompensaPostPartida`, `OtorgarRecompensaCPU`, `OtorgarRecompensaTorneo`, `OtorgarRecompensaJefe`, `NotificarCambioPollocoins`, `MostrarWidgetPollocoins`, `OcultarWidgetPollocoins`.

---

## Enums.cs

**Ruta:** `Assets/Ping Pong/Scripts/Enums.cs`  
**Tipo:** `enum`

Define las enumeraciones compartidas del juego:

- `Dificultad`: `Facil`, `Media`, `Dificil`, `Inhumano`
- `ModoJuego`: `VsCPU`, `PlayerVsPlayer`

Usadas por gameplay, torneo y práctica.

---

# 2. Gameplay y Lógica de Partida

## GameManager.cs

**Ruta:** `Assets/Ping Pong/Scripts/Game play/GameManager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)  
**Estado:** Archivo vacío (0 líneas) — pendiente de implementación o lógica delegada a otros controladores.

Nota: actualmente no contiene código. Toda la lógica de partida está distribuida en `UIManager`, `PingPongBall`, `BossManager` y `TournamentManager`.

---

## PingPongBall.cs

**Ruta:** `Assets/Ping Pong/Scripts/Game play/PingPongBall.cs`  
**Tipo:** `MonoBehaviour` (`[RequireComponent(typeof(Rigidbody))]`)

Controla la pelota: física, rebotes, velocidad, detección de punto anotado y notificación a jefes activos.

**Flujo principal:**
1. **`Start()`**: Configura el `Rigidbody` (`ContinuousDynamic`, sin rotación, gravedad desactivada), obtiene `TrailRenderer` y `AudioSource`, carga clips por defecto y llama a `CongelarPelota()`.
2. **`ResetBall()`**: Congela la pelota, resetea `currentSpeed = initialSpeed`, borra `lastHitObject`, centra la pelota y tras 0.5s invoca `LaunchBall()`.
3. **`LaunchBall()`**: Activa física, elige dirección X (aleatoria o `nextServeDirectionX`), lanza con velocidad horizontal `currentSpeed` e impulso vertical `racketLaunchUpward`.
4. **`OnCollisionEnter()`**: Filtra por tag (`Racket`, `WallSide`, `Table`) y redirige al manejador correspondiente. Usa `lastHitObject` para evitar golpes dobles.
5. **`HandleRacketCollision()`**: Núcleo del juego. Captura `velocidadXAntesDelGolpe` (clave para `BossZeus`), suma velocidad, invierte dirección, calcula ángulo por impacto en Z, comprueba resistencia de la raqueta, reproduce sonido según velocidad, registra combo, notifica a jefe activo si la CPU golpeó, anima y aplica velocidad final.
6. **`NotificarGolpeJefeActivoCPU()`**: Detecta jefes activos (`enabled` + `combateActivo`) en orden: `BossZeus` → `BossColossus` → `BossMirage` → genérico. Para Zeus pasa `velocidadXAntesDelGolpe`.
7. **`FixedUpdate()`**: Mantiene magnitud horizontal cercana a `currentSpeed`, empuja hacia abajo si la pelota está muy alta, clampa velocidad en Y.

**Campos clave:** `initialSpeed`, `speedIncreasePerHit`, `maxSpeed`, `racketLaunchUpward`, `tableY`, `outOfBoundsX`, `shockwave`, `velocidadShockwave`, `bossZeusActivo`, `shieldMarkPrefab`.

**Métodos públicos:** `ResetBall()`, `CancelPendingLaunch()`, `CongelarPelota()`, `ForzarRespawn(bool playerScored)`.

---

# 3. Jugadores y Raquetas

## RaquetaControl.cs

**Ruta:** `Assets/Ping Pong/Scripts/Game play/RaquetaControl.cs`  
**Tipo:** `MonoBehaviour`

Controla la raqueta del jugador 1. Movimiento en eje Z mediante teclado (A/D o flechas) con tres posiciones (izquierda/centro/derecha) y animación de golpe.

**Campos principales:** `zIzquierda`, `zCentro`, `zDerecha`, `velocidad`, `destinoZ`, `animacion`, `golpe`.

**Métodos:** `OnMover(InputAction.CallbackContext)` — input del sistema de Input System de Unity.

---

## Raquetacontrolp2.cs (RaquetaControlP2.cs)

**Ruta:** `Assets/Ping Pong/Scripts/Game play/Raquetacontrolp2.cs`  
**Tipo:** `MonoBehaviour`

Controla la raqueta del jugador 2 en modo PvP local. Input directo por teclado (flechas izquierda/derecha/abajo para mover, `Keypad2` para golpear).

**Campos principales:** `zIzquierda`, `zCentro`, `zDerecha`, `velocidad`, `destinoZ`, `animacion`, `golpe`.

---

## CPUControl.cs

**Ruta:** `Assets/Ping Pong/Scripts/Game play/CPUControl.cs`  
**Tipo:** `MonoBehaviour`

Controla la raqueta de la CPU con 4 niveles de dificultad, sistema de fallos intencionales y cálculo de posición objetivo.

**Dificultades:**
- `Facil`: vel 300, fallo hasta 50%, golpe inicial 30%, golpe rápido 70%.
- `Dificil`: vel 450, fallo hasta 8%, golpe inicial 55%, golpe rápido 95%.
- `Inhumano`: vel 800, fallo 0%, golpe siempre.

**Lógica de golpe:**
- Desactiva modo saque cuando la pelota está cerca de la CPU.
- Durante saque o pelota yendo al jugador → centro, nunca falla.
- Calcula probabilidad de fallo según velocidad. Si falla, va a la zona opuesta.
- Golpea solo si la pelota viene hacia la CPU, está cerca (`< 8f`) y supera umbral de velocidad.
- En modo épico/ultra (110+ velocidad) siempre golpea.

**Métodos públicos:** `ActivarModoSaque()`, `SetDificultad(Dificultad)`.

---

## DificultadCPU.cs

**Ruta:** `Assets/Ping Pong/Scripts/Game play/DificultadCPU.cs`  
**Tipo:** `enum`

Define los niveles de dificultad para oponentes CPU en el torneo: `Facil`, `Media`, `Dificil`, `Inhumano`.

---

## RaquetaGolpe.cs

**Ruta:** `Assets/Ping Pong/Scripts/Game play/RaquetaGolpe.cs`  
**Tipo:** `MonoBehaviour`

Gestiona el golpe de la raqueta: instanciación/activación, destrucción y regeneración, aplicación de materiales (skins), cooldown entre golpes y lógica de tienda de skins (BUY/EQUIP).

**Flujo:**
- `Update()`: detecta `KeyCode.J` para golpe del jugador.
- `OnGolpe()`: activa modo golpe, agranda collider, anima inmediatamente.
- `PuedeResistir(float velocidadPelota)`: consulta si la raqueta resiste un impacto. Jugador necesita presionar J a tiempo; CPU necesita haber activado el golpe. Si la velocidad supera `velocidadDestruccion` y no resiste, se destruye.
- `Destruir()` (corrutina): colapso visual de la raqueta (escala a 0), desactiva renderer y collider.
- `Regenerar()` (corrutina): espera a que termine la destrucción, luego restaura escala, renderer, collider y flags. Invoca evento `onRegenerada`.
- `AplicarMaterial(Material)`: cambia el material de la raqueta sin modificar el prefab.

**Campos principales:** `velocidadDestruccion`, `ventanaTiempo`, `multiplicadorGolpe`, `extraTamanoCollider`, `esJugador`, `botonPresionado`, `destruida`, `golpeActivado`, `ocultarRendererBaseAlRegenerar`.

**Propiedades:** `EstaDestruida`, `BotonPresionado`.

---

## Combomanager.cs (ComboManager.cs)

**Ruta:** `Assets/Ping Pong/Scripts/Game play/Combomanager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Gestiona el sistema de combos (rachas de golpes consecutivos cuando la pelota supera 110 de velocidad). Combo separado para jugador y CPU.

**Métodos principales:** `RegistrarGolpe(bool esJugador, float velocidadPelota)`, `RomperComboPorPunto()`, `ResetSessionMax()`.

**Propiedades:** `CurrentComboJugador`, `MaxComboJugadorSession`.

---

## ColliderSeguidor.cs

**Ruta:** `Assets/Ping Pong/Scripts/Game play/ColliderSeguidor.cs`  
**Tipo:** `MonoBehaviour` (`[RequireComponent(typeof(Collider))]`)

Hace que un collider siga a otro objeto (`objetivo`). Usado para detectar colisiones específicas (zona de golpe). Expone `TriggerGolpe()` y `GetRaquetaGolpe()`.

---

## RaquetaAnimator.cs (clase RaquetaAnimacion)

**Ruta:** `Assets/Ping Pong/Scripts/Animations/RaquetaAnimator.cs`  
**Tipo:** `MonoBehaviour` (clase pública `RaquetaAnimacion`)

Controla la animación de golpe de la raqueta. Expone `AnimarGolpe()` y `AnimarMovimiento(int direccion)` usados por `GameManager`, `CPUControl` y `RaquetaGolpe`.

---

# 4. Skins y Personalización

## SkinManager.cs

**Ruta:** `Assets/Ping Pong/Scripts/SkinManager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Gestiona las skins de raqueta: carga, desbloqueo, equipado y persistencia en PlayerPrefs.

**Funcionamiento:**
- Mantiene lista de `skinsPrefabs` con sus materiales.
- Persiste skin equipada en `SKIN_RAQUETA_EQUIPADA`.
- Sistema de tienda: BUY (desbloquear con Pollocoins), EQUIP (equipar), EQUIPPED (ya equipado).
- `AplicarSkinEquipadaConLog()` asigna `material` y `sharedMaterial` al renderer destino.
- Evento estático `OnSkinEquipada(int)` para notificar cambios en tiempo real.
- `previewRotador` muestra el modelo 3D de la skin seleccionada.
- Sincroniza con `EconomyManager.OnPollocoinsChanged` para habilitar/deshabilitar botón de compra según saldo.

**Métodos principales:** `SeleccionarSkin(int)`, `OnClickAccionSkin()`, `EquiparSkinSeleccionada()`, `AplicarSkinEquipada(Renderer)`, `RestaurarSkinTienda()`, `GetSkinMaterial(int)`, `GetSkinPrefab(int)`.

---

# 5. Mapas y Escenarios

## Mapmanager.cs (MapManager.cs)

**Ruta:** `Assets/Ping Pong/Scripts/Mapmanager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Gestiona los mapas/escenarios del juego con soporte para tienda, desbloqueo por jefe y carrusel UI.

**Mapas configurados:** Habitación Vacía (0), Habitación de Javi (1), Mapa Infinito (2, jefe Mirage), Mapa Nube (3), Space (4), Hielo (5, jefe Zeus), Lava (6, jefe Colossus), Habitación Práctica (7).

**Sistema de desbloqueo:**
- `TipoDesbloqueoMapa`: `Pollocoins` o `Jefe`.
- `EstadoDesbloqueoMapa`: `Desbloqueado`, `BloqueadoPollocoins`, `BloqueadoJefe`, `PendienteReclamar`.
- Los mapas requeridos por jefe se desbloquean al derrotar al jefe correspondiente (`JEFE_DERROTADO_[id]`).

**Transform de mapas:**
- Cada mapa conserva su transform al instanciar. `AplicarTransformDeConfig()` aplica posición/rotación/escala de MUNDO (no local) para evitar desplazamientos por herencia del contenedor.
- El mapa Infinito (Mirage) tiene constantes forzadas: `POSICION_MAPA_INFINITO = (-20.3, 40, 163.4)`, `ESCALA_MAPA_INFINITO = (348, 94, 6)`.

**Carrusel UI:** tarjetas con escala dinámica (central grande, laterales pequeñas), flechas de navegación, candado y overlay oscuro para mapas bloqueados, texto informativo de razón de bloqueo.

**Métodos principales:** `SeleccionarMapaPorIndice(int)`, `PuedeJugarMapaEnfocado()`, `OnClickConfirmarOComprarMapa()`, `ActualizarBotonCompraMapa()`, `ActualizarBotonCompraTienda()`, `SeleccionarMapaPracticaLimpio()`, `ForzarTransformDelMapaInfinito()`.

---

# 6. Sistema de Jefes (Bosses)

## BossManager.cs

**Ruta:** `Assets/Ping Pong/Scripts/Bosses/BossManager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Coordinador del sistema de jefes: desbloqueo, selección, progresión y inicio de combates.

**Responsabilidades:**
- Lista de `BossData` configurable en Inspector (Zeus, Colossus, Mirage).
- UI del menú de jefes: nombre, título, descripción, dificultad, retrato, indicadores, estado de bloqueo, botón Combatir.
- `StartBossBattle()` inicia el combate contra el jefe seleccionado: selecciona el mapa, llama a `UIManager.IniciarPartida()`, activa el controlador del jefe específico y aplica su visual en la raqueta CPU.
- Flags de reentrada: `batallaIniciada`, `startBossBattleEnCurso`, `iniciandoCombate`, `combateIniciado`, `bloquearMenuJefes` — evitan bucles infinitos entre `StartBossBattle()` y `IniciarCombate()` de los jefes.
- `JefeActivo` (propiedad pública): `BossController` preparado para la batalla actual, respetado por `GameManager.DetenerJefeActivo()` para no cancelar el combate antes de iniciarse.

**Métodos principales:** `ActualizarUI()`, `EntrarModoHistoriaJefes()`, `SalirModoHistoriaJefes()`, `StartBossBattle()`, `NextBoss()`, `PreviousBoss()`, `RegistrarVictoriaJefe(BossData)`, `DesactivarOtrosJefes(BossData)`, `AplicarVisualJefeCPU(BossData, BossController)`.

---

## BossData.cs

**Ruta:** `Assets/Ping Pong/Scripts/Bosses/BossData.cs`  
**Tipo:** `Serializable` (clase de datos, no ScriptableObject a pesar del nombre)

Datos de cada boss: `id`, `nombre`, `titulo`, `descripcion`, `imagen` (Sprite), `colorTema`, `dificultad`, `desbloqueado`, `derrotado`, `nombreMapa`, `mapaAsociado` (`MapManager.ConfigMapa`).

---

## BossController.cs

**Ruta:** `Assets/Ping Pong/Scripts/Bosses/BossController.cs`  
**Tipo:** `MonoBehaviour` (clase base abstracta de hecho)

Clase base para todos los jefes. Contiene funcionalidad común de combate.

**Campos:** `bossData`, `combateActivo`, `derrotado`, `gameManager`, `ball`, `jugador`, `jefe`, `mesa`.

**Métodos virtuales:**
- `IniciarCombate()`: establece `combateActivo = true`, `derrotado = false`, selecciona mapa via `MapManager.SeleccionarMapaBoss(bossData)`.
- `FinalizarCombate()`: establece `combateActivo = false`.
- `ActivarHabilidad()`, `DesactivarHabilidad()`: sin implementación base.
- `OnPuntoDelJugador()`: reacciona cuando el jugador anota un punto.
- `PuedeResistir(float velocidadPelota)`: por defecto devuelve `true` (los jefes no se rompen por velocidad estándar).
- `ObtenerDatosMesa(out centro, out tamano)`: obtiene centro y tamaño reales de la mesa por tag "Mesa" o referencia `mesa`.

---

## BossZeus.cs

**Ruta:** `Assets/Ping Pong/Scripts/Bosses/BossZeus.cs`  
**Tipo:** `MonoBehaviour` (hereda de `BossController`)  
**Singleton:** `public static BossZeus Instance`

Jefe Zeus: controla los rayos y teletransporte de la pelota.

**Habilidad (Fase 3):**
- Detecta cuándo la pelota se dirige hacia el jugador y cruzó el centro de la mesa.
- Instancia un rayo (`efectoRayoPrefab` con `LineRenderer`) que sigue a la pelota durante un tiempo de carga (`tiempoCargaRayo`).
- Al completarse la carga, teletransporta la pelota delante del jugador sin modificar su velocidad.

**Flujo de activación (dos caminos):**
1. **Camino oficial (recomendado):** `PingPongBall` detecta golpe de CPU → `NotificarGolpeJefeActivoCPU()` → `BossZeus.ActivarHabilidadDesdeGolpeCPU(velocidadXAntesDelGolpe)`.
   - Si hay estrella esperando (`estrellaEsperandoGolpe`) y el golpe es válido → arranca `CoroutineSecuenciaTeletransporte()`.
   - Si no hay estrella → prepara Fase 1 (`ActivarHabilidad()`): crea estrella en posición calculada, espera segundo golpe de CPU.
2. **Camino alternativo:** `ActualizarRayo()` detecta carga completa y arranca la secuencia directamente.

**Corrutinas clave:**
- `InicializarDiferido()`: inicialización diferida en `Awake()` para no bloquear el hilo principal al pulsar "Combatir".
- `EsperarInicializacionYIniciarCombate()`: espera a que termine la inicialización diferida.
- `CoroutineSecuenciaTeletransporte(Vector3)`: muestra estrella, espera variable (1-1s según velocidad), teletransporta, instancia aparición, vuelve a iniciar rayo como destello.

**Métodos principales:** `IniciarCombate()`, `FinalizarCombate()`, `ActivarHabilidad()` (Fase 1), `ActivarHabilidadDesdeGolpeCPU(float)`, `IniciarRayo()`, `ActualizarRayo()`, `EjecutarTeletransporte()`, `FinalizarRayo()`, `CalcularPosicionTeletransporte()`, `ReproducirSonidoRayo()`.

**Robustez:** `Awake()` valida contexto de juego (`BossManager.Instance != null || escena.Contains("Boss")`) antes de registrar el Singleton. `OnDestroy()` limpia `Instance = null`.

---

## BossColossus.cs

**Ruta:** `Assets/Ping Pong/Scripts/Bosses/BossColossus.cs`  
**Tipo:** `MonoBehaviour` (hereda de `BossController`)  
**Singleton:** `public static BossColossus Instance`

Jefe Colossus: fortaleza, resistencia y onda de choque.

**Sistemas principales:**
1. **Modo Fortaleza:** entra por `duracionFortaleza` segundos, incrementa escala de raqueta y devuelve la pelota con impulso extra (`multiplicadorFuerzaGolpe`).
2. **Absorción e Impacto Pesado:** absorbe golpes épicos consumiendo cargas de inmunidad (`rebotesResistenciaRestantes`). Al agotarse, la raqueta puede romperse.
3. **Fases (1-11):** cada fase aumenta la tolerancia a golpes épicos en 1. La fase avanza al anotar puntos o al absorber el máximo de golpes épicos.
4. **Onda de Choque:** habilidad especial con cooldown (`cooldownOndaDeChoque`). Impulsa la pelota hacia el jugador con `multiplicadorImpulsoOnda` e instancia efecto visual en la mesa.
5. **Crecimiento progresivo:** la raqueta crece según puntos del jugador anotados contra Colossus (`escalaInicialRaqueta` → `escalaMaximaRaqueta`).
6. **Dificultad Inhumana:** se fuerza al iniciar el combate, haciendo que el CPU nunca falle.

**Métodos principales:** `IniciarCombate()`, `FinalizarCombate()`, `ActivarHabilidad()` (ondas de choque), `ActivarHabilidadDesdeGolpeCPU(float)`, `RegistrarGolpeEnRaqueta(float)`, `DispararOndaDeChoque()`, `AvanzarFase()`, `ReiniciarInmunidad()`, `RecalcularEscalaBasePorPuntaje()`, `ObtenerMultiplicadorImpulso()`.

**Robustez:** `Awake()` valida contexto de juego. `OnDestroy()` fusionado limpia `Instance = null` y destruye `instanciaSkinActual`.

---

## BossMirage.cs

**Ruta:** `Assets/Ping Pong/Scripts/Bosses/BossMirage.cs`  
**Tipo:** `MonoBehaviour` (hereda de `BossController`)  
**Singleton:** `public static BossMirage Instance`

Jefe Mirage: ilusiones y espejismos.

**Habilidad:**
- Genera clones de la pelota (`prefabPelotaFalsa`) con transparencia y velocidad desviada (`anguloAperturaClones`, `spreadLateralClones`, `spreadVerticalClones`).
- Los clones ignoran gravedad opcionalmente y colisiones con la pelota real y raquetas.
- Se destruyen tras `tiempoVidaClones` con efecto de humo (`prefabEfectoHumo`).
- Cooldown entre espejismos: `tiempoEntreEspejismos`.
- Fuerza dificultad CPU a `Dificultad.Inhumano` durante el combate.

**Métodos principales:** `IniciarCombate()`, `FinalizarCombate()`, `NotificarGolpeCPU()`, `ActivarHabilidad()`, `GenerarEspejismo()`, `LimpiarClones()`, `ForzarDificultadCPU()`.

**Robustez:** `Awake()` valida contexto de juego. `OnDestroy()` limpia clones e `Instance = null`.

---

## BossCardWatcher.cs

**Ruta:** `Assets/Ping Pong/Scripts/Bosses/BossCardWatcher.cs`  
**Tipo:** `MonoBehaviour`

Script de vigilancia para el GameObject "BossCard" (padre de BossName, BossSubtitle, BossDescription y BossImage). En `Update`, re-forza la visibilidad de la tarjeta y todos sus hijos (TMP_Text, Image, CanvasGroup) si algo los apaga. `OnDisable` imprime el stack trace de quién la desactivó.

---

## BossCardGuardian.cs

**Ruta:** `Assets/Ping Pong/Scripts/Bosses/BossCardGuardian.cs`  
**Tipo:** `MonoBehaviour`

Script similar a BossCardWatcher pero más agresivo: en `Update` y `LateUpdate` fuerza `gameObject.SetActive(true)` y activa todos los componentes TMP_Text e Image hijos. Llama a `Canvas.ForceUpdateCanvases()`.

---

# 7. Torneo

## TournamentManager.cs

**Ruta:** `Assets/Ping Pong/Scripts/torneo/TournamentManager.cs`  
**Tipo:** Clase de datos (no MonoBehaviour)

Administra un torneo de eliminación directa de 16 participantes (1 humano + 15 CPUs).

**Flujo:**
- `CrearNuevoTorneo()`: genera 16 competidores, mezcla los CPUs, asigna dificultades y skinIDs, genera octavos.
- `RegistrarGanador(Match, Competidor)`: registra el ganador de un partido, simula partidos de CPU pendientes y avanza de ronda si todos los partidos de la ronda actual están jugados.
- `AvanzarRonda()`: de Octavos → Cuartos → Semifinales → Final → Terminado.
- `SimularPartidosCPU()`: resuelve automáticamente partidos entre dos CPUs con resultado aleatorio.
- `ObtenerPartidoDelJugador()`: devuelve el partido pendiente que involucra al jugador humano.
- `ObtenerDificultadParaRondaActual()`: Facil (Octavos), Media (Cuartos), Dificil (Semifinal), Inhumano (Final).

**Evento:** `OnTorneoActualizado` para notificar a la UI.

**Campos:** `Competidores`, `Octavos`, `Cuartos`, `Semifinales`, `Final`, `RondaActual`, `Campeon`.

---

## TournamentUIManager.cs

**Ruta:** `Assets/Ping Pong/Scripts/torneo/TournamentUIManager.cs`  
**Tipo:** `MonoBehaviour`

Interfaz de usuario del torneo: bracket, rondas y botones.

**Responsabilidades:**
- Lee datos de `TournamentManager` y actualiza nombres en las llaves (octavos, cuartos, semifinal, final).
- `PartidoActualJugando` (estático): partido que se está jugando, usado por `GameManager`.
- `TorneoActual` (estático): referencia al `TournamentManager` activo.
- `OnClickJugar()`: guarda el partido actual, oculta panel del torneo, muestra HUD y llama a `GameManager.IniciarPartidaTorneo()`.
- `skinsCPU`: lista de 15 configuraciones de skin para cada CPU del torneo.

**Métodos principales:** `ActualizarBracket()`, `LimpiarBracket()`, `LlenarOctavos()`, `LlenarCuartos()`, `LlenarSemifinales()`, `LlenarFinal()`, `ActualizarBotonJugar()`, `OnClickJugar()`, `OnClickVolver()`.

---

## TournamentRound.cs

**Ruta:** `Assets/Ping Pong/Scripts/torneo/TournamentRound.cs`  
**Tipo:** `enum`

Enumeración de rondas del torneo: `Octavos`, `Cuartos`, `Semifinal`, `Final`, `Terminado`.

---

## TournamentMatch.cs

**Ruta:** `Assets/Ping Pong/Scripts/torneo/TournamentMatch.cs`  
**Tipo:** `Serializable` (clase de datos)

Representa un partido individual del torneo: `jugadorA`, `jugadorB`, `ganador`, `EstaJugado`.

---

## Competidor.cs

**Ruta:** `Assets/Ping Pong/Scripts/torneo/Competidor.cs`  
**Tipo:** `Serializable` (clase de datos)

Representa un competidor: `nombre`, `esJugador`, `dificultad` (`DificultadCPU`), `skinID` (-1 = por defecto).

---

# 8. Modo Práctica

## PracticeModeManager.cs

**Ruta:** `Assets/Ping Pong/Scripts/Practica/PracticeModeManager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Coordinador del modo práctica: sesiones de entrenamiento sin oponente.

**Responsabilidades:**
- Entra en modo práctica: restaura skin de tienda, selecciona mapa de práctica, desactiva CPU y controles P2, instancia muro de rebote, añade `PracticeBallListener` a la pelota.
- Configura física: gravedad configurable (`gravity`).
- Sliders de UI para velocidad inicial, máxima, gravedad y fuerza de raqueta.
- `ResetPracticeSession()`: reinicia la sesión completa.

**Métodos principales:** `EnterPracticeMode()`, `ResetPracticeSession()`, `SetWallRandomness(bool)`, `SetPlayerRacketForce(float)`, `SetBallInitialSpeed(float)`, `SetBallMaxSpeed(float)`, `CreatePracticeWall()`.

---

## PracticeUI.cs

**Ruta:** `Assets/Ping Pong/Scripts/Practica/PracticeUI.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Interfaz de usuario del modo práctica generada por código (Canvas, sliders, botones, textos informativos).

**Controles:** toggle con tecla `P`, sliders para velocidad inicial/máxima/gravedad/fuerza de raqueta, botones para lanzar pelota, reiniciar centro, detener/reanudar pelota, toggle de muro con aleatoriedad.

**Información en tiempo real:** velocidad actual, rebotes, combo máximo de la sesión.

**Métodos principales:** `Initialize(PracticeModeManager)`, `ShowPracticeUI()`, `HidePracticeUI()`, `LaunchBallNow()`.

---

## PracticeWall.cs

**Ruta:** `Assets/Ping Pong/Scripts/Practica/PracticeWall.cs`  
**Tipo:** `MonoBehaviour` (`[RequireComponent(typeof(Collider))]`)

Pared de rebote usada en modo práctica para devolver la pelota al jugador. Invierte X y Z de la velocidad al colisionar, con opción de aleatoriedad en Z (`randomBounce`).

---

## PracticeBallListener.cs

**Ruta:** `Assets/Ping Pong/Scripts/Practica/PracticeBallListener.cs`  
**Tipo:** `MonoBehaviour`

Escucha eventos de colisión de la pelota en modo práctica para contar rebotes y medir velocidad actual. Usado por `PracticeUI` para mostrar estadísticas.

**Métodos públicos:** `GetBounces()`, `GetCurrentSpeed()`, `ResetCounters()`.

---

# 9. Interfaz de Usuario (UI)

## UIManager.cs

**Ruta:** `Assets/Ping Pong/Scripts/Game play/UIManager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Gestor general de la UI durante la partida y menú.

**Paneles gestionados:** HUD, Menú Principal, Skins, Modos, Mapas, Dificultad, Pausa, Game Over, Torneo, Bosses, Help, Options.

**Responsabilidades:**
- `MostrarPanel(GameObject)`: activa un panel y desactiva todos los demás. Gestiona visibilidad del widget de Pollocoins y sincronización con `BossManager`.
- `IniciarPartida(Dificultad)`: inicia partida VS CPU con la dificultad indicada, bloquea mapa si está bloqueado.
- `MostrarGameOver(bool, bool, int)`: muestra panel de victoria/derrota con marcador final, recompensa de Pollocoins y botones según modo (normal/torneo).
- `ActualizarMarcador(int, int)`: actualiza textos de puntuación.
- `MostrarToast(bool fueJugador)`: toast animado "¡JUGADOR ANOTA!" / "¡CPU ANOTA!".
- Gestión de música de fondo (reproducción aleatoria, fundido entrada/salida).
- Sonidos UI (click, hover, abrir/cerrar menú).
- Control de volumen master, música y SFX.
- Navegación entre modos: VS CPU, PvP, Torneo, Práctica, Jefes.

**Métodos principales:** `MostrarPanel()`, `IniciarPartida(Dificultad)`, `OnClickPlay()`, `OnClickBosses()`, `OnClickModoVsCPU()`, `OnClickModoPvP()`, `OnClickModoTorneo()`, `OnClickModoPractica()`, `OnClickContinuarTorneo()`, `OnClickNuevoTorneo()`, `MostrarGameOver()`, `ActualizarMarcador()`, `MostrarToast()`, `CambiarVolumenMaster/Musica/SFX()`.

---

# 10. Menú Principal y Navegación

## MenuTabManager.cs

**Ruta:** `Assets/Ping Pong/Scripts/MenuTabManager.cs`  
**Tipo:** `MonoBehaviour`

Gestiona las pestañas del menú principal (Skins, Mapas, Habilidades reservado) dentro del Panel_Skins. Muestra/oculta secciones y la preview 3D de skin.

**Métodos:** `MostrarSkins()`, `MostrarMapas()`, `MostrarHabilidades()`.

---

## MainMenuBackgroundManager.cs

**Ruta:** `Assets/Ping Pong/Scripts/MainMenuBackgroundManager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Fondo dinámico del menú: partido CPU vs CPU en cámara cinemática con mapa aleatorio.

**Responsabilidades:**
- `EntrarAlMenuPrincipal()`: activa fondo demo, desactiva jefes/habilidades, activa demo CPU vs CPU.
- `SalirDelDemoHaciaPartida()`: sale del demo hacia cualquier partida real, reactiva cámaras gameplay y controles.
- `DesactivarJefesYHabilidades()` / `RestaurarJefesYHabilidades()`: apaga/reactiva controladores de jefes durante el demo.
- `ActivarDemoCPU()`: configura las raquetas como CPUs con `DemoCpuPiloto`.
- `PausarFondoSubmenu()` / `ReanudarFondoSubmenu()`: congela/reanuda simulación al abrir submenús.
- `LimpiarModeloJefeEnMenu()`: destruye modelos visuales de jefes en raquetas al volver al menú.

**Clase interna:** `DemoCpuPiloto` — piloto CPU temporal para la pala del jugador durante el demo.

---

## PreviewRotador.cs

**Ruta:** `Assets/Ping Pong/Scripts/PreviewRotador.cs`  
**Tipo:** `MonoBehaviour`

Rotador de preview 3D para skins/mapas en el menú. Usa `Time.unscaledDeltaTime` para seguir girando incluso con `Time.timeScale = 0` (tiendas/pausas).

**Método:** `MostrarPrefab(GameObject)` — instancia el prefab frente a la cámara de preview, desactiva componentes innecesarios.

---

# 11. Efectos Visuales y Partículas

## Shockwaveeffect.cs

**Ruta:** `Assets/Ping Pong/Scripts/Shockwaveeffect.cs`  
**Tipo:** `MonoBehaviour` (`[RequireComponent(typeof(SpriteRenderer))]`)

Efecto de onda expansiva que aparece cuando la pelota supera la velocidad de "barrera del sonido". Se expande desde escala 0.1 a `escalaMaxima` durante `duracion` segundos y se desvanece.

**Método:** `Reproducir(Vector3 posicion)`.

---

## ShieldMark.cs

**Ruta:** `Assets/Ping Pong/Scripts/ShieldMark.cs`  
**Tipo:** `MonoBehaviour` (código comentado)

Script comentado para marca visual de escudo sobre una raqueta. Incluye corrutina de desvanecimiento y destrucción.

---

## LowPolyWater.cs

**Ruta:** `Assets/Ping Pong/Scripts/LowPolyWater.cs`  
**Tipo:** `MonoBehaviour` (namespace `LowPolyWater`)

Efecto de agua low-poly para escenarios. Reorganiza vértices del mesh para crear triángulos individuales y anima ondas mediante seno en `Update()`.

**Campos:** `waveHeight`, `waveFrequency`, `waveLength`, `waveOriginPosition`.

---

## Plexus.cs

**Ruta:** `Assets/Ping Pong/Texturas/Shaders/Plexus/Plexus.cs`  
**Tipo:** `MonoBehaviour`

Efecto visual de red/plexus: puntos que se mueven en un espacio 3D y se conectan con líneas si están dentro de `maxConnDistance`. Usa `ComputeShader` para mover partículas y `Graphics.DrawMesh` para renderizar líneas.

**Licencia:** Atribution 3.0 (Creative Commons).

---

## U10PS (SnowOverTime, MouseRotation, ExampleReview, DissolveOverTime)

**Ruta:** `Assets/Ping Pong/Scripts/U10PS_*`  
**Tipo:** `MonoBehaviour`

Scripts de ejemplo de efectos:

- `U10PS_SnowOverTime`: animación de nieve sobre material mediante `_SnowAmount` (seno).
- `U10PS_MouseRotation`: rotación de objeto con el mouse, con sensibilidad y bloqueo de ejes opcional.
- `U10PS_ExampleReview`: navegación entre ejemplos con flechas izquierda/derecha, activa/desactiva grupos de objetos.
- `U10PS_DissolveOverTime`: efecto de disolución sobre material mediante `_Cutoff` (seno).

---

# 12. Utilidades y Scripts Auxiliares

## UIAnimationManager.cs

**Ruta:** `Assets/Ping Pong/Scripts/UIAnimationManager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Gestor de animaciones UI: `ScaleIn`, `ScaleOut`, `SlideFromRight`, `SlideToLeft`, `SlideFromBottom`, `Fade`. Usa corrutinas con `Time.unscaledDeltaTime` y curvas de easing (`EaseOutBack`, `EaseInCubic`, `EaseOutCubic`).

---

## Readme.cs (TutorialInfo)

**Ruta:** `Assets/TutorialInfo/Scripts/Readme.cs`  
**Tipo:** `ScriptableObject`

ScriptableObject de información/tutorial usado por Unity para mostrar el Readme del proyecto. Contiene `icon`, `title`, `sections[]` y `loadedLayout`.

---

## ReadmeEditor.cs (TutorialInfo/Editor)

**Ruta:** `Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs`  
**Tipo:** `Editor` (`CustomEditor(typeof(Readme))`)

Editor personalizado para `Readme`. Muestra icono, título, secciones con texto y enlaces, y botón "Remove Readme Assets". Selecciona automáticamente el Readme al abrir el proyecto.

---

# 13. Scripts de Editor y Herramientas

## GeneratePlane.cs

**Ruta:** `Assets/Ping Pong/Scripts/Editor/GeneratePlane.cs`  
**Tipo:** Editor tool (`ScriptableWizard`, namespace `LowPolyWater`)

Herramienta de editor para generar planos (meshes de plano) con segmentos configurables, ancho/alto, collider opcional y material. Guarda los meshes generados como assets en `Assets/Low Poly Water/Plane Meshes/`. Añade componente `LowPolyWater` al objeto creado.

**Menú:** `GameObject/LowPoly Water/Generate Water Plane...`

---

# 14. Scripts de TextMesh Pro (Ejemplos)

Los siguientes scripts pertenecen al paquete de ejemplos estándar de TextMesh Pro y no forman parte de la lógica del juego. Se listan por completitud:

- `Benchmark01.cs`, `Benchmark01_UGUI.cs`, `Benchmark02.cs`, `Benchmark03.cs`, `Benchmark04.cs`
- `ChatController.cs`, `DropdownSample.cs`, `EnvMapAnimator.cs`, `ObjectSpin.cs`, `ShaderPropAnimator.cs`
- `SimpleScript.cs`, `SkewTextExample.cs`, `TeleType.cs`, `TextConsoleSimulator.cs`, `TextMeshProFloatingText.cs`
- `TextMeshSpawner.cs`, `TMP_DigitValidator.cs`, `TMP_ExampleScript_01.cs`, `TMP_FrameRateCounter.cs`
- `TMP_PhoneNumberValidator.cs`, `TMP_TextEventCheck.cs`, `TMP_TextInfoDebugTool.cs`, `TMP_UiFrameRateCounter.cs`
- `VertexColorCycler.cs`, `VertexJitter.cs`, `VertexShakeA.cs`, `VertexShakeB.cs`, `VertexZoom.cs`
- `WarpTextExample.cs`
- `TMP_TextSelector_A.cs`, `TMP_TextSelector_B.cs`, `TMP_TextEventHandler.cs`, `TMP_TextEventCheck.cs`

Ruta: `Assets/TextMesh Pro/Examples & Extras/Scripts/`

---

## Resumen de arquitectura

| Capa | Scripts clave |
| :-- | :-- |
| **Economía** | `EconomyManager` |
| **Core Gameplay** | `PingPongBall`, `GameManager` (vacío), `UIManager`, `ComboManager` |
| **Jugadores** | `RaquetaControl`, `RaquetaControlP2`, `CPUControl`, `RaquetaGolpe`, `ColliderSeguidor`, `RaquetaAnimator` |
| **Jefes** | `BossManager`, `BossController`, `BossData`, `BossZeus`, `BossColossus`, `BossMirage`, `BossCardWatcher`, `BossCardGuardian` |
| **Torneo** | `TournamentManager`, `TournamentUIManager`, `TournamentMatch`, `TournamentRound`, `Competidor`, `Match` |
| **Práctica** | `PracticeModeManager`, `PracticeUI`, `PracticeWall`, `PracticeBallListener` |
| **Personalización** | `SkinManager`, `MapManager`, `PreviewRotador`, `MenuTabManager` |
| **UI/Efectos** | `UIManager`, `UIAnimationManager`, `ShockwaveEffect`, `LowPolyWater`, `Plexus`, `MainMenuBackgroundManager` |
| **Editor/Tutorial** | `GeneratePlane`, `Readme`, `ReadmeEditor` |

---

*Fin de la documentación.*
