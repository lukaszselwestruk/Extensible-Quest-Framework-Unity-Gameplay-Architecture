# Extensible-Quest-Framework-Unity-Gameplay-Architecture

A modular Unity quest system built around polymorphic tasks, composable task groups, independent rewards, and event-driven progression.

## Features

- Polymorphic quest tasks
- Composable task groups
- Independent reward handlers
- Event-driven quest progression
- Save and load support
- Unity `ScriptableObject` configuration
- Multiplayer synchronization support

## Architecture

The system is built around a few core abstractions:

- `QuestManager` — controls the quest lifecycle
- `Quest` — defines quest data and progression
- `AQuestTask` — base class for different task types
- `QuestTaskGroup` — combines multiple tasks
- `AQuestReward` — base class for independent rewards
- `AQuestTargetFinder` — resolves quest-related targets

New task and reward types can be added without modifying the core quest flow.

## Technology

- Unity
- C#
- ScriptableObjects
- Mirror Networking
- Event-driven gameplay architecture
