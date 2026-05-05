using System.Collections.Generic;
using UnityEngine;

public enum TaskExitReason
{
    Completed,
    BlockedByHigherPriority,
    Replanned
}

public struct RobotTaskContextNew
{
    public RobotRole Role;
    public RobotTask CurrentTask;
    public object Payload;
    public BrainOption Options;
    public RobotHeartNew Heart;
    public RobotBodyController Body;
    public RobotMemoryNew Memory;
}

public interface IRobotTaskNew
{
    void Enter(RobotTaskContextNew context);
    void Exit(RobotTaskContextNew context, TaskExitReason reason);
}

/// <summary>
/// Runtime minimal des tasks pour le nouveau pipeline event-driven.
/// </summary>
public class RobotTaskNew : IRobotTaskNew
{
    private const float FleeDistance = 14f;
    private const float FleeFallbackSeconds = 2.5f;

    public void Enter(RobotTaskContextNew context)
    {
        if (context.CurrentTask == null)
            return;

        var task = context.CurrentTask;

        // Les hooks d'execution restent volontairement legers pour l'instant.
        // Les integrations body/animation seront appelees ici selon le type de task.
        switch (task.Type)
        {
            case RobotTaskType.SearchForMachine:
                // Objectif: trouver une machine exploitable ET construire le path vers elle.
                // Roles cibles: Worker, Guard, Spawner (tout role qui doit rejoindre une machine).
                // 1) Lire les waypoints/machines disponibles.
                // 2) Choisir une destination selon le role et les priorites.
                // 3) Si aucune destination valide -> BlockCurrentTask().
                // 4) Construire le path vers la destination.
                // 5) Si path valide -> CompleteCurrentTask() (la suite attendue est GoToMachine).
                // 6) Si path introuvable/impossible -> BlockCurrentTask().
                HandleSearchForMachine(context);
                break;

            case RobotTaskType.ReactivateMachine:
                // Objectif: reactiver une machine (principalement SecurityGuard).
                // Hypothese: le robot est deja devant la machine (apres un GoToMachine).
                // 1) Resoudre la machine cible depuis le payload/contexte.
                // 2) Si machine null -> BlockCurrentTask() (etat anormal).
                // 3) Lire l'etat machine (ON/OFF/disponibilite).
                // 4) Si OFF -> tenter power on.
                // 5) Si succes (ou deja ON) -> notifier le changement (event/memory) puis CompleteCurrentTask().
                // 6) Si echec de reactivation -> BlockCurrentTask().
                HandleReactivateMachine(context);
                break;

            case RobotTaskType.WaitAtMachine:
                // 1) Arreter le mouvement
                // 2) Jouer anim idle
                // 3) Attendre timeout (task.ExpireAt ou config)
                // 4) CompleteCurrentTask()
                HandleWaitAtMachine(context);
                break;

            case RobotTaskType.GoToMachine:
                // Objectif: deplacement pur vers une destination (machine/waypoint).
                // 1) Lire la destination depuis le payload.
                // 2) Si destination absente -> BlockCurrentTask().
                // 3) Construire path + deleguer mouvement au BodyController.
                // 4) Si arrivee -> CompleteCurrentTask().
                // 5) Si path/evolution impossible -> BlockCurrentTask().
                HandleGoToMachine(context);
                break;

            case RobotTaskType.Rest:
                // Objectif: executer le repos sur machine rest.
                // Hypothese: le robot est deja en position de repos.
                // 1) Lire l'etat de la machine rest.
                // 2) Si machine OFF/invalide -> BlockCurrentTask().
                // 3) Si machine ON -> lancer repos (timeout local, anim, etc.).
                // 4) A la fin du timeout -> CompleteCurrentTask().
                HandleRest(context);
                break;

            case RobotTaskType.ReturnHome:
                // Objectif: revenir a la start room/home.
                // 1) Resoudre la destination home/start.
                // 2) Construire path + deleguer le mouvement.
                // 3) A l'arrivee, event possible de save/safe-state.
                // 4) CompleteCurrentTask().
                HandleReturnHome(context);
                break;

            case RobotTaskType.Investigate:
                // Version intermediaire simple.
                // 1) Comportement d'investigation minimal (animation/deplacement court).
                // 2) Timeout local.
                // 3) CompleteCurrentTask().
                HandleInvestigate(context);
                break;

            case RobotTaskType.Cower:
                // Version intermediaire simple.
                // 1) Arreter le mouvement
                // 2) Jouer anim peur/cower
                // 3) Rester passif x temps
                // 4) apres x temps -> CompleteCurrentTask()
                HandleCower(context);
                break;

            case RobotTaskType.WorkAtMachine:
                // Objectif: executer le cycle de travail (task principale Worker).
                // Hypothese: le robot est deja sur le poste de travail.
                // 1) Lire et verifier les infos machine (power/occupation/validite).
                // 2) Si machine invalide ou OFF -> BlockCurrentTask().
                // 3) Si machine valide -> executer cycle de travail local.
                // 4) Sur event machine (power off, worker switch, slot perdu) -> BlockCurrentTask().
                // 5) Fin de cycle normal -> CompleteCurrentTask().
                HandleWorkAtMachine(context);
                break;

            case RobotTaskType.GuardPost:
                // Objectif: tenir un poste de garde sur machine security.
                // 1) Lire et verifier les infos machine/poste.
                // 2) Si machine/poste OFF ou invalide -> BlockCurrentTask().
                // 3) Maintenir la posture de garde locale.
                // 4) Sur relai/switch/fin cycle -> CompleteCurrentTask() (ou Block selon cas).
                HandleGuardPost(context);
                break;

            case RobotTaskType.SpawnFollowers:
                // Objectif: comportement WorkerSpawner sur sa machine.
                // 1) Lire et verifier les infos machine de spawn.
                // 2) Si machine OFF/invalide -> BlockCurrentTask() (fallback externe probable: Idle).
                // 3) Si machine OK -> lancer la sequence de spawn locale.
                // 4) Ajouter un timeout de securite anti-blocage.
                // 5) Fin de sequence -> CompleteCurrentTask() (ou rester selon design final).
                HandleSpawnFollowers(context);
                break;

            case RobotTaskType.Patrol:
                // Objectif: patrouiller de machine en machine si aucun guard post dispo.
                // 1) Construire une route de patrouille (waypoints machines).
                // 2) Suivre la route waypoint par waypoint via le BodyController.
                // 3) Option: lever event quand une machine OFF est detectee a l'arrivee.
                // 4) Fin de boucle/timeout -> CompleteCurrentTask().
                HandlePatrol(context);
                break;

            case RobotTaskType.Idle:
                // Idle simple.
                // 1) Arreter le mouvement
                // 2) Anim idle
                // 3) Attendre reevaluation (event/timeout court)
                // 4) CompleteCurrentTask() optionnel selon policy Heart
                HandleIdle(context);
                break;

            case RobotTaskType.AttackTarget:
                // Objectif: attaque locale si la cible est deja engageable.
                // 1) Resoudre la cible depuis payload.
                // 2) Appeler le module d'attaque (AttackController).
                // 3) Executer la sequence d'attaque.
                // 4) Fin attaque / cible perdue / cible morte -> CompleteCurrentTask().
                HandleAttackTarget(context);
                break;

            case RobotTaskType.ChasePlayer:
                // Objectif: poursuite du joueur (construction path + mouvement).
                // 1) Determiner destination joueur (payload/last known).
                // 2) Construire path puis deleguer mouvement.
                // 3) Rafraichir la destination periodiquement.
                // 4) Sur timeout/perte cible/zone d'attaque atteinte -> CompleteCurrentTask().
                HandleChasePlayer(context);
                break;

            case RobotTaskType.Flee:
                // Version intermediaire simple.
                // 1) Activer un etat defensif local.
                // 2) Timeout court.
                // 3) CompleteCurrentTask().
                // Extension plus tard: vrai deplacement de fuite.
                HandleFlee(context);
                break;

            case RobotTaskType.Faint:
                // Objectif: etat KO coherent sur toutes les sous-parties du robot.
                // 1) Arreter mouvement + appliquer etat faint.
                // 2) Bloquer interactions.
                // 3) Attendre un event de reveil (ou timeout selon design).
                // 4) Sortie valide du faint -> CompleteCurrentTask().
                HandleFaint(context);
                break;

            case RobotTaskType.Dead:
                // Objectif: etat mort final.
                // 1) Arreter toutes les actions (mouvement/combat/spawn).
                // 2) Appliquer l'etat dead final.
                // 3) Declencher la sortie de map/despawn via pipeline dedie.
                // 4) Task terminale.
                HandleDead(context);
                break;

            default:
                // Type de task inconnu: no-op + warning
                Debug.LogWarning($"[{nameof(RobotTaskNew)}] Task inconnue: {task.Type}");
                break;
        }
    }

