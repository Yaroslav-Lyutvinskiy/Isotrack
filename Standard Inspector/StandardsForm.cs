using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Windows.Forms.DataVisualization;
using System.Windows.Forms.DataVisualization.Charting;
using System.Data.SQLite;


namespace Standard_Inspector
{
    public partial class StandardsForm : Form
    {

        SQLiteConnection con = null;
        string SQLiteFileName = "";
        List<Dictionary<int, double>> RTs = null;

        public StandardsForm()
        {
            InitializeComponent();
        }

        public class StandardFile{
            public int FileID;
            public string FileName;
            public int Polarity;
        }

        public List<StandardFile> Files = new List<StandardFile>();

        private void toolStripButton1_Click(object sender, EventArgs e){
            //Open Files
            if (SQLiteFileDialog.ShowDialog() != DialogResult.OK)
                return;
            con = new SQLiteConnection(String.Format("Data Source = {0}",SQLiteFileDialog.FileName));
            con.Open();

            //Read Standard files
            SQLiteCommand com = new SQLiteCommand("Select FileIndex, FileName, Mode from Files order by FileIndex", con);
            SQLiteDataReader dr = com.ExecuteReader();
            Files.Clear();
            while(dr.Read()){
                StandardFile SF = new StandardFile();
                SF.FileID = dr.GetInt32(0);
                SF.FileName = dr.GetString(1);
                SF.Polarity = dr.GetInt32(2);
                Files.Add(SF);
            }

            LoadRTs();
            Text = "Standards Inspector - " + Path.GetFileName(SQLiteFileDialog.FileName);
            SQLiteFileName = Path.GetFileName(SQLiteFileDialog.FileName);

            //Read Standards
            Standards.Clear();
            com = new SQLiteCommand("Select Name, Desc, MZ, Candidates, TargetID from Targets order by Name", con);
            dr = com.ExecuteReader();
            StandardView.Rows.Clear();
            while(dr.Read()){
                Standart S = new Standart();
                S.Name = dr.GetString(0);
                S.ID = dr.GetInt32(4);
                S.MZ = dr.GetDouble(2);
                Standards.Add(S.ID,S);
                StandardView.Rows.Add(dr.GetString(0), dr.GetString(1), dr.GetDouble(2), dr.GetInt32(3), dr.GetInt32(4));
            }
            WarnIntThreshold = Convert.ToDouble(GetParam("IntensityThreshold"))*10;
            MassError = Convert.ToDouble(GetParam("Mass_Accuracy"));

            foreach(Standart S in Standards.Values){
                com = new SQLiteCommand(String.Format(
                    "Select Ions.IonID, Adduct, Mode, Ions.MZ, Candidates, Strongs, Ambigous, FileID from Ions, Candidates "+
                    "Where [Candidates].[IonID] = Ions.[IonID] and TargetID = {0} "+
                    "Group by Ions.IonID  ",S.ID), con);
                SQLiteDataReader Reader = com.ExecuteReader();
                while (Reader.Read()){
                    Ion I = new Ion();
                    I.ID = Reader.GetInt32(0);
                    I.Adduct = Reader.GetString(1);
                    I.Mode = Reader.GetString(2);
                    I.MZ = Reader.GetDouble(3);
                    I.Candidates = Reader.GetInt32(4);
                    I.Strongs = Reader.GetInt32(5);
                    I.Ambigous = Reader.GetInt32(6);
                    I.FileID = Reader.GetInt32(7);
                    S.Ions.Add(I);
                }
            }
            MarkStands();
            toolStripButton3.Enabled = true;
            toolStripButton4.Enabled = true;
            toolStripButton5.Enabled = true;
        }


        class StandState{
            public double Score;
            public double ApexIntensity;
            public double Width;
            public bool Amb;
            public bool toWarn;
            public int ID;
            public string Story;
        }

        double WarnIntThreshold = 1000000.0;
        double WarnScoreThreshold = 10.0;
        double WarnRTWidthThreshold = 3.0;

