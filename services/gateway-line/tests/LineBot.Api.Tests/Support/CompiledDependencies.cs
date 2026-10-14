// SPDX-License-Identifier: MIT
using System.Reflection;
using System.Reflection.Emit;
namespace LineBot.Api.Tests.Support;

// Resolve actual metadata tokens in compiled methods, including generated async
// state machines. Comments and spelling in source text are not dependencies.
internal static class CompiledDependencies
{
    private static readonly IReadOnlyDictionary<short, OpCode> Opcodes =
        typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(o => o.Value);
    internal sealed record Use(OpCode Opcode, MemberInfo? Member, string? Literal);
    internal static IEnumerable<Use> Inspect(Type owner)
    {
        foreach (var type in Family(owner)) {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                BindingFlags.Static | BindingFlags.DeclaredOnly).Cast<MethodBase>()
                .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))) {
                var bytes = method.GetMethodBody()?.GetILAsByteArray();
                if (bytes is null) continue;
                for (int offset = 0; offset < bytes.Length;) {
                    short value = bytes[offset++];
                    if (value == 0xfe) value = unchecked((short)(0xfe00 | bytes[offset++]));
                    var opcode = Opcodes[value];
                    var start = offset;
                    var size = opcode.OperandType switch {
                        OperandType.InlineNone => 0,
                        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                        OperandType.InlineVar => 2,
                        OperandType.InlineI8 or OperandType.InlineR => 8,
                        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, offset),
                        _ => 4
                    };
                    offset += size;
                    if (opcode.OperandType == OperandType.InlineString)
                        yield return new(opcode, null, method.Module.ResolveString(BitConverter.ToInt32(bytes, start)));
                    else if (opcode.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok)
                        yield return new(opcode, method.Module.ResolveMember(BitConverter.ToInt32(bytes, start),
                            type.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null), null);
                }
            }
        }
    }
    internal static IEnumerable<Type> Types(Type owner)
    {
        foreach (var use in Inspect(owner)) {
            var type = use.Member as Type ?? use.Member?.DeclaringType;
            if (type is not null) foreach (var part in Expand(type)) yield return part;
            if (use.Member is MethodInfo method) {
                foreach (var part in Expand(method.ReturnType)) yield return part;
                foreach (var arg in method.GetGenericArguments()) foreach (var part in Expand(arg)) yield return part;
                foreach (var parameter in method.GetParameters()) foreach (var part in Expand(parameter.ParameterType)) yield return part;
            }
        }
        foreach (var type in Family(owner))
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                foreach (var part in Expand(field.FieldType)) yield return part;
    }
    internal static IEnumerable<Type> Expand(Type type) {
        yield return type;
        if (type.HasElementType) foreach (var item in Expand(type.GetElementType()!)) yield return item;
        foreach (var arg in type.GetGenericArguments()) foreach (var item in Expand(arg)) yield return item;
    }
    private static IEnumerable<Type> Family(Type owner) {
        yield return owner;
        foreach (var nested in owner.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            foreach (var item in Family(nested)) yield return item;
    }
    internal static string SourceRoot() {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "roidoya-character-platform.sln"))) return directory.FullName;
        throw new InvalidOperationException("The checked-out solution sources are required by the file-boundary contract.");
    }
}
