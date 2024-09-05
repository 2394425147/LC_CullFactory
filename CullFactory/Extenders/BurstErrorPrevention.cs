using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Burst;

namespace CullFactory.Extenders;

internal static class BurstErrorPrevention
{
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BurstCompiler), nameof(BurstCompiler.GetILPPMethodFunctionPointer2))]
    private static IEnumerable<CodeInstruction> PatchGetILPPMethodFunctionPointer2(IEnumerable<CodeInstruction> instructions)
    {
        //   if (ilppMethod == IntPtr.Zero)
        //   {
        // -    throw new ArgumentNullException("ilppMethod");
        // +    return null;
        //   }
        var matcher = new CodeMatcher(instructions)
            .MatchForward(false, [
                new(OpCodes.Ldstr, "ilppMethod"),
                new(OpCodes.Newobj, typeof(ArgumentNullException).GetConstructor([typeof(string)])),
                new(OpCodes.Throw),
            ]);
        if (matcher.IsInvalid)
        {
            Plugin.LogError("Failed to find Burst function pointer getter's throw statement");
            return instructions;
        }

        matcher
            .RemoveInstructions(3)
            .Insert([
                new(OpCodes.Ldc_I4_0),
                new(OpCodes.Ret),
            ]);

        return matcher.Instructions();
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BurstCompiler), nameof(BurstCompiler.Compile), [typeof(object), typeof(MethodInfo), typeof(bool), typeof(bool)])]
    private static IEnumerable<CodeInstruction> PatchCompile(IEnumerable<CodeInstruction> instructions)
    {
        // - if (ptr == null)
        // - {
        // -     throw new InvalidOperationException($"Burst failed to compile the function pointer `{methodInfo}`");
        // - }
        //   return ptr;
        var matcher = new CodeMatcher(instructions)
            .MatchForward(false, [
                new(OpCodes.Ldloc_0),
                new(OpCodes.Ldc_I4_0),
                new(OpCodes.Conv_U),
                new(OpCodes.Bne_Un),

                new(OpCodes.Ldstr),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Call, typeof(string).GetMethod(nameof(string.Format), [typeof(string), typeof(object)])),
                new(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor([typeof(string)])),
                new(OpCodes.Throw),
            ]);
        if (matcher.IsInvalid)
        {
            Plugin.LogError("Failed to find Burst compilation failure throw statement");
            return instructions;
        }

        var labels = matcher.Instruction.labels;
        matcher
            .RemoveInstructions(9)
            .Labels.AddRange(labels);

        return matcher.Instructions();
    }
}
