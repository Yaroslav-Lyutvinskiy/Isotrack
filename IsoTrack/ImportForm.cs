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
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Data.SQLite;


namespace IsoTrack
{
    public partial class ImportForm : Form
    {
        public SQLiteConnection con;
        static public string ImportFile = "";
        static public bool Pairing = false;
        static public bool Targets = false;
        static public bool CTargets = false;


        public ImportForm(){
            InitializeComponent();
        }

        Dictionary<string,string> ImportedFiles = new Dictionary<string,string>();

        private void ImportForm_Load(object sender, EventArgs e){
            //Database analysis 
            con = new SQLiteConnection(String.Format("Data Source = {0}",ImportFile));
            con.Open();
            //Files
            SQLiteCommand Files = new SQLiteCommand("Select FileName From Files Order by FileIndex",con);
            SQLiteDataReader Reader = Files.ExecuteReader();
            bool NoFiles = true;
            bool AllFiles = true;
            while(Reader.Read()){
                string FN = Reader.GetString(0);
                if(Path.GetExtension(FN) == "raw") {
                    FileInfo FI = new FileInfo(FN);
                    if(!FI.Exists) {
                        ImportedFiles.Add(Path.GetFileName(FN), "");
                        AllFiles = false;
                    } else {
                        ImportedFiles.Add(Path.GetFileName(FN), FN);
                        NoFiles = false;
                    }
                } else {
                    DirectoryInfo DI = new DirectoryInfo(FN);
                    if(!DI.Exists) {
                        ImportedFiles.Add(Path.GetFileName(FN), "");
                        AllFiles = false;
                    } else {
                        ImportedFiles.Add(Path.GetFileName(FN), FN);
                        NoFiles = false;
                    }
                }
            }
            if (NoFiles){
                    FileLabel.Text = "!! No files are confirmed";
            }else{
                if (AllFiles){
                    FileLabel.Text = "All files are confirmed";
                }else{
                    FileLabel.Text = "!! Not all files are confirmed";
                }
            }

            //File pairing
            SQLiteCommand  Pairs = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type=\"table\" AND name=\"Report\";",con);
            Reader=Pairs.ExecuteReader();
            PairingAvail = Reader.Read();
            if (PairingAvail){
                Pairs = new SQLiteCommand("Select * from Report", con);
                Reader=Pairs.ExecuteReader();
                PairingAvail = Reader.Read();
                if (PairingAvail){
                    checkBox3.Checked = true;
                }else{
                    checkBox3.Enabled = false;
                }
            }else{
                checkBox3.Enabled = false;
            }
        }

        private void button2_Click(object sender, EventArgs e){
            ImportedFiles.Clear();
            ImportFile = "";
            con.Close();
        }

        private void button1_Click(object sender, EventArgs e){
            //validation
            if (FileLabel.Text[0] == '!' && checkBox3.Checked){
                if (MessageBox.Show("If not all raw files has been found then pairing, " +
                    "\nsample names and coloring scheme can't be imported. " +
                    "\nDo you wish to continue?", "File list is incomplete", MessageBoxButtons.YesNo,MessageBoxIcon.Exclamation) == DialogResult.No) {
                    this.DialogResult = DialogResult.None;
                    return; 
                }
            }
            //Database application
            //Settings
            if (checkBox1.Checked){
                SQLiteCommand Settings = new SQLiteCommand("Select Name, Value from Settings", con);
                SQLiteDataReader Reader = Settings.ExecuteReader();
                while(Reader.Read()){
                    string Name = Reader.GetString(0);
                    string Task =  Reader.GetString(1);
                    if ( Name == "Version" || Name == "FileList" || Name == "OutTargets" || Name == "Out_dbfile" || 
                        Name == "TargetList" || Name == "MySQLConnString" || Name == "Standards_List" || Name == "StandardsReport") 
                        continue;
                    if (Name == "Task" && !(Task == "Untargeted Analysis")) {
                        MessageBox.Show(String.Format("Isotrack does not support anymore task like \"{0}\". Therefore, import of settings will be limited", Task),
                            "No support for " + Task, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                    try {
                        Properties.Settings.Default[Reader.GetString(0)] =
                            Convert.ChangeType(
                                Reader.GetString(1),
                                Properties.Settings.Default[Reader.GetString(0)].GetType());
                    }
                    catch(Exception) { };
                }
            }
            //Files
            if (checkBox2.Checked){
                ListView RawList = Program.MainForm.RawList;
                RawList.Items.Clear();
                foreach(string FN in ImportedFiles.Values){
                    if (FN == "") continue;
                    ListViewItem LItem = new ListViewItem();
                    LItem.Text = FN;
                    LItem.SubItems.Add("Not processed yet.");
                    RawList.Items.Add(LItem);
                }
                Properties.Settings.Default.FileList = "Imported";
            }
            //Pairing (delayed)
            Pairing = checkBox3.Checked;
            con.Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            //browse for files 
            if ( RawFilesFolderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK){
                return;
            }
            string RPath = RawFilesFolderDialog.SelectedPath;
            List<string> Paths = new List<string>();
            Paths.Add(RPath);
            while (Paths.Count > 0){
                string[] CDirs=Directory.GetDirectories(Paths[0]);
                foreach(string P in CDirs){
                    string OStr;
                    if (ImportedFiles.TryGetValue(Path.GetFileName(P),out OStr)){
                        ImportedFiles[Path.GetFileName(P)] = P;
                        continue;
                    }
                    Paths.Add(P);
                }
                string[] CFiles = Directory.GetFiles(Paths[0]);
                foreach(string F in CFiles){
                    string OStr;
                    if (ImportedFiles.TryGetValue(Path.GetFileName(F),out OStr)){
                        ImportedFiles[Path.GetFileName(F)] = F;
                    }
                }
                Paths.RemoveAt(0);
                //here can be protection from recursive paths, say, by limiting of Paths length to something
            }
            bool NoFiles = true;
            bool AllFiles = true;
            foreach (string s in ImportedFiles.Values){
                if (s == ""){
                    AllFiles = false;
                }else{
                    NoFiles = false;
                }
            }
            if (NoFiles){
                    FileLabel.Text = "!! No files are confirmed";
            }else{
                if (AllFiles){
                    FileLabel.Text = "All files are confirmed";
                }else{
                    FileLabel.Text = "!! Not all files are confirmed";
                }
            }
        }

        bool PairingAvail;
        private void checkBox2_CheckedChanged(object sender, EventArgs e){
            if (!checkBox2.Checked){
                checkBox3.Checked = false;
                checkBox3.Enabled = false;
            }else{
                checkBox3.Enabled = PairingAvail;
            }

        }

    }
}
