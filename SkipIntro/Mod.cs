using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

[assembly: MelonInfo(typeof(SkipIntro.SkipIntroMod), "Skip Intro", "1.0.0", "Relsev")]
[assembly: MelonGame("BoltBlasterGames", "TheSpellBrigade")]

namespace SkipIntro;

public class SkipIntroMod : MelonMod
{
    public static MelonLogger.Instance Log;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        try
        {
            // Хук именно на Start: его вызывает сам Unity, поэтому он не может быть встроен
            // в другой метод. OpeningFlow.Execute IL2CPP встроил прямо в Start —
            // хук на Execute никогда не срабатывает.
            MethodInfo original = AccessTools.Method(typeof(OpeningFlowManager), nameof(OpeningFlowManager.Start))
                                  ?? throw new MissingMethodException(nameof(OpeningFlowManager), nameof(OpeningFlowManager.Start));

            // IL2CPP склеивает одинаковый машинный код разных методов: хук такого метода
            // задел бы и все его «двойники». Такие методы не трогаем.
            var sharedWith = SharedCodeGuard.FindMethodsSharingCode(original);
            if (sharedWith.Count > 0)
            {
                Log.Warning($"skipped OpeningFlowManager.Start: its native code is shared with {sharedWith.Count} other method(s), intro stays");
                return;
            }

            HarmonyInstance.Patch(original, postfix: new HarmonyMethod(AccessTools.Method(typeof(SkipIntroMod), nameof(StartPostfix))));
            Log.Msg("ready — logos and intro videos are skipped");
        }
        catch (Exception e)
        {
            Log.Error($"failed to hook OpeningFlowManager.Start, intro stays: {e.GetType().Name}: {e.Message}");
        }
    }

    // Сцена Opening: логотипы → видео BoltBlasterGames → видео VAF → (для Китая) плашка
    // о здоровье. Start собирает эту цепочку и запускает первый шаг. Дальше делаем то же,
    // что кнопка пропуска в игре: пропускаем текущий шаг — игра сама переходит к следующему,
    // а после последнего загружает следующую сцену. Экран согласия при первом запуске —
    // отдельная сцена, его не трогаем.
    private static void StartPostfix(OpeningFlowManager __instance)
    {
        MelonCoroutines.Start(SkipAll(__instance));
    }

    private static IEnumerator SkipAll(OpeningFlowManager manager)
    {
        // Каждый шаг пропускаем не больше одного раза: повторный Skip уже завершённого
        // шага мог бы второй раз запустить переход на следующую сцену.
        var skipped = new HashSet<IntPtr>();

        // ponytail: 10 с на случай, если шаг завершается не сразу — дольше сцена интро не живёт
        for (int frame = 0; frame < 600 && manager != null; frame++)
        {
            try
            {
                // Обычно Skip завершает шаг сразу и игра тут же запускает следующий —
                // тогда всё интро пропускается за один кадр, до первой отрисовки.
                OpeningFlow flow = manager.openingFlow;
                IOpeningStep step = flow?.currentStep;
                while (step != null && skipped.Add(step.Pointer))
                {
                    flow.SkipCurrentStep();
                    step = flow.currentStep;
                }
            }
            catch (Exception e)
            {
                Log.Error($"skip failed, rest of the intro plays as usual: {e}");
                yield break;
            }
            yield return null;
        }
        if (skipped.Count > 0) Log.Msg($"intro skipped ({skipped.Count} step(s))");
    }
}
