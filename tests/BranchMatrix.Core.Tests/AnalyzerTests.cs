using System.IO;
using System.Linq;
using BranchMatrix.Core;
using Xunit;

namespace BranchMatrix.Core.Tests;

public class AnalyzerTests
{
    private const string Sample = @"
class C
{
    int Foo(int x, object y, bool flag)
    {
        if (x > 0 && y != null)
            return 1;

        if (flag || x < 0)
            return 2;

        return x switch
        {
            0 => 10,
            > 0 => 20,
            _ => 30,
        };
    }

    void Bar() { } // 分岐なし
}";

    [Fact]
    public void Detects_one_method_per_function()
    {
        var methods = CSharpBranchAnalyzer.AnalyzeSource(Sample);
        Assert.Contains(methods, m => m.Name == "Foo");
        Assert.Contains(methods, m => m.Name == "Bar");
    }

    [Fact]
    public void Bar_has_no_branches()
    {
        var bar = CSharpBranchAnalyzer.AnalyzeSource(Sample).Single(m => m.Name == "Bar");
        Assert.False(bar.HasBranches);
    }

    [Fact]
    public void If_table_decomposes_into_atomic_conditions()
    {
        var foo = CSharpBranchAnalyzer.AnalyzeSource(Sample).Single(m => m.Name == "Foo");
        var firstIf = foo.Decisions.First(d => d.Kind == "if");

        Assert.Equal(new[] { "x > 0", "y != null" }, firstIf.Columns.ToArray());

        // then は (T,T)
        var thenRow = firstIf.Rows.Single(r => r.Outcome == "then");
        Assert.Equal(new[] { "T", "T" }, thenRow.Cells.ToArray());

        // else は (F,−) と (T,F)
        var elseRows = firstIf.Rows.Where(r => r.Outcome == "else").ToList();
        Assert.Equal(2, elseRows.Count);
        Assert.Contains(elseRows, r => r.Cells.SequenceEqual(new[] { "F", "−" }));
        Assert.Contains(elseRows, r => r.Cells.SequenceEqual(new[] { "T", "F" }));
    }

    [Fact]
    public void Switch_expression_lists_each_arm()
    {
        var foo = CSharpBranchAnalyzer.AnalyzeSource(Sample).Single(m => m.Name == "Foo");
        var sw = foo.Decisions.Single(d => d.Kind == "switch式");
        Assert.Equal(3, sw.Rows.Count);
        Assert.Contains(sw.Rows, r => r.Outcome.Contains("_")); // default arm
    }

    [Fact]
    public void Export_creates_xlsx_with_sheet_per_branching_method()
    {
        var methods = CSharpBranchAnalyzer.AnalyzeSource(Sample);
        var path = Path.Combine(Path.GetTempPath(), $"bm_test_{System.Guid.NewGuid():N}.xlsx");
        try
        {
            ExcelExporter.Export(methods, path);
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