    public void Exit(RobotTaskContextNew context, TaskExitReason reason)
    {
        if (context.CurrentTask == null)
            return;

        switch (context.CurrentTask.Type)
        {
            case RobotTaskType.AttackTarget:
                context.Body?.AttackController?.StopAttacking();
                RobotNewTrace.Log(
                    context.Heart,
                    eventSource: "TaskNew.Exit.AttackTarget",
                    memoryDelta: "none",
                    brainOptions: context.Options,
                    plannedTask: context.CurrentTask,
                    heartCurrentTask: context.Heart != null ? context.Heart.CurrentTask : null,
                    taskSignal: "stop_attack reason=" + reason);
                break;
        }
    }

    private static void HandleSearchForMachine(RobotTaskContextNew context)
    {
        if (context.Memory == null || context.Body == null)
        {
            Block(context);
            return;
        }

        int robotId = ResolveRobotId(context);
        RoomWaypoint waypoint = FindBestWaypointForRole(context.Role, context.Memory.Snapshot, robotId);
        if (waypoint == null)
        {
            if (context.Role == RobotRole.Worker && TryQueueReturnHome(context))
                return;

            Block(context);
            return;
        }

        if (RobotNewPipelineRuntime.ShouldDriveGameplay)
            context.Body.SetDestination(waypoint, includeUnavailable: true);
        if (context.Heart != null)
        {
            if (IsBalancedMachineWaypointType(waypoint.type))
                RobotMachineDestinationBalancer.AssignDestination(robotId, waypoint);

            context.Heart.CompleteCurrentTask();
            context.Heart.QueueTask(new RobotTask(RobotTaskType.GoToMachine, waypoint));
            return;
        }

        Complete(context);
    }

