using System;
using System.Collections.Generic;
using System.Linq;

namespace LARS.ENGINE.Documents.DailyPlan;

public enum ModelInfoField
{
    WorkOrder = 901,
    FullName = 902,
    Number = 903,
    SpecNumber = 904,
    Spec = 905,
    Type = 906,
    Species = 907,
    TySpec = 908,
    Color = 909,
    Suffix = 910
}

public enum GroupType { MainG, SubG }

public class GroupRange
{
    public int StartRow { get; set; }
    public int EndRow { get; set; }
    public GroupType Type { get; set; }
    public ModelInfo? Info { get; set; } // Representative info
}

public class ModelGrouper
{
    // C# Implementation of AR_2_ModelGrouping & D_Maps logic

    public List<GroupRange> MainGroups { get; private set; } = new();
    public List<GroupRange> SubGroups { get; private set; } = new();

    /// <summary>
    /// Groups the list of ModelInfos into Main and Sub groups based on VBA logic.
    /// </summary>
    /// <param name="models">List of models sorted by row order</param>
    public void GroupModels(List<ModelInfo> models)
    {
        MainGroups.Clear();
        SubGroups.Clear();

        if (models == null || models.Count == 0) return;

        // Sub Grouping Logic (AR_2_ModelGrouping Loop 1)
        ProcessSubGrouping(models);

        // Main Grouping Logic (AR_2_ModelGrouping Loop 2 equivalent)
        // Note: VBA Main Grouping iterated over SubLots and grouped them.
        ProcessMainGrouping();
    }

