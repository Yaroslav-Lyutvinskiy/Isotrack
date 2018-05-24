using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Data.SQLite;

namespace Inspector
{
    public partial class FilesForm : Form
    {
        public class FileRep{
            public int Order;
            public string NegFile;
            public string PosFile;
            public bool Reported;
            public string ShortName;
            public Color Color;
        }

        public static List<FileRep> Pairs;
        List<String> NegFiles;
        List<String> PosFiles;

        public FilesForm()
        {
            InitializeComponent();
        }


        private void FilesForm_Load(object sender, EventArgs e){
            NegFiles = new List<string>();
            PosFiles = new List<string>();
            foreach(FileRep FR in Pairs){
                if (FR.PosFile != null) PosFiles.Add(FR.PosFile);
                if (FR.NegFile != null) NegFiles.Add(FR.NegFile);
            }
            foreach(FileRep FR in Pairs){
                AddRow(FR);
            }
            ValueChRecursion = false;
        }

        private void FileView_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
        }

        private void FileView_CurrentCellDirtyStateChanged(object sender, EventArgs e){
            if (FileView.IsCurrentCellDirty){
                FileView.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        bool ValueChRecursion = true;

        private void FileView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex > 1) return;
            if (ValueChRecursion) return;
            ValueChRecursion = true;

            //проверить есть ли это значение еще где нибудь - если есть - убрать или очистить строчку 
            string NewValue = FileView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value as string;
            for (int i = 0 ; i < FileView.RowCount ; i++){
                if( FileView.Rows[i].Cells[e.ColumnIndex].Value == null && FileView.Rows[i].Cells[1-e.ColumnIndex].Value == null){
                   FileView.Rows.RemoveAt(i);
                }
                if (NewValue != null && (FileView.Rows[i].Cells[e.ColumnIndex].Value as string) == NewValue && i!=e.RowIndex){
                    if(FileView.Rows[i].Cells[1-e.ColumnIndex].Value  != null && (FileView.Rows[i].Cells[1-e.ColumnIndex].Value as string) != NewValue){
                        FileView.Rows[i].Cells[e.ColumnIndex].Value = null;
                    }else{
                        FileView.Rows.RemoveAt(i);
                    }
                    break;
                }
            }
            //проверить все ли имена файлов представлены (могут быть в обоих списках)
            List<string> NamestoCheck = new List<string>();
            NamestoCheck.AddRange(NegFiles);
            NamestoCheck.AddRange(PosFiles);
            for (int i = 0 ; i < FileView.RowCount ; i++){
                while(NamestoCheck.IndexOf(FileView.Rows[i].Cells[e.ColumnIndex].Value as string) != -1)
                    NamestoCheck.Remove((FileView.Rows[i].Cells[e.ColumnIndex].Value as string));
                while(NamestoCheck.IndexOf(FileView.Rows[i].Cells[1-e.ColumnIndex].Value as string) != -1)
                    NamestoCheck.Remove((FileView.Rows[i].Cells[1-e.ColumnIndex].Value as string));
            }
            foreach(string Name in NamestoCheck){
                AddRow(Name);
            }

            ValueChRecursion = false;
        }

        public int LastUsedColorIndex = 0;
        static public int MaxShortName = 0;

        private void AddRow(string File){
            FileRep FR = new FileRep();
            if (NegFiles.Contains(File)) FR.NegFile = File;
            if (PosFiles.Contains(File)) FR.PosFile = File;
            FR.ShortName = MaxShortName.ToString();
            MaxShortName++;
            FR.Reported = true;
            LastUsedColorIndex++;
            LastUsedColorIndex = LastUsedColorIndex % MetaRepForm.ColorsDefault.Length;
            FR.Color = MetaRepForm.ColorsDefault[LastUsedColorIndex];
            AddRow(FR);
        }

