using ClosedXML.Excel;
using System;
using System.IO;

// === RAW FILE ===
Console.WriteLine("========== RAW FILE (Excel_Export_) ==========");
using var rawWb = new XLWorkbook(@"d:\Workshop\LARS\TestSet\1\Excel_Export_[0224_163233].xlsx");
var rawWs = rawWb.Worksheet(1);
int rawLastRow = rawWs.LastRowUsed()?.RowNumber() ?? 0;
int rawLastCol = rawWs.LastColumnUsed()?.ColumnNumber() ?? 0;
Console.WriteLine($"Rows: {rawLastRow}, Cols: {rawLastCol}");

for (int r = 1; r <= Math.Min(rawLastRow, 6); r++)
{
    Console.Write($"Row{r}: ");
    for (int c = 1; c <= Math.Min(rawLastCol, 25); c++)
    {
        string val = rawWs.Cell(r, c).GetString().Replace("\n", "\\n");
        if (!string.IsNullOrWhiteSpace(val))
            Console.Write($"[{c}]{val} | ");
    }
    Console.WriteLine();
}

Console.WriteLine();

// === PROCESSED FILE ===
Console.WriteLine("========== PROCESSED FILE (DailyPlan 2월-26일) ==========");
using var procWb = new XLWorkbook(@"d:\Workshop\LARS\TestSet\1\DailyPlan 2월-26일_C11.xlsx");
var procWs = procWb.Worksheet(1);
int procLastRow = procWs.LastRowUsed()?.RowNumber() ?? 0;
int procLastCol = procWs.LastColumnUsed()?.ColumnNumber() ?? 0;
Console.WriteLine($"Rows: {procLastRow}, Cols: {procLastCol}");

for (int r = 1; r <= Math.Min(procLastRow, 6); r++)
{
    Console.Write($"Row{r}: ");
    for (int c = 1; c <= Math.Min(procLastCol, 25); c++)
    {
        string val = procWs.Cell(r, c).GetString().Replace("\n", "\\n");
        if (!string.IsNullOrWhiteSpace(val))
            Console.Write($"[{c}]{val} | ");
    }
    Console.WriteLine();
}

// Show all headers of both files
Console.WriteLine("\n========== RAW HEADERS (Row 1 ALL) ==========");
for (int c = 1; c <= rawLastCol; c++)
{
    string val = rawWs.Cell(1, c).GetString().Replace("\n", "\\n");
    Console.Write($"[{c}]{val} | ");
}
Console.WriteLine();

Console.WriteLine("\n========== PROCESSED HEADERS (Row 1 ALL) ==========");
for (int c = 1; c <= procLastCol; c++)
{
    string val = procWs.Cell(1, c).GetString().Replace("\n", "\\n");
    Console.Write($"[{c}]{val} | ");
}
Console.WriteLine();
