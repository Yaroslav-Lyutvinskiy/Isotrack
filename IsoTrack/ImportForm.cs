﻿using System;
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
            //SQLiteCommand Attach = new SQLiteCommand(String.Format("ATTACH {0} as Import",ImportFile),con);
            //Attach.ExecuteNonQuery();
            //Settings
            //check adducts
            SQLiteCommand Check = new SQLiteCommand("Select Value from Settings where Name = \"Adducts\" ", con);
            SQLiteDataReader Reader = Check.ExecuteReader();
            Reader.Read();
            string AddsStr = Reader.GetString(0);
            string[] Adds = AddsStr.Split(new char[] { ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string MissedAdds = "";
            foreach(string A in Adds){
                if (MasterForms.AdductsForm.Adducts.Select(Ad => Ad.Name == A) == null){
                    MissedAdds+=A+"; ";
                }
            }
            if (MissedAdds != ""){
                MessageBox.Show("Following adduct(s) has not been found in local adducts file: "+MissedAdds+
                    "\n Please, add them to Adducts.txt in binary folder of Isotrack","Unknown Adduct(s)");
                ImportFile = "";
                Close();
                return;
            }
            //Files
            SQLiteCommand Files = new SQLiteCommand("Select FileName From Files Order by FileIndex",con);
            Reader = Files.ExecuteReader();
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
            //Targets
            SQLiteCommand  CTargs = new SQLiteCommand("SELECT * FROM Targets WHERE CustomRTMin is not NULL",con);
            Reader=CTargs.ExecuteReader();
            CustomAvail = Reader.Read();
            if (!CustomAvail){
                checkBox5.Enabled = false;
            }else{
                checkBox5.Checked = true;
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
                    if ( Name == "Version" || Name == "FileList" || Name == "OutStandards" || Name == "Out_dbfile" || 
                        Name == "TargetList" || Name == "MySQLConnString" || Name == "Standards_List" || Name == "StandardsReport") 
                        continue;
                    Properties.Settings.Default[Reader.GetString(0)]=
                        Convert.ChangeType(
                            Reader.GetString(1),
                            Properties.Settings.Default[Reader.GetString(0)].GetType());
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
            //Targets (delayed)
            Targets = checkBox4.Checked;
            CTargets = checkBox5.Checked;
            if (Targets){
                Properties.Settings.Default.TargetList = "db3|" + ImportFile;
            }
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

        bool CustomAvail;
        private void checkBox4_CheckedChanged(object sender, EventArgs e){
            if (!checkBox4.Checked){
                checkBox5.Checked = false;
                checkBox5.Enabled = false;
            }else{
                checkBox5.Enabled = CustomAvail;
            }
        }

    }
}
