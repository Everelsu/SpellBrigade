using System;
using Il2Cpp;
using MelonLoader;
using Unity.Netcode;

namespace RunCustomizer;

internal enum Mode { Vanilla = 0, Random = 1, Custom = 2, RandomBase = 3 }

// Параметры сложности в «человеческих» единицах: во сколько раз больше/меньше.
internal struct Params
{
    public float Enemy;       // здоровье и урон врагов
    public float SpawnSpeed;  // скорость появления (игра хранит интервал = 1 / скорость)
    public float EnemyCount;  // минимум врагов на карте
    public float HealthDrops; // шанс аптечек

    public override string ToString() =>
        $"enemies x{Enemy:0.##}, spawn speed x{SpawnSpeed:0.##}, enemy count x{EnemyCount:0.##}, health drops x{HealthDrops:0.##}";
}

internal static class Modes
{
    // Значения из кода игры для Normal / Hard / Nightmare (индекс = Difficulty)
    public static readonly float[] BaseEnemy      = { 1.1f, 2.05f, 3f };
    public static readonly float[] BaseSpawnSpeed = { 1f, 1f / 0.7f, 1f / 0.4f };
    public static readonly float[] BaseEnemyCount = { 1f, 1.25f, 1.5f };
    public static readonly float[] BaseHealth     = { 1f, 0.5f, 0.25f };

    // Разброс «Случайной»: от заметно легче Normal до заметно жёстче Nightmare
    public static readonly (float Min, float Max) RangeEnemy      = (0.5f, 4.5f);
    public static readonly (float Min, float Max) RangeSpawnSpeed = (0.75f, 4f);
    public static readonly (float Min, float Max) RangeEnemyCount = (0.75f, 2f);
    public static readonly (float Min, float Max) RangeHealth     = (0.1f, 1.5f);

    public static Mode Selected
    {
        get => (Mode)Math.Clamp(RunCustomizerMod.ModeEntry.Value, 0, 3);
        set
        {
            if (Selected == value) return;
            RunCustomizerMod.ModeEntry.Value = (int)value;
            RunCustomizerMod.Save();
            _pending = null;
        }
    }

    public static Params Custom => new()
    {
        Enemy       = RunCustomizerMod.CustomEnemy.Value,
        SpawnSpeed  = RunCustomizerMod.CustomSpawnSpeed.Value,
        EnemyCount  = RunCustomizerMod.CustomEnemyCount.Value,
        HealthDrops = RunCustomizerMod.CustomHealth.Value,
    };

    // Бросок делается заранее (в лобби), чтобы базовая сложность успела разойтись
    // по сети штатным путём. Игроку он не показывается до начала забега.
    private static (Params Values, Difficulty Base)? _pending;

    // Что действует в текущем забеге (только у хоста — враги и дроп считаются на сервере)
    public static bool InRun { get; private set; }
    public static Params Active { get; private set; }

    public static (Params Values, Difficulty Base) Pending => _pending ??= Roll();

    public static void Reroll() => _pending = null;

    private static (Params, Difficulty) Roll()
    {
        var rnd = new Random();
        switch (Selected)
        {
            case Mode.Random:
            {
                var p = new Params
                {
                    Enemy       = Pick(rnd, RangeEnemy),
                    SpawnSpeed  = Pick(rnd, RangeSpawnSpeed),
                    EnemyCount  = Pick(rnd, RangeEnemyCount),
                    HealthDrops = Pick(rnd, RangeHealth),
                };
                return (p, NearestBase(p));
            }
            case Mode.Custom:
                return (Custom, NearestBase(Custom));
            case Mode.RandomBase:
            {
                int max = HighestUnlocked();
                var d = (Difficulty)rnd.Next(0, max + 1);
                return (Of(d), d);
            }
            default:
                return (Of(Difficulty.Normal), Difficulty.Normal);
        }
    }