        private void MarkStands(){
            SQLiteCommand com = new SQLiteCommand(
                "Select Name, Ions.Ambigous, Left, Right, Score, ApexIntensity, Strongs, Major, Count(RightCandID), Targets.TargetID " +
                "from Targets, Ions, Candidates, RTPeaks left outer join Intersects on (Candidates.[CandID]=[Intersects].[LeftCandID]) " +
                "Where Candidates.IonID=Ions.IonID and " +
                "Ions.TargetID = Targets.TargetID and " +
                "RTPeaks.TraceID=Candidates.TraceID and " +
                "Candidates.PeakNumber=RTPeaks.PeakNumber and Selected = 1 " +
                "Group by Name Order by Name ", con);
            SQLiteDataReader Reader = com.ExecuteReader();
            List<StandState> States = new List<StandState>();
            List<double> Widths = new List<double>();
            while (Reader.Read()){
                StandState S = new StandState();
                S.ID = Reader.GetInt32(9);
                S.Score = Reader.GetDouble(4);
                S.ApexIntensity = Reader.GetDouble(5);
                S.Width = Reader.GetDouble(3) - Reader.GetDouble(2);
                Widths.Add(Reader.GetDouble(3) - Reader.GetDouble(2));
                S.Amb = Reader.GetInt32(1)>0;
                //предупредить если Strongs > 1 или немажорный или есть пересечения или неоднозначный
                S.toWarn = Reader.GetInt32(6) > 1 || Reader.GetInt32(7) < 1 || Reader.GetInt32(8) > 0 || Reader.GetInt32(1) > 0;
                States.Add(S);
            }
            Widths.Sort();
            double Median = Widths[Widths.Count/2];
            foreach(StandState S in States){
                S.toWarn = S.toWarn || S.Score < WarnScoreThreshold || S.ApexIntensity < WarnIntThreshold || S.Width > WarnRTWidthThreshold * Median;
            }

            for ( int i = 0 ; i < StandardView.RowCount ; i++){
                bool Yellow = true;
                bool White = true;
                foreach(StandState S in States){
                    if (S.ID == (int)StandardView.Rows[i].Cells[4].Value){
                        White = false;
                        Yellow &= S.toWarn;
                    }
                }
                //если нет кандидатов - то белый
                if (White) {
                    StandardView.Rows[i].DefaultCellStyle = StandardView.DefaultCellStyle;
                    continue; 
                }
                //если неоднозначный то красный 
                //if (S.Amb){
                //    StandardView.Rows[i].DefaultCellStyle.BackColor = Color.LightCoral;
                //}
                //если низкий скор или низкая интенсивность или широкий пик 
                if (Yellow){
                    StandardView.Rows[i].DefaultCellStyle.BackColor = Color.LightGoldenrodYellow;
                }else{
                    StandardView.Rows[i].DefaultCellStyle.BackColor = Color.FromArgb(214,253,200);
                }
            }

        }

        private string GetParam(string Name)
        {
            SQLiteCommand com = new SQLiteCommand(String.Format("Select value from Settings where name = \"{0}\"",Name), con);
            SQLiteDataReader Reader = com.ExecuteReader();
            if (Reader.Read()){
                return Reader.GetString(0);
            }
            return null;
        }

        class Standart {
            public string Name;
            public int ID;
            public double MZ; //neutrall mass
            public List<Ion> Ions = new List<Ion>();
        }

        class Ion {
            public int ID;
            public int FileID;
            public double MZ;
            public string Adduct;
            public string Mode; 
            public int Candidates;
            public int Strongs;
            public int Ambigous;
        }

        class Candidate{
            public int StandardID;
            public int IonID;
            public int CandID;
            public int SameRTID;
            public int FileID;
            public string Adduct;
            public double Score;
            public double SumScore;
            public bool Selected;
            public double MZ;
            public double Apex;
            public double ApexIn;
            public double Left;
            public double Right;
            public string Mode;
        }

        Dictionary<int,Standart> Standards = new Dictionary<int,Standart>();
        Dictionary<int,Candidate> Candidates = new Dictionary<int,Candidate>();

        //читаем таблицу возможных RT и сохраняем ее на будущее
        public void LoadRTs(){
            RTs = new List<Dictionary<int, double>>();
            SQLiteCommand RTQuery = new SQLiteCommand(
                String.Format(
                    "Select ScanNumber, RT, FileName, Files.FileIndex from Spectra,Files where Spectra.FileIndex=Files.FileIndex and MSOnly = 1 order by Files.FileIndex,ScanNumber"),
                con);
            SQLiteDataReader Reader = RTQuery.ExecuteReader();
            string FileName="";
            while(Reader.Read()){
                if (Reader.GetString(2) != FileName){
                    FileName = Reader.GetString(2);
                    RTs.Add(new Dictionary<int,double>());
                }
                RTs[RTs.Count-1].Add(Reader.GetInt32(0), Reader.GetDouble(1));
            }
            Reader.Close();
        }

