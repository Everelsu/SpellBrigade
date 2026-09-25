using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Iced.Intel;
using Il2CppInterop.Common;
using SpellBrigade.Shared;

namespace MorePlayers;

// Лимит «4 игрока» в игре не вынесен в поле — компилятор вписал число 4 прямо в
// машинный код. Находим нужную инструкцию дизассемблером (по смыслу, а не по адресу —
// так правка переживёт мелкие обновления игры) и меняем в ней число.
internal sealed class CodePatch
{
    public string Name;
    public string Type, Method;
    public Func<Instruction, Instruction, bool> Match; // (инструкция, следующая)
    public Func<int> Value;
    public byte? NextOpcode; // вместо числа заменить код следующей инструкции (короткого перехода)

    private IntPtr _immediate;   // адрес числа в машинном коде
    private int _size;           // 1 или 4 байта
    private int _original;

    public bool Found => _immediate != IntPtr.Zero;

    [DllImport("kernel32.dll")]
    private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint newProtect, out uint oldProtect);

    public bool Locate()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => { try { return a.GetType(Type); } catch { return null; } })
            .FirstOrDefault(t => t != null);
        var method = type?.GetMethod(Method, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null) return false;

        // общий с другими методами код (identical code folding) не трогаем
        if (SharedCodeGuard.FindMethodsSharingCode(method).Count > 0) return false;

        var field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);
        if (field?.GetValue(null) is not IntPtr info || info == IntPtr.Zero) return false;
        IntPtr code = Marshal.ReadIntPtr(info);

        var bytes = new byte[8000];
        Marshal.Copy(code, bytes, 0, bytes.Length);
        var decoder = Iced.Intel.Decoder.Create(64, new ByteArrayCodeReader(bytes));
        decoder.IP = (ulong)code.ToInt64();
        var list = new List<(Instruction instr, ConstantOffsets offsets)>();
        ulong end = decoder.IP + (ulong)bytes.Length;
        while (decoder.IP < end)
        {
            decoder.Decode(out var instr);
            if (instr.Code == Code.INVALID || instr.Code == Code.Int3) break;
            list.Add((instr, decoder.GetConstantOffsets(instr)));
            if (instr.Mnemonic == Mnemonic.Ret)
            {
                int next = (int)(decoder.IP - (ulong)code.ToInt64());
                if (next < bytes.Length && bytes[next] == 0xCC) break;
            }
        }

        for (int i = 0; i < list.Count; i++)
        {
            var (instr, offsets) = list[i];
            var next = i + 1 < list.Count ? list[i + 1].instr : default;
            if (NextOpcode.HasValue)
            {
                // короткий условный переход (2 байта) сразу после найденной инструкции
                if (!Match(instr, next) || next.Length != 2 || !next.IsJccShort) continue;
                _immediate = (IntPtr)(long)next.IP;
                _size = 1;
                _original = Marshal.ReadByte(_immediate);
                return true;
            }
            if (!offsets.HasImmediate || !Match(instr, next)) continue;
            int offset = (int)(instr.IP - (ulong)code.ToInt64()) + offsets.ImmediateOffset;
            _immediate = code + offset;
            _size = offsets.ImmediateSize;
            _original = _size == 1 ? Marshal.ReadByte(_immediate) : Marshal.ReadInt32(_immediate);
            return true;
        }
        return false;
    }

    public void Apply() => Write(Value());
    public void Restore() => Write(_original);

    private void Write(int value)
    {
        if (!Found) return;
        if (_size == 1 && !NextOpcode.HasValue) value = Math.Clamp(value, 1, 127); // cmp r32, imm8 — знаковый байт
        VirtualProtect(_immediate, (UIntPtr)_size, 0x40 /* PAGE_EXECUTE_READWRITE */, out uint old);
        if (_size == 1) Marshal.WriteByte(_immediate, (byte)value);
        else Marshal.WriteInt32(_immediate, value);
        VirtualProtect(_immediate, (UIntPtr)_size, old, out _);
    }

    // --- типовые шаблоны ---

    // cmp <регистр>, 4  (сравнение числа подключённых с лимитом)
    public static bool CmpRegWith4(Instruction i, Instruction _) =>
        i.Mnemonic == Mnemonic.Cmp && i.Op0Kind == OpKind.Register && i.OpCount == 2 && Imm(i) == 4;

    // cmp <регистр>, 1  сразу перед je short (конец цепочки «1, 2, 3, 4 игрока»)
    public static bool CmpRegWith1(Instruction i, Instruction next) =>
        i.Mnemonic == Mnemonic.Cmp && i.Op0Kind == OpKind.Register && i.OpCount == 2 && Imm(i) == 1 && next.Code == Code.Je_rel8_64;

    // Радиус зоны: t = (игроки − 1) / 3, затем t ограничивается сверху единицей:
    // comiss t, 1.0 / jbe — берём первую такую пару после деления
    public static Func<Instruction, Instruction, bool> RadiusClamp()
    {
        bool divided = false;
        return (i, next) =>
        {
            if (i.Mnemonic == Mnemonic.Divss) divided = true;
            return divided && i.Mnemonic == Mnemonic.Comiss && next.Code == Code.Jbe_rel8_64;
        };
    }

    // mov <регистр>, 4  или  mov [память], 4
    public static bool MovWith4(Instruction i, Instruction _) =>
        i.Mnemonic == Mnemonic.Mov && i.OpCount == 2 && Imm(i) == 4;

    // mov <регистр>, 4 сразу перед вызовом (аргумент функции)
    public static bool MovWith4BeforeCall(Instruction i, Instruction next) =>
        MovWith4(i, next) && i.Op0Kind == OpKind.Register && next.IsCallNear;

    private static long Imm(Instruction i)
    {
        if (i.OpCount < 2) return -1;
        return i.Op1Kind switch
        {
            OpKind.Immediate8 or OpKind.Immediate8to32 or OpKind.Immediate8to64 or
            OpKind.Immediate32 or OpKind.Immediate32to64 => (long)i.GetImmediate(1),
            _ => -1,
        };
    }
}
