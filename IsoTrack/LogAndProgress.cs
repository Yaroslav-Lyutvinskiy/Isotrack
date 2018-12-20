/*******************************************************************************
  Copyright 2015-2018 Yaroslav Lyutvinskiy <Yaroslav.Lyutvinskiy@ki.se> and 
  Roland Nilsson <Roland.Nilsson@ki.se>
 
  Licensed under the Apache License, Version 2.0 (the "License");
  you may not use this file except in compliance with the License.
  You may obtain a copy of the License at

  http://www.apache.org/licenses/LICENSE-2.0

  Unless required by applicable law or agreed to in writing, software
  distributed under the License is distributed on an "AS IS" BASIS,
  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
  See the License for the specific language governing permissions and
  limitations under the License.
 
 *******************************************************************************/

 using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace IsoTrack
{
    public interface ILogAndProgress {
        void Log(string Message);
        void Log(string Message, System.Windows.Forms.MessageBoxIcon WarrningLevel, string StackInfo);
        void Log(string Message, string FileName, System.Windows.Forms.MessageBoxIcon WarrningLevel, string StackInfo);
        void ProgressMessage(string Message);
        void RepProgress(int Perc);
        void RepProgress(int HowMatch, int From);
    }

    public partial class Form1 : Form,ILogAndProgress{

        public delegate void LogDelegate(string Message, string FileName, MessageBoxIcon WarrningLevel, string StackInfo);
 
        public void Log(string Message){
            Log(Message,MessageBoxIcon.Information,null);
        }

        public void Log(string Message,MessageBoxIcon WarrningLevel,string StackInfo){
            Log(Message, "", WarrningLevel, StackInfo);
        }

        public void Log(string Message, string FileName, MessageBoxIcon WarrningLevel,string StackInfo){
           if (InvokeRequired) {
                // We're not in the UI thread, so we need to call BeginInvoke
                Invoke(new LogDelegate(Log), new object[]{Message,FileName, WarrningLevel, StackInfo});
                return;
            }
            //from forms thread
            DateTime date = DateTime.Now;
            string TimeString = date.ToString("H:mm:ss.fff");
            string LogFileName;
            //to file 
            try {
                LogFileName = Path.GetDirectoryName(Properties.Settings.Default.Out_dbfile) + Path.DirectorySeparatorChar + "Isotrack.log";
            }
            catch(Exception) { // для кэширования сырых нет нормального меcта для лога 
                LogFileName = Directory.GetCurrentDirectory()+ Path.DirectorySeparatorChar + "Isotrack.log";
            }
            StreamWriter sw = new StreamWriter(LogFileName,true);
            string FileMessage = "Info:";
            if (WarrningLevel == MessageBoxIcon.Warning) FileMessage = "Warning!"; 
            if (WarrningLevel == MessageBoxIcon.Error)   FileMessage = "ERROR!"; 
            FileMessage += "\t"+TimeString;
            FileMessage += "\t"+Message;
            if (StackInfo != null){
                FileMessage += "\n StackInfo:"+StackInfo;
            }
            sw.WriteLine(FileMessage);
            sw.Close();

            //to form 
            ListViewItem LItem = new ListViewItem();
            LItem.Text = TimeString;
            LItem.SubItems.Add(FileName);
            LItem.SubItems.Add(Message);
            if (StackInfo != null){
                LItem.SubItems.Add(StackInfo);
            }
            LItem.ToolTipText = LItem.Text;
            if (WarrningLevel == MessageBoxIcon.Warning){
                LItem.BackColor = Color.Yellow;
            }
            if (WarrningLevel == MessageBoxIcon.Error){
                LItem.BackColor = Color.Red;
            }
            LogList.Items.Add(LItem);
            LogList.EnsureVisible(LogList.Items.Count-1);
            Application.DoEvents();
        }


        public void RepProgress(int Perc){
            RepProgress(Perc, 100);
        }

        public void RepProgress(int HowMatch, int From){
            //if (HowMatch == 0){
            //    progressBar1.Value=0;
            //    Application.DoEvents();
            //    return;
            //}
            //int Progr = (int)(((double)HowMatch / (double)From) * 100.0);
            //if (Progr!=progressBar1.Value){
            //    progressBar1.Value=Progr;
            //}
            Application.DoEvents();
        }

        public void ProgressMessage(string Message){
            //ProgressLabel.Text = Message;
            Application.DoEvents();
        }

    }

//taken from http://stackoverflow.com/questions/1548312/sorting-a-listview-by-column
public class ListViewColumnSorterExt : IComparer {
    /// <summary>
    /// Specifies the column to be sorted
    /// </summary>
    private int ColumnToSort;
    /// <summary>
    /// Specifies the order in which to sort (i.e. 'Ascending').
    /// </summary>
    private SortOrder OrderOfSort;
    /// <summary>
    /// Case insensitive comparer object
    /// </summary>
    private CaseInsensitiveComparer ObjectCompare;

    private ListView listView;
    /// <summary>
    /// Class constructor.  Initializes various elements
    /// </summary>
    public ListViewColumnSorterExt(ListView lv) {
        listView = lv;
        listView.ListViewItemSorter = this;
        listView.ColumnClick += new ColumnClickEventHandler(listView_ColumnClick);

        // Initialize the column to '0'
        ColumnToSort = 0;

        // Initialize the sort order to 'none'
        OrderOfSort = SortOrder.None;

        // Initialize the CaseInsensitiveComparer object
        ObjectCompare = new CaseInsensitiveComparer();
    }

    private void listView_ColumnClick(object sender, ColumnClickEventArgs e) {
        ReverseSortOrderAndSort(e.Column, (ListView)sender);
    }

    /// <summary>
    /// This method is inherited from the IComparer interface.  It compares the two objects passed using a case insensitive comparison.
    /// </summary>
    /// <param name="x">First object to be compared</param>
    /// <param name="y">Second object to be compared</param>
    /// <returns>The result of the comparison. "0" if equal, negative if 'x' is less than 'y' and positive if 'x' is greater than 'y'</returns>
    public int Compare(object x, object y) {
        int compareResult;
        ListViewItem listviewX, listviewY;

        // Cast the objects to be compared to ListViewItem objects
        listviewX = (ListViewItem)x;
        listviewY = (ListViewItem)y;

        // Compare the two items
        compareResult = ObjectCompare.Compare(listviewX.SubItems[ColumnToSort].Text, listviewY.SubItems[ColumnToSort].Text);

        // Calculate correct return value based on object comparison
        if (OrderOfSort == SortOrder.Ascending) {
            // Ascending sort is selected, return normal result of compare operation
            return compareResult;
        }
        else if (OrderOfSort == SortOrder.Descending) {
            // Descending sort is selected, return negative result of compare operation
            return (-compareResult);
        }
        else {
            // Return '0' to indicate they are equal
            return 0;
        }
    }

    /// <summary>
    /// Gets or sets the number of the column to which to apply the sorting operation (Defaults to '0').
    /// </summary>
    private int SortColumn {
        set {
            ColumnToSort = value;
        }
        get {
            return ColumnToSort;
        }
    }

    /// <summary>
    /// Gets or sets the order of sorting to apply (for example, 'Ascending' or 'Descending').
    /// </summary>
    private SortOrder Order {
        set {
            OrderOfSort = value;
        }
        get {
            return OrderOfSort;
        }
    }

    private void ReverseSortOrderAndSort(int column, ListView lv) {
        // Determine if clicked column is already the column that is being sorted.
        if (column == this.SortColumn) {
            // Reverse the current sort direction for this column.
            if (this.Order == SortOrder.Ascending) {
                this.Order = SortOrder.Descending;
            }
            else {
                this.Order = SortOrder.Ascending;
            }
        }
        else {
            // Set the column number that is to be sorted; default to ascending.
            this.SortColumn = column;
            this.Order = SortOrder.Ascending;
        }

        // Perform the sort with these new sort options.
        lv.Sort();
    }
}  
}
