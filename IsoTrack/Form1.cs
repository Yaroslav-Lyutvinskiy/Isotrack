using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System.Data.SQLite;
using MySql.Data.MySqlClient;

namespace IsoTrack
{

    public partial class Form1 : Form,ILogAndProgress
    {
        public Form1()
        {
            InitializeComponent();
            listViewColumnSorter = new ListViewColumnSorterExt(LogList);  
        }

        private ListViewColumnSorterExt listViewColumnSorter;  

        public List<Target> Targets;
        SQLiteInterface SQLite;

        private void Form1_Load(object sender, EventArgs e)
        {
            Properties.Settings.Default.FileList = "Empty";
            Properties.Settings.Default.Out_dbfile = "";

            int PCount = 0;
            try{
                foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_Processor").Get())
                {
                    PCount += int.Parse(item["NumberOfCores"].ToString());
                }
            }catch(Exception){
                PCount = 2;
            }
            Properties.Settings.Default.Processes = PCount;

            InitialDesc = TypeDescriptor.GetProvider(IsoTrack.Properties.Settings.Default).GetTypeDescriptor(IsoTrack.Properties.Settings.Default);
            ChangeProperties();
            propertyGrid1.SelectedObject = IsoTrack.Properties.Settings.Default;

        }


        private void propertyGrid1_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            if (e.ChangedItem.Label == "\t\t\tTask"){
                ChangeProperties();
                propertyGrid1.SelectedObject = IsoTrack.Properties.Settings.Default;
                propertyGrid1.Refresh();
            }
        }

        //main procedure
        ProcessesHolder Holder;
        private void MainButton_Click(object sender, EventArgs e)
        {
            //Check Parameters
            if (!StartValidation()) return;
            Properties.Settings.Default.Save();
            ResetFileState();
            //Create Output Database 
            SQLite = new SQLiteInterface();
            SQLite.CreateDB(Properties.Settings.Default.Out_dbfile);
            //Save Parameters
            SaveParameters();
            //Save Targets
            if(ImportForm.Pairing) {
                ImportPairing();
            }
            //User Interface Lock
            UILock(true);
            //Start Processes
            Holder = new ProcessesHolder(RawList, this, splitContainer3.Panel2);
            Holder.CreateProcesses(RawList.Items.Count);
            timer1.Enabled = true;
        }

        private void ImportPairing(){
            SQLiteCommand Attach = new SQLiteCommand(String.Format("ATTACH \"{0}\" as Import",ImportForm.ImportFile),SQLite.con);
            Attach.ExecuteNonQuery();
            SQLiteCommand InsSel = new SQLiteCommand("Insert Into Report Select * from Import.Report ",SQLite.con);
            InsSel.ExecuteNonQuery();
            SQLiteCommand Detach = new SQLiteCommand("DETACH Import",SQLite.con);
            Detach.ExecuteNonQuery();
            //
        }

        private void ResetFileState(){
            for (int i = 0 ; i < RawList.Items.Count ; i++){
                ListViewItem LItem = RawList.Items[i];
                string FileName = LItem.Text;
                LItem.SubItems.Clear();
                LItem.Text = FileName;
             	LItem.SubItems.Add("File is not processed yet.");
                LItem.BackColor = Color.White;
            }
        }

        private bool StartValidation(){
            try {
                if (RawList.Items.Count == 0){
                    MessageBox.Show("Please, select raw files to process...","Can't start processing",MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
                    return false;
                }
                //Outer db3 File
                string OutFileName = Properties.Settings.Default.Out_dbfile;
                try{
                    if ( OutFileName == "" || !Directory.Exists(Path.GetDirectoryName(OutFileName)) || 
                        !(Path.GetExtension(OutFileName)==".db3")){
                        MessageBox.Show("Please, select output db3 file to store processing results...","Can't start processing",MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
                        return false;
                    }
                }catch(Exception){
                    MessageBox.Show("Please, select output db3 file to store processing results...","Can't start processing",MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
                    return false;
                }
                return true;
            }catch(Exception){
                MessageBox.Show("Exception on parameters validation when starting processing session...",
                    "Can't start processing",MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
                return false;
            }
        }

        private void SaveParameters()
        {
            Properties.Settings Settings = Properties.Settings.Default;
            SQLite.tr=SQLite.con.BeginTransaction();
            SQLite.SaveSetting("Version",Text);
            foreach (System.Configuration.SettingsPropertyValue V in Settings.PropertyValues){
                SQLite.SaveSetting(V.Name,V.PropertyValue.ToString());
            }
            SQLite.tr.Commit();
        }

        private void UILock(bool Lock){
            propertyGrid1.Enabled = !Lock;
            MainButton.Enabled = !Lock;
            StopButton.Enabled = Lock;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try{
                timer1.Enabled = false;
                if (Holder.Continue()){
                    timer1.Enabled = true;
                }else{
                    if(PostProcessing()) {
                        System.Media.SystemSounds.Beep.Play();
                        if (SQLite != null)
                            SQLite.con.Close();
                        UILock(false);
                    }
                }
            }
            catch(Exception ex){
                //WorkCompleted(ex);
                Log(ex.Message,MessageBoxIcon.Error,ex.StackTrace);
                if(SQLite != null) 
                    SQLite.con.Close();
                UILock(false);
            }
        }

        private bool PostProcessing(){
            PostProcessing PP = new PostProcessing();
            Log("Indexing database...");
            SQLite.CreateIndexes();
            PP.SelectTarget(SQLite.con);
            if (Properties.Settings.Default.OutTargets != ""){
                Targets=Target.ReadTargets(SQLite.con);
                StreamWriter sw = new StreamWriter(Properties.Settings.Default.OutTargets, true);
                SQLiteCommand FileList = new SQLiteCommand("Select FileName From Files Order by FileIndex",SQLite.con);
                SQLiteDataReader Reader = FileList.ExecuteReader();
                while(Reader.Read()) {
                    sw.WriteLine(Reader.GetString(0));
                }
                sw.Close();
                Target.SaveToFile(Targets, Properties.Settings.Default.OutTargets);
            }
            return true;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.Save();
            if (StopButton.Enabled){
                StopButton_Click(null, null);
            }
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            Holder.Stop();
            if(SQLite != null) {
                SQLite.con.Close();
            }
            Log("Processing session has been canceled by user.", MessageBoxIcon.Warning, null);
            UILock(false);
        }

        private void ImportButton_Click(object sender, EventArgs e){
            if (DB3ImportFileDialog.ShowDialog() == DialogResult.OK){
                ImportForm IF = new ImportForm();
                //IF.con = SQLite.con;
                ImportForm.ImportFile = DB3ImportFileDialog.FileName;
                if (IF.ShowDialog()!=DialogResult.OK){
                    ImportForm.ImportFile = "";
                }else{
                    propertyGrid1.Refresh();
                }
            }
        }

    }
}
