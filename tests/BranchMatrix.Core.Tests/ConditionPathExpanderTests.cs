using System.Linq;
using BranchMatrix.Core;
using Xunit;

namespace BranchMatrix.Core.Tests;

public class ConditionPathExpanderTests
{
    private static Atom A(int id) => new(id, $"c{id}");

    [Fact]
    public void Atom_true_and_false_paths()
    {
        var (t, f) = ConditionPathExpander.Expand(A(0));

        Assert.Single(t);
        Assert.Single(f);
        Assert.Equal(TriState.True, t[0].Of(0));
        Assert.Equal(TriState.False, f[0].Of(0));
    }

    [Fact]
    public void And_short_circuits_on_left_false()
    {
        // A && B
        var expr = new AndExpr(A(0), A(1));
        var (t, f) = ConditionPathExpander.Expand(expr);

        // true は (T,T) の1通り
        Assert.Single(t);
        Assert.Equal(TriState.True, t[0].Of(0));
        Assert.Equal(TriState.True, t[0].Of(1));

        // false は (F,−) と (T,F) の2通り
        Assert.Equal(2, f.Count);
        Assert.Equal(TriState.False, f[0].Of(0));
        Assert.Equal(TriState.NotEvaluated, f[0].Of(1)); // 短絡で未評価
        Assert.Equal(TriState.True, f[1].Of(0));
        Assert.Equal(TriState.False, f[1].Of(1));
    }

    [Fact]
    public void Or_short_circuits_on_left_true()
    {
        // A || B
        var expr = new OrExpr(A(0), A(1));
        var (t, f) = ConditionPathExpander.Expand(expr);

        // true は (T,−) と (F,T) の2通り
        Assert.Equal(2, t.Count);
        Assert.Equal(TriState.True, t[0].Of(0));
        Assert.Equal(TriState.NotEvaluated, t[0].Of(1));
        Assert.Equal(TriState.False, t[1].Of(0));
        Assert.Equal(TriState.True, t[1].Of(1));

        // false は (F,F) の1通り
        Assert.Single(f);
        Assert.Equal(TriState.False, f[0].Of(0));
        Assert.Equal(TriState.False, f[0].Of(1));
    }

    [Fact]
    public void Not_swaps_true_and_false()
    {
        var (t, f) = ConditionPathExpander.Expand(new NotExpr(A(0)));

        Assert.Equal(TriState.False, t[0].Of(0)); // !A が true ⇔ A が false
        Assert.Equal(TriState.True, f[0].Of(0));
    }

    [Fact]
    public void Nested_and_or_keeps_short_circuit()
    {
        // (A && B) || C
        var expr = new OrExpr(new AndExpr(A(0), A(1)), A(2));
        var (t, f) = ConditionPathExpander.Expand(expr);

        // false: (A&&B) が false かつ C が false
        //   = {A=F, C=F}, {A=T,B=F, C=F}
        Assert.Equal(2, f.Count);
        Assert.All(f, p => Assert.Equal(TriState.False, p.Of(2)));

        // true パターンには必ず式全体が true になる組み合わせが含まれる
        Assert.Contains(t, p => p.Of(2) == TriState.True);                       // C 短絡
        Assert.Contains(t, p => p.Of(0) == TriState.True && p.Of(1) == TriState.True); // A&&B
    }
}
