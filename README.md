# BranchMatrix

[![CI](https://github.com/hodaka/BranchMatrix/actions/workflows/ci.yml/badge.svg)](https://github.com/hodaka/BranchMatrix/actions/workflows/ci.yml)

C# ソース（`.cs`）を静的解析し、**メソッドごとに条件分岐の組み合わせチェックシート（Excel）** を生成するオフラインツール。

## 仕組み

- **解析**: Roslyn（`Microsoft.CodeAnalysis.CSharp`）で構文解析
- **分岐展開**: `if` / 三項演算子 / `switch`（文・式）を抽出。`&&` / `||` / `!` を**原子条件に分解**し、**短絡評価を考慮**して then/else に到達する条件パターンを列挙
- **出力**: ClosedXML で `.xlsx` を生成（Excel本体・ネット接続不要）。メソッド1つにつき1シート

短絡評価の例 — `if (qty >= 10 && isMember)`:

| 分岐 | qty >= 10 | isMember | 確認 |
|------|-----------|----------|------|
| then | T | T | □ |
| else | F | −（未評価） | □ |
| else | T | F | □ |

## 構成

```
BranchMatrix.sln
├ src/BranchMatrix.Core   … 解析・分岐展開・Excel出力（ロジック本体）
├ src/BranchMatrix.App    … WPF (.NET 9) UI
└ tests/BranchMatrix.Core.Tests … xUnit
```

## 使い方

```powershell
dotnet run --project src/BranchMatrix.App
```

1. `.cs` を選択（またはウィンドウにドラッグ＆ドロップ）→「解析」
2. メソッド一覧と分岐数を確認
3. 「Excel出力...」で保存先を指定

`samples/Sample.cs` が動作確認用サンプル。

## ビルド / テスト

```powershell
dotnet build
dotnet test
```

> 初回の `dotnet restore` のみネットが必要（NuGet取得）。以降の解析・出力は完全オフライン。
