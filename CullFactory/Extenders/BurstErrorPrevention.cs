using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Burst;

namespace CullFactory.Extenders;

internal static class BurstErrorPrevention
{
    private static readonly FieldInfo f_IntPtr_Zero = typeof(IntPtr).GetField("Zero");

    private static IEnumerable<CodeInstruction> ReturnInsteadOfThrowing(IEnumerable<CodeInstruction> instructions, CodeInstruction valueToReturn)
    {
        var instructionsList = instructions.ToList();

        int match = 0;

        while (true)
        {
            match = instructionsList.FindIndex(match, insn => insn.opcode == OpCodes.Throw);
            if (match == -1)
                break;
            instructionsList[match] = new CodeInstruction(OpCodes.Pop);
            instructionsList.InsertRange(match + 1, [
                new CodeInstruction(valueToReturn),
                    new CodeInstruction(OpCodes.Ret)
            ]);
            match += 3;
        }

        return instructionsList;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BurstCompiler), nameof(BurstCompiler.CompileILPPMethod2))]
    private static IEnumerable<CodeInstruction> PatchCompileILPPMethod2(IEnumerable<CodeInstruction> instructions)
    {
        return ReturnInsteadOfThrowing(instructions, new CodeInstruction(OpCodes.Ldsfld, f_IntPtr_Zero));
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BurstCompiler), nameof(BurstCompiler.GetILPPMethodFunctionPointer2))]
    private static IEnumerable<CodeInstruction> PatchGetILPPMethodFunctionPointer2(IEnumerable<CodeInstruction> instructions)
    {
        return ReturnInsteadOfThrowing(instructions, new CodeInstruction(OpCodes.Ldsfld, f_IntPtr_Zero));
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BurstCompiler), nameof(BurstCompiler.Compile), [typeof(object), typeof(bool)])]
    private static IEnumerable<CodeInstruction> PatchCompileSimple(IEnumerable<CodeInstruction> instructions)
    {
        return ReturnInsteadOfThrowing(instructions, new CodeInstruction(OpCodes.Ldc_I4_0));
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BurstCompiler), nameof(BurstCompiler.Compile), [typeof(object), typeof(MethodInfo), typeof(bool), typeof(bool)])]
    private static IEnumerable<CodeInstruction> PatchCompileImpl(IEnumerable<CodeInstruction> instructions)
    {
        return ReturnInsteadOfThrowing(instructions, new CodeInstruction(OpCodes.Ldc_I4_0));
    }
}
