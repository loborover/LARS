using System;

namespace ENGINE.Documents;

/// <summary>
/// 각 item별 공통 메타 정보
/// (파트별명, 파트번호, 파트단위, 제조사 등)
/// </summary>
public class itemUnit
{
    public string? NickName { get; } 
    public string? PartNumber { get; } 
    public long QTY { get; } 
    public string? Vender { get; } 

    public itemUnit(string? nickname = null, string? partnumber = null, string? vender = "UnKnown", long qty = 1)
    {
        NickName = nickname;
        PartNumber = partnumber;
        QTY = qty;
        Vender = vender;
    }
}
