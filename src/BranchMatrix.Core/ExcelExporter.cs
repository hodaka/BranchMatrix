using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace BranchMatrix.Core;

/// <summary>解析結果を ClosedXML でチェックシート（メソッド毎に1シート）に出力する。</summary>
public static class ExcelExporter
{
    public static void Export(IReadOnlyList<MethodAnalysis> methods, string outputPath, bool includeEmpty = false)
    {
        using var wb = new XLWorkbook();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var targets = methods.Where(m => includeEmpty || m.HasBranches).ToList();
        foreach (var m in targets)
        {
            var ws = wb.Worksheets.Add(UniqueSheetName(m.Name, used));
            WriteMethodSheet(ws, m);
        }

        if (!wb.Worksheets.Any())
            wb.Worksheets.Add("（分岐なし）").Cell(1, 1).Value = "条件分岐を含むメソッドはありませんでした。";

        wb.SaveAs(outputPath);
    }

    private static void WriteMethodSheet(IXLWorksheet ws, MethodAnalysis m)
    {
        int r = 1;
        ws.Cell(r, 1).Value = m.Signature;
        ws.Cell(r, 1).Style.Font.Bold = true;
        ws.Cell(r, 1).Style.Font.FontSize = 12;
        r += 2;

        if (m.Decisions.Count == 0)
        {
            ws.Cell(r, 1).Value = "（条件分岐なし）";
            ws.Columns().AdjustToContents();
            return;
        }

        foreach (var d in m.Decisions)
        {
            int totalCols = 1 + d.Columns.Count + 1; // 分岐 + 条件列... + 確認

            // 見出し
            var title = ws.Cell(r, 1);
            title.Value = d.Title;
            title.Style.Font.Bold = true;
            title.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
            ws.Range(r, 1, r, totalCols).Merge();
            r++;

            // ヘッダ
            ws.Cell(r, 1).Value = "分岐";
            for (int i = 0; i < d.Columns.Count; i++)
                ws.Cell(r, 2 + i).Value = d.Columns[i];
            ws.Cell(r, totalCols).Value = "確認";
            var header = ws.Range(r, 1, r, totalCols);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.Gainsboro;
            r++;

            int dataStart = r;
            foreach (var row in d.Rows)
            {
                ws.Cell(r, 1).Value = row.Outcome;
                for (int i = 0; i < row.Cells.Count; i++)
                {
                    var c = ws.Cell(r, 2 + i);
                    c.Value = row.Cells[i];
                    c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    if (row.Cells[i] == "T") c.Style.Fill.BackgroundColor = XLColor.FromHtml("#E6F4EA");
                    else if (row.Cells[i] == "F") c.Style.Fill.BackgroundColor = XLColor.FromHtml("#FCE8E6");
                }
                var chk = ws.Cell(r, totalCols);
                chk.Value = "□";
                chk.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                r++;
            }

            // 罫線（ヘッダ＋データ全体）
            var block = ws.Range(dataStart - 1, 1, r - 1, totalCols);
            block.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            block.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            r += 1; // テーブル間の空行
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    /// <summary>Excel のシート名制約（31文字・禁則文字・一意）に収める。</summary>
    private static string UniqueSheetName(string raw, HashSet<string> used)
    {
        char[] invalid = { '[', ']', ':', '*', '?', '/', '\\' };
        var name = new string(raw.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        if (string.IsNullOrWhiteSpace(name)) name = "sheet";
        if (name.Length > 31) name = name[..31];

        var baseName = name;
        int n = 2;
        while (used.Contains(name))
        {
            var suffix = $"_{n++}";
            name = baseName.Length + suffix.Length > 31
                ? baseName[..(31 - suffix.Length)] + suffix
                : baseName + suffix;
        }
        used.Add(name);
        return name;
    }
}
