using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WhileYouAreNearbyFix
{
    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
            new Harmony("ifchen0.whileyouarenearbyfix").PatchAll();
        }
    }

    /// <summary>
    /// While You Are Nearby looks for a job within its search radius, but for cell-based work givers (sowing, roofing,
    /// snow clearing, ...) it runs HasJobOnCell and a reachability check on every candidate cell of the whole map and
    /// only then compares the distance. Hands its loop only the cells inside the search radius instead. Every cell it can
    /// accept is within that radius, so the chosen job is unchanged.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_TryIssueJobPackage_NearbyCells
    {
        private static readonly MethodInfo PotentialWorkCellsGlobal =
            AccessTools.Method(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.PotentialWorkCellsGlobal));

        private static readonly PropertyInfo SettingsProperty =
            AccessTools.Property(AccessTools.TypeByName("MjRimMods.WhileYouAreNearby.Utils"), "settings");

        private static readonly FieldInfo MaxSearchRadiusField =
            AccessTools.Field(AccessTools.TypeByName("MjRimMods.WhileYouAreNearby.SimpleSettings"), "maxSearchRadius");

        public static bool Prepare() => TargetMethod() != null && SettingsProperty != null && MaxSearchRadiusField != null;

        public static MethodBase TargetMethod() =>
            AccessTools.Method("MjRimMods.WhileYouAreNearby.JobGiver_Work_TryIssueJobPackagePatch:After_TryIssueJobPackage");

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(PotentialWorkCellsGlobal))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(Patch_TryIssueJobPackage_NearbyCells), nameof(CellsWithinRadius));
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced != 1)
                Log.Warning($"[While You Are Nearby Fix] Expected one PotentialWorkCellsGlobal call in While You Are Nearby, found {replaced}.");
        }

        public static IEnumerable<IntVec3> CellsWithinRadius(WorkGiver_Scanner scanner, Pawn pawn)
        {
            IEnumerable<IntVec3> cells = scanner.PotentialWorkCellsGlobal(pawn);
            if (cells == null)
                yield break;
            int radius = (int)MaxSearchRadiusField.GetValue(SettingsProperty.GetValue(null));
            int radiusSquared = radius * radius;
            IntVec3 root = pawn.Position;
            foreach (IntVec3 cell in cells)
            {
                if (cell.DistanceToSquared(root) < radiusSquared)
                    yield return cell;
            }
        }
    }
}
