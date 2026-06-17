using System;
using System.Collections.Generic;
using System.Linq;

namespace BranchMatrix.Core;

/// <summary>
/// 条件式の抽象表現（原子条件と &amp;&amp; / || / ! の木）。
/// Roslyn から切り離してテスト可能にするための中間モデル。
/// </summary>
public abstract record BoolExpr;

/// <summary>これ以上分解しない原子条件。Id は出現順に一意。</summary>
public sealed record Atom(int Id, string Text) : BoolExpr;
public sealed record AndExpr(BoolExpr Left, BoolExpr Right) : BoolExpr;
public sealed record OrExpr(BoolExpr Left, BoolExpr Right) : BoolExpr;
public sealed record NotExpr(BoolExpr Operand) : BoolExpr;

public enum TriState { False, True, NotEvaluated }

/// <summary>
/// 条件式が true（または false）に評価される1パターン。
/// atomId -&gt; 評価値。含まれない原子条件は短絡評価で未評価（NotEvaluated）。
/// </summary>
public sealed class ConditionPath
{
    private readonly Dictionary<int, bool> _values;

    public ConditionPath(Dictionary<int, bool> values) => _values = values;

    public IReadOnlyDictionary<int, bool> Values => _values;

    public TriState Of(int atomId)
        => _values.TryGetValue(atomId, out var v)
            ? (v ? TriState.True : TriState.False)
            : TriState.NotEvaluated;

    public static ConditionPath Single(int atomId, bool value)
        => new(new Dictionary<int, bool> { [atomId] = value });

    /// <summary>互いに素な原子集合のパスを合成する。</summary>
    public ConditionPath Merge(ConditionPath other)
    {
        var d = new Dictionary<int, bool>(_values);
        foreach (var kv in other._values) d[kv.Key] = kv.Value;
        return new ConditionPath(d);
    }
}

/// <summary>
/// 短絡評価を考慮して、条件式が true / false になるパターン群を列挙する。
/// </summary>
public static class ConditionPathExpander
{
    public static (IReadOnlyList<ConditionPath> True, IReadOnlyList<ConditionPath> False) Expand(BoolExpr expr)
    {
        switch (expr)
        {
            case Atom a:
                return (new[] { ConditionPath.Single(a.Id, true) },
                        new[] { ConditionPath.Single(a.Id, false) });

            case NotExpr n:
            {
                var (t, f) = Expand(n.Operand);
                return (f, t); // ! は真偽を入れ替えるだけ
            }

            case AndExpr and:
            {
                var (lt, lf) = Expand(and.Left);
                var (rt, rf) = Expand(and.Right);
                // true : 左true かつ 右true
                var truePaths = (from a in lt from b in rt select a.Merge(b)).ToList();
                // false: 左false（短絡で右は未評価） | 左true かつ 右false
                var falsePaths = lf.Concat(from a in lt from b in rf select a.Merge(b)).ToList();
                return (truePaths, falsePaths);
            }

            case OrExpr or:
            {
                var (lt, lf) = Expand(or.Left);
                var (rt, rf) = Expand(or.Right);
                // true : 左true（短絡で右は未評価） | 左false かつ 右true
                var truePaths = lt.Concat(from a in lf from b in rt select a.Merge(b)).ToList();
                // false: 左false かつ 右false
                var falsePaths = (from a in lf from b in rf select a.Merge(b)).ToList();
                return (truePaths, falsePaths);
            }

            default:
                throw new NotSupportedException($"未対応の式: {expr.GetType().Name}");
        }
    }
}
