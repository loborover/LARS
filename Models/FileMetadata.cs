using System.ComponentModel;

namespace LARS.Models;

public class FileMetadata
{
    [Category("File Info")]
    [Description("Name of the file")]
    public string Name { get; set; } = "";

    [Category("File Info")]
    [Description("Size in Kilobytes")]
    public long SizeKB { get; set; }

    [Category("Dates")]
    public DateTime Created { get; set; }

    [Category("Dates")]
    public DateTime Modified { get; set; }

    [Category("Location")]
    public string? Directory { get; set; }
}
