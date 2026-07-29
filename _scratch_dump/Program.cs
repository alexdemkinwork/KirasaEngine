using System.Reflection;
using System.Text;

var mode = args.Length > 0 ? args[0] : "types";
var pattern = args.Length > 1 ? args[1] : "";

var asms = new[]
{
    typeof(Silk.NET.Direct3D12.D3D12).Assembly,
    typeof(Silk.NET.DXGI.DXGI).Assembly,
    typeof(Silk.NET.Direct3D.Compilers.D3DCompiler).Assembly,
    typeof(Silk.NET.Core.Native.SilkMarshal).Assembly,
};

var sb = new StringBuilder();

foreach (var asm in asms)
{
    Type[] types;
    try { types = asm.GetTypes(); }
    catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

    foreach (var t in types.Where(t => t.IsPublic || t.IsNestedPublic).OrderBy(t => t.FullName))
    {
        if (!t.FullName.Contains(pattern, StringComparison.OrdinalIgnoreCase)) continue;
        if (mode == "types")
        {
            sb.AppendLine(t.FullName);
        }
        else if (mode == "members")
        {
            sb.AppendLine("### " + t.FullName + (t.IsEnum ? " (enum)" : t.IsValueType ? " (struct)" : " (class)"));
            if (t.IsEnum)
            {
                foreach (var n in Enum.GetNames(t)) sb.AppendLine("   " + n);
                continue;
            }
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                sb.AppendLine("   F " + Sig(f.FieldType) + " " + f.Name);
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                sb.AppendLine("   P " + Sig(p.PropertyType) + " " + p.Name);
            var seen = new HashSet<string>();
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(m => m.Name).ThenBy(m => m.GetParameters().Count(pp => pp.ParameterType.IsByRef)))
            {
                if (m.IsSpecialName) continue;
                var key = m.Name + "/" + m.GetParameters().Length;
                if (!seen.Add(key)) continue;
                sb.AppendLine("   M " + Sig(m.ReturnType) + " " + m.Name + "(" + string.Join(", ", m.GetParameters().Select(pp => (pp.IsOut ? "out " : pp.ParameterType.IsByRef ? "ref " : "") + Sig(pp.ParameterType) + " " + pp.Name)) + ")");
            }
            foreach (var c in t.GetConstructors())
                sb.AppendLine("   C .ctor(" + string.Join(", ", c.GetParameters().Select(pp => Sig(pp.ParameterType) + " " + pp.Name)) + ")");
        }
    }
}

Console.Out.Write(sb.ToString());

static string Sig(Type t)
{
    if (t == null) return "?";
    if (t.IsByRef) return Sig(t.GetElementType());
    if (t.IsPointer) return Sig(t.GetElementType()) + "*";
    var n = t.Name;
    if (t.IsGenericType) return n.Split('`')[0] + "<" + string.Join(",", t.GetGenericArguments().Select(Sig)) + ">";
    return n;
}
