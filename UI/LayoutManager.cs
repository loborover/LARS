using System;

namespace LARS.UI;

public static class LayoutManager
{
    // Constants
    public const int LeftPanelMinSize = 400; // User Request
    public const int MetaPanelMinHeight = 170; // User Request Updated
    public const int HeaderPanelHeight = 50; 
    public const int TopMenuHeight = 60;
    public static readonly System.Windows.Forms.Padding HeaderPadding = new System.Windows.Forms.Padding(5, 15, 5, 5);

    // State
    public static int CurrentMainSplitter { get; private set; } = LeftPanelMinSize;
    public static int CurrentMetaSplitter { get; private set; } = MetaPanelMinHeight;

    // Sync Events
    public static event EventHandler<int>? MainSplitterDistanceChanged;
    public static event EventHandler<int>? MetaSplitterDistanceChanged;

    private static bool _broadcastingMain = false;
    private static bool _broadcastingMeta = false;

    public static void NotifyMainSplitter(int distance)
    {
        if (_broadcastingMain) return;
        _broadcastingMain = true;
        CurrentMainSplitter = distance; // Update State
        MainSplitterDistanceChanged?.Invoke(null, distance);
        _broadcastingMain = false;
    }

    public static void NotifyMetaSplitter(int distance)
    {
        if (_broadcastingMeta) return;
        _broadcastingMeta = true;
        CurrentMetaSplitter = distance; // Update State
        MetaSplitterDistanceChanged?.Invoke(null, distance);
        _broadcastingMeta = false;
    }
}
