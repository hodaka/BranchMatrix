using System;

namespace Demo;

public class OrderService
{
    // && と || の混在 → 短絡を考慮した分岐表
    public decimal CalcDiscount(int qty, bool isMember, decimal price)
    {
        if (qty >= 10 && isMember)
            return price * 0.8m;

        if (isMember || price > 10000)
            return price * 0.95m;

        return price;
    }

    // 否定 + 三項
    public string Status(bool active, bool blocked)
    {
        var label = active && !blocked ? "OK" : "NG";
        return label;
    }

    // switch 文（パターン + when）
    public int Rank(int score)
    {
        switch (score)
        {
            case >= 90: return 1;
            case >= 70 when score < 90: return 2;
            case >= 50: return 3;
            default: return 4;
        }
    }

    // switch 式
    public string Grade(int score) => score switch
    {
        >= 80 => "A",
        >= 60 => "B",
        _ => "C",
    };

    // 分岐なし（既定では出力対象外）
    public int Echo(int x) => x;
}