    private static void HandleReactivateMachine(RobotTaskContextNew context)
    {
        BaseMachine machine = ResolveMachine(context.Payload);
        if (machine == null)
        {
            Block(context);
            return;
        }

        if (machine.IsOn)
        {
            CompleteReactivateMachineTask(context, machine);
            return;
        }

        if (RobotNewPipelineRuntime.ShouldDriveGameplay && !IsAtReactivateTarget(context, machine))
        {
            if (!TryMoveToPayload(context, machine, includeUnavailable: true))
            {
                Block(context);
                return;
            }

            RobotNewTrace.Log(
                context.Heart,
                eventSource: "TaskNew.ReactivateMachine.MoveToTarget",
                memoryDelta: "none",
                brainOptions: context.Options,
                plannedTask: context.CurrentTask,
                heartCurrentTask: context.Heart != null ? context.Heart.CurrentTask : null,
                taskSignal: "move_to_reactivate");
            return;
        }

        if (RobotNewPipelineRuntime.ShouldDriveGameplay)
            machine.PowerOn();

        CompleteReactivateMachineTask(context, machine);
    }

    private static void CompleteReactivateMachineTask(RobotTaskContextNew context, BaseMachine machine)
    {
        MachineType? nextDesiredMachineType = context.Role == RobotRole.SecurityGuard
            && machine != null
            && machine.Type != MachineType.SecurityMachine
            ? MachineType.SecurityMachine
            : null;

        Complete(context);

        if (context.Memory != null && machine != null)
        {
            Debug.Log(
                $"[RobotTaskNew] Reactivation task completed machine={machine.name} nextDesired={(nextDesiredMachineType.HasValue ? nextDesiredMachineType.Value.ToString() : "none")}",
                machine);
            context.Memory.NotifyReactivationCompleted(machine, nextDesiredMachineType);
        }
    }