    private void ProcessSubGrouping(List<ModelInfo> models)
    {
        int startRow = models[0].Row;
        int currentStartRow = startRow;
        ModelInfoField criterionField = ModelInfoField.SpecNumber; // Default? Not explicitly set in VBA init usually

        // We iterate through models.
        // VBA logic: Checks Current vs Next.
        // If changed, creates a Lot (Group).

        for (int i = 0; i < models.Count - 1; i++)
        {
            var curr = models[i];
            var next = models[i + 1];

            bool isSplitting = false;

            // Logic from VBA Loop:
            // If StartRow_Prcss == 0 Then (Beginning of a potential group)
            //   Check Criteria Preference: SpecNumber > TySpec (if not LS63) > Species (if not LS63)
            //   Set CriterionField
            // ElseIf Not Compare2Models(curr, next, CriterionField) Then
            //   End Group
            
            // Wait, the VBA logic is slightly stateful about 'CriterionField'.
            // "If StartRow_Prcss = 0 Then" means we are looking for a break point to determine WHAT defines the current group?
            // Actually, in VBA "StartRow_Prcss" seems to track the start of the CURRENT group being built.
            // If it's valid, it checks if the *CriterionField* still holds.
            
            // Let's simplified version suited for C#:
            // We want to group items that are "Similar enough".
            // The VBA logic dynamically selects the "Criterion" based on the transition from the FIRST item of the group to the Second.
            
            // Re-reading VBA:
            // If StartRow_Prcss = 0 Then
            //    If Same(SpecNumber) -> StartRow = Curr.Row, Criterion = SpecNumber
            //    ElseIf Species != "LS63"
            //        If Same(TySpec) -> StartRow = Curr.Row, Criterion = TySpec
            //        ElseIf Same(Species) -> StartRow = Curr.Row, Criterion = Species
            //        Else -> (Single Item Group effectively, or break immediately?)
            //    End If
            // ElseIf Not Same(Criterion) Then
            //    End Group
            
            // My interpretation:
            // It tries to find the "Tightest" bond between the first two items of a block.
            // If they share SpecNumber, then the whole block must share SpecNumber.
            // If they only share TySpec (and not LS63), then block must share TySpec.
            // If they only share Species, block must share Species.
            // If they share nothing, the first item is a single-item group (implied).

            if (currentStartRow == 0) currentStartRow = curr.Row; // Should be set.

            // If we are just starting a new group (or effectively treating this index as start for criterion check)
            // Actually VBA: StartRow_Prcss is reset to 0 after closing a group.
            // So if StartRow_Prcss != 0, we are IN a group.
            
            // Let's track the active criterion for the current group.
            // If we are "continuing" a group, we check if it holds.
            
            // But verify: The VBA sets StartRow_Prcss ONLY when it finds a match.
            // What if it doesn't find a match (Single item)?
            // It seems the loop increments.
            
            // Let's replicate this state machine.
            int groupStartRow = -1;
            ModelInfoField activeCriterion = ModelInfoField.SpecNumber; 
            
            // Redoing the loop structure to match VBA closely
            int startRowProcess = 0; // 0 means "Not currently inside a multi-item group definition"?
            // Actually "StartRow_Prcss = 0" implies we haven't established the *start* of the group yet?
            // No, "If StartRow_Prcss = 0 Then StartRow_Prcss = CurrRow" (VBA Line 779)
            // So it ALWAYS sets it.
            
            // Wait, look at VBA line 802 "If StartRow_Prcss = 0 Then" inside the block?
            // Ah, Line 779 sets it to CurrRow.
            // But Line 802 checks it again? No.
            // Line 779: If StartRow_Prcss = 0 Then StartRow_Prcss = CurrRow
            // Line 802: If StartRow_Prcss = 0 Then ... (This condition seems impossible if 779 executed?)
            // Look closer at VBA dump (Step 1042):
            // 779: If StartRow_Prcss = 0 Then StartRow_Prcss = CurrRow
            // ...
            // 802: If StartRow_Prcss = 0 Then (Wait, this is inside `Compare2Models`? No.)
            // The dump lines might be disjointed or logic is weird.
            // Ah, Line 802 is "If StartRow_Prcss = 0 Then".
            // Maybe line 779 logic was from a previous version or I misread?
            // Let's re-read Step 1042 carefully.
            
            // 779: If StartRow_Prcss = 0 Then StartRow_Prcss = CurrRow
            // 780: If CurrRow <> startRow Then Checker.NextModel ...
            // 784: If Checker.Crr.Number <> Checker.Nxt.Number Then
            //      EndRow = CurrRow; Marker.Set_Lot... StartRow_Prcss = 0
            
            // Wait, this is `MainOrSub = SubG` logic (Lines 777-791).
            // It uses `Checker.Crr.Number <> Checker.Nxt.Number` (Full Model Number).
            // So SubGroups are strictly "Same Model Number".
            // SIMPLE!
            
            // The complex logic (802+) is for MAIN GROUP (Lines 793+).
            // Ok, I was confusing the two.
            
        }
        
        // Re-implementing correctly:
        
        // 1. Sub Grouping: Sequential items with SAME Full Number.
        int subGroupStartRow = models[0].Row;
        for (int i = 0; i < models.Count; i++)
        {
            var curr = models[i];
            bool isLast = (i == models.Count - 1);
            
            if (isLast)
            {
                // Close current group
                SubGroups.Add(new GroupRange { StartRow = subGroupStartRow, EndRow = curr.Row, Type = GroupType.SubG, Info = curr });
            }
            else
            {
                var next = models[i+1];
                // Compare Number (e.g. LSGL6335F.A vs LSGL6335F.B?)
                // VBA says "Checker.Crr.Number <> Checker.Nxt.Number"
                // ModelInfo.Number is "LSGL6335F" (without suffix).
                
                if (curr.Number != next.Number)
                {
                    // Close group
                    SubGroups.Add(new GroupRange { StartRow = subGroupStartRow, EndRow = curr.Row, Type = GroupType.SubG, Info = curr });
                    subGroupStartRow = next.Row;
                }
            }
        }
    }

