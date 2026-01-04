using System;

namespace LARS.ENGINE.Documents.DailyPlan;

public class ModelInfo
{
    public string WorkOrder { get; set; } = "";
    public string FullName { get; private set; } = "";
    public string Number { get; private set; } = "";
    public string SpecNumber { get; private set; } = "";
    public string Spec { get; private set; } = "";
    public string Type { get; private set; } = "";
    public string Species { get; private set; } = ""; // 2 chars of Type + 2 chars of Spec
    public string TySpec { get; private set; } = ""; // 2 chars of Type + Spec
    public string Color { get; private set; } = "";
    public string Suffix { get; private set; } = "";

    // For tracking position in Excel
    public int Row { get; set; }
    public int Col { get; set; }

    public ModelInfo() { }

    public ModelInfo(string fullName)
    {
        SetFullName(fullName);
    }

    public void SetFullName(string fullName)
    {
        if (FullName == fullName) return;
        ParseModelInfo(fullName);
    }

    /// <summary>
    /// Parses the model name string into components.
    /// Logic ported from VBA 'ModelInfo.cls' -> 'ParseModelinfo'
    /// Example: "LSGL6335F.A"
    /// </summary>
    private void ParseModelInfo(string fullName)
    {
        FullName = fullName;
        if (string.IsNullOrWhiteSpace(fullName)) return;

        int dotIndex = fullName.IndexOf('.');
        if (dotIndex == -1)
        {
            // Handle case where no dot exists (safe fallback)
            Number = fullName;
            Suffix = "";
        }
        else
        {
            Number = fullName.Substring(0, dotIndex);
            Suffix = fullName.Substring(dotIndex + 1);
        }

        // Logic based on VBA:
        // vSpec = mid(vNumber, 5, 4) (VBA 1-based start, length 4) -> C# Index 4, Length 4
        // vType = Left(vNumber, 4) -> C# Substring(0, 4)
        
        if (Number.Length >= 8)
        {
             Type = Number.Substring(0, 4);
             Spec = Number.Substring(4, 4); // Length 4 usually? VBA says "mid(vNumber, 5, 4)"
             
             // vSpecies = Left(vType, 2) & Left(vSpec, 2)
             Species = (Type.Length >= 2 ? Type.Substring(0, 2) : Type) + 
                       (Spec.Length >= 2 ? Spec.Substring(0, 2) : Spec);

             // vSpecNumber = vType & vSpec
             SpecNumber = Type + Spec;

             // vTnS = Left(vType, 2) & vSpec
             TySpec = (Type.Length >= 2 ? Type.Substring(0, 2) : Type) + Spec;
             
             // vColor = mid(vNumber, 9) (VBA 9 means index 8 in C#?)
             if (Number.Length > 8)
                 Color = Number.Substring(8);
             else
                 Color = "";
        }
        else
        {
            // Fallback for short names
            Type = Number;
            Spec = "";
            Species = Number;
            SpecNumber = Number;
            TySpec = Number;
            Color = "";
        }
    }
}