        private void StandardView_SelectionChanged(object sender, EventArgs e){
            if (StandardView.SelectedRows.Count == 0) return;
            Standart S = Standards[(int)StandardView.SelectedRows[0].Cells[4].Value];
            //int St = Standards[StandardView.SelectedRows[0].Index];
            //Show traces
            CandidateView.SelectionChanged -= Candidate_SelectionChanged;
            CandidateView.Rows.Clear();
            AltStandView.Rows.Clear();
            AltCandView.Rows.Clear();

            //Show table 
            SQLiteCommand com = new SQLiteCommand(
                "Select CandID, Candidates.TraceID, Score, PPMError, RTPeaks.[ApexIntensity], "+
                "RTPeaks.[Apex], Candidates.Selected, Left, Right, Ions.Adduct, "+
                "FileID, Candidates.MZ, SameRTID, Ions.IonID, SumScore, Mode  "+ 
                "from Ions, Candidates, Traces, RTPeaks "+
                "where Ions.TargetID = "+S.ID.ToString()+" and "+
                "Candidates.IonID = Ions.IonID and "+
                "Candidates.[TraceID] = traces.[TraceID] and "+
                "RTPeaks.[TraceID] = Candidates.[TraceID] and "+
                "[RTPeaks].[PeakNumber] = Candidates.PeakNumber", con);
            SQLiteDataReader Reader = com.ExecuteReader();
            CandidateView.Rows.Clear();
            Candidates.Clear();
            while(Reader.Read()){
                Candidate C = new Candidate();
                C.StandardID = S.ID;
                C.IonID = Reader.GetInt32(13);
                C.SameRTID = Reader.GetInt32(12);
                C.Score = Reader.GetDouble(2);
                C.SumScore = Reader.GetDouble(14);
                C.MZ = Reader.GetDouble(11);
                C.CandID = Reader.GetInt32(0);
                C.Apex = Reader.GetDouble(5);
                C.Left = Reader.GetDouble(7);
                C.Right = Reader.GetDouble(8);
                C.ApexIn = Reader.GetDouble(4);
                C.Selected = Reader.GetInt32(6) == 1;
                C.Adduct = Reader.GetString(9);
                C.FileID = Reader.GetInt32(10);
                C.Mode = Reader.GetString(15);
                Candidates.Add(C.CandID,C);    
                CandidateView.Rows.Add(C.Adduct,C.FileID,C.MZ,C.Apex,C.SumScore,C.Score,C.ApexIn,Reader.GetDouble(3),C.Selected,C.CandID);
            }
            Reader.Close();
            //Show aternative candidates table
            //double MinMass = S.MZ - (S.MZ * MassError) / 1000000; 
            //double MaxMass = S.MZ + (S.MZ * MassError) / 1000000; 
            //com = new SQLiteCommand(String.Format(
            //    "Select Name, Desc, FileID, Candidates, TargetID from Targets "+
            //    "where MZ > {0} and MZ < {1} order by Name",MinMass,MaxMass), con);
            //Reader = com.ExecuteReader();
            //while(Reader.Read()){
            //    //add legends
            //    if (Reader.GetInt32(3)>0){
            //        TraceView.Series[TraceToFiles[Reader.GetInt32(2)]].LegendText += "; " + Reader.GetString(0);
            //    }
            //    if (Reader.GetInt32(4) != S.ID){
            //        AltStandView.Rows.Add(Reader.GetString(0), Reader.GetString(1), Reader.GetInt32(2), Reader.GetInt32(3));
            //    }
            //}
            CandidateView.SelectionChanged += Candidate_SelectionChanged;
            if (CandidateView.RowCount > 0){
                CandidateView.Rows[0].Selected = true;
                PrevAdduct = "";
                Candidate_SelectionChanged(null, null);
            }else{
                TraceView.Series.Clear();
                TraceToFiles.Clear();
                TraceView.ChartAreas[0].AxisX.ScaleView.ZoomReset(0);
                TraceView.ChartAreas[0].AxisY.ScaleView.ZoomReset(0);
            }
        }

