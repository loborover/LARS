using System;

namespace ENGINE.Documents;

/// <summary>
/// ModelInfo 공통 메타 정보
/// (모델번호, 별명, 종류, 연료, 등급, 색상, 수출대상국가, 개발단계 등)
/// </summary>
public class ModelInfo
{
    public string? sModelNum { get; } 
    public string? sNickname { get; } 
    public string? sTypes { get; } 
    public string? sFuel { get; } 
    public string? sGrade { get; } 
    public string? sColor { get; } 
    public string? sCustomer { get; } 
    public string? sDevLevel { get; } 

    ///ProductModel

    public ModelInfo( string? modelnumber = null, 
                    string? nickname = null,
                    string? types = null,
                    string? fuel = null,
                    string? grade = null,
                    string? color = null,
                    string? customer = null,
                    string? devlevel = null
    )
    {
        sModelNum = modelnumber;
        sNickname = nickname;
        sTypes = types;
        sFuel = fuel;
        sGrade = grade;
        sColor = color;
        sCustomer = customer;
        sDevLevel = devlevel;
    }
}
