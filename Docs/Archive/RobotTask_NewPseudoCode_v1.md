# RobotTask v1 - Event-Driven, Simple et Stable

Objectif: architecture claire ou
- `Memory` stocke les faits,
- `Brain` planifie l'action concrete,
- `Heart` orchestre la pile,
- `Task` execute uniquement l'action physique.

Pas de logique de recherche strategique dans les tasks.

## 1) Responsabilites (regle principale)

## Memory
- Source de verite des faits runtime.
- Emet des events (`OnChanged`) quand un fait change.

## Brain
- Lit snapshot Memory + role.
- Construit `BrainOption`.
- Traduit les options en action concrete (`RobotTaskType` + payload).
- Emet une demande vers Heart seulement si changement utile.
- Dans `*New`, il n'y a pas de couche `intent` intermediaire.

## Heart
- Possede la `TaskStack`.
- Push/pop selon les demandes du Brain.
- Garde la task par defaut quand la pile se vide.
- Quand le top change, lance l'execution de la nouvelle task.
- Dans `*New`, la pile est LIFO simple (`RobotTaskStackNew`) sans table de priorites.

## Task
- Execute une action physique (move/work/attack/anim).
- Ne fait pas de planification.
- Ne modifie pas Memory directement.

## 2) Separation des cas `NeedMachine` vs `MachineUnavailable`

- `NeedMachine`: robot non connecte a une machine.
- `MachineUnavailable`: aucune machine/waypoint valide actuellement.

Ces flags servent au Brain pour choisir la bonne action concrete.

Exemple mapping Brain:
- `NeedMachine && !MachineUnavailable` -> `GoToMachine` (payload = waypoint cible)
- `NeedMachine && MachineUnavailable` -> `Wait` ou `GoToStartRoom` (selon role/politique)
- `!NeedMachine` -> `WorkAtMachine`

Selection waypoint role-aware (dans Brain):
- Worker: `Work -> Rest -> Center(start room)`
- SecurityGuard: `Security -> Work/Rest -> Center`
- WorkerSpawner: `Spawner d'origine -> Spawner`
- Follower: pas de waypoint machine; priorite a `LastKnownPlayerPosition`

Note `RobotTaskType` dans le flux `*New`:
- utilises: `GoToMachine`, `WorkAtMachine`, `Rest`, `Flee`, `AttackTarget`, `ChasePlayer`, `Faint`, `Dead`, `Idle`, `SpawnFollowers`, `Patrol`.
- non utilises (logique deplanifiee par Brain): `SearchForMachine`, `ReactivateMachine`, `WaitAtMachine`.

Regle pratique:
- `BuildTaskFromOptions` retourne une seule task concrete selon `role + flags + snapshot`.
- Le Brain evite de repush la meme task (`IsSameTask`).
- Le Heart n'interprete pas la logique metier du Brain; il applique la pile.

## 3) Contrat task minimal

```csharp
public interface IRobotTask
{
    void Enter(RobotTaskContext context);
    void Exit(TaskExitReason reason);
}
```

```csharp
public enum TaskExitReason
{
    Completed,
    BlockedByHigherPriority,
    Replanned
}
```

```csharp
public readonly struct RobotTaskContext
{
    public RobotRole Role { get; init; }
    public RobotTask CurrentTask { get; init; }
    public object Payload { get; init; }
}
```

Note:
- Ici le contexte task est volontairement simple.
- Le Brain a deja decide la logique; la task recoit une action pre-resolue.
- `RobotTaskType` reste dans `RobotTask`, pas duplique dans l'interface + contexte.
- `Exit` ne veut pas dire "task terminee". C'est un hook de sortie pour stop/cleanup.
- La completion reelle vient de `OnTaskCompleted`.

## 4) Cycle event-driven (sans polling de decision)

1. Monde change -> `Memory.OnChanged`.
2. `Brain.OnMemoryChanged` recalcule options.
3. Si options/action changent, Brain envoie une demande a Heart.
4. Heart met a jour la pile.
5. Si top task change, Heart fait `Exit(old, reason)` puis `Enter(new)`.
6. Body/animation signale `OnTaskCompleted` / `OnTaskBlocked`.
7. Heart reagit (pop/push) et active la suivante.