    private static bool IsAtReactivateTarget(RobotTaskContextNew context, BaseMachine machine)
    {
        if (context.Body == null || machine == null)
            return false;

        RoomWaypoint waypoint = machine.GetComponent<RoomWaypoint>() ?? machine.GetComponentInParent<RoomWaypoint>();
        if (waypoint != null && ReferenceEquals(context.Body.CurrentTarget, waypoint) && context.Body.HasArrivedAtDestination())
            return true;

        return Vector2.Distance(context.Body.transform.position, machine.transform.position) <= 2f;
    }

    private static void HandleWaitAtMachine(RobotTaskContextNew context)
    {
        context.Body?.StopMovement();
        ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: 1f);
    }

    private static void HandleGoToMachine(RobotTaskContextNew context)
    {
        if (context.Memory != null)
            context.Memory.SetDesiredMachineType(ResolveDesiredMachineType(context.Payload));

        BaseMachine machine = ResolveMachine(context.Payload);
        if (machine != null && !machine.IsOn)
        {
            if (context.Memory != null)
            {
                RoomWaypoint waypoint = machine.GetComponent<RoomWaypoint>() ?? machine.GetComponentInParent<RoomWaypoint>();
                if (waypoint != null)
                    context.Memory.SetRoomWaypointAvailability(waypoint, false);
                context.Memory.ChangeConnectionToMachine(false);
            }

            Block(context);
            return;
        }

        if (!RobotNewPipelineRuntime.ShouldDriveGameplay)
        {
            ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: 0.1f);
            return;
        }

        if (!TryMoveToPayload(context, context.Payload, includeUnavailable: true))
        {
            Block(context);
            return;
        }

        // Le mouvement est asynchrone: la completion arrivera via le body/anim event.
    }

    private static void HandleRest(RobotTaskContextNew context)
    {
        BaseMachine machine = ResolveMachine(context.Payload);
        if (machine != null && !machine.IsOn)
        {
            Block(context);
            return;
        }

        context.Body?.StopMovement();
        if (ShouldWaitForWorkerMachineLifecycle(context))
            return;

        ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: 2f);
    }

    private static void HandleReturnHome(RobotTaskContextNew context)
    {
        object payload = context.Payload;
        if (payload == null && context.Body != null)
            payload = context.Body.StartPoint;

        if (!TryMoveToPayload(context, payload, includeUnavailable: true))
        {
            // Fallback: pas de payload, on ne bloque pas immédiatement pour éviter un hard-lock.
            ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: 0.5f);
        }
    }

    private static void HandleInvestigate(RobotTaskContextNew context)
    {
        ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: 1.5f);
    }

    private static void HandleCower(RobotTaskContextNew context)
    {
        context.Body?.StopMovement();
        ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: 1.5f);
    }

    private static void HandleWorkAtMachine(RobotTaskContextNew context)
    {
        BaseMachine machine = ResolveMachine(context.Payload);
        if (machine != null && !machine.IsOn)
        {
            if (context.Memory != null)
            {
                RoomWaypoint waypoint = machine.GetComponent<RoomWaypoint>();
                if (waypoint != null)
                    context.Memory.SetRoomWaypointAvailability(waypoint, false);
                context.Memory.ChangeConnectionToMachine(false);
            }

            Block(context);
            return;
        }

        if (ShouldWaitForWorkerMachineLifecycle(context))
        {
            context.Body?.StopMovement();
            return;
        }

        ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: 2f);
    }

    private static void HandleGuardPost(RobotTaskContextNew context)
    {
        BaseMachine machine = ResolveMachine(context.Payload);
        if (machine != null && !machine.IsOn)
        {
            Block(context);
            return;
        }

        ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: 2f);
    }

    private static void HandleSpawnFollowers(RobotTaskContextNew context)
    {
        BaseMachine machine = ResolveMachine(context.Payload);
        if (machine != null && !machine.IsOn)
        {
            Block(context);
            return;
        }

        ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: 3f);
    }

    private static void HandlePatrol(RobotTaskContextNew context)
    {
        object payload = context.Payload;
        if (context.Role == RobotRole.Boss && payload == null)
        {
            payload = FindRandomEndRoomWaypoint(context.Memory);
            if (payload != null && context.Heart != null)
            {
                Debug.Log($"[Boss] Queued concrete patrol target: {DescribePayload(payload)}.", context.Heart);
                context.Heart.QueueTask(new RobotTask(RobotTaskType.Patrol, payload));
                return;
            }
        }

        if (payload != null && TryMoveToPayload(context, payload, includeUnavailable: true))
        {
            RobotNewTrace.Log(
                context.Heart,
                eventSource: "TaskNew.BossPatrol",
                memoryDelta: "none",
                brainOptions: context.Options,
                plannedTask: context.CurrentTask,
                heartCurrentTask: context.Heart != null ? context.Heart.CurrentTask : null,
                taskSignal: "patrol_target=" + DescribePayload(payload));

            if (context.Role == RobotRole.Boss)
                Debug.Log($"[Boss] Patrol target set: {DescribePayload(payload)}.", context.Heart);

            if (context.Role == RobotRole.Boss)
                ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: 3f);
            return;
        }

        if (context.Role == RobotRole.Boss)
        {
            Debug.LogWarning("[Boss] No end-room patrol waypoint available; idling in place.", context.Heart);
            context.Body?.StopMovement();
        }

        ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: 2f);
    }

    private static void HandleIdle(RobotTaskContextNew context)
    {
        context.Body?.StopMovement();
        ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: 1f);
    }

    private static void HandleAttackTarget(RobotTaskContextNew context)
    {
        Transform target = context.Payload as Transform;
        bool started = false;
        context.Body?.StopMovement();

        if (RobotNewPipelineRuntime.ShouldDriveGameplay
            && context.Body != null
            && context.Body.AttackController != null
            && target != null)
        {
            started = context.Body.AttackController.TryStartAttack(target);
        }

        RobotNewTrace.Log(
            context.Heart,
            eventSource: "TaskNew.AttackTarget",
            memoryDelta: "none",
            brainOptions: context.Options,
            plannedTask: context.CurrentTask,
            heartCurrentTask: context.Heart != null ? context.Heart.CurrentTask : null,
            taskSignal: "attack_started=" + started
                + " hasBody=" + (context.Body != null)
                + " hasAttackController=" + (context.Body != null && context.Body.AttackController != null)
                + " payload=" + DescribePayload(context.Payload));

        ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: 1f);
    }

    private static void HandleChasePlayer(RobotTaskContextNew context)
    {
        if (!RobotNewPipelineRuntime.ShouldDriveGameplay)
        {
            ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: 0.25f);
            return;
        }

        if (context.Payload is RobotPlayerChaseTarget chaseTarget)
        {
            if (chaseTarget.Waypoint == null || context.Body == null)
            {
                Block(context);
                return;
            }

            Vector3? finalPosition = chaseTarget.HasPlayerPosition ? chaseTarget.PlayerPosition : null;
            context.Body.SetDestination(chaseTarget.Waypoint, finalPosition, includeUnavailable: true);
            RobotNewTrace.Log(
                context.Heart,
                eventSource: "TaskNew.ChasePlayer",
                memoryDelta: "none",
                brainOptions: context.Options,
                plannedTask: context.CurrentTask,
                heartCurrentTask: context.Heart != null ? context.Heart.CurrentTask : null,
                taskSignal: "targetWaypoint=" + DescribeWaypoint(chaseTarget.Waypoint)
                    + " hasFinalPosition=" + chaseTarget.HasPlayerPosition
                    + " finalPosition=" + (chaseTarget.HasPlayerPosition ? chaseTarget.PlayerPosition.ToString("F2") : "none"));
            return;
        }

        RobotNewTrace.Log(
            context.Heart,
            eventSource: "TaskNew.ChasePlayer",
            memoryDelta: "none",
            brainOptions: context.Options,
            plannedTask: context.CurrentTask,
            heartCurrentTask: context.Heart != null ? context.Heart.CurrentTask : null,
            taskSignal: "fallbackPayload=" + DescribePayload(context.Payload));

        if (!TryMoveToPayload(context, context.Payload, includeUnavailable: true))
        {
            if (context.Memory != null && context.Memory.Snapshot.HasLastKnownPlayerPosition)
                context.Body?.SetDestination(context.Memory.Snapshot.LastKnownPlayerPosition, includeUnavailable: true);
            else
                Block(context);
        }
    }

    private static void HandleFlee(RobotTaskContextNew context)
    {
        if (!RobotNewPipelineRuntime.ShouldDriveGameplay)
        {
            ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: FleeFallbackSeconds);
            return;
        }

        if (context.Body == null)
        {
            Block(context);
            return;
        }

        Vector3 robotPosition = context.Body.transform.position;
        Vector3 threatPosition;
        bool hasThreatPosition = TryGetThreatPosition(context, out threatPosition);

        if (!hasThreatPosition)
        {
            context.Body.StopMovement();
            RobotNewTrace.Log(
                context.Heart,
                eventSource: "TaskNew.Flee",
                memoryDelta: "none",
                brainOptions: context.Options,
                plannedTask: context.CurrentTask,
                heartCurrentTask: context.Heart != null ? context.Heart.CurrentTask : null,
                taskSignal: "fallback_stop no_threat_position");
            ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: FleeFallbackSeconds);
            return;
        }

        Vector3 away = robotPosition - threatPosition;
        away.y = 0f;
        away.z = 0f;
        if (away.sqrMagnitude < 0.01f)
            away = Vector3.right;

        Vector3 fleeDestination = robotPosition + away.normalized * FleeDistance;
        context.Body.SetDestination(fleeDestination, includeUnavailable: true);

        RobotNewTrace.Log(
            context.Heart,
            eventSource: "TaskNew.Flee",
            memoryDelta: "none",
            brainOptions: context.Options,
            plannedTask: context.CurrentTask,
            heartCurrentTask: context.Heart != null ? context.Heart.CurrentTask : null,
            taskSignal: "threat=" + threatPosition.ToString("F2")
                + " destination=" + fleeDestination.ToString("F2"));

        ScheduleOrCompleteByTaskExpiry(context, fallbackSeconds: FleeFallbackSeconds);
    }

    private static bool TryGetThreatPosition(RobotTaskContextNew context, out Vector3 threatPosition)
    {
        if (context.Memory != null)
        {
            var snapshot = context.Memory.Snapshot;
            if (snapshot.HasLastAttackPosition)
            {
                threatPosition = snapshot.LastAttackPosition;
                return true;
            }

            if (snapshot.HasLastKnownPlayerPosition)
            {
                threatPosition = snapshot.LastKnownPlayerPosition;
                return true;
            }
        }

        threatPosition = Vector3.zero;
        return false;
    }

    private static void HandleFaint(RobotTaskContextNew context)
    {
        context.Body?.StopMovement();
    }

    private static void HandleDead(RobotTaskContextNew context)
    {
        context.Body?.StopMovement();
    }

    private static void ScheduleOrCompleteByTaskExpiry(RobotTaskContextNew context, float fallbackSeconds)
    {
        float delay = fallbackSeconds;
        if (context.CurrentTask != null && context.CurrentTask.ExpireAt.HasValue)
            delay = Mathf.Max(0f, context.CurrentTask.ExpireAt.Value - Time.time);

        if (context.Heart != null)
            context.Heart.ScheduleCompleteCurrentTask(delay);
        else
            Complete(context);
    }

    private static bool ShouldWaitForWorkerMachineLifecycle(RobotTaskContextNew context)
    {
        if (context.CurrentTask == null)
            return false;

        if (context.CurrentTask.Type == RobotTaskType.WorkAtMachine)
            return context.Role == RobotRole.Worker;

        if (context.CurrentTask.Type == RobotTaskType.Rest)
            return context.Role == RobotRole.Worker || context.Role == RobotRole.SecurityGuard;

        return false;
    }

    private static bool TryMoveToPayload(RobotTaskContextNew context, object payload, bool includeUnavailable)
    {
        if (!RobotNewPipelineRuntime.ShouldDriveGameplay)
            return payload != null;

        if (context.Body == null || payload == null)
            return false;

        if (payload is RoomWaypoint waypoint && waypoint != null)
        {
            context.Body.SetDestination(waypoint, includeUnavailable);
            return true;
        }

        if (payload is BaseMachine machine && machine != null)
        {
            RoomWaypoint target = machine.GetComponent<RoomWaypoint>();
            if (target != null)
                context.Body.SetDestination(target, includeUnavailable);
            else
                context.Body.SetDestination(machine.transform.position, includeUnavailable);
            return true;
        }

        if (payload is Vector3 v3)
        {
            context.Body.SetDestination(v3, includeUnavailable);
            return true;
        }

        if (payload is Vector2 v2)
        {
            context.Body.SetDestination(v2, includeUnavailable);
            return true;
        }

        if (payload is Transform tr && tr != null)
        {
            context.Body.SetDestination(tr.position, includeUnavailable);
            return true;
        }

        if (payload is RobotPlayerChaseTarget chaseTarget)
        {
            if (chaseTarget.Waypoint == null)
                return false;

            Vector3? finalPosition = chaseTarget.HasPlayerPosition ? chaseTarget.PlayerPosition : null;
            context.Body.SetDestination(chaseTarget.Waypoint, finalPosition, includeUnavailable);
            return true;
        }

        return false;
    }

    private static bool TryQueueReturnHome(RobotTaskContextNew context)
    {
        RoomWaypoint startPoint = context.Body != null ? context.Body.StartPoint : null;
        if (context.Heart == null || startPoint == null)
            return false;

        RobotNewTrace.Log(
            context.Heart,
            eventSource: "TaskNew.SearchForMachineFallback",
            memoryDelta: "none",
            brainOptions: context.Options,
            plannedTask: new RobotTask(RobotTaskType.ReturnHome, startPoint),
            heartCurrentTask: context.Heart.CurrentTask,
            taskSignal: "return_home");
        context.Heart.QueueTask(new RobotTask(RobotTaskType.ReturnHome, startPoint));
        return true;
    }

    private static RoomWaypoint FindBestWaypointForRole(RobotRole role, RobotMemorySnapshotNew snapshot, int robotId)
    {
        switch (role)
        {
            case RobotRole.Worker:
                return FindByPriorityBalanced(snapshot, robotId, WaypointType.Work, WaypointType.Rest, WaypointType.Center);
            case RobotRole.SecurityGuard:
                return FindByPriorityBalanced(snapshot, robotId, WaypointType.Security, WaypointType.Rest, WaypointType.Center);
            case RobotRole.WorkerSpawner:
                return FindByPriority(snapshot, WaypointType.Spawner, WaypointType.Center);
            default:
                return FindByPriority(snapshot, WaypointType.Center, WaypointType.Work, WaypointType.Rest, WaypointType.Security);
        }
    }

    private static RoomWaypoint FindByPriorityBalanced(RobotMemorySnapshotNew snapshot, int robotId, params WaypointType[] orderedTypes)
    {
        if (snapshot.AllAvailableWaypoints == null || snapshot.AllAvailableWaypoints.Count == 0)
            return null;

        var candidates = new List<RoomWaypoint>();
        foreach (WaypointType type in orderedTypes)
        {
            candidates.Clear();
            foreach (var pair in snapshot.AllAvailableWaypoints)
            {
                if (pair.Key == null || !pair.Value)
                    continue;
                if (pair.Key.type != type)
                    continue;

                candidates.Add(pair.Key);
            }

            if (!IsBalancedMachineWaypointType(type))
            {
                if (candidates.Count > 0)
                    return candidates[0];
                continue;
            }

            RoomWaypoint selected = RobotMachineDestinationBalancer.SelectLeastTargeted(candidates, robotId);
            if (selected != null)
                return selected;
        }

        return null;
    }

    private static int ResolveRobotId(RobotTaskContextNew context)
    {
        if (context.Heart != null)
            return context.Heart.gameObject.GetInstanceID();

        if (context.Body != null)
            return context.Body.gameObject.GetInstanceID();

        return 0;
    }

    private static bool IsBalancedMachineWaypointType(WaypointType type)
    {
        return type == WaypointType.Work
            || type == WaypointType.Rest
            || type == WaypointType.Security;
    }

    private static RoomWaypoint FindByPriority(RobotMemorySnapshotNew snapshot, params WaypointType[] orderedTypes)
    {
        if (snapshot.AllAvailableWaypoints == null || snapshot.AllAvailableWaypoints.Count == 0)
            return null;

        foreach (WaypointType type in orderedTypes)
        {
            foreach (var pair in snapshot.AllAvailableWaypoints)
            {
                if (pair.Key == null || !pair.Value)
                    continue;

                if (pair.Key.type == type)
                    return pair.Key;
            }
        }

        return null;
    }

    private static RoomWaypoint FindRandomEndRoomWaypoint(RobotMemoryNew memory)
    {
        if (memory == null)
            return null;

        var waypoints = memory.Snapshot.AllAvailableWaypoints;
        if (waypoints == null || waypoints.Count == 0)
            return null;

        var candidates = new List<RoomWaypoint>();
        foreach (var pair in waypoints)
        {
            var waypoint = pair.Key;
            if (waypoint == null || waypoint.parentRoom == null || waypoint.parentRoom.roomProperties == null)
                continue;
            if (waypoint.parentRoom.roomProperties.usageType != UsageType.End)
                continue;
            if (waypoint.type != WaypointType.Center)
                continue;

            candidates.Add(waypoint);
        }

        if (candidates.Count == 0)
            return null;

        var lastVisited = memory.LastVisitedPoint;
        if (lastVisited != null && candidates.Count > 1)
            candidates.Remove(lastVisited);

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private static BaseMachine ResolveMachine(object payload)
    {
        if (payload is BaseMachine baseMachine)
            return baseMachine;

        if (payload is RoomWaypoint waypoint && waypoint != null)
            return waypoint.GetComponent<BaseMachine>() ?? waypoint.GetComponentInParent<BaseMachine>();

        if (payload is Component component && component != null)
            return component.GetComponent<BaseMachine>() ?? component.GetComponentInParent<BaseMachine>();

        if (payload is GameObject go && go != null)
            return go.GetComponent<BaseMachine>() ?? go.GetComponentInParent<BaseMachine>();

        return null;
    }

    private static string DescribePayload(object payload)
    {
        if (payload == null)
            return "null";
        if (payload is Transform transform && transform != null)
            return "Transform:" + transform.name;
        if (payload is RobotPlayerChaseTarget chaseTarget)
            return "PlayerChaseTarget:" + DescribeWaypoint(chaseTarget.Waypoint)
                + " final=" + (chaseTarget.HasPlayerPosition ? chaseTarget.PlayerPosition.ToString("F2") : "none");
        if (payload is Vector3 v3)
            return "Vector3:" + v3.ToString("F2");
        if (payload is Vector2 v2)
            return "Vector2:" + v2.ToString("F2");
        if (payload is Component component && component != null)
            return component.GetType().Name + ":" + component.name;
        if (payload is GameObject gameObject && gameObject != null)
            return "GameObject:" + gameObject.name;

        return payload.GetType().Name;
    }

    private static string DescribeWaypoint(RoomWaypoint waypoint)
    {
        return waypoint != null ? waypoint.type + "@" + waypoint.WorldPos.ToString("F2") : "null";
    }

    private static MachineType? ResolveDesiredMachineType(object payload)
    {
        BaseMachine machine = ResolveMachine(payload);
        if (machine != null)
            return machine.Type;

        if (payload is RoomWaypoint waypoint && waypoint != null)
            return MapWaypointToMachineType(waypoint.type);

        return null;
    }

    private static MachineType? MapWaypointToMachineType(WaypointType waypointType)
    {
        switch (waypointType)
        {
            case WaypointType.Work:
                return MachineType.WorkStation;
            case WaypointType.Rest:
                return MachineType.RestStation;
            case WaypointType.Security:
                return MachineType.SecurityMachine;
            case WaypointType.Spawner:
                return MachineType.SpawningMachine;
            default:
                return null;
        }
    }

    private static void Complete(RobotTaskContextNew context)
    {
        RobotNewTrace.Log(
            context.Heart,
            eventSource: "TaskNew.Complete",
            memoryDelta: "none",
            brainOptions: context.Options,
            plannedTask: context.CurrentTask,
            heartCurrentTask: context.Heart != null ? context.Heart.CurrentTask : null,
            taskSignal: "complete");
        context.Heart?.CompleteCurrentTask();
    }

    private static void Block(RobotTaskContextNew context)
    {
        RobotNewTrace.Log(
            context.Heart,
            eventSource: "TaskNew.Block",
            memoryDelta: "none",
            brainOptions: context.Options,
            plannedTask: context.CurrentTask,
            heartCurrentTask: context.Heart != null ? context.Heart.CurrentTask : null,
            taskSignal: "block");
        context.Heart?.BlockCurrentTask();
    }
}
