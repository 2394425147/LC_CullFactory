using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Burst;
using Unity.Burst.LowLevel;

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
        //   if (Options.EnableBurstCompilation && BurstCompilerHelper.IsBurstGenerated)
        //   {
        //       ptr = BurstCompilerService.GetAsyncCompiledAsyncDelegateMethod(BurstCompilerService.CompileAsyncDelegateMethod(delegateObj, string.Empty));
        //   }
        // - else
        // + if (ptr == null)
        //   {
        //       if (isILPostProcessing)
        //           return null;
        //       GCHandle.Alloc(@delegate);
        //       ptr = (void*)Marshal.GetFunctionPointerForDelegate(@delegate);
        //   }
        // - if (ptr == null)
        // -     throw new InvalidOperationException($"Burst failed to compile the function pointer `{methodInfo}`");
        //   return ptr;
        var matcher = new CodeMatcher(instructions);

        var asyncCompileMethod = typeof(BurstCompilerService).GetMethod(nameof(BurstCompilerService.GetAsyncCompiledAsyncDelegateMethod), [typeof(int)]);
        if (asyncCompileMethod == null)
        {
            Plugin.LogError("Failed to find the Burst async compilation method");
            return instructions;
        }

        // Replace else with null check.
        matcher
            .MatchForward(true, [
                new(OpCodes.Call, asyncCompileMethod),
                new(OpCodes.Stloc_0),
            ])
            .Advance(1);
        if (matcher.IsInvalid || !matcher.Instruction.Branches(out var returnLabel))
        {
            Plugin.LogAlways($"{matcher.Instruction}");
            Plugin.LogError("Failed to find the call to the Burst async compilation method");
            return instructions;
        }
        matcher
            .RemoveInstruction()
            .InsertAndAdvance([
                    new(OpCodes.Ldloc_0),
                    new(OpCodes.Ldc_I4_0),
                    new(OpCodes.Conv_U),
                    new(OpCodes.Bne_Un, returnLabel),
                ]);

        // Remove the throw statement.
        var exceptionMatchers = new CodeMatch[]
            {
                new(OpCodes.Ldloc_0),
                new(OpCodes.Ldc_I4_0),
                new(OpCodes.Conv_U),
                new(OpCodes.Bne_Un),

                new(OpCodes.Ldstr),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Call, typeof(string).GetMethod(nameof(string.Format), [typeof(string), typeof(object)])),
                new(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor([typeof(string)])),
                new(OpCodes.Throw),
            };
        matcher.MatchForward(false, exceptionMatchers);
        if (matcher.IsInvalid)
        {
            Plugin.LogError("Failed to find Burst compilation failure throw statement");
            return instructions;
        }
        var labels = matcher.Instruction.labels;
        matcher
            .RemoveInstructions(exceptionMatchers.Length)
            .Labels.AddRange(labels);

        return matcher.Instructions();
    }
}