        Dictionary<int, int> TraceToFiles = new Dictionary<int /*FileID*/, int /*Series number*/>(); //File Id to Serie Number relationship

        double MassError = 20.0; //прочитать из таблицы Settings

        bool LoadMassTrace(int FileID, double MZ, /*ref?*/ DataPointCollection P){
            P.Clear();
            double MinMass = MZ - (MZ * MassError) / 1000000; 
            double MaxMass = MZ + (MZ * MassError) / 1000000; 
            SQLiteCommand com = new SQLiteCommand(String.Format(
                "Select StartScan, EndScan, Points from Features, Traces, PointGroups "+
                "where Features.[FeatureID] = Traces.[onFeatureID] and "+
                "Traces.[GroupID] = PointGroups.[GroupID] and "+
                "Features.FileID = {0} and "+
                "Traces.MeanMass > {1} and Traces.MeanMass < {2} "+
                "Order by 1", FileID, MinMass, MaxMass),con);

            SQLiteDataReader Reader = com.ExecuteReader();
            if (!Reader.Read()) return false;
            do{
                int StartScan = Reader.GetInt32(0);
                int EndScan = Reader.GetInt32(1);
                string PStr = Reader.GetString(2);
                byte[] Pbyte = Convert.FromBase64String(PStr);
                int ByteCounter = 0;
                List<double> Masses = new List<double>();
                List<double> Intensities = new List<double>();
                List<double> RTTraces = new List<double>();
                while (ByteCounter < Pbyte.Length){
                    Masses.Add(BitConverter.ToDouble(Pbyte, ByteCounter));
                    ByteCounter += 8;
                    Intensities.Add(BitConverter.ToSingle(Pbyte, ByteCounter));
                    ByteCounter += 4;
                }
                int RTCount = StartScan;
                double RT = RTs[FileID][StartScan];
                while (RTCount <= EndScan){
                    RTTraces.Add(RT);
                    do{
                        RTCount++;
                    } while (!(RTs[FileID].TryGetValue(RTCount, out RT)) && RTCount <= EndScan);
                }
                for (int j = 0; j < Math.Min(Intensities.Count,RTTraces.Count); j++){
                    if (P.Count==0 || RTTraces[j]>P[P.Count-1].XValue){
                        P.AddXY(RTTraces[j], Intensities[j]);
                    }
                }
            } while (Reader.Read());
            return true;
        }

        string PrevAdduct = "";

        void SelectRTInterval(int FileID, double Left, double Right){
            Series S = TraceView.Series[TraceToFiles[FileID]];
            bool flag = false;
            for ( int i = 0 ; i < S.Points.Count ; i++){
                if (S.Points[i].XValue > Right) return;
                if (flag) S.Points[i].BorderWidth = 3;
                flag = S.Points[i].XValue > Left;
            }
        }

        void ClearRTSelection()
        {
            for (int i = 0; i < TraceView.Series.Count; i++){ 
                Series S = TraceView.Series[i];
                for ( int j = 0 ; j < S.Points.Count ; j++){
                    S.Points[j].BorderWidth = 1;
                }
            }
        }