        private void AddRow(FileRep FR){
            int Index = FileView.Rows.Add();
            FileView.Rows[Index].Cells[0].ValueType = typeof(string);
            FileView.Rows[Index].Cells[0].Value = FR.NegFile;
            (FileView.Rows[Index].Cells[0] as DataGridViewComboBoxCell).Items.Add("");
            foreach(string Neg in NegFiles){
                (FileView.Rows[Index].Cells[0] as DataGridViewComboBoxCell).Items.Add(Neg);
            }
            FileView.Rows[Index].Cells[1].ValueType = typeof(string);
            FileView.Rows[Index].Cells[1].Value = FR.PosFile;
            (FileView.Rows[Index].Cells[1] as DataGridViewComboBoxCell).Items.Add("");
            foreach(string Pos in PosFiles){
                (FileView.Rows[Index].Cells[1] as DataGridViewComboBoxCell).Items.Add(Pos);
            }
            FileView.Rows[Index].Cells[2].Value = FR.ShortName;
            FileView.Rows[Index].Cells[3].Value = FR.Reported;
            FileView.Rows[FileView.Rows.Count - 1].Cells[4].Style.BackColor = FR.Color;
        }

        DataGridViewCellEventArgs DataChanged = null;

        private void FileView_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex <2 ){
                FileView.BeginEdit(true);
                DataChanged = e;
                (FileView.EditingControl as ComboBox).SelectionChangeCommitted +=new EventHandler(ComboChanged);
                (FileView.EditingControl as ComboBox).DroppedDown = true;
                return;
            }
            if (FileView.CurrentCell.ColumnIndex == 4) {
                BarColorDialog.Color = FileView.CurrentCell.Style.BackColor;
                if (BarColorDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                FileView.CurrentCell.Style.BackColor = BarColorDialog.Color;
                FileView.CurrentCell.Selected = false;
            }

        }

        private void ComboChanged(object sender, EventArgs e){
            FileView_CellValueChanged(null, DataChanged);
        }

        public SQLiteConnection con;


        //Ok button
        private void button1_Click(object sender, EventArgs e){
            //validate from empty and duplicated strings in short names 
            for ( int i = 0 ; i < FileView.RowCount ; i++){
                string ShN = FileView.Rows[i].Cells[2].Value as string;
                if (ShN == null) {
                    MessageBox.Show("There is empty ShortNames for samples in the table. Please, fill it.");
                    DialogResult = DialogResult.None;
                    return;
                }
                for( int j = 0 ; j < FileView.RowCount ; j++){
                    if (i!=j && ShN == (FileView.Rows[j].Cells[2].Value as string)){
                        MessageBox.Show("There is duplicated short name \""+ShN+"\" in the table. Please, resolve duplication.");
                        DialogResult = DialogResult.None;
                        return;
                    }
                }
            }
            //FileReps
            Pairs.Clear();
            for(int i = 0 ; i < FileView.RowCount ; i++) {
                FileRep F = new FileRep();
                F.NegFile = FileView.Rows[i].Cells[0].Value as string;
                F.PosFile = FileView.Rows[i].Cells[1].Value as string;
                F.Order = i;
                F.Reported = (FileView.Rows[i].Cells[3] as DataGridViewCheckBoxCell).Value as bool? ?? true;
                F.ShortName = FileView.Rows[i].Cells[2].Value as string;
                F.Color = FileView.Rows[i].Cells[4].Style.BackColor;
                Pairs.Add(F);
            }
        }

        //Row Up Button
        private void toolStripButton1_Click(object sender, EventArgs e){
            FileView.ClearSelection();
            int Index = FileView.CurrentRow.Index;
            if (Index == 0) return;
            DataGridViewRow Row = FileView.CurrentRow;
            FileView.Rows.RemoveAt(Index);
            FileView.Rows.Insert(Index-1,Row);
            FileView.CurrentCell = FileView.Rows[Index-1].Cells[0];
            FileView.ClearSelection();
        }

        //Row Down Button
        private void toolStripButton2_Click(object sender, EventArgs e){
            FileView.ClearSelection();
            int Index = FileView.CurrentRow.Index;
            if (Index == FileView.RowCount-1) return;
            DataGridViewRow Row = FileView.CurrentRow;
            FileView.Rows.RemoveAt(Index);
            FileView.Rows.Insert(Index+1,Row);
            FileView.CurrentCell = FileView.Rows[Index+1].Cells[0];
            FileView.ClearSelection();
        }

        //Button - Selection/All
        private void toolStripButton3_Click(object sender, EventArgs e){
            foreach(DataGridViewRow Row in FileView.Rows){
                Row.Cells[3].Value = true;
            }
        }

        //Button - Selection/None
        private void toolStripButton4_Click(object sender, EventArgs e){
            foreach(DataGridViewRow Row in FileView.Rows){
                Row.Cells[3].Value = false;
            }
        }

    }
}
