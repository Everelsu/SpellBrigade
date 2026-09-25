using System;
using System.Collections;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using SpellBrigade.Shared;

[assembly: MelonInfo(typeof(SkipIntro.SkipIntroMod), "Skip Intro", "1.0.0", "Relsev")]
[assembly: MelonGame("BoltBlasterGames", "TheSpellBrigade")]

namespace SkipIntro;

public class SkipIntroMod : MelonMod
{
    public static MelonLogger.Instance Log;
    private static MelonPreferences_Entry<bool> _enabled;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        // UserData/MelonPreferences.cfg, секция [SkipIntro]; в Mod Menu — сам, без кода
        var category = MelonPreferences.CreateCategory("SkipIntro", "Skip Intro");
        _enabled = category.CreateEntry("Enabled", true, "Skip Intro", "Пропускать логотипы и вступительные ролики");
        category.SaveToFile(false);
        // Хук именно на Start: его вызывает сам Unity, поэтому он не может быть встроен
        // в другой метод. OpeningFlow.Execute IL2CPP встроил прямо в Start —
        // хук на Execute никогда не срабатывает.
        if (Hooks.Patch(HarmonyInstance, Log, typeof(OpeningFlowManager), nameof(OpeningFlowManager.Start),
                        typeof(SkipIntroMod), postfix: nameof(StartPostfix)))
            Log.Msg("ready — logos and intro videos are skipped");
        else
            Log.Warning("intro stays");
    }

    // Сцена Opening: логотипы → видео BoltBlasterGames → видео VAF → (для Китая) плашка
    // о здоровье. Start собирает эту цепочку и запускает первый шаг. Дальше делаем то же,
    // что кнопка пропуска в игре: пропускаем текущий шаг — игра сама переходит к следующему,
    // а после последнего загружает следующую сцену. Экран согласия при первом запуске —
    // отдельная сцена, его не трогаем.
    private static void StartPostfix(OpeningFlowManager __instance)
    {
        if (!_enabled.Value) return;
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
