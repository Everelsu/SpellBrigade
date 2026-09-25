using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace SpellBrigade.Shared;

// Хуки ставятся по одному: если после обновления игры метод пропал или его машинный код
// стал общим с другими методами, этот хук пропускается с понятной строкой в логе,
// а остальное продолжает работать.
internal static class Hooks
{
    public static bool Patch(HarmonyLib.Harmony harmony, MelonLogger.Instance log, Type target, string method,
                             Type patchClass, string prefix = null, string postfix = null) =>
        Patch(harmony, log, AccessTools.Method(target, method), patchClass, prefix, postfix, $"{target.Name}.{method}");

    public static bool Patch(HarmonyLib.Harmony harmony, MelonLogger.Instance log, MethodBase original,
                             Type patchClass, string prefix = null, string postfix = null, string name = null)
    {
        name ??= original != null ? $"{original.DeclaringType?.Name}.{original.Name}" : $"{prefix ?? postfix} target";
        try
        {
            if (original == null) throw new MissingMethodException($"{name} not found");

            // IL2CPP склеивает одинаковый машинный код разных методов: хук такого метода
            // задел бы и все его «двойники». Такие методы не трогаем.
            var sharedWith = SharedCodeGuard.FindMethodsSharingCode(original);
            if (sharedWith.Count > 0)
            {
                log.Warning($"skipped {name}: its native code is shared with {sharedWith.Count} other method(s), " +
                            $"patching would break them (e.g. {string.Join(", ", sharedWith.GetRange(0, Math.Min(5, sharedWith.Count)))})");
                return false;
            }

            harmony.Patch(original,
                prefix:  prefix  != null ? new HarmonyMethod(AccessTools.Method(patchClass, prefix))  : null,
                postfix: postfix != null ? new HarmonyMethod(AccessTools.Method(patchClass, postfix)) : null);
            return true;
        }
        catch (Exception e)
        {
            log.Error($"failed to hook {name} — this part is disabled. {e.GetType().Name}: {e.Message}");
            return false;
        }
    }
}
