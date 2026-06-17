using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using BranchMatrix.Core;
using Microsoft.Win32;

namespace BranchMatrix.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<MethodRow> _rows = new();
    private IReadOnlyList<MethodAnalysis> _analysis = Array.Empty<MethodAnalysis>();

    public MainWindow()
    {
        InitializeComponent();
        MethodsGrid.ItemsSource = _rows;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "C# ソース (*.cs)|*.cs|すべてのファイル (*.*)|*.*",
            Title = "解析する .cs ファイルを選択",
        };
        if (dlg.ShowDialog() == true)
        {
            PathBox.Text = dlg.FileName;
            Analyze();
        }
    }

    private void Analyze_Click(object sender, RoutedEventArgs e) => Analyze();

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            PathBox.Text = files[0];
            Analyze();
        }
    }

    private void Analyze()
    {
        var path = PathBox.Text?.Trim();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            SetStatus("ファイルが見つかりません。", error: true);
            return;
        }

        try
        {
            _analysis = CSharpBranchAnalyzer.AnalyzeFile(path);
            _rows.Clear();
            foreach (var m in _analysis.OrderBy(m => m.Line))
                _rows.Add(new MethodRow(m));

            var branching = _analysis.Count(m => m.HasBranches);
            ExportButton.IsEnabled = _analysis.Count > 0;
            SetStatus($"{_analysis.Count} メソッド検出（条件分岐あり: {branching}）。");
        }
        catch (Exception ex)
        {
            SetStatus($"解析エラー: {ex.Message}", error: true);
            ExportButton.IsEnabled = false;
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_analysis.Count == 0) return;

        var srcName = Path.GetFileNameWithoutExtension(PathBox.Text);
        var dlg = new SaveFileDialog
        {
            Filter = "Excel ブック (*.xlsx)|*.xlsx",
            FileName = $"{srcName}_チェックシート.xlsx",
            Title = "チェックシートの保存先",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            ExcelExporter.Export(_analysis, dlg.FileName, IncludeEmptyBox.IsChecked == true);
            SetStatus($"出力しました: {dlg.FileName}");

            if (MessageBox.Show("出力したExcelを開きますか？", "完了",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            SetStatus($"出力エラー: {ex.Message}", error: true);
        }
    }

    private void SetStatus(string text, bool error = false)
    {
        StatusText.Text = text;
        StatusText.Foreground = error
            ? System.Windows.Media.Brushes.Firebrick
            : System.Windows.Media.Brushes.DimGray;
    }

    private sealed class MethodRow
    {
        public MethodRow(MethodAnalysis m)
        {
            Name = m.Name;
            Line = m.Line;
            DecisionCount = m.Decisions.Count;
            Signature = m.Signature;
        }

        public string Name { get; }
        public int Line { get; }
        public int DecisionCount { get; }
        public string Signature { get; }
    }
}