Pas de boucle `Update()` pour recalculer la decision globale.

## 5) Pseudo-code Brain

```csharp
private BrainOption lastOptions;
private RobotTask lastPlannedTask;

private void OnMemoryChanged(MemoryChangeEvent e)
{
    var options = BuildOptions(e.Snapshot);
    if (options != lastOptions)
    {
        lastOptions = options;
        UpdateBrainOption?.Invoke(options);
    }

    var nextTask = BuildTaskFromOptions(options, e.Snapshot, role);
    if (!IsSameTask(nextTask, lastPlannedTask))
    {
        lastPlannedTask = nextTask;
        UpdatePlannedTask?.Invoke(nextTask);
    }
}
```

```csharp
private static RoomWaypoint FindBestWaypointForRole(RobotRole role, RobotMemorySnapshotNew snapshot)
{
    switch (role)
    {
        case RobotRole.Worker:
            return FindByPriority(snapshot, WaypointType.Work, WaypointType.Rest, WaypointType.Center);
        case RobotRole.SecurityGuard:
            return FindByPriority(snapshot, WaypointType.Security, WaypointType.Work, WaypointType.Rest, WaypointType.Center);
        case RobotRole.WorkerSpawner:
            return FindByPriority(snapshot, WaypointType.Spawner);
        default:
            return null;
    }
}
```

## 6) Pseudo-code Heart

```csharp
private void OnPlannedTask(RobotTask planned)
{
    if (planned == null)
        return;

    // Brain pousse seulement si different; Heart applique une pile LIFO.
    taskStack.PushOrRefresh(planned);
    StartTopTaskIfChanged();
}

private void OnTaskCompleted()
{
    taskStack.CompleteCurrent();
    if (taskStack.Current == null)
        taskStack.PushOrRefresh(BuildDefaultTaskForRole(role));

    StartTopTaskIfChanged();
}

private void OnTaskBlocked()
{
    // point debug important:
    // la task reste en top, Heart attend une nouvelle planification Brain/Memory.
}
```

`PushOrRefresh` (`RobotTaskStackNew`):
- si la meme task existe deja, elle est remontee au top (refresh).
- sinon, la task est push au top.
- profondeur cible de pile: `5` (suffisant pour les chains reelles de routine + interruption).
- `StartTopTaskIfChanged()` garantit que seule la task top est active.

```csharp
private RobotTask activeTopTask;

private void StartTopTaskIfChanged()
{
    var newTop = taskStack.Current;
    if (IsSameTask(activeTopTask, newTop))
        return;

    if (activeTopTask != null)
    {
        var reason = newTop == null
            ? TaskExitReason.Completed
            : TaskExitReason.Replanned;
        taskRuntime.Exit(reason);
    }

    activeTopTask = newTop;
    if (activeTopTask == null)
        return;

    var context = new RobotTaskContext
    {
        Role = role,
        CurrentTask = activeTopTask,
        Payload = activeTopTask.Payload
    };

    taskRuntime.Enter(context);
}
```

## 7) Execution de la premiere task

- Au `Start`/`OnEnable`, Heart pousse la task par defaut du role.
- Heart active immediatement cette task (`Enter`).
- Ensuite, seuls les events changent la pile.

## 8) Exemple Worker Spawner (simple)

- Default task: `WorkAtMachine` (ou `SpawnFollowers` selon design exact role).
- Si machine OFF -> Brain planifie `Faint` (ou `Idle` selon regle).
- Si attaque mortelle -> Brain planifie `Dead`.
- `Dead` est une task push en top de pile (pas de purge globale automatique).

Le spawner ne fait pas de checks lourds en continu.

## 9) Definition de done v1

1. Aucune logique de replanification globale en `Update`.
2. Les tasks ne font que l'execution physique.
3. La recherche/comprehension reste dans Brain.
4. `NeedMachine` et `MachineUnavailable` sont traites distinctement.
5. La task par defaut est restauree quand la pile se vide.
