using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BranchMatrix.Core;

/// <summary>
/// C# ソースを Roslyn で解析し、メソッドごとの条件分岐表を組み立てる。
/// </summary>
public static class CSharpBranchAnalyzer
{
    public static IReadOnlyList<MethodAnalysis> AnalyzeFile(string path)
        => AnalyzeSource(File.ReadAllText(path));

    public static IReadOnlyList<MethodAnalysis> AnalyzeSource(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();

        var results = new List<MethodAnalysis>();
        foreach (var fn in root.DescendantNodes().Where(IsFunction))
        {
            results.Add(new MethodAnalysis
            {
                Name = FunctionName(fn),
                Signature = FunctionSignature(fn),
                Line = LineOf(fn),
                Decisions = CollectDecisions(fn),
            });
        }
        return results;
    }

    // ---- メソッド境界の判定 -------------------------------------------------

    private static bool IsFunction(SyntaxNode n)
        => n is MethodDeclarationSyntax or LocalFunctionStatementSyntax or ConstructorDeclarationSyntax;

    private static SyntaxNode? NearestFunction(SyntaxNode node)
        => node.Ancestors().FirstOrDefault(IsFunction);

    private static IReadOnlyList<DecisionTable> CollectDecisions(SyntaxNode fn)
    {
        var tables = new List<DecisionTable>();
        foreach (var node in fn.DescendantNodes())
        {
            // 入れ子のローカル関数などは別シート側で拾うのでスキップ
            if (NearestFunction(node) != fn) continue;

            switch (node)
            {
                case IfStatementSyntax ifs:
                    tables.Add(BuildBooleanTable("if", ifs.Condition, LineOf(ifs), "then", "else"));
                    break;
                case ConditionalExpressionSyntax ce:
                    tables.Add(BuildBooleanTable("三項", ce.Condition, LineOf(ce), "? (true)", ": (false)"));
                    break;
                case SwitchStatementSyntax sw:
                    tables.Add(BuildSwitchTable(sw));
                    break;
                case SwitchExpressionSyntax swe:
                    tables.Add(BuildSwitchExprTable(swe));
                    break;
            }
        }
        return tables.OrderBy(t => t.Line).ToList();
    }

    // ---- if / 三項：短絡を考慮した真偽パターン表 ----------------------------

    private static DecisionTable BuildBooleanTable(
        string kind, ExpressionSyntax condition, int line, string thenLabel, string elseLabel)
    {
        int counter = 0;
        var atoms = new List<Atom>();
        var expr = Build(condition, atoms, ref counter);
        var (truePaths, falsePaths) = ConditionPathExpander.Expand(expr);

        var rows = new List<BranchRow>();
        foreach (var p in truePaths) rows.Add(Row(thenLabel, p, atoms));
        foreach (var p in falsePaths) rows.Add(Row(elseLabel, p, atoms));

        return new DecisionTable
        {
            Kind = kind,
            Line = line,
            Title = $"{kind} ({condition})  @L{line}",
            Columns = atoms.Select(a => a.Text).ToList(),
            Rows = rows,
        };
    }

    private static BranchRow Row(string outcome, ConditionPath p, List<Atom> atoms)
        => new() { Outcome = outcome, Cells = atoms.Select(a => Cell(p.Of(a.Id))).ToList() };

    private static string Cell(TriState t) => t switch
    {
        TriState.True => "T",
        TriState.False => "F",
        _ => "−",
    };

    // ---- ExpressionSyntax -> BoolExpr ---------------------------------------

