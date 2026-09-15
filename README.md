<div align="center">

# 🏓 Ping Pong Ultimate

### A 3D Table Tennis Experience Built with Unity

[![Unity](https://img.shields.io/badge/Unity-3D%20Game-000000?style=for-the-badge\&logo=unity)](https://unity.com/)
[![C%23](https://img.shields.io/badge/C%23-Game%20Logic-512BD4?style=for-the-badge\&logo=csharp\&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Version](https://img.shields.io/badge/Version-v0.7.0-blue?style=for-the-badge)](../../releases)
[![Status](https://img.shields.io/badge/Status-In%20Development-orange?style=for-the-badge)](#-project-status)

<br>

> **Ping Pong Ultimate** is a 3D table tennis game developed in Unity, featuring dynamic matches, CPU opponents, tournaments, unlockable content, skins, special abilities, and boss battles.

<br>

<!-- ADD YOUR MAIN GAMEPLAY IMAGE OR GIF HERE -->

<img width="1456" height="819" alt="preview" src="https://github.com/user-attachments/assets/2d13dce9-b4dc-403b-ad88-e5f97cf35bb2" />

<br>

[🎮 Features](#-features)
•
[📸 Screenshots](#-screenshots)
•
[⚙️ Installation](#️-installation)
•
[🏗️ Project Structure](#️-project-structure)
•
[🚀 Roadmap](#-roadmap)


</div>

---

# 📖 About the Project

**Ping Pong Ultimate** is a Unity-based 3D table tennis project focused on creating a more dynamic and arcade-style experience.

The project goes beyond a traditional ping pong match by introducing additional gameplay systems such as:

* 🤖 CPU opponents
* 🏆 Tournament progression
* 🎨 Character and CPU skins
* ⚡ Special abilities
* 👑 Boss battles
* 🎮 Dynamic gameplay mechanics

The goal is to combine classic table tennis gameplay with progression, unique opponents, visual effects, and special mechanics.
<img width="1402" height="1122" alt="ChatGPT Image 13 ago 2026, 01_18_39 p m" src="https://github.com/user-attachments/assets/f11ad4c2-4fc5-46e0-bd35-b5054246cd33" />

---

# ✨ Features

## 🏓 Dynamic Table Tennis Gameplay

Experience fast-paced 3D ping pong matches with physics-based ball movement and player interaction.

The gameplay system is designed around:

* Real-time ball physics
* Player vs CPU matches
* Dynamic ball trajectories
* Increasing gameplay speed
* Different match situations
<img width="1036" height="543" alt="image" src="https://github.com/user-attachments/assets/dd43270c-3a14-410f-bec0-a39d58db8f62" />

---

## 🤖 CPU Opponents

Face multiple CPU opponents with different configurations and visual appearances.

The project includes systems designed to support:

* Multiple CPU opponents
* Individual opponent configurations
* CPU skins
* Tournament opponents
* Different gameplay behaviors

<!-- ADD SCREENSHOT -->

<p align="center">
<img width="1033" height="490" alt="image" src="https://github.com/user-attachments/assets/e792b1ce-f931-4a79-9bf8-cba762563dee" />

</p>

---

## 🏆 Tournament System

Progress through a tournament system and face different opponents.

The tournament system manages the progression between matches and opponents.

### Tournament Features

* Opponent progression
* Tournament matches
* CPU selection
* Match flow management
* Player progression
<img width="1232" height="585" alt="image" src="https://github.com/user-attachments/assets/c267e6a6-7159-4123-ad6c-bdc55493129f" />

---

## 🎨 CPU Skins

CPU opponents can use different visual skins.

The skin system allows different CPU characters or appearances to be assigned and used during matches.

This helps create more visual variety between opponents and tournaments.

---

## ⚡ Special Abilities

The game includes special gameplay mechanics designed to make matches more dynamic.

Abilities can introduce:

* Visual effects
* Ball manipulation
* Special attacks
* Unique gameplay situations
* Character-specific mechanics

---

## 👑 Boss Battles

Face powerful boss opponents with unique abilities and gameplay behavior.

Bosses are designed to introduce mechanics beyond normal ping pong gameplay.

### Example Boss Systems

* Special attacks
* Ball teleportation
* Lightning effects
* Custom abilities
* Unique visual effects
<img width="940" height="457" alt="image" src="https://github.com/user-attachments/assets/c7287c51-4f38-4d4d-bc37-0b4ed902b075" />

---

# 🎮 Gameplay

The gameplay loop is designed around fast-paced matches between the player and CPU opponents.

```text
START MATCH
     │
     ▼
🏓 PLAY MATCH
     │
     ▼
🤖 FACE CPU OPPONENT
     │
     ▼
⚡ USE SPECIAL MECHANICS
     │
     ▼
🏆 WIN THE MATCH
     │
     ▼
➡️ ADVANCE THROUGH THE TOURNAMENT

```
<img width="1233" height="588" alt="image" src="https://github.com/user-attachments/assets/3f168842-2a92-4e81-8a46-5797a14b9c30" />
Boss encounters can introduce additional mechanics:

```text
BOSS ENCOUNTER
      │
      ▼
⚡ SPECIAL ABILITY ACTIVATED
      │
      ▼
✨ VISUAL EFFECT
      │
      ▼
🏓 BALL GAMEPLAY MECHANIC
      │
      ▼
🔥 CONTINUE THE MATCH
```
<img width="1236" height="590" alt="image" src="https://github.com/user-attachments/assets/54c3fd67-c5ed-47ee-8975-a2f34aac4d91" />

---

# 🛠️ Built With

The project is developed using:

| Technology   | Purpose                     |
| ------------ | --------------------------- |
| 🎮 Unity     | Game Engine                 |
| 💻 C#        | Gameplay and Game Systems   |
| 🎨 ShaderLab | Shader Development          |
| 🌌 HLSL      | Graphics and Visual Effects |
| 🔧 Git       | Version Control             |
| 🐙 GitHub    | Repository Hosting          |

The repository currently contains a project structure with Unity's `Assets`, `Packages`, and `ProjectSettings` directories. The codebase is primarily C#, with ShaderLab and HLSL also present.

---

# ⚙️ Installation

## 1. Clone the repository

```bash
git clone https://github.com/Zepas678/PingPongUltimateUnity.git
```

## 2. Open Unity Hub

Open **Unity Hub** and select:

```text
Add Project
```

## 3. Select the project folder

Choose the cloned folder:

```text
PingPongUltimateUnity
```

## 4. Open the project

Open the project using the Unity version configured for the project.

> It is recommended to check the Unity version configured in the project's settings before opening it.

## 5. Run the game

Open the desired scene and press:

```text
▶ Play
```

---

# 📁 Project Structure

```text
PingPongUltimateUnity
│
├── Assets/
│   ├── Scripts/
│   │   ├── Player/
│   │   ├── CPU/
│   │   ├── Bosses/
│   │   ├── Tournament/
│   │   └── Managers/
│   │
│   ├── Prefabs/
│   ├── Materials/
│   ├── Animations/
│   ├── Scenes/
│   ├── Shaders/
│   └── Resources/
│
├── Packages/
│
├── ProjectSettings/
│
└── README.md
```

> Some folders may vary depending on the current version of the project.

---

# ⚡ Main Systems

The project contains and/or is being developed around several gameplay systems.

```text
                    ┌──────────────────┐
                    │   Game Manager   │
                    └────────┬─────────┘
                             │
            ┌────────────────┼────────────────┐
            │                │                │
            ▼                ▼                ▼
      🏓 Match System   🏆 Tournament    🤖 CPU System
            │                │                │
            └────────────────┼────────────────┘
                             │
                             ▼
                    ⚡ Gameplay Systems
                             │
                  ┌──────────┼──────────┐
                  ▼          ▼          ▼
             🎨 Skins   👑 Bosses   ✨ Abilities
```

---

# 🚀 Roadmap

The project is currently under development.

### Gameplay

* [x] Basic ping pong gameplay
* [x] CPU opponents
* [x] Tournament system
* [x] CPU skins
* [x] Initial boss systems
* [x] Special ability systems

### In Progress

* [ ] More boss mechanics
* [ ] Additional CPU opponents
* [ ] More skins
* [ ] Gameplay balancing
* [ ] Visual effect improvements
* [ ] Match improvements

### Future Ideas

* [ ] Additional game modes
* [ ] More tournaments
* [ ] New bosses
* [ ] Unlockable content
* [ ] Improved AI behavior
* [ ] More special abilities
* [ ] Additional arenas
* [ ] Advanced progression system

---

# 📦 Releases

The project uses GitHub Releases to track different versions.

## Latest Release

### 🚀 v0.6.0

Recent development includes improvements related to:

* 🏆 Tournament systems
* 🎨 CPU skins
* 🤖 CPU opponent configuration

Check the **Releases** section of the repository for available versions and release notes.

---

# 🧪 Project Status

<div align="center">

![Development](https://img.shields.io/badge/Development-Active-success?style=for-the-badge)
![Unity](https://img.shields.io/badge/Engine-Unity-black?style=for-the-badge\&logo=unity)
![Platform](https://img.shields.io/badge/Platform-PC-blue?style=for-the-badge\&logo=windows)

</div>

**Ping Pong Ultimate is currently under active development.**

New systems, gameplay improvements, visual effects, bosses, and additional content may be added in future versions.

---

# 🤝 Contributing

Contributions, suggestions, bug reports, and ideas are welcome.

If you find a problem:

1. Check existing issues.
2. Create a new issue describing the problem.
3. Include screenshots or reproduction steps when possible.

For improvements:

1. Fork the repository.
2. Create a new branch.

```bash
git checkout -b feature/my-new-feature
```

3. Make your changes.
4. Commit your changes.

```bash
git commit -m "Add new feature"
```

5. Push your branch.

```bash
git push origin feature/my-new-feature
```

6. Open a Pull Request.

---

# 🐛 Bug Reports

When reporting a bug, please include:

* Unity version
* Operating system
* Steps to reproduce the issue
* Expected behavior
* Actual behavior
* Screenshots or videos if available

---

# 📜 License

This project currently does not specify a license.

If you plan to make the project available for others to use, modify, or redistribute, consider adding an appropriate license to the repository.

---

# 👤 Author

<div align="center">

## Zepas678

Creator and developer of **Ping Pong Ultimate**.

[![GitHub](https://img.shields.io/badge/GitHub-Zepas678-181717?style=for-the-badge\&logo=github)](https://github.com/Zepas678)

</div>

---

<div align="center">

### 🏓 Ping Pong Ultimate

**A Unity-powered 3D table tennis experience.**

Made with ❤️ using Unity and C#.

⭐ If you like the project, consider giving the repository a star.

</div>
