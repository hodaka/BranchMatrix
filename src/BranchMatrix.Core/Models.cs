using System.Collections.Generic;

namespace BranchMatrix.Core;

/// <summary>1メソッド（=1シート）の解析結果。</summary>
public sealed class MethodAnalysis
{
    /// <summary>メソッド名（シート名のもと）。</summary>
    public required string Name { get; init; }

    /// <summary>シート見出しに出す宣言シグネチャ。</summary>
    public required string Signature { get; init; }

    /// <summary>宣言の開始行（1始まり）。</summary>
    public required int Line { get; init; }

    /// <summary>メソッド内の条件分岐テーブル（出現順）。</summary>
    public required IReadOnlyList<DecisionTable> Decisions { get; init; }

    public bool HasBranches => Decisions.Count > 0;
}

/// <summary>1つの if / 三項 / switch に対応する分岐表。</summary>
public sealed class DecisionTable
{
    /// <summary>"if" / "三項" / "switch" / "switch式"。</summary>
    public required string Kind { get; init; }

    /// <summary>分岐の開始行（1始まり）。</summary>
    public required int Line { get; init; }

    /// <summary>表の見出し（元の条件式を含む）。</summary>
    public required string Title { get; init; }

    /// <summary>原子条件の見出し（左→右の出現順）。switch では空。</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>分岐行。</summary>
    public required IReadOnlyList<BranchRow> Rows { get; init; }
}

/// <summary>分岐表の1行。</summary>
public sealed class BranchRow
{
    /// <summary>分岐先ラベル（then / else / case 値 など）。</summary>
    public required string Outcome { get; init; }

    /// <summary>各原子条件の値（"T" / "F" / "−"）。Columns と整列。</summary>
    public required IReadOnlyList<string> Cells { get; init; }
}