        private void Candidate_SelectionChanged(object sender, EventArgs e){
            if (CandidateView.SelectedRows.Count == 0) return;

            Candidate MC = Candidates[(int)CandidateView.SelectedRows[0].Cells[9].Value];
            Standart S = Standards[(int)StandardView.SelectedRows[0].Cells[4].Value];

            //вытаскиваем подтверждающие кандидаты
            List<int> CIDs = new List<int>();
            foreach(Candidate C in Candidates.Values){
                if (C!=MC && C.SameRTID == MC.SameRTID && C.SameRTID!=0){
                    CIDs.Add(C.CandID);
                }
            }
            //красим линии
            for( int i = 0 ; i<CandidateView.RowCount ; i++){
                if (CIDs.Contains((int)CandidateView.Rows[i].Cells[9].Value)){
                    CandidateView.Rows[i].DefaultCellStyle.BackColor = Color.FromArgb(186,220,236);
                }else{
                    CandidateView.Rows[i].DefaultCellStyle.BackColor = CandidateView.DefaultCellStyle.BackColor;
                }
            }


            if( PrevAdduct != MC.Adduct){
                PrevAdduct = MC.Adduct;
                //Трейсы рисуем 
                TraceView.Series.Clear();
                TraceToFiles.Clear();
                TraceView.ChartAreas[0].AxisX.ScaleView.ZoomReset(0);
                TraceView.ChartAreas[0].AxisY.ScaleView.ZoomReset(0);
                //для каждого файла нужно вывести свой трэйс если таковой будет найден, 
                //трэйс может состоять из нескольких участков соответствующих трейсам в терминах Isotrack
                //подсветить нужно альтернативные гипотезы и конкукрирующие метаболиты - 
                //их же нужно подсвечивать при выборе соответствующих строк 

                //Сначала вытащим ведущий трэйс
                Series LineSerie = null;
                LineSerie = new Series();
                LineSerie.ChartType = SeriesChartType.Line;
                LineSerie.Color = Color.Red;
                int MainFileID = MC.FileID;
                double MZ = MC.MZ;
                LineSerie.LegendText = "File " + MainFileID.ToString();
                //вытаскиваем трэйсы на этой массе
                LoadMassTrace(MainFileID,MZ,LineSerie.Points);
                TraceView.Series.Add(LineSerie);
                TraceToFiles.Add(MainFileID, 0);
                for ( int i = 0 ; i < Files.Count ; i++){
                    int OFileID = Files[i].FileID;
                    if (OFileID == MainFileID) continue;
                    LineSerie = new Series();
                    LineSerie.ChartType = SeriesChartType.Line;
                    LineSerie.LegendText = "File " + OFileID.ToString();
                    if (LoadMassTrace(OFileID, MZ, LineSerie.Points)) {
                        TraceView.Series.Add(LineSerie);
                        TraceToFiles.Add(OFileID, TraceView.Series.Count - 1);
                    }
                }

                //вытаскиваем подтверждающие трейсы
                foreach(int CID in CIDs){
                    //выводим трейс ?- подрезать до границ текущего кандидата?
                    Candidate C;
                    if (Candidates.TryGetValue(CID,out C)){
                        LineSerie = new Series();
                        LineSerie.ChartType = SeriesChartType.Line;
                        LineSerie.BorderDashStyle = ChartDashStyle.Dash;
                        LineSerie.LegendText = "File " + C.FileID.ToString()+"; "+S.Name+"-"+C.Adduct;
                        if (LoadMassTrace(C.FileID, C.MZ, LineSerie.Points)) {
                            TraceView.Series.Add(LineSerie);
                        }
                    }
                }
                TraceView.ChartAreas[0].AxisX.LabelStyle.Format = "f3";
                TraceView.ChartAreas[0].AxisY.LabelStyle.Format = "0.00e+00";


                //Show aternative ions table
                double MinMass = MC.MZ - (MC.MZ * MassError) / 1000000; 
                double MaxMass = MC.MZ + (MC.MZ * MassError) / 1000000; 
                SQLiteCommand com = new SQLiteCommand(String.Format(
                    "Select Name, Desc, FileID, Ions.Candidates, Targets.TargetID, Adduct "+
                    "from Targets,Ions,Candidates "+
                    "where Ions.MZ > {0} and Ions.MZ < {1} and "+
                    "Ions.[TargetID] = Targets.[TargetID]  and "+
                    "Candidates.[IonID] = Ions.[IonID] and Mode = \"{2}\" "+
                    "Group by FileID, Name order by FileID, Name ",MinMass,MaxMass,MC.Mode), con);
                SQLiteDataReader Reader = com.ExecuteReader();
                AltStandView.Rows.Clear();
                while(Reader.Read()){
                    //add legends
                    if (Reader.GetInt32(3)>0){
                        TraceView.Series[TraceToFiles[Reader.GetInt32(2)]].LegendText += "; " + Reader.GetString(0)+"-"+Reader.GetString(5);
                    }
                    if (Reader.GetInt32(4) != S.ID){
                        AltStandView.Rows.Add(Reader.GetString(0), Reader.GetString(1), Reader.GetString(5), Reader.GetInt32(3),Reader.GetInt32(4));
                    }
                }
            }
            AltStandView_SelectionChanged(null, null);
            AltCandView_SelectionChanged(null, null);
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            TraceView.ChartAreas[0].AxisX.ScaleView.ZoomReset(0);
            TraceView.ChartAreas[0].AxisY.ScaleView.ZoomReset(0);
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            SettingsForm SF = new SettingsForm();
            SQLiteCommand com = new SQLiteCommand("Select name, value from Settings", con);
            //DAS = new SQLiteDataAdapter(com);
            SQLiteDataReader dr = com.ExecuteReader();
            SF.SettingsView.Rows.Clear();
            while(dr.Read()){
                SF.SettingsView.Rows.Add(dr.GetString(0), dr.GetString(1));
            }
            SF.ShowDialog();
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            FilesForm FF = new FilesForm();
            for (int i = 0; i < Files.Count ; i++ ){
                FF.FileView.Rows.Add(Files[i].FileID, Files[i].FileName, Files[i].Polarity==1?"+":"-");
            }
            FF.ShowDialog();
        }

