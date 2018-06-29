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
    enum WorkflowTask{
        Targeted_Analysis,
        Untargeted_Analysis,
        Standards_Refine,
        Raw_Caching
    };

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
            Properties.Settings.Default.OutStandards = "";
            Properties.Settings.Default.Standards_List = "";
            Properties.Settings.Default.StandardsReport = "";
            Properties.Settings.Default.TargetList = "";

            MasterForms.AdductsForm.ReadAdducts(); 

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


            //MySqlConnectionStringBuilder MySqlConn = new MySqlConnectionStringBuilder();
            //MySqlConn.Server = "charon.cmm.ki.se";
            //MySqlConn.Password = "lab";
            //MySqlConn.UserID = "lab";
            //MySqlConn.Database = "Metabolism";
            //Metaflow.Properties.Settings.Default.MySQLConnString = MySqlConn.GetConnectionString(true);
            //MySqlConnection con = new MySqlConnection(Metaflow.Properties.Settings.Default.MySQLConnString);
            //con.Open();
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
            if(Properties.Settings.Default.Task != "Raw Data Caching") {
                //Create Output Database 
                SQLite = new SQLiteInterface();
                SQLite.CreateDB(Properties.Settings.Default.Out_dbfile);
                //Save Parameters
                SaveParameters();
                //Save Targets
                if(Properties.Settings.Default.Task != "Untargeted Analysis") {
                    SaveTargets();
                }
                if(ImportForm.Pairing) {
                    ImportPairing();
                }
            } else {
                SQLite = null;
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
                switch (Properties.Settings.Default.Task){
                    case "Targeted Analysis":{
                        //File List
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
                        //Targets List
                        if (!LoadTarget()){
                            return false;
                        }
                        return true;
                    }
                    case "Untargeted Analysis":{
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
                    }
                    case "Standards Refine":{
                        //File List
                        if (RawList.Items.Count == 0){
                            MessageBox.Show("Please, select \"standards-to-raw files\" match list to process...","Can't start processing",MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
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
                        //Standart report 
                        String Report = Properties.Settings.Default.StandardsReport;
                        if (Report!="" && (!Directory.Exists(Path.GetDirectoryName(OutFileName)))){
                            MessageBox.Show("Please, select correct file name for saving of standart report...","Can't start processing",MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
                        }
                        //Out Target List
                        String OutTarget = Properties.Settings.Default.OutStandards;
                        if (OutTarget!="" && (!Directory.Exists(Path.GetDirectoryName(OutFileName)))){
                            MessageBox.Show("Please, select correct file name for saving of standards list","Can't start processing",MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
                        }
                        if (!LoadTarget()){
                            return false;
                        }
                        CloneTargets();
                        return true;
                    }
                    case "Raw Data Caching": {
                        //File List
                        if (RawList.Items.Count == 0){
                            MessageBox.Show("Please, select Standatds to raw files match list to process...","Can't start processing",MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
                            return false;
                        }
                        return true;
                    }
                    default: {
                        MessageBox.Show("Unknown task to start processing...",
                            "Can't start processing",MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
                        return false;
                    }
                }
            }catch(Exception){
                MessageBox.Show("Exception on parameters validation when starting processing session...",
                    "Can't start processing",MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
                return false;
            }
        }


        private bool LoadTarget(){
            string TSource = Properties.Settings.Default.TargetList;
            try{
                string[] Tokens = TSource.Split(new char[] { '|' });
                switch (Tokens[0]){
                    case "file":{
                        string FileName = Tokens[1];
                        Targets = Target.ReadTargets(FileName);
                        return true;
                    }
                    case "db3":{
                        string DBFile = Tokens[1];
                        SQLiteConnection con = new SQLiteConnection(String.Format("Data Source ={0}",DBFile));
                        con.Open();
                        Targets = Target.ReadTargets(con,ImportForm.CTargets);
                        con.Close();
                        return true;
                    }
                    case "mysql":{
                        return false;
                    }
                    default: return false;
                }
            }catch(Exception e){
                MessageBox.Show(e.Message,"Can't start processing",MessageBoxButtons.OK,MessageBoxIcon.Exclamation);
                return false;
            }
        }

        List<List<string>> FileToStannds;
        
        public bool LoadStandFiles(string FileName){
            FileToStannds = new List<List<string>>();
            StreamReader sr = new StreamReader(FileName);
            sr.ReadLine();
            string str = sr.ReadLine().Trim();
            while(str!="" && str!=null){
                string[] Tokens = str.Split(new char[] {'\t'});
                for (int i = 0 ; i < Tokens.Length ; i++){
                    Tokens[i] = Tokens[i].Trim();
                }
                string ext = Path.GetExtension(Tokens[0]);
                switch (ext) {
                    case ".raw":{
                        if (File.Exists(Tokens[0])){
                            break;
                        }else{
                            MessageBox.Show(
                                String.Format("File {0} specified in file to standards match list does not exist, please correct file name",Tokens[0]),
                                "Invalid file to standard match");
                            return false;
                        }
                    }
                    case ".d":{
                        if (Directory.Exists(Tokens[0])){
                            break;
                        }else{
                            MessageBox.Show(
                                String.Format("File {0} specified in file to standards match list does not exist, please correct file name",Tokens[0]),
                                "Invalid file to standard match");
                            return false;
                        }
                    }
                    default:{
                            MessageBox.Show(
                                String.Format("Invalid file {0} specified in file to standards match list",Tokens[0]),
                                "Invalid file to standard match");
                            return false;
                    }
                }
                bool Exists = false;
                for(int i = 0 ; i <FileToStannds.Count ; i++){
                    if (FileToStannds[i].Contains(Tokens[0])){
                        FileToStannds[i].Add(Tokens[1]);
                        Exists = true;
                        break;
                    }
                }
                if (!Exists){
                    List<String> NewFile = new List<string>();
                    NewFile.Add(Tokens[0].Trim());
                    NewFile.Add(Tokens[1].Trim());
                    FileToStannds.Add(NewFile);
                }
                str = sr.ReadLine();
            }
            //FileToStannds.Sort();
            for (int i = 0; i < FileToStannds.Count; i++ ){
                ListViewItem LItem = new ListViewItem(FileToStannds[i][0].Trim());
                LItem.SubItems.Add("File is not processed yet.");
                RawList.Items.Add(LItem);
            }
            return true;
        }


        public List<MasterForms.AdductsForm.Adduct> PreparedAdducts;

        private void PrepareAdducts(){
            PreparedAdducts = new List<MasterForms.AdductsForm.Adduct>();
            String AdductStr = Properties.Settings.Default.Adducts;
            List<MasterForms.AdductsForm.Adduct> PermAdducts = MasterForms.AdductsForm.Adducts;
            for ( int i = 0 ; i < PermAdducts.Count ; i++){
                if (AdductStr.Contains(PermAdducts[i].Name)){
                    PreparedAdducts.Add(PermAdducts[i]);
                }
            }
            string[] Tokens = AdductStr.Split(new char[] { ';' });
            for (int i = 0 ; i < Tokens.Length ; i++){
                if (Tokens[i].Trim().IndexOf("Custom")==0){
                    string[] CustAdd = Tokens[i].Split(new char[] { ':', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    MasterForms.AdductsForm.Adduct A = new MasterForms.AdductsForm.Adduct();
                    A.Name = CustAdd[1].Trim();
                    A.Mode = CustAdd[2].Trim()[0];
                    A.Mass = Convert.ToDouble(CustAdd[3].Trim());
                    PreparedAdducts.Add(A);
                }
            }
        }

        private void CloneTargets()
        {
            PrepareAdducts();
            List<Target> Clones = new List<Target>();
            foreach(Target T in Targets){
                T.AdductTargets = new List<Target>();
                List<Target> TF = CloneTargettoFiles(T);
                foreach(Target TFF in TF){
                    T.AdductTargets.AddRange(CloneTargettoAdducts(TFF));
                }
                Clones.AddRange(T.AdductTargets);
            }

            //for( int i = 0 ; i < Clones.Count ; i++){
            //    Clones[i].ID = i;
            //}
            for(int i = 0 ; i < Targets.Count ; i++){
                if (Targets[i].AdductTargets.Count == 0){
                    Log(String.Format("Standard file for target {0} has not been found", Targets[i].Name),MessageBoxIcon.Warning,null);
                }
            }
            Standards = Targets;
            Targets = Clones;
        }

        public List<Target> Standards;

        private List<Target> CloneTargettoAdducts(Target T)
        {
            List<Target> TargetClones = new List<Target>();
            for( int j = 0 ; j < PreparedAdducts.Count ; j++){
                Target NT = T.Copy();
                NT.Mode = (PreparedAdducts[j].Mode == '+') ? 1 : -1;
                NT.MZ += PreparedAdducts[j].Mass;
                NT.Adduct = PreparedAdducts[j].Name;
                TargetClones.Add(NT);
            }
            return TargetClones;
        }


        private List<Target> CloneTargettoFiles(Target T){
            List<Target> TargetClones = new List<Target>();
            for( int j = 0 ; j < FileToStannds.Count ; j++){
                if (FileToStannds[j].Contains(T.Name)){
                    Target NT = T.Copy();
                    NT.FileID = j;
                    TargetClones.Add(NT);
                }
            }
            return TargetClones;
        }

        private void FilesToTargetMatch(){
            for(int i = 0 ; i < Targets.Count ; i++){
                for( int j = 0 ; j < FileToStannds.Count ; j++){
                    if (FileToStannds[j].Contains(Targets[i].Name)){
                        Targets[i].FileID = j;
                        break;
                    }
                }
            }
            for(int i = 0 ; i < Targets.Count ; i++){
                if (Targets[i].FileID == -1){
                    Log(String.Format("Standard file for target {0} has not been found", Targets[i].Name),MessageBoxIcon.Warning,null);
                }
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

        private void SaveTargets(){
            SQLite.tr=SQLite.con.BeginTransaction();
            foreach(Target T in Targets){
                T.SaveDB(SQLite.con);
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


        private bool SecondPass = false;

        private bool PostProcessing(){
            PostProcessing PP = new PostProcessing();
            if(SQLite != null) {
                Log("Indexing database...");
                SQLite.CreateIndexes();
            }
            if (Properties.Settings.Default.Task == "Untargeted Analysis"){
                PP.SelectTarget(SQLite.con);
            }
            if (Properties.Settings.Default.Task == "Standards Refine"){
                PP.RefineStands(SQLite.con);
            }
            if (Properties.Settings.Default.Task == "Targeted Analysis" && Properties.Settings.Default.BackFilling ){
                if(SecondPass) {
                    SecondPass = false;
                } else {
                    Log("Preparing for Second pass for Back filling.");
                    SecondPass = true;
                    PP.BackFilling(SQLite.con);
                    Holder = new ProcessesHolder(RawList, this, splitContainer3.Panel2);
                    Holder.CreateProcesses(RawList.Items.Count);
                    Log("Second pass started.");
                    timer1.Enabled = true;
                    return false;
                }
            }
            if (Properties.Settings.Default.Task != "Targeted Analysis" && Properties.Settings.Default.OutStandards != ""){
                Targets=Target.ReadTargets(SQLite.con);
                StreamWriter sw = new StreamWriter(Properties.Settings.Default.OutStandards,true);
                SQLiteCommand FileList = new SQLiteCommand("Select FileName From Files Order by FileIndex",SQLite.con);
                SQLiteDataReader Reader = FileList.ExecuteReader();
                while(Reader.Read()) {
                    sw.WriteLine(Reader.GetString(0));
                }
                sw.Close();
                Target.SaveToFile(Targets, Properties.Settings.Default.OutStandards);
            }
            if (Properties.Settings.Default.Task == "Targeted Analysis"){
                Log("Targeted analysis has been finished.");
            }
            if (Properties.Settings.Default.Task == "Raw Data Caching"){
                Log("Raw data caching has been finished.");
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

        private void button2_Click(object sender, EventArgs e)
        {
            SQLite = new SQLiteInterface();
            SQLite.InitDB(Properties.Settings.Default.Out_dbfile);
            if (Properties.Settings.Default.Task == "Targeted Analysis" || Properties.Settings.Default.Task == "Standards Refine"){
                LoadTarget();
            }
            if (Properties.Settings.Default.Task == "Standards Refine"){
                CloneTargets();
            }
            PostProcessing();
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
