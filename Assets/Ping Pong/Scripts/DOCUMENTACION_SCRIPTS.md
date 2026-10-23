# Documentación de Scripts - Ping Pong Ultimate Unity

Este documento recopila y describe todos los scripts C# del proyecto, organizados por categoría funcional. Cada entrada incluye un resumen de qué hace el script, sus responsabilidades principales y sus métodos/campos clave. Los scripts de ejemplo de TextMesh Pro se documentan al final por separado, ya que pertenecen a paquetes de ejemplo estándar de Unity.

---

## Índice

1. [Economía y Sistema de Tienda](#economía-y-sistema-de-tienda)
2. [Gameplay y Lógica de Partida](#gameplay-y-lógica-de-partida)
3. [Jugadores y Raquetas](#jugadores-y-raquetas)
4. [Skins y Personalización](#skins-y-personalización)
5. [Mapas y Escenarios](#mapas-y-escenarios)
6. [Sistema de Jefes (Bosses)](#sistema-de-jefes-bosses)
7. [Torneo](#torneo)
8. [Modo Práctica](#modo-práctica)
9. [Interfaz de Usuario (UI)](#interfaz-de-usuario-ui)
10. [Menú Principal y Navegación](#menú-principal-y-navegación)
11. [Efectos Visuales y Partículas](#efectos-visuales-y-partículas)
12. [Utilidades y Scripts Auxiliares](#utilidades-y-scripts-auxiliares)
13. [Scripts de Editor y Herramientas](#scripts-de-editor-y-herramientas)
14. [Scripts de TextMesh Pro (Ejemplos)](#scripts-de-textmesh-pro-ejemplos)

---

# 1. Economía y Sistema de Tienda

## EconomyManager.cs
**Ruta:** `Assets/Ping Pong/Scripts/EconomyManager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Gestiona toda la economía del juego (monedas "Pollocoins"). Persiste el saldo en PlayerPrefs con migración automática desde la clave legada `MONEDAS_JUGADOR` a la nueva `POLLOCOINS_JUGADOR`. Expone eventos `OnPollocoinsChanged`/`OnCoinsChanged` para que la UI se actualice sola.

**Resumen de funcionamiento:** Al iniciar, migra el saldo legacy si es necesario y pinta el saldo en la tienda y el widget animado. Sumar/consumir monedas actualiza PlayerPrefs y dispara eventos. Incluye notificación flotante "+X Pollocoins", widget con conteo rápido (easeOutCubic) y slide in/out, y secuencia post-partida que muestra el widget, anima el conteo, espera ~3.5s y lo retrae (salvo que la tienda esté activa). También define recompensas configurables por dificultad CPU, ronda de torneo y jefe vencido.

**Campos principales:** `monedasIniciales`, `textoShopPollocoins`, `panelNotificacionPollocoins`, `widgetPollocoins`, `duracionConteoWidget`, `duracionVisibleWidget`, `widgetOffsetOcultoX`, recompensas CPU/torneo/jefes.

**Métodos principales:** `ObtenerPollocoins`, `AgregarPollocoins`, `ConsumirPollocoins`, `TieneSuficiente`, `MostrarNotificacionPollocoins`, `MostrarRecompensaPostPartida`, `OtorgarRecompensaCPU`, `OtorgarRecompensaTorneo`, `OtorgarRecompensaJefe`, `NotificarCambioPollocoins`, `MostrarWidgetPollocoins`, `OcultarWidgetPollocoins`.

## Enums.cs
**Ruta:** `Assets/Ping Pong/Scripts/Enums.cs`  
**Tipo:** `enum`

Define las enumeraciones compartidas del juego (p. ej. `Dificultad`: Facil, Media, Difícil, Inhumano) y otras constantes de estado usadas por gameplay, torneo y práctica.

---

# 2. Gameplay y Lógica de Partida

## GameManager.cs
**Ruta:** `Assets/Ping Pong/Scripts/Game play/GameManager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Controlador principal de la lógica de partida. Maneja puntuación, condiciones de victoria/derrota (puntos a ganar, ventaja de 2) y coordinación entre modos de juego.

**Resumen de funcionamiento:** Centraliza el flujo de una partida: instanciación de la pelota, configuración de raquetas y mapa, detección de punto anotado y fin de partida. Gestiona paneles (menú principal, mapas, dificultad, pausa, game over, torneo, jefes) y la navegación entre escenas/menús. Integra `EconomyManager` para recompensas post-partida y `MapManager`/`UIManager`. Soporta modos VS CPU, PvP local, torneo y práctica.

**Métodos principales:** `IniciarPartida(Dificultad)`, `IniciarPartidaPvP()`, `IniciarPartidaTorneo()`, `AsegurarGameplayCamera()`, `SeleccionarMapaPorIndice(int)`.

## PingPongBall.cs
**Ruta:** `Assets/Ping Pong/Scripts/Game play/PingPongBall.cs`  
**Tipo:** `MonoBehaviour`

Controla la pelota: física, rebotes, velocidad y detección de punto anotado. Notifica a `GameManager` cuando alguien anota.

---

# 3. Jugadores y Raquetas

## RaquetaControl.cs
**Ruta:** `Assets/Ping Pong/Scripts/Game play/RaquetaControl.cs`  
**Tipo:** `MonoBehaviour`

Controla la raqueta del jugador 1. Movimiento en eje Z mediante teclado (A/D o flechas izq/der) con tres posiciones (izquierda/centro/derecha) y animación de golpe.

**Campos principales:** `velocidad`, `zIzquierda`, `zCentro`, `zDerecha`.

## Raquetacontrolp2.cs
**Ruta:** `Assets/Ping Pong/Scripts/Game play/Raquetacontrolp2.cs`  
**Tipo:** `MonoBehaviour`

Controla la raqueta del jugador 2 en modo PvP local (NumPad2 para golpear, flechas para mover).

## CPUControl.cs
**Ruta:** `Assets/Ping Pong/Scripts/Game play/CPUControl.cs`  
**Tipo:** `MonoBehaviour`

Controla la raqueta de la CPU. Incluye 4 niveles de dificultad, sistema de fallos intencionales, cálculo de posición objetivo según trayectoria de la pelota y detección de cuándo golpear (por velocidad/distancia).

## DificultadCPU.cs
**Ruta:** `Assets/Ping Pong/Scripts/Game play/DificultadCPU.cs`  
**Tipo:** `enum`

Define los niveles de dificultad: `Facil`, `Media`, `Difícil`, `Inhumano`.

## RaquetaGolpe.cs
**Ruta:** `Assets/Ping Pong/Scripts/Game play/RaquetaGolpe.cs`  
**Tipo:** `MonoBehaviour`

Gestiona el golpe de la raqueta: instanciación/activación, destrucción y regeneración cuando es golpeada por pelotas rápidas, aplicación de materiales (skins), cooldown entre golpes y lógica de la tienda de skins (BUY/EQUIP).

**Métodos públicos:** `SeleccionarSkin(int)`, `SeleccionarSkinSeleccionada()`, `OnClickAccionSkin()`, `ActualizarBotonAccionSkin()`, `RestaurarSkinTienda()`.

## Combomanager.cs
**Ruta:** `Assets/Ping Pong/Scripts/Game play/Combomanager.cs`  
**Tipo:** `MonoBehaviour`

Gestiona el sistema de combos (rachas de golpes consecutivos) y su visualización.

## ColliderSeguidor.cs
**Ruta:** `Assets/Ping Pong/Scripts/Game play/ColliderSeguidor.cs`  
**Tipo:** `MonoBehaviour`

Hace que un collider siga a otro objeto, usado para detectar colisiones específicas (p. ej. zona de golpe).

## RaquetaAnimator.cs
**Ruta:** `Assets/Ping Pong/Scripts/Animations/RaquetaAnimator.cs`  
**Tipo:** `MonoBehaviour` (clase `RaquetaAnimacion`)

Controla la animación de golpe de la raqueta. Expone `AnimarGolpe()` usado por `GameManager` y `CPUControl`.

---

# 4. Skins y Personalización

## SkinManager.cs
**Ruta:** `Assets/Ping Pong/Scripts/SkinManager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Gestiona las skins de raqueta: carga, desbloqueo, equipado y persistencia.

**Resumen de funcionamiento:** Mantiene la lista de `SkinData` (nombre, precio, prefab, material), persiste desbloqueo y skin equipada en PlayerPrefs, aplica la skin equipada a las raquetas en escena y notifica cambios para actualizar la UI de la tienda.

---

# 5. Mapas y Escenarios

## MapManager.cs
**Ruta:** `Assets/Ping Pong/Scripts/Mapmanager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Gestiona los mapas/escenarios del juego.

**Resumen de funcionamiento:** Mantiene la lista de `ConfigMapa`, persiste el mapa seleccionado, aplica el mapa activo (materiales, iluminación, skybox) a la escena y sincroniza la preview con economía/UI.

---

# 6. Sistema de Jefes (Bosses)

## BossManager.cs
**Ruta:** `Assets/Ping Pong/Scripts/Bosses/BossManager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Coordinador del sistema de jefes: desbloqueo, selección y progresión de bosses.

## BossData.cs
**Ruta:** `Assets/Ping Pong/Scripts/Bosses/BossData.cs`  
**Tipo:** `ScriptableObject`

Datos de cada boss: nombre, retrato, dificultad, recompensas, prefab, etc.

## BossController.cs
**Ruta:** `Assets/Ping Pong/Scripts/Bosses/BossController.cs`  
**Tipo:** `MonoBehaviour`

Controlador base del boss en partida: IA, animaciones y comportamiento especial.

## BossZeus.cs
**Ruta:** `Assets/Ping Pong/Scripts/Bosses/BossZeus.cs`  
**Tipo:** `MonoBehaviour` (hereda de BossController)

Boss Zeus: habilidades eléctricas y patrones de ataque propios.

## BossColossus.cs
**Ruta:** `Assets/Ping Pong/Scripts/Bosses/BossColossus.cs`  
**Tipo:** `MonoBehaviour` (hereda de BossController)

Boss Colossus: comportamiento de boss gigante y pesado.

## BossMirage.cs
**Ruta:** `Assets/Ping Pong/Scripts/Bosses/BossMirage.cs`  
**Tipo:** `MonoBehaviour` (hereda de BossController)

Boss Mirage: ilusiones y teletransporte.

## BossCardWatcher.cs
**Ruta:** `Assets/Ping Pong/Scripts/Bosses/BossCardWatcher.cs`  
**Tipo:** `MonoBehaviour`

Observador de las cartas de boss en la UI; actualiza su estado visual (desbloqueado/bloqueado, seleccionado).

---

# 7. Torneo

## TournamentManager.cs
**Ruta:** `Assets/Ping Pong/Scripts/torneo/TournamentManager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Gestiona el flujo completo del torneo: brackets, rondas, resultados y recompensas.

## TournamentUIManager.cs
**Ruta:** `Assets/Ping Pong/Scripts/torneo/TournamentUIManager.cs`  
**Tipo:** `MonoBehaviour`

Interfaz de usuario del torneo: brackets, resultados y botones de acción.

## TournamentRound.cs
**Ruta:** `Assets/Ping Pong/Scripts/torneo/TournamentRound.cs`  
**Tipo:** clase de datos

Representa una ronda del torneo con sus partidos.

## TournamentMatch.cs
**Ruta:** `Assets/Ping Pong/Scripts/torneo/TournamentMatch.cs`  
**Tipo:** clase de datos

Representa un partido individual dentro del torneo.

## Competidor.cs
**Ruta:** `Assets/Ping Pong/Scripts/torneo/Competidor.cs`  
**Tipo:** clase de datos

Representa un competidor (jugador o CPU) en el torneo.

---

# 8. Modo Práctica

## PracticeModeManager.cs
**Ruta:** `Assets/Ping Pong/Scripts/Practica/PracticeModeManager.cs`  
**Tipo:** Singleton (`MonoBehaviour`)

Coordinador del modo práctica: sesiones de entrenamiento sin oponente.

## PracticeUI.cs
**Ruta:** `Assets/Ping Pong/Scripts/Practica/PracticeUI.cs`  
**Tipo:** `MonoBehaviour`

Interfaz de usuario del modo práctica (controles, contadores, botones).

## PracticeWall.cs
**Ruta:** `Assets/Ping Pong/Scripts/Practica/PracticeWall.cs`  
**Tipo:** `MonoBehaviour`

Pared de rebote usada en modo práctica para devolver la pelota.

## PracticeBallListener.cs
**Ruta:** `Assets/Ping Pong/Scripts/Practica/PracticeBallListener.cs`  
**Tipo:** `MonoBehaviour`

Escucha eventos de la pelota en modo práctica (golpes, fallos) para actualizar estadísticas.

---

# 9. Interfaz de Usuario (UI)

## UIManager.cs
**Ruta:** `Assets/Ping Pong/Scripts/Game play/UIManager.cs`  
**Tipo:** `MonoBehaviour`

Gestor general de la UI durante la partida: marcadores, paneles, botones y navegación. Expone `TiendaActiva()` usado por `EconomyManager` para decidir si retraer el widget.

---

# 10. Menú Principal y Navegación

## MenuTabManager.cs
**Ruta:** `Assets/Ping Pong/Scripts/MenuTabManager.cs`  
**Tipo:** `MonoBehaviour`

Gestiona las pestañas del menú principal (Jugar, Tienda, Skins, Mapas, etc.) y la navegación entre ellas.

## MainMenuBackgroundManager.cs
**Ruta:** `Assets/Ping Pong/Scripts/MainMenuBackgroundManager.cs`  
**Tipo:** `MonoBehaviour`

Administra el fondo animado del menú principal.

## PreviewRotador.cs
**Ruta:** `Assets/Ping Pong/Scripts/PreviewRotador.cs`  
**Tipo:** `MonoBehaviour`

Rotador de preview 3D para skins/mapas en el menú.

---

# 11. Efectos Visuales y Partículas

## Shockwaveeffect.cs
**Ruta:** `Assets/Ping Pong/Scripts/Shockwaveeffect.cs`  
**Tipo:** `MonoBehaviour`

Efecto de onda de choque visual al golpear la pelota.

## ShieldMark.cs
**Ruta:** `Assets/Ping Pong/Scripts/ShieldMark.cs`  
**Tipo:** `MonoBehaviour`

Marca visual de escudo sobre una raqueta.

## LowPolyWater.cs
**Ruta:** `Assets/Ping Pong/Scripts/LowPolyWater.cs`  
**Tipo:** `MonoBehaviour`

Efecto de agua low-poly para escenarios.

## Plexus.cs
**Ruta:** `Assets/Ping Pong/Texturas/Shaders/Plexus/Plexus.cs`  
**Tipo:** `MonoBehaviour`

Efecto visual de red/plexus (líneas conectando puntos).

## U10PS (SnowOverTime, MouseRotation, ExampleReview, DissolveOverTime)
**Ruta:** `Assets/Ping Pong/Scripts/U10PS_*`  
**Tipo:** `MonoBehaviour`

Scripts de ejemplo de efectos de partículas (nieve, rotación con mouse, review, disolución).

## RaquetaAnimator.cs
**Ruta:** `Assets/Ping Pong/Scripts/Animations/RaquetaAnimator.cs`  
**Tipo:** `MonoBehaviour` (clase `RaquetaAnimacion`)

Controla la animación de golpe de la raqueta. Expone `AnimarGolpe()` usado por `GameManager` y `CPUControl`.

---

# 12. Utilidades y Scripts Auxiliares

## GeneratePlane.cs (Editor)
**Ruta:** `Assets/Ping Pong/Scripts/Editor/GeneratePlane.cs`  
**Tipo:** Editor tool

Herramienta de editor para generar planos.

## Readme.cs (TutorialInfo)
**Ruta:** `Assets/TutorialInfo/Scripts/Readme.cs`  
**Tipo:** `ScriptableObject`

ScriptableObject de información/tutorial usado por Unity para mostrar el Readme del proyecto.

---

# 13. Scripts de Editor y Herramientas

- `GeneratePlane.cs` — Herramienta de editor para generar planos (documentado arriba).

---

# 14. Scripts de TextMesh Pro (Ejemplos)

Scripts estándar de ejemplo de TextMesh Pro (no forman parte de la lógica del juego). Se listan por completitud:

- `Benchmark01.cs`, `Benchmark01_UGUI.cs`, `Benchmark02.cs`, `Benchmark03.cs`, `Benchmark04.cs`
- `ChatController.cs`, `DropdownSample.cs`, `EnvMapAnimator.cs`, `ObjectSpin.cs`, `ShaderPropAnimator.cs`
- `SimpleScript.cs`, `SkewTextExample.cs`, `TeleType.cs`, `TextConsoleSimulator.cs`, `TextMeshProFloatingText.cs`
- `TextMeshSpawner.cs`, `TMP_DigitValidator.cs`, `TMP_ExampleScript_01.cs`, `TMP_FrameRateCounter.cs`
- `TMP_PhoneNumberValidator.cs`, `TMP_TextEventCheck.cs`, `TMP_TextInfoDebugTool.cs`, `TMP_UiFrameRateCounter.cs`
- `VertexColorCycler.cs`, `VertexJitter.cs`, `VertexShakeA.cs`, `VertexShakeB.cs`, `VertexZoom.cs`
- `WarpTextExample.cs`

Estos scripts pertenecen al paquete de ejemplos de TextMesh Pro y no requieren documentación funcional adicional.

---

*Fin de la documentación.*
