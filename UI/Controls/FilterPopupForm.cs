using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LARS.UI.Controls;

public partial class FilterPopupForm : Form
{
    private CheckedListBox chkList;
    private Button btnOk;
    private Button btnClear;
    public List<string> SelectedValues { get; private set; } = new List<string>();

    public FilterPopupForm(IEnumerable<string> values, IEnumerable<string> currentSelection)
    {
        InitializeComponent();
        PopulateList(values, currentSelection);
    }

    private void InitializeComponent()
    {
        this.Size = new Size(250, 350);
        this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
        this.StartPosition = FormStartPosition.Manual;
        this.Text = "Filter";

        chkList = new CheckedListBox
        {
            Dock = DockStyle.Top,
            Height = 250,
            CheckOnClick = true
        };

        var panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.WhiteSmoke };
        
        btnOk = new Button 
        { 
            Text = "OK", 
            DialogResult = DialogResult.OK, 
            Location = new Point(160, 10), 
            Size = new Size(60, 30),
            BackColor = Color.ForestGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        
        btnClear = new Button 
        { 
            Text = "Clear (All)", 
            Location = new Point(20, 10), 
            Size = new Size(80, 30) 
        };
        btnClear.Click += (s, e) => {
            for(int i=0; i<chkList.Items.Count; i++) chkList.SetItemChecked(i, true);
        };

        panelBottom.Controls.Add(btnOk);
        panelBottom.Controls.Add(btnClear);

        this.Controls.Add(panelBottom);
        this.Controls.Add(chkList);

        btnOk.Click += (s, e) =>
        {
            SelectedValues = chkList.CheckedItems.Cast<string>().ToList();
            this.Close();
        };
    }

    private void PopulateList(IEnumerable<string> values, IEnumerable<string> currentSelection)
    {
        var distinctValues = values.Distinct().OrderBy(x => x).ToList();
        var selectedSet = new HashSet<string>(currentSelection ?? distinctValues);

        foreach (var val in distinctValues)
        {
            chkList.Items.Add(val, selectedSet.Contains(val));
        }
    }
}
