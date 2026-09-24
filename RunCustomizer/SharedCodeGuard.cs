using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime;

namespace RunCustomizer;

// IL2CPP-сборка склеивает методы с одинаковым машинным кодом (identical code folding).
// Harmony-хук ставится на машинный код, поэтому патч такого метода цепляет и все его
// «двойники» — вплоть до падений в совершенно посторонних местах. Перед патчем
// проверяем, что адрес кода метода уникален среди всех методов игры.
internal static class SharedCodeGuard
{
    // адрес машинного кода → Il2CppMethodInfo* всех методов с этим кодом
    private static Dictionary<IntPtr, List<IntPtr>> _methodsByCode;

    public static List<string> FindMethodsSharingCode(MethodBase generatedMethod)
    {
        var result = new List<string>();
        FieldInfo field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(generatedMethod);
        if (field?.GetValue(null) is not IntPtr self || self == IntPtr.Zero) return result;

        // Il2CppMethodInfo начинается с поля methodPointer
        IntPtr code = Marshal.ReadIntPtr(self);
        if (code == IntPtr.Zero) return result;

        _methodsByCode ??= BuildIndex();
        if (!_methodsByCode.TryGetValue(code, out var owners)) return result;

        foreach (IntPtr method in owners)
            if (method != self)
                result.Add(Describe(method));
        return result;
    }

    private static string Describe(IntPtr method)
    {
        IntPtr klass = IL2CPP.il2cpp_method_get_class(method);
        string ns = IL2CPP.il2cpp_class_get_namespace_(klass);
        string type = string.IsNullOrEmpty(ns) ? IL2CPP.il2cpp_class_get_name_(klass) : $"{ns}.{IL2CPP.il2cpp_class_get_name_(klass)}";
        return $"{type}::{IL2CPP.il2cpp_method_get_name_(method)}";
    }

    private static unsafe Dictionary<IntPtr, List<IntPtr>> BuildIndex()
    {
        var index = new Dictionary<IntPtr, List<IntPtr>>();
        uint assemblyCount = 0;
        IntPtr* assemblies = IL2CPP.il2cpp_domain_get_assemblies(IL2CPP.il2cpp_domain_get(), ref assemblyCount);

        for (uint a = 0; a < assemblyCount; a++)
        {
            IntPtr image = IL2CPP.il2cpp_assembly_get_image(assemblies[a]);
            uint classCount = IL2CPP.il2cpp_image_get_class_count(image);
            for (uint c = 0; c < classCount; c++)
            {
                IntPtr klass = IL2CPP.il2cpp_image_get_class(image, c);
                if (klass == IntPtr.Zero) continue;

                IntPtr iter = IntPtr.Zero;
                IntPtr method;
                while ((method = IL2CPP.il2cpp_class_get_methods(klass, ref iter)) != IntPtr.Zero)
                {
                    IntPtr code = Marshal.ReadIntPtr(method);
                    if (code == IntPtr.Zero) continue;
                    if (!index.TryGetValue(code, out var list)) index[code] = list = new List<IntPtr>(1);
                    list.Add(method);
                }
            }
        }
        return index;
    }
}
