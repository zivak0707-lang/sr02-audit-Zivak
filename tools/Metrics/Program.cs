// Вимірювач метрик для СР2: методика М1 (CC) і М2 (LOC, вкладеність,
// параметри, Fan-out), М3 (клони від 6 логічних рядків).
// Запуск: dotnet run --project tools/Metrics -- src docs/metrics-before.csv
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var srcDir = args.Length > 0 ? args[0] : "src";
var csvPath = args.Length > 1 ? args[1] : "metrics.csv";
var files = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
    .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
             && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
    .OrderBy(f => f, StringComparer.Ordinal).ToList();
var trees = files.ToDictionary(f => f, f => CSharpSyntaxTree.ParseText(File.ReadAllText(f)));

// Власні типи проєкту — для Fan-out.
var typeNames = trees.Values.SelectMany(t => t.GetRoot().DescendantNodes()
    .OfType<BaseTypeDeclarationSyntax>()).Select(t => t.Identifier.Text).ToHashSet();

var rows = new List<Row>();
var units = new List<(string Where, List<(int Line, string Norm)> Lines)>();
int physical = 0, logical = 0, typesCount = 0;

foreach (var (file, tree) in trees)
{
    var text = File.ReadAllLines(file);
    physical += text.Length;
    logical += text.Count(IsLogical);
    var root = tree.GetRoot();
    foreach (var type in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
    {
        typesCount++;
        if (type is not TypeDeclarationSyntax cls) continue;
        var used = cls.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Select(i => i.Identifier.Text)
            .Where(n => typeNames.Contains(n) && n != cls.Identifier.Text)
            .ToHashSet();
        int fanOut = used.Count;
        foreach (var m in cls.Members.OfType<BaseMethodDeclarationSyntax>())
        {
            SyntaxNode? body = (SyntaxNode?)m.Body ?? m.ExpressionBody;
            if (body is null) continue;
            string name = m switch
            {
                MethodDeclarationSyntax md => md.Identifier.Text,
                ConstructorDeclarationSyntax cd => cd.Identifier.Text + " (ctor)",
                _ => m.Kind().ToString(),
            };
            var span = body.GetLocation().GetLineSpan();
            int first = span.StartLinePosition.Line, last = span.EndLinePosition.Line;
            var bodyLines = new List<(int, string)>();
            for (int i = first; i <= last; i++)
            {
                string l = i == first && m.Body is null ? text[i][span.StartLinePosition.Character..] : text[i];
                if (IsLogical(l)) bodyLines.Add((i + 1, l));
            }
            int loc = m.Body is null ? Math.Max(1, bodyLines.Count) : bodyLines.Count;
            rows.Add(new Row(Path.GetFileName(file), cls.Identifier.Text, name,
                m.GetLocation().GetLineSpan().StartLinePosition.Line + 1, loc,
                Cc(body), 1 + Depth(body, 0), m.ParameterList.Parameters.Count, fanOut));
            units.Add(($"{Path.GetFileName(file)}:{cls.Identifier.Text}.{name}",
                bodyLines.Select(b => (b.Item1, Normalize(b.Item2))).Where(b => b.Item2.Length > 0).ToList()));
        }
    }
}

var sb = new StringBuilder("File;Class;Method;Line;LOC;CC;Nesting;Params;FanOut\n");
foreach (var r in rows.OrderByDescending(r => r.Cc).ThenByDescending(r => r.Loc))
    sb.Append($"{r.File};{r.Class};{r.Method};{r.Line};{r.Loc};{r.Cc};{r.Nest};{r.Pars};{r.FanOut}\n");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(csvPath))!);
File.WriteAllText(csvPath, sb.ToString(), new UTF8Encoding(true));

var clones = FindClones(units, 6);
Console.WriteLine($"Файлів: {files.Count}; типів: {typesCount}; методів: {rows.Count}");
Console.WriteLine($"Рядків фізичних: {physical}; логічних: {logical}; LOC методів: {rows.Sum(r => r.Loc)}");
Console.WriteLine($"Макс. CC: {rows.Max(r => r.Cc)}; середня CC: {rows.Average(r => r.Cc):F2}");
Console.WriteLine($"Найдовший метод: {rows.Max(r => r.Loc)}; макс. вкладеність: {rows.Max(r => r.Nest)}; "
    + $"макс. параметрів: {rows.Max(r => r.Pars)}; макс. Fan-out: {rows.Max(r => r.FanOut)}");
Console.WriteLine($"Клонів від 6 рядків: {clones.Count}");
foreach (var c in clones) Console.WriteLine("  " + c);
Console.WriteLine($"CSV: {csvPath}");

// Логічний рядок: не порожній, не лише дужка, не лише коментар.
static bool IsLogical(string line)
{
    var t = line.Trim();
    return t.Length > 0 && !Regex.IsMatch(t, @"^[{}();,]+$") && !t.StartsWith("//") && !t.StartsWith("/*") && !t.StartsWith("*");
}