    private void ProcessMainGrouping()
    {
        // Now grouping the SubGroups into MainGroups.
        // VBA logic (Lines 793+):
        // Iterates through SubGroups (Marker.Sub_Lot).
        
        if (SubGroups.Count == 0) return;

        int startPrcss = 0; // Used to track start Row of MainGroup
        ModelInfoField criterion = ModelInfoField.SpecNumber;
        
        // We iterate SubGroups to merge them.
        // Let's assume we map SubGroups index 0 to N.
        
        int groupStartRow = SubGroups[0].StartRow;
        startPrcss = 0; // State flag: 0 = looking for criterion, >0 = inside group with criterion

        for (int i = 0; i < SubGroups.Count - 1; i++)
        {
            var currLot = SubGroups[i];
            var nextLot = SubGroups[i+1];
            
            var curr = currLot.Info!;
            var next = nextLot.Info!;

            if (startPrcss == 0)
            {
                // Deciding the Criterion for this new group based on Curr vs Next
                startPrcss = currLot.StartRow; // Tentative start
                if (Compare(curr, next, ModelInfoField.SpecNumber))
                {
                    criterion = ModelInfoField.SpecNumber;
                }
                else if (curr.Species != "LS63")
                {
                     if (Compare(curr, next, ModelInfoField.TySpec))
                         criterion = ModelInfoField.TySpec;
                     else if (Compare(curr, next, ModelInfoField.Species))
                         criterion = ModelInfoField.Species;
                     else
                     {
                         // No match, so Curr is its own group?
                         // VBA: "ElseIf Not Checker.Compare... Then Close Group"
                         // If we fail to set criterion here, we basically admit these two don't merge.
                         // So we close the group containing just Curr?
                         
                         // If we are here, it means we entered `startPrcss = 0`.
                         // If no match found, we effectively close the group immediately at `EndRow` of Curr.
                         // But the loop continues.
                         
                         // We should add Curr as a MainGroup (Single Lot).
                         MainGroups.Add(new GroupRange { StartRow = currLot.StartRow, EndRow = currLot.EndRow, Type = GroupType.MainG, Info = curr });
                         
                         startPrcss = 0; // Reset for Next
                         continue; // Next iteration (Next becomes Curr)
                     }
                }
                else
                {
                     // LS63 and SpecNumber diff -> No merge?
                     MainGroups.Add(new GroupRange { StartRow = currLot.StartRow, EndRow = currLot.EndRow, Type = GroupType.MainG, Info = curr });
                     startPrcss = 0;
                     continue; 
                }
            }
            else
            {
                // We are inside a group defined by `criterion`.
                // Check if Next adheres to it.
                if (!Compare(curr, next, criterion))
                {
                    // Close the group
                    MainGroups.Add(new GroupRange { StartRow = startPrcss, EndRow = currLot.EndRow, Type = GroupType.MainG, Info = curr });
                    startPrcss = 0; // Reset
                }
            }
        }
        
        // Handle the last item
        var lastLot = SubGroups.Last();
        if (startPrcss != 0)
        {
            // Close open group
            MainGroups.Add(new GroupRange { StartRow = startPrcss, EndRow = lastLot.EndRow, Type = GroupType.MainG, Info = lastLot.Info });
        }
        else
        {
            // Single group
            MainGroups.Add(new GroupRange { StartRow = lastLot.StartRow, EndRow = lastLot.EndRow, Type = GroupType.MainG, Info = lastLot.Info });
        }
    }

    private bool Compare(ModelInfo a, ModelInfo b, ModelInfoField field)
    {
        return field switch
        {
            ModelInfoField.WorkOrder => a.WorkOrder == b.WorkOrder,
            ModelInfoField.FullName => a.FullName == b.FullName,
            ModelInfoField.Number => a.Number == b.Number,
            ModelInfoField.SpecNumber => a.SpecNumber == b.SpecNumber,
            ModelInfoField.Spec => a.Spec == b.Spec,
            ModelInfoField.Type => a.Type == b.Type,
            ModelInfoField.Species => a.Species == b.Species,
            ModelInfoField.TySpec => a.TySpec == b.TySpec,
            ModelInfoField.Color => a.Color == b.Color,
            ModelInfoField.Suffix => a.Suffix == b.Suffix,
            _ => false
        };
    }
}