        private void AltStandView_SelectionChanged(object sender, EventArgs e){
            AltCandView.Rows.Clear();
            if (AltStandView.SelectedRows.Count == 0) return;
            for ( int i = 0 ; i < AltStandView.SelectedRows.Count ; i++){
                int SID = (int)AltStandView.SelectedRows[i].Cells[4].Value;
                string Adduct = (string)AltStandView.SelectedRows[i].Cells[2].Value;
                SQLiteCommand com = new SQLiteCommand(
                    "Select CandID, Score, RTPeaks.[ApexIntensity], RTPeaks.[Apex], "+
                    "Candidates.Selected, Left, Right, FileID "+ 
                    "from Candidates, RTPeaks, Ions "+
                    "where Ions.TargetID = "+SID.ToString()+" and "+
                    "[Candidates].[IonID]= Ions.[IonID] and "+
                    "RTPeaks.[TraceID] = Candidates.[TraceID] and "+
                    "Ions.Adduct = \""+Adduct+"\" and "+
                    "[RTPeaks].[PeakNumber] = Candidates.PeakNumber order by 2 desc", con);
                    SQLiteDataReader Reader = com.ExecuteReader();
                    while(Reader.Read()){
                        AltCandView.Rows.Add(Reader.GetDouble(3),Reader.GetDouble(1),Reader.GetDouble(2),Reader.GetInt32(4)!=0,Reader.GetDouble(5),Reader.GetDouble(6),Reader.GetInt32(7));
                    }
                Reader.Close();
           }
        }


        private void CandidateView_CellValueChanged(object sender, DataGridViewCellEventArgs e){
            if (e.RowIndex == -1) return;
            if (e.ColumnIndex != 8) return;
            CandidateView.CellValueChanged -= CandidateView_CellValueChanged;
            try{
                //Add radio button functionality
                //collect SameRTID indexes 
                int CID = (int)CandidateView.Rows[e.RowIndex].Cells[9].Value;
                List<int> SRTs = new List<int>();
                SRTs.Add(CID);
                foreach (Candidate C in Candidates.Values){
                    if (Candidates[CID].SameRTID == C.SameRTID && C.SameRTID != 0 ){
                        SRTs.Add(C.CandID);
                    }
                }

                if ((bool)CandidateView.Rows[e.RowIndex].Cells[8].Value){
                    //checked
                    for (int i=0 ; i < CandidateView.RowCount ; i++){
                        if (SRTs.Contains((int)CandidateView.Rows[i].Cells[9].Value)){
                            if (i!=e.RowIndex){
                                CandidateView.Rows[i].Cells[8].Value = true;
                            }
                            SQLiteCommand com = new SQLiteCommand(String.Format("Update Candidates Set Selected = 1 where CandID={0} ", 
                                ((int)CandidateView.Rows[i].Cells[9].Value)), con);
                            com.ExecuteNonQuery();
                        }else{
                            if((bool)CandidateView.Rows[i].Cells[8].Value){
                                CandidateView.Rows[i].Cells[8].Value = false;
                                SQLiteCommand com = new SQLiteCommand(String.Format("Update Candidates Set Selected = 0 where CandID={0} ", 
                                    ((int)CandidateView.Rows[i].Cells[9].Value)), con);
                                com.ExecuteNonQuery();
                            }
                        }
                    }
                }else{
                    //unchecked
                    for (int i=0 ; i < CandidateView.RowCount ; i++){
                        if (SRTs.Contains((int)CandidateView.Rows[i].Cells[9].Value)){
                            CandidateView.Rows[i].Cells[8].Value = false;
                            SQLiteCommand com = new SQLiteCommand(String.Format("Update Candidates Set Selected = 0 where CandID={0} ", 
                                ((int)CandidateView.Rows[e.RowIndex].Cells[9].Value)), con);
                            com.ExecuteNonQuery();
                        }
                    }
                }
            }
            finally{
                CandidateView.CellValueChanged += CandidateView_CellValueChanged;
            }
        }