// М1: 1 + if, ?:, цикли, case/плече, &&, ||, catch, ??, ?.
static int Cc(SyntaxNode body) => 1 + body.DescendantNodesAndSelf().Sum(n => n switch
{
    IfStatementSyntax or ConditionalExpressionSyntax => 1,
    ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax => 1,
    CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax => 1,
    SwitchExpressionArmSyntax a when a.Pattern is not DiscardPatternSyntax => 1,
    CatchClauseSyntax or ConditionalAccessExpressionSyntax => 1,
    BinaryExpressionSyntax b when b.IsKind(SyntaxKind.LogicalAndExpression)
        || b.IsKind(SyntaxKind.LogicalOrExpression) || b.IsKind(SyntaxKind.CoalesceExpression) => 1,
    BinaryPatternSyntax => 1,
    _ => 0,
});

// М2: кількість вкладених керівних блоків; else if не поглиблює.
static int Depth(SyntaxNode node, int level)
{
    int best = level;
    foreach (var child in node.ChildNodes())
    {
        bool block = child is IfStatementSyntax or ForStatementSyntax or ForEachStatementSyntax
            or WhileStatementSyntax or DoStatementSyntax or SwitchStatementSyntax or TryStatementSyntax
            or CatchClauseSyntax or UsingStatementSyntax or LockStatementSyntax;
        bool elseIf = child is IfStatementSyntax && child.Parent is ElseClauseSyntax;
        bool viaTry = child is CatchClauseSyntax;
        int next = block && !elseIf && !viaTry ? level + 1 : level;
        if (child is ElseClauseSyntax) next = level;
        best = Math.Max(best, Depth(child, next));
    }
    return best;
}

// М3: рядок без коментаря; імена → ID, числа → N, рядки → S (клони 1-го і 2-го типу).
static string Normalize(string line)
{
    var code = Regex.Replace(line, @"//.*$", "");
    var toks = SyntaxFactory.ParseTokens(code).Where(t => !t.IsKind(SyntaxKind.EndOfFileToken));
    return string.Join(" ", toks.Select(t => t.Kind() switch
    {
        SyntaxKind.IdentifierToken => "ID",
        SyntaxKind.NumericLiteralToken => "N",
        SyntaxKind.StringLiteralToken or SyntaxKind.CharacterLiteralToken => "S",
        _ => t.Text,
    })).Trim();
}

static List<string> FindClones(List<(string Where, List<(int Line, string Norm)> Lines)> units, int min)
{
    var seen = new Dictionary<string, List<(int U, int I)>>();
    for (int u = 0; u < units.Count; u++)
        for (int i = 0; i + min <= units[u].Lines.Count; i++)
        {
            var key = string.Join("\n", units[u].Lines.Skip(i).Take(min).Select(l => l.Norm));
            if (!seen.TryGetValue(key, out var list)) seen[key] = list = new();
            list.Add((u, i));
        }
    // Сусідні вікна подовжуємо до максимального збігу, а групи, що
    // перекриваються в тих самих методах, зливаємо в одну родину клонів.
    var groups = new List<(int Len, List<(int U, int From, int To)> Occ)>();
    foreach (var hits in seen.Values.Where(h => h.Count > 1))
    {
        int len = min;
        while (true)
        {
            var next = hits.Select(h => h.I + len < units[h.U].Lines.Count
                ? units[h.U].Lines[h.I + len].Norm : null).ToList();
            if (next.Any(n => n is null) || next.Distinct().Count() != 1) break;
            len++;
        }
        var occ = hits.Select(h => (h.U, h.I, h.I + len - 1)).ToList();
        var same = groups.FindIndex(g => g.Occ.Any(a => occ.Any(b =>
            a.U == b.U && a.From <= b.Item3 && b.I <= a.To)));
        if (same < 0) { groups.Add((len, occ)); continue; }
        var merged = groups[same].Occ.ToList();
        foreach (var o in occ)
        {
            int k = merged.FindIndex(a => a.U == o.U && a.From <= o.Item3 && o.I <= a.To);
            if (k < 0) merged.Add(o);
            else merged[k] = (o.U, Math.Min(merged[k].From, o.I), Math.Max(merged[k].To, o.Item3));
        }
        groups[same] = (Math.Max(groups[same].Len, len), merged);
    }
    return groups.Select(g => $"до {g.Len} рядк., {g.Occ.Count} входж.: " + string.Join(", ",
        g.Occ.Select(o => $"{units[o.U].Where} [{units[o.U].Lines[o.From].Line}-{units[o.U].Lines[o.To].Line}]"))).ToList();
}

record Row(string File, string Class, string Method, int Line, int Loc, int Cc, int Nest, int Pars, int FanOut);
