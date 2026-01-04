using System;
using System.Drawing;
using System.Windows.Forms;

namespace LARS.Forms;

public partial class DataViewerControl : UserControl
{
    private TabControl tabControl;
    private TabPage tabBom;
    private TabPage tabPartList;
    private TabPage tabDailyPlan;

    public DataViewerControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Dock = DockStyle.Fill;

        tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10)
        };

        // BOM Viewer Tab
        tabBom = new TabPage("BOM");
        tabBom.BackColor = Color.White;
        tabBom.Padding = new Padding(5); // Add padding for visual separation
        
        var bomContainer = new Panel 
        { 
            Dock = DockStyle.Fill, 
            BorderStyle = BorderStyle.FixedSingle // Border line
        };
        var bomControl = new BomViewerControl();
        bomControl.Dock = DockStyle.Fill;
        bomContainer.Controls.Add(bomControl);
        tabBom.Controls.Add(bomContainer);

        // PartList Viewer Tab
        tabPartList = new TabPage("PartList");
        tabPartList.BackColor = Color.White;
        tabPartList.Padding = new Padding(5);

        var partListContainer = new Panel 
        { 
            Dock = DockStyle.Fill, 
            BorderStyle = BorderStyle.FixedSingle 
        };
        var partListControl = new PartListControl();
        partListControl.Dock = DockStyle.Fill;
        partListContainer.Controls.Add(partListControl);
        tabPartList.Controls.Add(partListContainer);

        // DailyPlan Viewer Tab
        tabDailyPlan = new TabPage("DailyPlan");
        tabDailyPlan.BackColor = Color.White;
        tabDailyPlan.Padding = new Padding(5);

        var dailyPlanContainer = new Panel 
        { 
            Dock = DockStyle.Fill, 
            BorderStyle = BorderStyle.FixedSingle 
        };
        var dailyPlanControl = new DailyPlanControl();
        dailyPlanControl.Dock = DockStyle.Fill;
        dailyPlanContainer.Controls.Add(dailyPlanControl);
        tabDailyPlan.Controls.Add(dailyPlanContainer);

        tabControl.TabPages.Add(tabBom);
        tabControl.TabPages.Add(tabPartList);
        tabControl.TabPages.Add(tabDailyPlan);

        this.Controls.Add(tabControl);
    }
}