        private void CandidateView_CurrentCellDirtyStateChanged(object sender, EventArgs e){
            if (CandidateView.IsCurrentCellDirty){
                CandidateView.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        //Save Targets
        private void toolStripButton5_Click(object sender, EventArgs e){
            if (SaveTargetDialog.ShowDialog() != DialogResult.OK) return;
            SQLiteCommand com = new SQLiteCommand(
                "Select Name, Adduct, Mode, Desc, Candidates.MZ, Apex, Left, Right, C13ToCheck, N15ToCheck "+
                "from Targets, Ions, Candidates, RTPeaks "+
                "Where Targets.[TargetID]=[Ions].[TargetID] and "+
                "Candidates.[IonID]=Ions.IonID and "+
                "RTPeaks.TraceID=Candidates.TraceID and "+
                "Candidates.PeakNumber=RTPeaks.PeakNumber and "+
                "Selected = 1", con);
            SQLiteDataReader Reader = com.ExecuteReader();
            StreamWriter sw = new StreamWriter(SaveTargetDialog.FileName);
            sw.WriteLine("Name\tAdduct\tMode\tDesc\tMZ\tRT\tRTMin\tRTMax\tC13TOCHECK\tN15TOCHECK");
            while(Reader.Read()){
                sw.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}",
                    Reader.GetString(0),Reader.GetString(1),Reader.GetString(2),Reader.GetString(3),Reader.GetDouble(4),Reader.GetDouble(5),
                    Reader.GetDouble(6),Reader.GetDouble(7),Reader.GetInt32(8),Reader.GetInt32(9));
            }
            sw.Close();
            Reader.Close();
        }

        private void toolStripButton6_Click(object sender, EventArgs e)
        {
            ThresholdsForm TF = new ThresholdsForm();
            TF.textBox1.Text = String.Format("{0:f1}",WarnIntThreshold);
            TF.textBox2.Text = String.Format("{0:f2}",WarnScoreThreshold);
            TF.textBox3.Text = String.Format("{0:f2}",WarnRTWidthThreshold);
            if (TF.ShowDialog() != DialogResult.OK) return;
            try{
                WarnIntThreshold = Convert.ToDouble(TF.textBox1.Text);
                WarnScoreThreshold = Convert.ToDouble(TF.textBox2.Text);
                WarnRTWidthThreshold = Convert.ToDouble(TF.textBox3.Text);
            }catch(Exception ex){
                MessageBox.Show(ex.Message);
                return;
            }
            MarkStands();
        }

        private void AltCandView_SelectionChanged(object sender, EventArgs e){
            ClearRTSelection();
            for( int i = 0 ; i < AltCandView.SelectedRows.Count ; i++){
                SelectRTInterval(
                    (int)AltCandView.SelectedRows[i].Cells[6].Value, 
                    (double)AltCandView.SelectedRows[i].Cells[4].Value, 
                    (double)AltCandView.SelectedRows[i].Cells[5].Value);
            }
            if (CandidateView.SelectedRows.Count != 0) {
                Candidate MC = Candidates[(int)CandidateView.SelectedRows[0].Cells[9].Value];
                SelectRTInterval(MC.FileID, MC.Left, MC.Right);
            }
        }

        private void StandardView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex < 2 ){
                StoryForm SF = new StoryForm();

                SF.textBox1.Text = "Some text to show";
                SF.Show(); 
            }
        }

        private void toolStripButton7_Click(object sender, EventArgs e)
        {
            if (TraceView.Series.Count == 0) return;
            MemoryStream ms = new MemoryStream();
            TraceView.SaveImage(ms, ChartImageFormat.Bmp);
            Bitmap bm = new Bitmap(ms);
            Clipboard.SetImage(bm);
        }

        //private void MarkStand(string ID, Color C){
        //    for (int i = 0; i < StandardView.RowCount; i++ ){
        //        if (StandardView.Rows[i].Cells[0].Value as string == ID){
        //            StandardView.Rows[i].DefaultCellStyle.BackColor = C;
        //        }
        //    }
        //}


    }
}