    private static float Pick(Random rnd, (float Min, float Max) r) =>
        (float)Math.Round(r.Min + rnd.NextDouble() * (r.Max - r.Min), 2);

    public static Params Of(Difficulty d)
    {
        int i = (int)d;
        return new Params { Enemy = BaseEnemy[i], SpawnSpeed = BaseSpawnSpeed[i], EnemyCount = BaseEnemyCount[i], HealthDrops = BaseHealth[i] };
    }

    // «Насколько тяжело» по шкале Normal = 0, Hard = 1, Nightmare = 2 (за краями — экстраполяция).
    // Аптечки обратны сложности, поэтому для них шкала перевёрнута.
    public static float Score(Params p) =>
        (Position(p.Enemy, BaseEnemy) + Position(p.SpawnSpeed, BaseSpawnSpeed) +
         Position(p.EnemyCount, BaseEnemyCount) + Position(p.HealthDrops, BaseHealth)) / 4f;

    private static float Position(float v, float[] bases)
    {
        bool rising = bases[2] > bases[0];
        int seg = rising ? (v < bases[1] ? 0 : 1) : (v > bases[1] ? 0 : 1);
        float a = bases[seg], b = bases[seg + 1];
        return seg + (v - a) / (b - a);
    }

    // База определяет события волн, прогресс миров и бонус золота игры.
    // Не выше открытой — иначе своя сложность открывала бы Nightmare в обход прогресса.
    private static Difficulty NearestBase(Params p) =>
        (Difficulty)Math.Clamp((int)Math.Round(Score(p)), 0, HighestUnlocked());

    public static int HighestUnlocked()
    {
        int best = 0;
        for (int d = 1; d <= 2; d++)
        {
            try
            {
                var config = DifficultyConfigurationRepository.Get((Difficulty)d);
                if (config != null && !config.IsLockedByProgression()) best = d;
            }
            catch { /* репозиторий ещё не загружен — считаем закрытой */ }
        }
        return best;
    }

    public static bool IsHost
    {
        get
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsServer;
        }
    }

    // От старта забега до возврата в меню сложность лобби не трогаем: клиенты читают её,
    // когда до них дойдёт сигнал старта, и должны увидеть то же, что хост
    private static bool _inLevel;

    // MainMenuManager.PrepareForLevelStart: игра перенесла сложность лобби в RunData
    public static void OnLevelStart()
    {
        InRun = false;
        _inLevel = true;
        LastRunMode = Mode.Vanilla;
        if (Selected == Mode.Vanilla || !IsHost) return;

        var (values, _) = Pending;
        _pending = null; // после забега — новый бросок
        LastRunMode = Selected;
        if (Selected == Mode.RandomBase)
        {
            RunCustomizerMod.Log.Msg($"run difficulty: {RunDifficulty} (random from base)");
            return;
        }
        Active = values;
        InRun = true;
        RunCustomizerMod.Log.Msg($"run difficulty ({Selected}, base {RunDifficulty}): {values}");
    }

    // Что было в последнем забеге — для паузы и экрана итогов (он показывается уже в меню)
    public static Mode LastRunMode { get; private set; }

    public static void OnBackToMenu() => InRun = _inLevel = false;

    public static Difficulty RunDifficulty
    {
        get
        {
            try { return SingletonPersistent<RunData>.Instance?.SelectedDifficulty ?? Difficulty.Normal; }
            catch { return Difficulty.Normal; }
        }
    }

    // Держим базовую сложность лобби равной броску. Зовётся раз в полсекунды в меню.
    public static void SyncLobby()
    {
        if (_inLevel || Selected == Mode.Vanilla || !IsHost) return;
        var mm = NetworkSingleton<MainMenuManager>.Instance;
        if (mm == null) return;
        var want = Pending.Base;
        if (mm.CurrentDifficulty.Value != want)
        {
            mm.SetDifficulty(want);
            SingletonPersistent<RunSetupPreferences>.Instance?.SetPreferredDifficulty(want);
        }
    }
}
