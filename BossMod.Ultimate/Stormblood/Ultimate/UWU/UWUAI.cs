using BossMod.AI;  
using BossMod.Autorotation;  
using BossMod.Pathfinding;  
  
namespace BossMod.Stormblood.Ultimate.UWU;  
  
// note: this assumes the positions my static was using  
sealed class UWUAI(RotationModuleManager manager, Actor player) : AIRotationModule(manager, player)  
{  
    public enum Track { Movement }  
    public enum MovementStrategy { None, Pathfind, PathfindMeleeGreed, Explicit, ExplicitMelee, Prepull, DragToCenter }  
  
    public static RotationModuleDefinition Definition()  
    {  
        var res = new RotationModuleDefinition("AI Experiment", "Experimental encounter-specific rotation", "Encounter AI", "veyn", RotationModuleQuality.WIP, new(~1ul), 70, 1, RotationModuleOrder.Movement, typeof(UWU));  
        res.Define(Track.Movement).As<MovementStrategy>("Movement", "Movement")  
            .AddOption(MovementStrategy.None, "None", "No automatic movement")  
            .AddOption(MovementStrategy.Pathfind, "Pathfind", "Use standard pathfinding to move")  
            .AddOption(MovementStrategy.PathfindMeleeGreed, "PathfindMeleeGreed", "Melee greed: find closest safespot, then move to maxmelee closest to it")  
            .AddOption(MovementStrategy.Explicit, "Explicit", "Move to specific point", supportedTargets: ActionTargets.Area)  
            .AddOption(MovementStrategy.ExplicitMelee, "ExplicitMelee", "Move to the point in maxmelee that is closest to specific point", supportedTargets: ActionTargets.Area)  
            .AddOption(MovementStrategy.Prepull, "Prepull", "Pre-pull position: as close to the clock-spot as possible")  
            .AddOption(MovementStrategy.DragToCenter, "DragToCenter", "Drag boss to the arena center");  
        return res;  
    }  
  
    private readonly UWUConfig _config = Service.Config.Get<UWUConfig>();  
  
    public override void Execute(StrategyValues strategy, ref Actor? primaryTarget, float estimatedAnimLockDelay, bool isMoving)  
    {  
        if (Bossmods.ActiveModule is UWU module && module.Raid.FindSlot(Player.InstanceID) is var playerSlot && playerSlot >= 0)  
        {  
            SetForcedMovement(CalculateDestination(module, primaryTarget, strategy.Option(Track.Movement), Service.Config.Get<PartyRolesConfig>()[module.Raid.Members[playerSlot].ContentId]));  
        }  
    }  
  
    private WPos? CalculateDestination(UWU module, Actor? primaryTarget, StrategyValues.OptionRef strategy, PartyRolesConfig.Assignment assignment) => strategy.As<MovementStrategy>() switch  
    {  
        MovementStrategy.Pathfind => PathfindPosition(null),  
        MovementStrategy.PathfindMeleeGreed => PathfindPosition(ResolveTargetOverride(strategy.Value) ?? primaryTarget),  
        MovementStrategy.Explicit => ResolveTargetLocation(strategy.Value),  
        MovementStrategy.ExplicitMelee => ExplicitMeleePosition(ResolveTargetLocation(strategy.Value), ResolveTargetOverride(strategy.Value) ?? primaryTarget),  
        MovementStrategy.Prepull => PrepullPosition(module, assignment),  
        MovementStrategy.DragToCenter => DragToCenterPosition(module),  
        _ => null  
    };  
  
    private WPos PathfindPosition(Actor? meleeGreedTarget)  
    {  
        var res = NavigationDecision.Build(NavigationContext, World, Hints, Player, Speed());  
        return meleeGreedTarget != null && res.Destination != null ? ClosestInMelee(res.Destination.Value, meleeGreedTarget) : (res.Destination ?? Player.Position);  
    }  
  
    private WPos ExplicitMeleePosition(WPos ideal, Actor? target) => target != null ? ClosestInMelee(ideal, target) : ideal;  
  
    private WPos PrepullPosition(UWU module, PartyRolesConfig.Assignment assignment)  
    {  
        var safeRange = 12.5f;  
        var desiredRange = assignment is PartyRolesConfig.Assignment.MT or PartyRolesConfig.Assignment.OT or PartyRolesConfig.Assignment.M1 or PartyRolesConfig.Assignment.M2 ? 5 : 10;  
        var dir = _config.PrepullSpots[assignment]; // TODO: add this GroupAssignmentUnique to UWUConfig  
        if (dir < 0)  
            dir = 0;  
        var desiredPos = module.Center + desiredRange * (180 - 45 * dir).Degrees().ToDirection();  
        var off = desiredPos - module.PrimaryActor.Position;  
        var distSq = off.LengthSq();  
        if (distSq >= safeRange * safeRange)  
            return desiredPos;  
        off /= MathF.Sqrt(distSq);  
        return module.PrimaryActor.Position + off * safeRange;  
    }  
  
    private WPos DragToCenterPosition(UWU module)  
    {  
        if (module.PrimaryActor.Position.Z >= module.Center.Z - 1)  
            return module.Center - new WDir(0, 6);  
        var dragDistance = module.PrimaryActor.HitboxRadius + Player.HitboxRadius + 2.25f;  
        var meleeDistance = module.PrimaryActor.HitboxRadius + Player.HitboxRadius + 2.75f;  
        var dragDir = (module.Center - module.PrimaryActor.Position).Normalized();  
        var dragSpot = module.Center + dragDistance * dragDir;  
        var timeToMelee = ((dragSpot - module.PrimaryActor.Position).Length() - meleeDistance) / (Speed() + 8.5f);  
        return GCD > timeToMelee + 0.1f ? dragSpot : module.PrimaryActor.Position + meleeDistance * dragDir;  
    }  
}