    private static BoolExpr Build(ExpressionSyntax expr, List<Atom> atoms, ref int counter)
    {
        switch (expr)
        {
            case ParenthesizedExpressionSyntax p:
                return Build(p.Expression, atoms, ref counter);

            case BinaryExpressionSyntax b when b.IsKind(SyntaxKind.LogicalAndExpression):
            {
                var l = Build(b.Left, atoms, ref counter);
                var r = Build(b.Right, atoms, ref counter);
                return new AndExpr(l, r);
            }

            case BinaryExpressionSyntax b when b.IsKind(SyntaxKind.LogicalOrExpression):
            {
                var l = Build(b.Left, atoms, ref counter);
                var r = Build(b.Right, atoms, ref counter);
                return new OrExpr(l, r);
            }

            case PrefixUnaryExpressionSyntax u when u.IsKind(SyntaxKind.LogicalNotExpression):
            {
                // !(A && B) のように中身が論理式なら分解。!flag のような単純否定はそのまま原子に。
                var inner = Unwrap(u.Operand);
                if (IsLogical(inner)) return new NotExpr(Build(inner, atoms, ref counter));
                return MakeAtom(u, atoms, ref counter);
            }

            default:
                return MakeAtom(expr, atoms, ref counter);
        }
    }

    private static BoolExpr MakeAtom(ExpressionSyntax expr, List<Atom> atoms, ref int counter)
    {
        var atom = new Atom(counter++, expr.ToString());
        atoms.Add(atom);
        return atom;
    }

    private static bool IsLogical(ExpressionSyntax e)
    {
        e = Unwrap(e);
        return e.IsKind(SyntaxKind.LogicalAndExpression)
            || e.IsKind(SyntaxKind.LogicalOrExpression)
            || e.IsKind(SyntaxKind.LogicalNotExpression);
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax e)
        => e is ParenthesizedExpressionSyntax p ? Unwrap(p.Expression) : e;

    // ---- switch -------------------------------------------------------------

    private static DecisionTable BuildSwitchTable(SwitchStatementSyntax sw)
    {
        var rows = new List<BranchRow>();
        foreach (var section in sw.Sections)
        foreach (var label in section.Labels)
            rows.Add(new BranchRow { Outcome = LabelText(label), Cells = Array.Empty<string>() });

        return new DecisionTable
        {
            Kind = "switch",
            Line = LineOf(sw),
            Title = $"switch ({sw.Expression})  @L{LineOf(sw)}",
            Columns = Array.Empty<string>(),
            Rows = rows,
        };
    }

    private static DecisionTable BuildSwitchExprTable(SwitchExpressionSyntax swe)
    {
        var rows = swe.Arms.Select(arm => new BranchRow
        {
            Outcome = arm.Pattern + (arm.WhenClause != null ? $" when {arm.WhenClause.Condition}" : ""),
            Cells = Array.Empty<string>(),
        }).ToList();

        return new DecisionTable
        {
            Kind = "switch式",
            Line = LineOf(swe),
            Title = $"switch式 ({swe.GoverningExpression})  @L{LineOf(swe)}",
            Columns = Array.Empty<string>(),
            Rows = rows,
        };
    }

    private static string LabelText(SwitchLabelSyntax label) => label switch
    {
        CaseSwitchLabelSyntax c => $"case {c.Value}",
        CasePatternSwitchLabelSyntax cp =>
            $"case {cp.Pattern}" + (cp.WhenClause != null ? $" when {cp.WhenClause.Condition}" : ""),
        DefaultSwitchLabelSyntax => "default",
        _ => label.ToString(),
    };

    // ---- 関数メタ情報 -------------------------------------------------------

    private static string FunctionName(SyntaxNode fn) => fn switch
    {
        MethodDeclarationSyntax m => m.Identifier.Text,
        LocalFunctionStatementSyntax lf => lf.Identifier.Text,
        ConstructorDeclarationSyntax c => $"{c.Identifier.Text}.ctor",
        _ => "func",
    };

    private static string FunctionSignature(SyntaxNode fn) => fn switch
    {
        MethodDeclarationSyntax m => Collapse($"{m.Modifiers} {m.ReturnType} {m.Identifier}{m.ParameterList}"),
        LocalFunctionStatementSyntax lf => Collapse($"{lf.Modifiers} {lf.ReturnType} {lf.Identifier}{lf.ParameterList}"),
        ConstructorDeclarationSyntax c => Collapse($"{c.Modifiers} {c.Identifier}{c.ParameterList}"),
        _ => fn.ToString(),
    };

    private static string Collapse(string s) => string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static int LineOf(SyntaxNode n) => n.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}
