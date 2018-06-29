using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Data.SQLite;
using System.Xml;




namespace Inspector
{
    public partial class MetaRepForm : Form {
        public MetaRepForm() {
            InitializeComponent();
        }

        SQLiteConnection con = null;
        public static mzAccess_Service.MSDataService Service = new mzAccess_Service.MSDataService();

        string TargetFileName = "";
        string ProgramCaption = "IsoTrack Inspector - v.1.6.2 ";


        private void Open_db3_Click(object sender, EventArgs e) {
            //CorrBox.Enabled = false;
            if(SQLiteFileDialog.ShowDialog() != DialogResult.OK)
                return;
            if(!Open_db3(SQLiteFileDialog.FileName))
                return;
            Text = ProgramCaption + Path.GetFileName(SQLiteFileDialog.FileName);
            TargetFileName = Path.GetFileName(SQLiteFileDialog.FileName);
            LFRep(null, null);
            foreach(TargetNum T in TargetIDs) {
                SameMZ(T);
            }
            MRUList.AddRecentFileToMruList(SQLiteFileDialog.FileName);
            ContentChanged = false;
        }

        private bool Open_db3(string FileName) {
            //Interface tunning
            SettingsButton.Enabled = true;
            FilesButton.Enabled = true;
            MaxDisplay = 0.0;

            //Database Indexing
            con = new SQLiteConnection(String.Format("Data Source = {0}", FileName));
            con.Open();
            //Reported table for UI setup
            SQLiteCommand com = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type=\"table\" AND name=\"Report\";", con);
            SQLiteDataReader Reader = com.ExecuteReader();
            bool RepFilled = Reader.Read();
            if(!RepFilled) {
                com = new SQLiteCommand("CREATE TABLE if not exists [Report] (" +
                    " OrderID INT, " +
                    " PosFile INT, " +
                    " NegFile  INT, " +
                    " Reported Int, " +
                    " ShortName VARCHAR(64), " +
                    " Color Int );", con);
                com.ExecuteNonQuery();
            }
            com = new SQLiteCommand("select * from Report ", con);
            Reader = com.ExecuteReader();
            //Reader.Read();
            RepFilled = Reader.Read();
            Reader.Close();

            int Count = 0;
            Files.Clear();
            if(!RepFilled) {
                com = new SQLiteCommand("Select FileName, FileIndex, Mode from Files order by FileIndex", con);
                Reader = com.ExecuteReader();
                Count = 0;
                while(Reader.Read()) {
                    int Mode = Reader.GetInt32(2);
                    Color C = ColorsDefault[Count % ColorsDefault.Length];
                    SQLiteCommand Insert = new SQLiteCommand(String.Format(
                        "Insert Into Report ( OrderID, PosFile, NegFile, Reported, ShortName, Color) Values({0}, {1}, {2}, {3}, \"{4}\", {5} )",
                        Count, Mode > 0 ? Convert.ToString(Reader.GetInt32(1)) : "null", Mode < 0 ? Convert.ToString(Reader.GetInt32(1)) : "null", 1, Path.GetFileName(Reader.GetString(0)), C.ToArgb()), con);
                    Insert.ExecuteNonQuery();
                    Count++;
                }
            }
            FilesForm.MaxShortName = Count;
            //FullRTs

            //Load Reporting Structure
            FilesForm.Pairs = new List<FilesForm.FileRep>();
            LoadPairs();

            com = new SQLiteCommand("Select name, Ions.TargetID, RTMin, RTMax, Ions.MZ, RT, Adduct, Desc, Ions.IonID, Mode, CustomRTMin, CustomRTMax, C13toCheck, N15toCheck, FullRTMin, FullRTMax " +
                "from Targets,Ions where Targets.TargetID==Ions.TargetID", con);
            Reader = com.ExecuteReader();

            TargetIDs = new List<TargetNum>();

            while(Reader.Read()) {
                TargetNum T = new TargetNum();
                T.Name = Reader.GetString(0);
                T.TargetID = Reader.GetInt32(1);
                T.RTMin = Reader.IsDBNull(10) ? Reader.GetDouble(2) : Reader.GetDouble(10);
                T.RTMax = Reader.IsDBNull(11) ? Reader.GetDouble(3) : Reader.GetDouble(11);
                T.MZ = Reader.GetDouble(4);
                T.Adduct = Reader.GetString(6);
                T.Desc = Reader.GetString(7);
                T.IonID = Reader.GetInt32(8);
                T.Mode = Reader.GetString(9);
                T.C13 = Reader.GetInt32(12);
                T.N15 = Reader.GetInt32(13);
                T.FullRTMin = Reader.IsDBNull(14) ? T.RTMin - Convert.ToDouble(Properties.Settings.Default.RTMargins) : Reader.GetDouble(14);
                T.FullRTMin = T.FullRTMin < 0.0 ? 0.0 : T.FullRTMin;
                T.FullRTMax = Reader.IsDBNull(15) ? T.RTMax + Convert.ToDouble(Properties.Settings.Default.RTMargins) : Reader.GetDouble(15);
                T.FullRTMax = T.FullRTMax > MaxDisplay ? MaxDisplay : T.FullRTMax;
                TargetIDs.Add(T);
            }
            return true;
        }

        //Represents single compound (on strings)
        class TargetNum {
            public string Name;
            public int TargetID;
            private double _RTMin;
            public double RTMin {
                set { CachedRTMin = -1.0; CachedRTMax = -1.0; _RTMin = value; }
                get { return _RTMin; }
            }
            private double _RTMax;
            public double RTMax{
                set { CachedRTMin = -1.0; CachedRTMax = -1.0; _RTMax = value; }
                get { return _RTMax; }
            }
            public double FullRTMin;
            public double FullRTMax;
            public double MZ;
            public string Adduct;
            public int IonID;
            public string Desc;
            public string Mode;
            public int C13;
            public int N15;
            public List<string> Isotopes = new List<string>();
            public List<TargetNum> SameMZs = new List<TargetNum>();
            public DataGridViewRow Row;
            public TargetNum Prev = null;

            public double GetCache(FilesForm.FileRep Pair,int Isotope) {
                if(CachedRTMin != RTMin || CachedRTMax != RTMax) {
                    Totals.Clear();
                    CachedRTMin = RTMin;
                    CachedRTMax = RTMax;
                }
                Dictionary<FilesForm.FileRep, double> IsoTotals;
                Totals.TryGetValue(Isotope, out IsoTotals);
                double stub;
                //pairs have been changed
                if (IsoTotals != null && !IsoTotals.TryGetValue(Pair,out stub)) {
                    Totals.Clear();
                    IsoTotals = null;
                }
                if(IsoTotals == null) { 
                    //fill isotope
                    double MZD = (MZ / 1000000.0) * Convert.ToDouble(Properties.Settings.Default.MZDev);
                    double MinMZ = (MZ+1.003354838*((double)Isotope)) - MZD;
                    double MaxMZ = (MZ+1.003354838*((double)Isotope)) + MZD;
                    List<FilesForm.FileRep> Pairs =
                        FilesForm.Pairs.Where(
                            a => a.Reported == true &&
                            !String.IsNullOrEmpty(Mode == "+" ? a.PosFile : a.NegFile)).ToList();

                    string[] FNames = new string[Pairs.Count];
                    double[] MinMasses = new double[Pairs.Count];
                    double[] MaxMasses = new double[Pairs.Count];
                    double[] MinRTs = new double[Pairs.Count];
                    double[] MaxRTs = new double[Pairs.Count];

                    for(int j = 0 ; j < Pairs.Count ; j++) {
                        FNames[j] = Mode == "+" ? Pairs[j].PosFile : Pairs[j].NegFile; 
                        MinMasses[j] = MinMZ; MaxMasses[j] = MaxMZ;
                        MinRTs[j] = RTMin; MaxRTs[j] = RTMax;
                    }
                    string Error;
                    double[] Data = Service.GetTotalArray(FNames, MinMasses, MaxMasses, MinRTs, MaxRTs, Properties.Settings.Default.CacheMode, out Error);
                    if (!String.IsNullOrEmpty(Error)) {
                        MessageBox.Show("mzAccess error: " + Error, "mzAccess error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return 0.0;
                    }
                    IsoTotals = new Dictionary<FilesForm.FileRep, double>();
                    for(int i = 0 ; i < Pairs.Count ; i++) {
                        IsoTotals.Add(Pairs[i], Data[i]);
                    }
                    Totals.Add(Isotope, IsoTotals);
                }
                return IsoTotals[Pair];
            }

            double CachedRTMin = 0.0;
            double CachedRTMax = 0.0;

            Dictionary<int, Dictionary<FilesForm.FileRep, double>> Totals = new Dictionary<int, Dictionary<FilesForm.FileRep, double>>();


            public override string ToString() {
                return Name;
            }
        }

        //List<string> Files = new List<string>();
        //List of targets
        List<TargetNum> TargetIDs = null;

        //Trace info - corresponds to cells in the table
        class TraceNumbers {
            public string FileName;
            public TargetNum Target;
            public FilesForm.FileRep Pair = null;
            public DataGridViewCell Cell = null;
            public string Isotope;

            private double LoadedMinRT = 0.0;
            private double LoadedMaxRT = 0.0;

            public override string ToString() {
                double? V = GetValue();
                if(V == null || V == 0.0) {
                    return "";
                } else {
                    return V.Value.ToString("0.###e+00");
                }
            }

            //Get value for the cell
            public double? GetValue(double CorrTh = -1.0) {
                if(FileName == null)
                    return null;
                Total = LoadTotal();
                return Total;
            }

            List<double> Intensities = null;
            List<double> RTTraces = null;

            private bool LoadPoints() {
                //by default - loading from target cache
                    return LoadPoints(Target.FullRTMin, Target.FullRTMax, out this.Intensities, out this.RTTraces);
            }

            double? Total = null;

            public double LoadTotal() {
                //if(Isotope == "C0N0") {
                    int CX = Convert.ToInt32(Isotope.Substring(1, Isotope.IndexOf('N') - 1));
                    return Target.GetCache(Pair,CX);
                //} else {
                //    double MZ = Target.MZ + MetaRepForm.IsotopesMassShift(Isotope);
                //    double MZD = (MZ / 1000000.0) * Convert.ToDouble(Properties.Settings.Default.MZDev);
                //    double MinMZ = MZ - MZD;
                //    double MaxMZ = MZ + MZD;
                //    string Error;
                //    if(String.IsNullOrWhiteSpace(FileName)) {
                //        return 0.0;
                //    }
                //    double ret = Service.GetTotal(FileName, MinMZ, MaxMZ, Target.RTMin, Target.RTMax, Properties.Settings.Default.CacheMode, out Error);
                //    if(!String.IsNullOrWhiteSpace(Error)) {
                //        MessageBox.Show(Error, "WebService Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //        return 0.0;
                //    }
                //    return ret;
                //}
            }

            public bool LoadPoints(double MinRT, double MaxRT, out List<double> Intensities, out List<double> RTTraces) {
                if(MinRT < LoadedMinRT || MaxRT > LoadedMaxRT) {
                    double MZ = Target.MZ + MetaRepForm.IsotopesMassShift(Isotope);
                    double MZD = (MZ / 1000000.0) * Convert.ToDouble(Properties.Settings.Default.MZDev);
                    double MinMZ = MZ - MZD;
                    double MaxMZ = MZ + MZD;
                    double[] data = null;
                    string Error;
                    Intensities = new List<double>();
                    RTTraces = new List<double>();
                    if(String.IsNullOrWhiteSpace(FileName)) {
                        Intensities.Add(0.0);
                        Intensities.Add(0.0);
                        RTTraces.Add(Target.FullRTMin);
                        RTTraces.Add(Target.FullRTMax);
                        return true;
                    }
                    data = Service.GetChromatogram(FileName, MinMZ, MaxMZ, MinRT, MaxRT, Properties.Settings.Default.CacheMode, out Error);
                    if(data == null) {
                        MessageBox.Show(Error, "WebService Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Intensities = null;
                        RTTraces = null;
                        return false;
                    }
                    for(int i = 0 ; i < data.Length ; i += 2) {
                        Intensities.Add(data[i + 1]);
                        RTTraces.Add(data[i]);
                    }
                    if(data.Length == 0) {
                        Intensities.Add(0.0);
                        Intensities.Add(0.0);
                        RTTraces.Add(Target.FullRTMin);
                        RTTraces.Add(Target.FullRTMax);
                    }

                    LoadedMinRT = MinRT;
                    LoadedMaxRT = MaxRT;
                    if(Intensities != this.Intensities) {
                        this.Intensities = Intensities.ToList();
                    }
                    if(RTTraces != this.RTTraces) {
                        this.RTTraces = RTTraces.ToList();
                    }
                    return true;
                } else {
                    //copy from existing points 
                    RTTraces = new List<double>();
                    Intensities = new List<double>();
                    for(int i = 0 ; i < this.RTTraces.Count ; i++) {
                        if(this.RTTraces[i] >= MinRT && this.RTTraces[i] <= MaxRT) {
                            RTTraces.Add(this.RTTraces[i]);
                            Intensities.Add(this.Intensities[i]);
                        }
                    }
                    return true;
                }
            }

            public void ClearPoints() {
                LoadedMinRT = 0.0;
                LoadedMaxRT = 0.0;
                Intensities = null;
                RTTraces = null;
            }
        }

        //All reported traces
        static public int Fixed = 8;

        public class SFile {
            public int ID;
            public string Name;
            public int Mode;
            public bool toRep;
            public string ColumnName;
        }

        public static List<SFile> Files = new List<SFile>();

        private void LFRep(object sender, EventArgs e) {
            //CorrBox.Enabled = false;
            ValueView.Rows.Clear();
            for(int i = ValueView.Columns.Count - 1 ; i >= Fixed ; i--) {
                ValueView.Columns.Remove(ValueView.Columns[i]);
            }
            //columns by files
            DataGridViewColumn Col = null;
            //File pair columns
            for(int i = 0 ; i < FilesForm.Pairs.Count ; i++) {
                if(FilesForm.Pairs[i].Reported) {
                    Col = new DataGridViewColumn();
                    Col.HeaderCell.Style.BackColor = FilesForm.Pairs[i].Color;
                    if((int)FilesForm.Pairs[i].Color.R + (int)FilesForm.Pairs[i].Color.G + (int)FilesForm.Pairs[i].Color.B < 384) {
                        Col.HeaderCell.Style.ForeColor = Color.White;
                    }
                    Col.HeaderText = FilesForm.Pairs[i].ShortName;
                    Col.CellTemplate = new DataGridViewTextBoxCell();
                    Col.CellTemplate.ValueType = typeof(double);
                    Col.DefaultCellStyle.Format = "0.000E+00";
                    Col.ReadOnly = true;
                    Col.Width = 80;
                    ValueView.Columns.Add(Col);
                }
            }
            foreach(DataGridViewColumn C in ValueView.Columns) {
                C.SortMode = DataGridViewColumnSortMode.Programmatic;
                C.HeaderCell.SortGlyphDirection = SortOrder.None;
            }


            foreach(TargetNum T in TargetIDs) {
                ValueView.Rows.Add(T, T.Desc, T.Adduct, T.C13, T.Mode, T.MZ, T.RTMin, T.RTMax);
                T.Row = ValueView.Rows[ValueView.Rows.Count - 2];
                if(T.Row.Index % 2 == 0) {
                    T.Row.DefaultCellStyle.BackColor = Color.FromArgb(255, 222, 230, 244);
                } else {
                    T.Row.DefaultCellStyle.BackColor = Color.FromArgb(255, 242, 247, 255);
                }
            }

            //Trace collection
            for(int i = 0 ; i < ValueView.RowCount - 1 ; i++) {
                int ColNumber = Fixed-1;
                for(int j = 0 ; j < FilesForm.Pairs.Count ; j++) {
                    if(FilesForm.Pairs[j].Reported) {
                        ColNumber++;
                    } else {
                        continue;
                    }
                    TraceNumbers T = new TraceNumbers();
                    T.Target = TargetIDs[i];
                    T.Pair = FilesForm.Pairs[j];
                    T.FileName = T.Target.Mode == "+" ? T.Pair.PosFile : T.Pair.NegFile;
                    T.Isotope = "C0N0";
                    ValueView.Rows[i].Cells[ColNumber].Value = T;
                    T.Cell = ValueView.Rows[i].Cells[ColNumber];
                }
            }
        }

        List<int> UniqCols;
        List<int> UniqRows;
        List<int> Rows = new List<int>();
        List<int> Columns = new List<int>();
        TargetNum SingleTarget = null;
        List<TraceNumbers> SelectedTraces = new List<TraceNumbers>();
        List<TargetNum> TargetstoShow = new List<TargetNum>();
        double OrigRTMin = 0.0;
        double OrigRTMax = 0.0;
        bool SingleShow = true;

        private void ValueView_SelectionChanged(object sender, EventArgs e) {
            TraceView.ChartAreas[0].AxisX.ScaleView.ZoomReset(0);
            TraceView.ChartAreas[0].AxisY.ScaleView.ZoomReset(0);
            TraceView.Series.Clear();
            BarView.Series.Clear();

            //Выделение набора ячеек 
            //Single Traget, Single Peak, SingleShow
            bool SinglePeak = ValueView.SelectedCells.Count == 1;
            SingleShow = true;
            if(ValueView.SelectedCells.Count == 0)
                return;

            TargetstoShow.Clear();
            Rows.Clear();
            Columns.Clear();
            SelectedTraces.Clear();

            for(int i = ValueView.SelectedCells.Count - 1 ; i >= 0 ; i--) {
                if(ValueView.SelectedCells[i].ColumnIndex >= Fixed && ValueView.SelectedCells[i].Value != null) {
                    TraceNumbers TN = ValueView.SelectedCells[i].Value as TraceNumbers;
                    SelectedTraces.Add(TN);
                    if(!TargetstoShow.Contains(TN.Target)) {
                        TargetstoShow.Add(TN.Target);
                    }
                    Rows.Add(ValueView.SelectedCells[i].RowIndex);
                    Columns.Add(ValueView.SelectedCells[i].ColumnIndex);
                }
            }

            if(TargetstoShow.Count == 0) {
                SingleTarget = null;
                return;
            }

            SingleShow = (TargetstoShow.Count == 1);
            SingleTarget = TargetstoShow.Count == 1 ? TargetstoShow[0] : null;

            //определение границ графиков 
            if(sender != null) {
                OrigRTMin = 0.0;
                OrigRTMax = 0.0;
                foreach(TargetNum TN in TargetstoShow) {
                    if(TN.FullRTMax > 0.0) {
                        OrigRTMax = Math.Max(OrigRTMax, TN.FullRTMax);
                        if(OrigRTMin == 0.0) {
                            OrigRTMin = TN.FullRTMin;
                        } else {
                            OrigRTMin = Math.Min(OrigRTMin, TN.FullRTMin);
                        }
                    }

                }
                DisplayRTMax = OrigRTMax;
                DisplayRTMin = OrigRTMin;
            }

            ShowGraphs();
            ShowBars();
        }


        public void ShowGraphs() {

            double StartRT = 0.0;
            double EndRT = 0.0;
            TraceView.Series.Clear();
            TraceView.ChartAreas[0].RecalculateAxesScale();
            TraceView.ChartAreas[0].AxisY.Maximum = double.NaN;
            //Graphs!
            for(int i = SelectedTraces.Count - 1 ; i >= 0 ; i--) {

                TraceNumbers TN = SelectedTraces[i];

                Series LineSerie = null;
                LineSerie = new Series(String.Format("{0}({1})-{3} on {2}", TN.Target.Name, TN.Target.Adduct, TN.Pair.ShortName, TN.Isotope));
                LineSerie.ChartType = SeriesChartType.Line;
                //LineSerie.Color = TN.Pair.Color;

                List<double> Intensities;
                List<double> RTTraces;

                if(!TN.LoadPoints(DisplayRTMin, DisplayRTMax, out Intensities, out RTTraces))
                    continue;

                for(int j = 0 ; j < Intensities.Count ; j++) {
                    LineSerie.Points.AddXY(RTTraces[j], Intensities[j]);
                }

                //Selection boundaries 
                StartRT = TN.Target.RTMin;
                EndRT = TN.Target.RTMax;
                //if(!SingleShow) {
                //    foreach(DataPoint P in LineSerie.Points) {
                //        if(P.XValue >= StartRT && P.XValue <= EndRT) {
                //            P.BorderWidth = 3;
                //        }
                //    }
                //}
                TraceView.Series.Add(LineSerie);
            }

            TraceView.ChartAreas[0].AxisX.StripLines.Clear();
            TargetstoShow.Sort((T1, T2) => T1.RTMin.CompareTo(T2.RTMin));
            for(int i = 0 ; i < TargetstoShow.Count ; i++) {
                TargetNum T = TargetstoShow[i];
                StripLine SL = new StripLine();
                SL.IntervalOffset = T.RTMin;
                SL.StripWidth = T.RTMax - T.RTMin;
                SL.BackColor = Color.FromArgb(120, Color.LightGray);
                if(!SingleShow) { //if several targets selected - show them as a stips 
                    string ns = "";
                    for (int j = i-1 ; j >= 0 ; j--) {
                        if(TargetstoShow[j].RTMax > T.RTMin)
                            ns += "\n";
                    }
                    SL.Text = ns+T.Name+"("+T.Adduct+")";
                    SL.TextOrientation = TextOrientation.Horizontal;
                    SL.TextAlignment = StringAlignment.Center;
                    SL.TextLineAlignment = StringAlignment.Near;
                }
                TraceView.ChartAreas[0].AxisX.StripLines.Add(SL);
            }


            if(SingleShow) {
                StartSelectedRT = StartRT;
                EndSelectedRT = EndRT;
                //if (SingleTarget.SameMZs.Count > 0) {
                    StripLine SL = TraceView.ChartAreas[0].AxisX.StripLines[0];
                    SL.Text = SingleTarget.Name+"("+SingleTarget.Adduct+")";
                    SL.TextOrientation = TextOrientation.Horizontal;
                    SL.TextAlignment = StringAlignment.Center;
                    SL.TextLineAlignment = StringAlignment.Near;
                //}
                if(ShowNeighbours) {
                    for(int i = 0 ; i < SingleTarget.SameMZs.Count ; i++) {
                        TargetNum T = SingleTarget.SameMZs[i];
                        SL = new StripLine();
                        string ns = "";
                        for(int j = i - 1 ; j >= 0 ; j--) {
                            if(SingleTarget.SameMZs[j].RTMax > T.RTMin)
                                ns += "\n";
                        }
                        if(!(SingleTarget.RTMin > T.RTMax || SingleTarget.RTMax < T.RTMin))
                            ns += "\n";
                        SL.IntervalOffset = T.RTMin;
                        SL.StripWidth = T.RTMax - T.RTMin;
                        SL.BackColor = Color.FromArgb(120, Color.LightCoral);
                        SL.Text = ns + T.Name + "(" + T.Adduct + ")";
                        SL.TextOrientation = TextOrientation.Horizontal;
                        SL.TextAlignment = StringAlignment.Center;
                        SL.TextLineAlignment = StringAlignment.Near;
                        TraceView.ChartAreas[0].AxisX.StripLines.Add(SL);
                    }
                }
            } else {
                StartSelectedRT = 0.0;
                EndSelectedRT = 0.0;
            }

            TraceView.ChartAreas[0].AxisX.LabelStyle.Format = "f2";
            TraceView.ChartAreas[0].AxisY.LabelStyle.Format = "0.00e+00";
            if(OrigRTMax > 0.0) {
                TraceView.ChartAreas[0].AxisX.Minimum = DisplayRTMin;
                TraceView.ChartAreas[0].AxisX.Maximum = DisplayRTMax;
                CustomAxisXScale();
            }
        }

        public void ShowBars() {
            //series for columns
            BarView.Series.Clear();
            UniqCols = new List<int>();
            for(int i = 0 ; i < Columns.Count ; i++) {
                if(!UniqCols.Contains(Columns[i])) {
                    UniqCols.Add(Columns[i]);
                }
            }
            UniqCols.Sort();
            UniqRows = new List<int>();
            for(int i = 0 ; i < Rows.Count ; i++) {
                if(!UniqRows.Contains(Rows[i])) {
                    UniqRows.Add(Rows[i]);
                }
            }
            UniqRows.Sort();
            List<List<int>> RowsbyCols = new List<List<int>>();
            for(int i = 0 ; i < UniqCols.Count ; i++) {
                RowsbyCols.Add(new List<int>());
            }
            for(int i = 0 ; i < Rows.Count ; i++) {
                RowsbyCols[UniqCols.IndexOf(Columns[i])].Add(Rows[i]);
            }
            //Filling with values
            BarView.Series.Clear();
            BarView.ChartAreas[0].AxisX.CustomLabels.Clear();
            if(UniqRows.Count == 0)
                return;
            for(int i = 0 ; i < UniqCols.Count ; i++) {
                string Name = Path.GetFileName(ValueView.Columns[UniqCols[i]].HeaderText);
                Series BarSerie = new Series(Name);
                BarSerie.Color = ValueView.Columns[UniqCols[i]].HeaderCell.Style.BackColor;
                for(int j = 0 ; j < UniqRows.Count ; j++) {
                    if(RowsbyCols[i].IndexOf(UniqRows[j]) != -1) {
                        BarSerie.Points.AddY((ValueView.Rows[UniqRows[j]].Cells[UniqCols[i]].Value as TraceNumbers).GetValue());
                    } else {
                        BarSerie.Points.AddY(0.0);
                    }
                    //differ nitrogen by color
                    //BarSerie.Points[i].
                }
                BarView.Series.Add(BarSerie);
            }
            //Axis labels
            int LastI = -1;
            string AdductStr = "(" + ValueView.Rows[UniqRows[0]].Cells[2].Value.ToString() + ")";
            if(AdductStr == "()")
                AdductStr = "";
            string Comp = ValueView.Rows[UniqRows[0]].Cells[0].Value.ToString() + AdductStr;
            for(int i = 0 ; i < UniqRows.Count ; i++) {
                //Isotope Label
                BarView.ChartAreas[0].AxisX.CustomLabels.Add(new CustomLabel((double)i + 0.5, (double)i + 1.5,
                    (ValueView.Rows[UniqRows[i]].Cells[0].Value is TargetNum)?"C0":"C"+ValueView.Rows[UniqRows[i]].Cells[3].Value.ToString(),
                    0, LabelMarkStyle.SideMark));

                //Compound label
                AdductStr = i + 1 < UniqRows.Count ? "(" + ValueView.Rows[UniqRows[i + 1]].Cells[2].Value.ToString() + ")" : "";
                if(AdductStr == "()")
                    AdductStr = "";
                string NewComp = (i + 1 < UniqRows.Count ? ValueView.Rows[UniqRows[i + 1]].Cells[0].Value.ToString() : "") + AdductStr;
                if(i + 1 == UniqRows.Count || NewComp != Comp) {
                    BarView.ChartAreas[0].AxisX.CustomLabels.Add(new CustomLabel((double)LastI + 1.5, (double)i + 1.5,
                        //ValueView.Rows[UniqRows[i]].Cells[0].Value.ToString(),1,LabelMarkStyle.SideMark));
                        Comp, 1, LabelMarkStyle.SideMark));
                    if(i + 1 < UniqRows.Count) {
                        LastI = i;
                        Comp = NewComp;
                    }
                }
            }
            //Relative and absolute values
            double Max = 0.0;
            if(BarRelativeButton.Checked) {
                //нормализация пометаболитно!!
                for(int i = 0 ; i < BarView.Series.Count ; i++) {
                    double Sum = 0.0;
                    for(int j = 0 ; j < BarView.Series[i].Points.Count ; j++) {
                        Sum += BarView.Series[i].Points[j].YValues[0];
                    }
                    for(int j = 0 ; j < BarView.Series[i].Points.Count ; j++) {
                        if(Sum > 0.0) {
                            BarView.Series[i].Points[j].YValues[0] = BarView.Series[i].Points[j].YValues[0] / Sum;
                        } else {
                            BarView.Series[i].Points[j].YValues[0] = 0.0;
                        }
                        if(BarView.Series[i].Points[j].YValues[0] > Max)
                            Max = BarView.Series[i].Points[j].YValues[0];
                    }
                    BarView.ChartAreas[0].AxisY.LabelStyle.Format = "";
                }
            } else {
                for(int i = 0 ; i < BarView.Series.Count ; i++) {
                    for(int j = 0 ; j < BarView.Series[i].Points.Count ; j++) {
                        if(BarView.Series[i].Points[j].YValues[0] > Max)
                            Max = BarView.Series[i].Points[j].YValues[0];
                    }
                }
                BarView.ChartAreas[0].AxisY.LabelStyle.Format = "0.00e+00";
            }
            if(Max > 0.0) {
                BarView.ChartAreas[0].AxisY.Maximum = Max * 1.05;
            } else {
                BarView.ChartAreas[0].AxisY.Maximum = 1.0;
            }
        }

        private void CopyPicture_Click(object sender, EventArgs e) {
            if(ValueView.Rows.Count == 0 || TraceView.Series.Count == 0)
                return;
            MemoryStream ms = new MemoryStream();
            TraceView.SaveImage(ms, ChartImageFormat.Bmp);
            Bitmap bm = new Bitmap(ms);
            Clipboard.SetImage(bm);
        }

        private void SaveReport(object sender, EventArgs e) {
            SaveTextDialog.FileName = Path.GetFileNameWithoutExtension(TargetFileName);
            if(SaveTextDialog.ShowDialog() != DialogResult.OK)
                return;
            string ToSave = "";
            StreamWriter sw = new StreamWriter(SaveTextDialog.FileName);
            foreach(DataGridViewColumn C in ValueView.Columns) {
                ToSave += String.Format("{0}\t", C.HeaderText);
            }
            ToSave = ToSave.Substring(0, ToSave.Length - 1);
            sw.WriteLine(ToSave);
            foreach(DataGridViewRow R in ValueView.Rows) {
                ToSave = "";
                foreach(DataGridViewCell C in R.Cells) {
                    if(C.Value != null) {
                        ToSave += String.Format("{0}\t", C.Value.ToString());
                    } else {
                        ToSave += "0.0\t";
                    }
                }
                ToSave = ToSave.Substring(0, ToSave.Length - 1);
                sw.WriteLine(ToSave);
            }
            sw.Close();
        }

        //isotope report
        private void AddIsotopes(TargetNum T) {
            T.Row.Cells[3].Value = 0;
            for(int i = T.C13 ; i > 0 ; i--) {
                //if next record belong to new ion
                DataGridViewRow R = T.Row.Clone() as DataGridViewRow;
                R.Cells[0].Value = T.Name;
                R.Cells[1].Value = T.Desc;
                R.Cells[2].Value = T.Adduct;
                R.Cells[3].Value = i;
                R.Cells[4].Value = T.Mode;
                R.Cells[5].Value = T.MZ + IsotopesMassShift("C" + i.ToString() + "N0");
                R.Cells[6].Value = T.RTMin;
                R.Cells[7].Value = T.RTMax;
                R.Frozen = T.Row.Frozen;
                R.DefaultCellStyle = (i % 2 == 0)?ValueView.DefaultCellStyle:ValueView.AlternatingRowsDefaultCellStyle;
                ValueView.Rows.Insert(T.Row.Index + 1, R);
                //ValueView.Rows.Insert(T.Row.Index + 1, T.Name, T.Desc, T.Adduct, i, T.MZ + IsotopesMassShift("C" + i.ToString() + "N0"), T.RTMin, T.RTMax);
                int ColNumber = 0;
                for(int j = 0 ; j < FilesForm.Pairs.Count ; j++) {
                    if(FilesForm.Pairs[j].Reported) {
                        ColNumber++;
                    } else {
                        continue;
                    }
                    TraceNumbers Tr = new TraceNumbers();
                    Tr.Target = T;
                    Tr.Isotope = "C" + i.ToString() + "N0";
                    Tr.Pair = FilesForm.Pairs[j];
                    if(T.Mode == "+") {
                        Tr.FileName = Path.GetFileName(FilesForm.Pairs[j].PosFile);
                    } else {
                        Tr.FileName = Path.GetFileName(FilesForm.Pairs[j].NegFile);
                    }
                    ValueView.Rows[T.Row.Index + 1].Cells[ColNumber + Fixed - 1].Value = Tr;
                    Tr.Cell = ValueView.Rows[T.Row.Index + 1].Cells[ColNumber + Fixed - 1];
                    ValueView.Rows[T.Row.Index + 1].Cells[ColNumber + Fixed - 1].ContextMenuStrip = strip;
                }
                ValueView.Rows[T.Row.Index + 1].ReadOnly = true;
            }
            if(ColorIsoButton.Checked) {
                for (int i = Fixed ; i < ValueView.ColumnCount ; i++) {
                    double Acc = 0.0;
                    for (int j = T.Row.Index ; j <= T.Row.Index+T.C13 ; j++) {
                        Acc += (ValueView.Rows[j].Cells[i].Value as TraceNumbers).GetValue()??0.0;
                    }
                    if(Acc == 0.0) continue;
                    for (int j = T.Row.Index + 1 ; j <= T.Row.Index+T.C13 ; j++) {
                        double Part = ((ValueView.Rows[j].Cells[i].Value as TraceNumbers).GetValue() ?? 0.0) / Acc;
                        if(Part == 0.0)
                            continue;
                        Color C = Color.FromArgb(255, 255, (int)(255.0 * (1.0 - Part)), (int)(255.0 * (1.0 - Part)));
                        //Color C = ValueView.Columns[i].HeaderCell.Style.BackColor;
                        ValueView.Rows[j].Cells[i].Style.BackColor = ControlPaint.Light(C, (float)Part);
                    }
                }
            }

            T.Row.Cells[2].ReadOnly = true;
            T.Row.Cells[3].ReadOnly = true;
            if(T.Row.Frozen) {
                for(int i = 0 ; i < T.C13 ; i++) {
                    ValueView.Rows[T.Row.Index + i + 1].Frozen = true;
                    FrozenRowCount++;
                }
            }
        }

        private void HideIsotopes(TargetNum T) {
            for(int RowIndex = T.Row.Index + 1 ; RowIndex < ValueView.RowCount - 1 ;) {
                DataGridViewRow R = ValueView.Rows[RowIndex];
                if(R.Cells[0].Value.ToString() != ValueView.Rows[T.Row.Index].Cells[0].Value.ToString() ||     //ID
                   R.Cells[2].Value.ToString() != ValueView.Rows[T.Row.Index].Cells[2].Value.ToString()) {     //Adduct
                    break;
                }
                if(R.Frozen)
                    FrozenRowCount--;
                ValueView.Rows.Remove(R);
            }
            T.Row.Cells[3].Value = T.C13;
            T.Row.Cells[2].ReadOnly = false;
            T.Row.Cells[3].ReadOnly = false;
        }

        private void ChangeIsotopes(TargetNum T) {
            T.Row.Cells[3].Value = 0;
            for(int i = 1 ; i <= T.C13 ; i++) {
                //if next record belong to new ion
                DataGridViewRow R = ValueView.Rows[T.Row.Index + i];
                R.Cells[1].Value = T.Desc;
                R.Cells[4].Value = T.Mode;
                R.Cells[5].Value = T.MZ + IsotopesMassShift("C" + i.ToString() + "N0");
                R.Cells[6].Value = T.RTMin;
                R.Cells[7].Value = T.RTMax;
                for(int j = Fixed ; j < ValueView.ColumnCount ; j++) {
                    (R.Cells[j].Value as TraceNumbers).ClearPoints();
                    if (T.Mode == "-") {
                        (R.Cells[j].Value as TraceNumbers).FileName =
                            (R.Cells[j].Value as TraceNumbers).Pair.NegFile;
                    }else {
                        (R.Cells[j].Value as TraceNumbers).FileName =
                            (R.Cells[j].Value as TraceNumbers).Pair.PosFile;
                    }
                    ValueView.InvalidateCell(j, T.Row.Index + i); 
                }
            }
        }


        private void CopyData_Click(object sender, EventArgs e) {
            if(ValueView.Rows.Count == 0)
                return;
            string ToCopy = "";
            for(int i = 0 ; i < TraceView.Series.Count ; i++) {
                Series Serie = TraceView.Series[i];
                string Caption = Serie.Name + "\t";
                string Rts = "";
                string Ints = "\t";
                for(int j = 0 ; j < Serie.Points.Count ; j++) {
                    Rts += String.Format("{0}\t", Serie.Points[j].XValue);
                    Ints += String.Format("{0}\t", Serie.Points[j].YValues[0]);
                }
                ToCopy += Caption + Rts + "\n";
                ToCopy += Ints + "\n";
            }
            Clipboard.SetText(ToCopy);
        }


        private void ValueView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e) {
            SortOrder SO = new SortOrder();
            switch(ValueView.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection) {
                case SortOrder.None: {
                        SO = SortOrder.Ascending;
                        break;
                    }
                case SortOrder.Ascending: {
                        SO = SortOrder.Descending;
                        break;
                    }
                case SortOrder.Descending:  {
                        ValueView.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection = SortOrder.None;
                        return;
                }
            }
            foreach(DataGridViewColumn c in ValueView.Columns) {
                c.HeaderCell.SortGlyphDirection = SortOrder.None;
            }
            ValueView.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection = SO;

            List<DataGridViewRow> toFix = new List<DataGridViewRow>();
            for(int i = 0 ; i < ValueView.RowCount ; i++) {
                if(ValueView.Rows[i].Frozen) {
                    toFix.Add(ValueView.Rows[i]);
                }else {
                    break;
                }
            }
            foreach(DataGridViewRow R in toFix) {
                UnFixRow(R);
            }

            if(e.ColumnIndex <= 2) {
                ValueView.Sort(new RowComparerString(SO,e.ColumnIndex));
            }else {
                ValueView.Sort(new RowComparerNumeric(SO,e.ColumnIndex));
            }

            foreach(DataGridViewRow R in toFix) {
                FixRow(R);
            }
            RecolorRows();
        }


        private class RowComparerNumeric : System.Collections.IComparer {
            private static int sortOrderModifier = 1;
            private int ColumnNumber;

            public RowComparerNumeric(SortOrder sortOrder, int ColumnNumber) {
                if(sortOrder == SortOrder.Descending) {
                    sortOrderModifier = -1;
                } else if(sortOrder == SortOrder.Ascending) {
                    sortOrderModifier = 1;
                }
                this.ColumnNumber = ColumnNumber;
            }

            public int Compare(object x, object y) {
                DataGridViewRow RowX = (DataGridViewRow)x;
                DataGridViewRow RowY = (DataGridViewRow)y;

                DataGridViewRow TargetRowX;
                DataGridViewRow TargetRowY;

                TargetNum TargetX;
                TargetNum TargetY;

                int CompareResult = 0;

                if (RowX.Cells[0].Value is TargetNum) {
                    TargetRowX = RowX;
                    TargetX = RowX.Cells[0].Value as TargetNum;
                }else {
                    TargetRowX = (RowX.Cells[Fixed + 1].Value as TraceNumbers).Target.Row;
                    TargetX = (RowX.Cells[Fixed + 1].Value as TraceNumbers).Target;
                }
                if (RowY.Cells[0].Value is TargetNum) {
                    TargetRowY = RowY;
                    TargetY = RowY.Cells[0].Value as TargetNum;
                }else {
                    TargetRowY = (RowY.Cells[Fixed + 1].Value as TraceNumbers).Target.Row;
                    TargetY = (RowY.Cells[Fixed + 1].Value as TraceNumbers).Target;
                }

                if (TargetX == TargetY) { //compare by isotopes 
                    CompareResult = Convert.ToInt32(RowX.Cells[3].Value).CompareTo(Convert.ToInt32(RowY.Cells[3].Value));
                    return CompareResult;
                }

                string strX = TargetRowX.Cells[ColumnNumber].Value.ToString();
                string strY = TargetRowY.Cells[ColumnNumber].Value.ToString();

                double ValueX = strX == "" ? 0.0 : Convert.ToDouble(strX);
                double ValueY = strY == "" ? 0.0 : Convert.ToDouble(strY);
                CompareResult = ValueX.CompareTo(ValueY) * sortOrderModifier;
                if (CompareResult == 0) {
                    strX = TargetX.Name + TargetX.Adduct;
                    strY = TargetY.Name + TargetY.Adduct;
                    CompareResult = strX.CompareTo(strY);
                }
                return CompareResult;
            }
        }

        private class RowComparerString : System.Collections.IComparer {
            private static int sortOrderModifier = 1;
            private int ColumnNumber;

            public RowComparerString(SortOrder sortOrder, int ColumnNumber) {
                if(sortOrder == SortOrder.Descending) {
                    sortOrderModifier = -1;
                } else if(sortOrder == SortOrder.Ascending) {
                    sortOrderModifier = 1;
                }
                this.ColumnNumber = ColumnNumber;
            }

            public int Compare(object x, object y) {
                DataGridViewRow RowX = (DataGridViewRow)x;
                DataGridViewRow RowY = (DataGridViewRow)y;

                DataGridViewRow TargetRowX;
                DataGridViewRow TargetRowY;

                TargetNum TargetX;
                TargetNum TargetY;

                int CompareResult = 0;

                if (RowX.Cells[0].Value is TargetNum) {
                    TargetRowX = RowX;
                    TargetX = RowX.Cells[0].Value as TargetNum;
                }else {
                    TargetRowX = (RowX.Cells[Fixed + 1].Value as TraceNumbers).Target.Row;
                    TargetX = (RowX.Cells[Fixed + 1].Value as TraceNumbers).Target;
                }
                if (RowY.Cells[0].Value is TargetNum) {
                    TargetRowY = RowY;
                    TargetY = RowY.Cells[0].Value as TargetNum;
                }else {
                    TargetRowY = (RowY.Cells[Fixed + 1].Value as TraceNumbers).Target.Row;
                    TargetY = (RowY.Cells[Fixed + 1].Value as TraceNumbers).Target;
                }

                if (TargetX == TargetY) { //compare by isotopes 
                    CompareResult = Convert.ToInt32(RowX.Cells[3].Value).CompareTo(Convert.ToInt32(RowY.Cells[3].Value));
                    return CompareResult;
                }
                string ValueX = TargetRowX.Cells[ColumnNumber].Value.ToString();
                string ValueY = TargetRowY.Cells[ColumnNumber].Value.ToString();
                CompareResult = ValueX.CompareTo(ValueY) * sortOrderModifier;
                if (CompareResult == 0) {
                    ValueX = TargetX.Name + TargetX.Adduct;
                    ValueY = TargetY.Name + TargetY.Adduct;
                    CompareResult = ValueX.CompareTo(ValueY);
                }
                return CompareResult;
            }
        }

        private static double IsotopesMassShift(string Iso) {
            //sequence C0N0 C0N1 C1N0 C1N1 C2N0 and so for
            if(Iso.Contains('/')) {
                Iso = Iso.Split(new char[] { '/' })[0];
            }
            int CX = Convert.ToInt32(Iso.Substring(1, Iso.IndexOf('N') - 1));
            int NX = Convert.ToInt32(Iso.Substring(Iso.IndexOf('N') + 1));
            double Res = CX * 1.003354838 + NX * 0.9970348934;
            return Res;
        }


        //изменение интервала для выбранных данных задевает не только выбранные ячейки но и весь таргет 
        private void SetSelectedData() {
            for(int i = ValueView.Rows.IndexOf(SingleTarget.Row) ; i < ValueView.Rows.Count - 1 ; i++) {
                if(ValueView.Rows[i].Cells[0].Value.ToString() != SingleTarget.Name ||
                    ValueView.Rows[i].Cells[2].Value.ToString() != SingleTarget.Adduct)
                    break;
                //change cells
                for(int j = Fixed ; j < ValueView.ColumnCount ; j++) {
                    ValueView.InvalidateCell(j, i);
                }
                //change RT
                ValueView.Rows[i].Cells[Fixed - 2].Value = SingleTarget.RTMin;
                ValueView.Rows[i].Cells[Fixed - 1].Value = SingleTarget.RTMax;
            }
            ShowGraphs();
            ShowBars();
        }


        private void UnZoomButton_Click(object sender, EventArgs e) {
            DisplayRTMin = OrigRTMin;
            DisplayRTMax = OrigRTMax;
            ShowGraphs();
        }

        private void CopyBarButton_Click(object sender, EventArgs e) {
            if(ValueView.Rows.Count == 0 || BarView.Series.Count == 0)
                return;
            MemoryStream ms = new MemoryStream();
            BarView.SaveImage(ms, ChartImageFormat.Bmp);
            Bitmap bm = new Bitmap(ms);
            Clipboard.SetImage(bm);
        }

        private void AbsolyteBar_Click(object sender, EventArgs e) {
            if(BarAbsoluteButton.Checked)
                return;
            BarAbsoluteButton.Checked = true;
            BarRelativeButton.Checked = false;
            ShowBars();
        }

        private void RelativeBar_Click(object sender, EventArgs e) {
            if(BarRelativeButton.Checked)
                return;
            BarAbsoluteButton.Checked = false;
            BarRelativeButton.Checked = true;
            ShowBars();
        }

        private void TraceLegendButton_Click(object sender, EventArgs e) {
            TraceLegendButton.Checked = !TraceLegendButton.Checked;
            foreach(Legend L in TraceView.Legends) {
                L.Enabled = TraceLegendButton.Checked;
            }
        }

        private void BarLegendButton_Click(object sender, EventArgs e) {
            BarLegendButton.Checked = !BarLegendButton.Checked;
            foreach(Legend L in BarView.Legends) {
                L.Enabled = BarLegendButton.Checked;
            }
        }

        private void SettingsButton_Click(object sender, EventArgs e) {
            SettingsForm SForm = new SettingsForm();
            SQLiteCommand com = new SQLiteCommand("Select name, value from Settings", con);
            SQLiteDataReader dr = com.ExecuteReader();
            SForm.SettingsView.Rows.Clear();
            while(dr.Read()) {
                SForm.SettingsView.Rows.Add(dr.GetString(0), dr.GetString(1));
            }
            SForm.ShowDialog();
        }

        private void FilesButton_Click(object sender, EventArgs e) {
            FilesForm FForm = new FilesForm();
            FForm.con = con;
            DialogResult DRes = FForm.ShowDialog();
            if (DRes == DialogResult.OK) {
                ContentChanged = true;
            }
            LFRep(null, null);
        }

        //for db3 only
        private void LoadPairs() {
            FilesForm.Pairs.Clear();
            SQLiteCommand com = new SQLiteCommand(
                "Select OrderID, FPos.FileName , FNeg.FileName, Reported, ShortName, Color "+
                "from (Report Left outer join Files as FNeg on Report.NegFile = FNeg.FileIndex)"+
                "Left outer join Files as FPos on Report.PosFile = FPos.FileIndex  "+
                "order by OrderID", con);
            SQLiteDataReader Reader = com.ExecuteReader();
            while(Reader.Read()) {
                FilesForm.FileRep FR = new FilesForm.FileRep();
                FR.Order = Reader.GetInt32(0);
                FR.PosFile = Reader.IsDBNull(1)?null:Path.GetFileNameWithoutExtension(Reader.GetString(1));
                FR.NegFile = Reader.IsDBNull(2)?null:Path.GetFileNameWithoutExtension(Reader.GetString(2));
                FR.Reported = Reader.GetInt32(3) > 0;
                FR.ShortName = Reader.GetString(4);
                FR.Color = Color.FromArgb(Reader.GetInt32(5));
                FilesForm.Pairs.Add(FR);
                if (!(FR.PosFile == null)) {
                    CheckmzAccessFile(FR.PosFile);
                }
                if (!(FR.NegFile == null)) {
                    CheckmzAccessFile(FR.NegFile);
                }
            }
        }

        public static Color[] ColorsDefault =
        {
            Color.FromArgb(65,140,240),
            Color.FromArgb(252,180,65),
            Color.FromArgb(224,64,10),
            Color.FromArgb(5,100,146),
            Color.FromArgb(191,191,191),
            Color.FromArgb(26,59,105),
            Color.FromArgb(255,227,130),
            Color.FromArgb(18,156,221),
            Color.FromArgb(202,107,75),
            Color.FromArgb(0,92,219),
            Color.FromArgb(243,210,136),
            Color.FromArgb(80,99,129),
            Color.FromArgb(241,185,168),
            Color.FromArgb(224,131,10),
            Color.FromArgb(120,147,190),
        };

        //Search
        private void SearchTextBox_TextChanged(object sender, EventArgs e) {
            if(ValueView.RowCount <= 1)
                return;
            for(int i = 0 ; i < ValueView.RowCount - 1 ; i++) {
                if(ValueView.Rows[i].Cells[0].Value.ToString().ToUpper().Contains(SearchTextBox.Text.ToUpper())) {
                    ValueView.CurrentCell = ValueView.Rows[i].Cells[0];
                    break;
                }
                if(ValueView.Rows[i].Cells[1].Value.ToString().ToUpper().Contains(SearchTextBox.Text.ToUpper())) {
                    ValueView.CurrentCell = ValueView.Rows[i].Cells[1];
                    break;
                }

            }
        }

        bool SearchEnabled = true;
        private void MetaRepForm_KeyPress(object sender, KeyPressEventArgs e) {
            if(SearchEnabled) {
                if(SearchTextBox.Focused)
                    return;
                if(e.KeyChar == 9 || e.KeyChar == 13 || e.KeyChar == 8 || e.KeyChar == 27) { e.Handled = false; return; }
                SearchTextBox.Text = "";
                SearchTextBox.Focus();
                SendKeys.Flush();
                SendKeys.Send(e.KeyChar.ToString());
            }
        }

        private void NextButton_Click(object sender, EventArgs e) {
            if(ValueView.RowCount <= 1)
                return;
            bool SecondCol = ValueView.CurrentCell.OwningColumn.Index == 0;
            if(!SecondCol && ValueView.CurrentRow.Index < ValueView.RowCount - 1)
                ValueView.CurrentCell = ValueView.Rows[ValueView.CurrentCell.OwningRow.Index + 1].Cells[0];
            for(int i = ValueView.CurrentCell.OwningRow.Index ; i < ValueView.RowCount - 1 ; i++) {
                if(SecondCol) {
                    SecondCol = false;
                } else {
                    if(ValueView.Rows[i].Cells[0].Value.ToString().ToUpper().Contains(SearchTextBox.Text.ToUpper())) {
                        ValueView.CurrentCell = ValueView.Rows[i].Cells[0];
                        break;
                    }
                }
                if(ValueView.Rows[i].Cells[1].Value.ToString().ToUpper().Contains(SearchTextBox.Text.ToUpper())) {
                    ValueView.CurrentCell = ValueView.Rows[i].Cells[1];
                    break;
                }
            }
        }

        private void BarCopyDataButton_Click(object sender, EventArgs e) {
            string ToCopy = "";
            if(BarView.Series.Count == 0)
                return;
            //Caption
            for(int i = 0 ; i < Fixed ; i++) {
                ToCopy += ValueView.Columns[i].HeaderText + "\t";
            }
            for(int i = 0 ; i < UniqCols.Count ; i++) {
                ToCopy += ValueView.Columns[UniqCols[i]].HeaderText + "\t";
            }
            ToCopy = ToCopy.Substring(0, ToCopy.Length - 1);
            ToCopy += "\n";
            for(int i = 0 ; i < UniqRows.Count ; i++) {
                for(int j = 0 ; j < Fixed ; j++) {
                    if(ValueView.Rows[UniqRows[i]].Cells[j].ValueType == typeof(string)) {
                        ToCopy += (ValueView.Rows[UniqRows[i]].Cells[j].Value as string) + "\t";
                    } else {
                        //if(ValueView.Rows[UniqRows[i]].Cells[j].Value is TargetNum) {
                            ToCopy += ValueView.Rows[UniqRows[i]].Cells[j].Value.ToString() + "\t";
                        //} else {
                        //    ToCopy += ((double)(ValueView.Rows[UniqRows[i]].Cells[j].Value)).ToString() + "\t";
                        //}
                    }
                }
                for(int j = 0 ; j < UniqCols.Count ; j++) {
                    if(ValueView.Rows[UniqRows[i]].Cells[UniqCols[j]].Selected && ValueView.Rows[UniqRows[i]].Cells[UniqCols[j]].Value != null) {
                        ToCopy += BarView.Series[j].Points[i].YValues[0].ToString() + "\t";
                    } else {
                        ToCopy += "\t";
                    }
                }
                ToCopy = ToCopy.Substring(0, ToCopy.Length - 1);
                ToCopy += "\n";
            }
            ToCopy = ToCopy.Substring(0, ToCopy.Length - 1);
            Clipboard.SetText(ToCopy);
        }


        bool InTraceView = false;
        bool InZoomMode = true;
        private void TraceView_MouseEnter(object sender, EventArgs e) {
            InTraceView = true;
            if(ModifierKeys.HasFlag(Keys.Control)) {
                TraceView.Cursor = Cursors.VSplit;
                InZoomMode = false;
            } else {
                TraceView.Cursor = Cursors.Default;
                InZoomMode = true;
            }
        }

        protected override bool ProcessDialogKey(Keys keyData) {
            if(InTraceView) {
                if(keyData.HasFlag(Keys.Control) && SingleTarget != null) {
                    TraceView.Cursor = Cursors.VSplit;
                    InZoomMode = false;
                } else {
                    InZoomMode = true;
                    TraceView.Cursor = Cursors.Default;
                }
            }
            return base.ProcessDialogKey(keyData);
        }

        private void TraceView_MouseLeave(object sender, EventArgs e) {
            InTraceView = false;
            InZoomMode = true;
        }

        private void MetaRepForm_KeyUp(object sender, KeyEventArgs e) {
            if(e.KeyCode == Keys.ControlKey) {
                if(TraceView.Cursor != Cursors.Default) {
                    TraceView.Cursor = Cursors.Default;
                    InZoomMode = true;
                }
            }
        }


        double StartSelectedRT = 0.0;
        double EndSelectedRT = 0.0;
        private void TraceView_SelectionRangeChanged(object sender, CursorEventArgs e) {
            if(!InZoomMode && SingleTarget != null) {
                //Set Custom Range
                SingleTarget.RTMin = Math.Min(e.NewSelectionStart, e.NewSelectionEnd);
                SingleTarget.RTMax = Math.Max(e.NewSelectionStart, e.NewSelectionEnd);
                SingleTarget.FullRTMin = Math.Min(SingleTarget.FullRTMin, SingleTarget.RTMin - Convert.ToDouble(Properties.Settings.Default.RTMargins));
                SingleTarget.FullRTMax = Math.Max(SingleTarget.FullRTMax, SingleTarget.RTMax + Convert.ToDouble(Properties.Settings.Default.RTMargins));
                TraceView.ChartAreas[0].CursorX.SetSelectionPosition(0.0, 0.0);
                SetSelectedData();
                ContentChanged = true;
            } else {
                //Set Zoom
                if(Math.Abs(e.NewSelectionStart - e.NewSelectionEnd) < 0.01)
                    return;
                DisplayRTMax = Math.Max(e.NewSelectionStart, e.NewSelectionEnd);
                DisplayRTMin = Math.Min(e.NewSelectionStart, e.NewSelectionEnd);
                TraceView.ChartAreas[0].AxisX.Maximum = DisplayRTMax;
                TraceView.ChartAreas[0].AxisX.Minimum = DisplayRTMin;
                CustomAxisXScale();
                // Set selection color back
                //TraceView.ChartAreas[0].CursorX.SelectionColor = Color.Gray;
                TraceView.ChartAreas[0].CursorX.SetSelectionPosition(0.0, 0.0);
                ShowGraphs();
            }
        }

        private void SaveCustomButton_Click(object sender, EventArgs e) {
            SaveTextDialog.Title = "Save Custom Target File";
            if(SaveTextDialog.ShowDialog() != DialogResult.OK)
                return;
            StreamWriter sw = new StreamWriter(SaveTextDialog.FileName);
            sw.WriteLine("Name\tAdduct\tMode\tDesc\tMZ\tRTMin\tRTMax\tC13TOCHECK\tN15TOCHECK");
            for(int i = 0 ; i < TargetIDs.Count ; i++) {
                sw.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{6:f5}\t{7:f5}\t{8}\t{9}",
                    TargetIDs[i].Name, TargetIDs[i].Adduct, TargetIDs[i].Mode, TargetIDs[i].Desc, TargetIDs[i].MZ,
                    TargetIDs[i].RTMin, TargetIDs[i].RTMax,
                    TargetIDs[i].C13, TargetIDs[i].N15);
            }
            sw.Close();
        }

        private void onRawData(object sender, EventArgs e) {
            int RowIndex = ValueView.SelectedCells[0].RowIndex;
            int ColumnIndex = ValueView.SelectedCells[0].ColumnIndex;
            if(ColumnIndex < Fixed)
                return;
            string Target = ValueView.Rows[RowIndex].Cells[0].Value.ToString();
            string Adduct = ValueView.Rows[RowIndex].Cells[2].Value.ToString();
            string ColHeader = ValueView.Columns[ColumnIndex].HeaderText;
            int Count = -1;
            FilesForm.FileRep Pair = null;
            for(int j = 0 ; j < FilesForm.Pairs.Count ; j++) {
                if(FilesForm.Pairs[j].Reported)
                    Count++;
                if(Count == ColumnIndex - Fixed) {
                    Pair = FilesForm.Pairs[j];
                    break;
                }
            }
            string Isotope = ValueView.Rows[RowIndex].Cells[3].Value.ToString();
            int IonID = 0;
            //int FileID = -1;
            int TI = 0;
            string FileName = "";
            for(int j = 0 ; j < TargetIDs.Count ; j++) {
                if(TargetIDs[j].Name == Target && TargetIDs[j].Adduct == Adduct) {
                    IonID = TargetIDs[j].IonID;
                    switch(TargetIDs[j].Mode) {
                        case "+": { FileName = Pair.PosFile; break; }
                        case "-": { FileName = Pair.NegFile; break; }
                        default: { FileName = Pair.PosFile ?? Pair.NegFile; break; }
                    }
                    //FileID = Files.Single(SF => SF.Name == FileName).ID;
                    TI = j;
                    break;
                }
            }

            //TraceNumbers TN = null;
            //TraceNumbers TN = Traces.SingleOrDefault(T => 
            //    T.Target.Name == Target && 
            //    T.Target.Adduct == Adduct && 
            //    T.Isotope == Isotope &&
            //    Path.GetFileName(T.FileName) == FileName);

            double MinRT = TargetIDs[TI].FullRTMin;
            double MaxRT = TargetIDs[TI].FullRTMax;
            if(MinRT == 0.0) {
                MinRT = TargetIDs[TI].RTMin;
                MaxRT = TargetIDs[TI].RTMax;
            }
            double MZ = TargetIDs[TI].MZ + IsotopesMassShift(Isotope);
            //+/- 20 ppm window by default
            double MinMZ = MZ - (MZ / 50000.0);
            double MaxMZ = MZ + (MZ / 50000.0);
            RawDataForm RawForm = new RawDataForm();
            RawForm.FileName = Path.GetFileName(FileName);
            RawForm.TrueMZ = MZ;
            RawForm.Desc = String.Format("Compound: {0}; Adduct:{1}; Isotope: {2}; M/Z: {3:f4};", Target, Adduct, Isotope, MZ);
            RawForm.MinMZ = MinMZ;
            RawForm.MaxMZ = MaxMZ;
            RawForm.MinRT = MinRT;
            RawForm.MaxRT = MaxRT;
            RawForm.ShowDialog();
            return;
        }

        ContextMenuStrip strip;
        public event EventHandler forOnRowData;

        private void MetaRepForm_Load(object sender, EventArgs e) {
            string ErrorMessage;
            bool ServiceFlag = true;
            Service.Timeout = 5000;
            Application.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            try {
                TotalFiles = Service.FileList("*", out ErrorMessage).ToList();
                ServiceFlag = ErrorMessage == null;
            }
            catch(Exception) {
                ServiceFlag = false;
            }
            if(!ServiceFlag) {
                MessageBox.Show("mzAccess service is not available. Please, set up mzAccess URL");
                mzAccess_Button_Click(null, null);
                if(TotalFiles == null || TotalFiles.Count == 0) {
                    Application.Exit();
                }
            }
            strip = new ContextMenuStrip();
            forOnRowData = new EventHandler(onRawData);
            strip.Items.Add("Raw data", null, forOnRowData);
            Text = ProgramCaption;
            MRUList.RecentFileList = Properties.Settings.Default.MRUList.Split(';');

            string OldFile = RestoreForm.ChooseSession();
            if(OldFile != null)
                TargetFileName = OldFile;
            if (TargetFileName != "") {
                OpenText(TargetFileName);
                Text = ProgramCaption + "(Restore Mode) ";
                StreamReader sr = new StreamReader(TargetFileName);
                string Str = ""; 
                while(!sr.EndOfStream) {
                    Str = sr.ReadLine();
                }
                Text += Str;
                LFRep(null, null);
                foreach(TargetNum T in TargetIDs) {
                    SameMZ(T);
                }
                ContentChanged = false;
            }
        }

        private void ValueView_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e) {
            //Context menu
            Console.WriteLine("MouseClick");
            if(e.Button == MouseButtons.Right) {
                ValueView.ClearSelection();
                ValueView.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected = true;
                return;
            }
            if(e.Button == MouseButtons.Left) {
                if(e.ColumnIndex != 0 || e.RowIndex == -1)
                    return;
                if(!(ValueView.Rows[e.RowIndex].Cells[0].Value is TargetNum)) {
                    return;
                }
                ValueView.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected = true;
                TargetNum T = ValueView.Rows[e.RowIndex].Cells[0].Value as TargetNum;
                DataObject DO = new DataObject(DataFormats.Text, String.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}",
                    T.Name, T.Desc, T.Adduct, T.C13, T.Mode, T.MZ, T.RTMin, T.RTMax));
                ValueView.DoDragDrop(DO, DragDropEffects.Copy);
            }
        }

        DataGridViewRow DraggedRow = null;
        private void ValueView_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e) {
            //Context menu
            if(e.Button == MouseButtons.Right) {
                ValueView.ClearSelection();
                ValueView.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected = true;
                return;
            }
            if(e.Button == MouseButtons.Left) {
                if(e.ColumnIndex != 0 || e.RowIndex == -1)
                    return;
                if(!(ValueView.Rows[e.RowIndex].Cells[0].Value is TargetNum)) {
                    return;
                }
                ValueView.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected = true;
                TargetNum T = ValueView.Rows[e.RowIndex].Cells[0].Value as TargetNum;
                DataObject DO = new DataObject(DataFormats.Text, String.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}",
                    T.Name, T.Desc, T.Adduct, T.C13, T.Mode, T.MZ, T.RTMin, T.RTMax,Text));
                string[] s = DO.GetFormats();
                ValueView.DoDragDrop(DO, DragDropEffects.Copy);
            }
        }

        private void ValueView_DragEnter(object sender, DragEventArgs e) {
            if(e.Data.GetDataPresent(DataFormats.Text)) {
                string Data = e.Data.GetData("Text") as string;
                string[] Tokens = Data.Split(new char[] { '\t' });
                if (Tokens.Length == Fixed+1 && Tokens[8]!=Text) {
                    int C13;
                    double MZ, RTMin, RTMax;
                    if(int.TryParse(Tokens[3], out C13) &&
                         double.TryParse(Tokens[5], out MZ) &&
                         double.TryParse(Tokens[6], out RTMin) &&
                         double.TryParse(Tokens[7], out RTMax) &&
                         (Tokens[4] == "+" || Tokens[4] == "-")) {
                        e.Effect = DragDropEffects.Copy;
                        return;
                    }
                }
            } 
            e.Effect = DragDropEffects.None;
        }

        private void ValueView_DragDrop(object sender, DragEventArgs e) {
            string Data = e.Data.GetData("Text") as string;
            string[] Tokens = Data.Split(new char[] { '\t' });
            if(Tokens.Length != Fixed+1)
                return;
            if (!CheckUnique(Tokens[0],Tokens[2])) {
                string NewID;
                int Counter = 0;
                do {
                    Counter++;
                    NewID = String.Format("{0}-{1}",Tokens[0],Counter);
                } while(!CheckUnique(NewID, Tokens[2]));
                if(MessageBox.Show(
                    String.Format("Metabolite \"{0}\" {1} is already there. New metabolite can be added with Metabolite ID \"{2}\" \n\n Would you like to continue?",
                        Tokens[0],String.IsNullOrWhiteSpace(Tokens[2])?"with no specifiied adduct":"with "+Tokens[2]+" adduct",NewID), "Drop new compound", 
                    MessageBoxButtons.YesNoCancel) == DialogResult.Yes) {
                    Tokens[0] = NewID;
                } else {
                    return;
                }
            }

            TargetNum T = new TargetNum();
            T.Name = Tokens[0];
            T.Desc = Tokens[1];
            T.Adduct = Tokens[2];
            T.C13 = int.Parse(Tokens[3]);
            T.Mode = Tokens[4];
            T.MZ = double.Parse(Tokens[5]);
            T.RTMin = double.Parse(Tokens[6]);
            T.RTMax = double.Parse(Tokens[7]);
            T.FullRTMax = Math.Max(T.RTMin - Convert.ToDouble(Properties.Settings.Default.RTMargins), 0.0);
            T.FullRTMax = Math.Min(T.RTMax + Convert.ToDouble(Properties.Settings.Default.RTMargins), MaxDisplay);
            int RIndex = ValueView.Rows.Add(T, T.Desc, T.Adduct, T.C13, T.Mode, T.MZ, T.RTMin, T.RTMax);
            T.Row = ValueView.Rows[RIndex];
            if(T.Row.Index % 2 == 0) {
                T.Row.DefaultCellStyle.BackColor = Color.FromArgb(255, 222, 230, 244);
            } else {
                T.Row.DefaultCellStyle.BackColor = Color.FromArgb(255, 242, 247, 255);
            }

            int ColNumber = Fixed-1;
            for(int j = 0 ; j < FilesForm.Pairs.Count ; j++) {
                if(FilesForm.Pairs[j].Reported) {
                    ColNumber++;
                } else {
                    continue;
                }
                TraceNumbers TN = new TraceNumbers();
                TN.Target = T;
                TN.Pair = FilesForm.Pairs[j];
                TN.FileName = T.Mode == "+" ? TN.Pair.PosFile : TN.Pair.NegFile;
                TN.Isotope = "C0N0";
                ValueView.Rows[RIndex].Cells[ColNumber].Value = TN;
                TN.Cell = ValueView.Rows[RIndex].Cells[ColNumber];
            }

            SameMZ(T);
            ContentChanged = true;
            for(int i = 0 ; i < ValueView.Columns.Count ; i++) {
                ValueView.InvalidateCell(i, T.Row.Index);
            }
            ValueView.CurrentCell = ValueView.Rows[RIndex].Cells[0];
        }




        //old code for "x2" button
        double DisplayRTMin;
        double DisplayRTMax;
        double MaxDisplay;
        private void ExtendButton_Click(object sender, EventArgs e) {
            double AveRT = (DisplayRTMax + DisplayRTMin) / 2.0;
            DisplayRTMin -= AveRT - DisplayRTMin;
            if(DisplayRTMin < 0.0)
                DisplayRTMin = 0.0;
            DisplayRTMax += DisplayRTMax - AveRT;
            if(DisplayRTMax > MaxDisplay)
                DisplayRTMax = MaxDisplay;
            ShowGraphs();
        }

        private void TraceView_MouseMove(object sender, MouseEventArgs e) {
            if(HDragMode && e.Button.HasFlag(MouseButtons.Left)) {
                Axis A = TraceView.ChartAreas[0].AxisX;
                double range = A.Maximum - A.Minimum;
                if(e.Location.X < 0)
                    return;
                double xv = A.PixelPositionToValue(e.Location.X);
                A.Minimum = Math.Max(A.Minimum - xv + MouseDown, 0);
                A.Maximum = Math.Min(A.Minimum + range, MaxDisplay);
                CustomAxisXScale();
                return;
            }
            if(VDragMode && e.Button.HasFlag(MouseButtons.Left)) {
                Axis A = TraceView.ChartAreas[0].AxisY;
                if(e.Location.Y < 0 )
                    return;
                double yv;
                try {
                    yv = A.PixelPositionToValue(e.Location.Y);
                }
                catch(Exception) {
                    return;
                }
                double ScaleFactor = Math.Max(1.0, (yv / MouseDown)*(VDragMax/A.Maximum));
                A.Maximum = VDragMax/ScaleFactor;
                //Console.WriteLine("{0:f3}  {1}   {2:e3}  {3:e3}", ScaleFactor,e.Location.Y,yv,A.Maximum); 
                return;
            }
            HitTestResult HTR = TraceView.HitTest(e.X, e.Y);
            if(HTR.Axis != null &&
                HTR.Axis == TraceView.ChartAreas[0].AxisX &&
                HTR.ChartElementType != ChartElementType.Gridlines &&
                HTR.ChartElementType != ChartElementType.StripLines &&
                InZoomMode) {
                TraceView.Cursor = Cursors.SizeWE;
                return;
            } 
            if(HTR.Axis != null &&
                HTR.Axis == TraceView.ChartAreas[0].AxisY &&
                HTR.ChartElementType != ChartElementType.Gridlines &&
                HTR.ChartElementType != ChartElementType.StripLines &&
                InZoomMode) {
                TraceView.Cursor = Cursors.SizeNS;
                return;
            } 
            if(InZoomMode)
                TraceView.Cursor = Cursors.Default;
        }


        List<string> TotalFiles = null;
        private void mzAccess_Button_Click(object sender, EventArgs e) {
            mzAccessForm AF = new mzAccessForm();
            if(AF.ShowDialog() == DialogResult.OK) {
                TotalFiles = AF.FileList;
                Properties.Settings.Default.Save();
                Service.Url = AF.textBox1.Text;
            }
        }

        private void FullZoomButton_Click(object sender, EventArgs e) {
            DisplayRTMin = 0.0;
            DisplayRTMax = MaxDisplay;
            ShowGraphs();
        }

        double MouseDown = 0.0;
        bool HDragMode = false;
        bool VDragMode = false;
        double VDragMax = 0.0;
        private void TraceView_MouseDown(object sender, MouseEventArgs e) {
            if(TraceView.Cursor == Cursors.SizeWE) {
                Axis A = TraceView.ChartAreas[0].AxisX;
                MouseDown = A.PixelPositionToValue(e.Location.X);
                HDragMode = true;
            }
            if(TraceView.Cursor == Cursors.SizeNS) {
                Axis A = TraceView.ChartAreas[0].AxisY;
                MouseDown = A.PixelPositionToValue(e.Location.Y);
                if (VDragMax == 0.0)
                    VDragMax = A.Maximum;
                VDragMode = true;
            }
        }

        private void TraceView_MouseUp(object sender, MouseEventArgs e) {
            if(HDragMode) {
                Axis A = TraceView.ChartAreas[0].AxisX;
                DisplayRTMax = A.Maximum;
                DisplayRTMin = A.Minimum;
                ShowGraphs();
                TraceView.Cursor = Cursors.Default;
                HDragMode = false;
            }
            if(VDragMode) {
                TraceView.Cursor = Cursors.Default;
                VDragMode = false;
            }
        }

        private void ValueView_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e) {
            if(e.ColumnIndex < Fixed) {
                if(e.RowIndex >= 0 && ValueView.Rows[e.RowIndex].Cells[0].Value is TargetNum) {
                    TargetNum T = ValueView.Rows[e.RowIndex].Cells[0].Value as TargetNum;
                    if(e.RowIndex == ValueView.RowCount - 2 ||
                        ValueView.Rows[e.RowIndex + 1].Cells[0].Value is TargetNum) {
                        AddIsotopes(T);
                    } else {
                        HideIsotopes(T);
                    }
                }
            }
        }

        private void SaveText(string FileName, bool Temp = false) {
            //File pairs
            StreamWriter sw = new StreamWriter(FileName);
            foreach(FilesForm.FileRep F in FilesForm.Pairs) {
                sw.WriteLine("<FilePair Name=\"{0}\" FileNeg=\"{1}\" FilePos=\"{2}\" Order=\"{3}\" Reported=\"{4}\" Color=\"{5:X}\"/>",
                    F.ShortName, F.NegFile, F.PosFile, F.Order, F.Reported, F.Color.ToArgb());
            }
            sw.WriteLine();
            sw.WriteLine("NAME\tADDUCT\tMODE\tDESC\tMZ\tRTMIN\tRTMAX\tC13TOCHECK");
            foreach(TargetNum T in TargetIDs) {
                sw.WriteLine("{0}\t{1}\t{2}\t\"{3}\"\t{4:f5}\t{5:f2}\t{6:f2}\t{7}",
                    T.Name, T.Adduct, T.Mode, T.Desc, T.MZ, T.RTMin, T.RTMax, T.C13);
            }
            if(Temp) {
                sw.WriteLine(TargetFileName+" - "+DateTime.Now.ToString());
            } else {
                MRUList.AddRecentFileToMruList(FileName);
            }
            sw.Close();
        }

        private bool OpenText(string FileName) {
            try {
                FilesForm.Pairs = new List<FilesForm.FileRep>();
                TargetIDs = new List<TargetNum>();
                StreamReader sr = new StreamReader(FileName);
                MaxDisplay = 0.0;
                FilesButton.Enabled = true;
                //Parse files
                //single filename 
                int Count = 0;
                int LineCount = 0;
                string str = "";
                while(!sr.EndOfStream) {
                    str = sr.ReadLine();
                    LineCount++;
                    if(str.ToUpper().Contains("NAME") && str.ToUpper().Contains("MZ"))
                        break;
                    if(str.Trim() == "")
                        continue;
                    FilesForm.FileRep P = new FilesForm.FileRep();
                    //single filename 
                    if(!str.Contains("<FilePair")) {
                        P.PosFile = Path.GetFileNameWithoutExtension(str.Trim());
                        P.NegFile = Path.GetFileNameWithoutExtension(str.Trim());
                        P.Reported = true;
                        if(!CheckmzAccessFile(P.PosFile)) {
                            sr.Close();
                            return false;
                        }
                        P.ShortName = Path.GetFileNameWithoutExtension(str.Trim());
                        P.Color = ColorsDefault[Count % ColorsDefault.Length];
                        Count++;
                    } else {
                        //XML-style
                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(str);
                        P.PosFile = xmlDoc.DocumentElement.Attributes["FilePos"].Value;
                        P.NegFile = xmlDoc.DocumentElement.Attributes["FileNeg"].Value;
                        P.ShortName = xmlDoc.DocumentElement.Attributes["Name"].Value;
                        P.Reported = bool.Parse(xmlDoc.DocumentElement.Attributes["Reported"].Value);
                        P.Color = Color.FromArgb(int.Parse(xmlDoc.DocumentElement.Attributes["Color"].Value, System.Globalization.NumberStyles.AllowHexSpecifier));
                        P.Order = int.Parse(xmlDoc.DocumentElement.Attributes["Order"].Value);
                        if ((!String.IsNullOrWhiteSpace(P.NegFile) && !CheckmzAccessFile(P.NegFile)) || 
                           (!String.IsNullOrWhiteSpace(P.PosFile) && !CheckmzAccessFile(P.PosFile))) {
                            sr.Close();
                            return false;
                        }
                    }
                    FilesForm.Pairs.Add(P);
                }
                //Target List - caption string is already loaded 
                List<string> Tokens = new List<string>(str.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries));
                for(int i = 0 ; i < Tokens.Count ; i++) {
                    Tokens[i] = Tokens[i].ToUpper().Trim();
                }
                int[] Indexes = new int[8];
                int ColumnNumber = Tokens.Count;
                //expected columns
                string[] cNames = { "NAME", "MZ", "RTMIN", "RTMAX", "C13TOCHECK", "DESC", "ADDUCT", "MODE" };
                for(int i = 0 ; i < Indexes.Length ; i++) {
                    Indexes[i] = Tokens.IndexOf(cNames[i]);
                }
                if(Indexes[1] == -1) {
                    Exception e = new Exception("Target text file insufficient data. No \"MZ\" column");
                    return false;
                }
                string Duplicates = "";
                while(!sr.EndOfStream) {
                    str = sr.ReadLine();
                    LineCount++;
                    Tokens = new List<string>(str.Split(new char[] { '\t' }));
                    if(Tokens.Count < ColumnNumber)
                        continue;
                    for(int i = 0 ; i < Tokens.Count ; i++)
                        Tokens[i] = Tokens[i].Trim();
                    try {
                        TargetNum T = new TargetNum();
                        T.MZ = Convert.ToDouble(Tokens[Indexes[1]]);
                        T.Name = (Indexes[0] != -1) ? Tokens[Indexes[0]].Trim() : "MZ - " + Tokens[Indexes[1]] + ";RT - " + Tokens[Indexes[2]];
                        T.RTMin = (Indexes[2] != -1 && Tokens[Indexes[2]].Trim() != "") ? Convert.ToDouble(Tokens[Indexes[2]]) : 0.0;
                        T.RTMax = (Indexes[3] != -1 && Tokens[Indexes[3]].Trim() != "") ? Convert.ToDouble(Tokens[Indexes[3]]) : 0.0;
                        T.C13 = (Indexes[4] != -1 && Tokens[Indexes[4]].Trim() != "") ? Convert.ToInt32(Tokens[Indexes[4]]) : 0;
                        T.Desc = (Indexes[5] != -1) ? Tokens[Indexes[5]] : "";
                        T.Adduct = (Indexes[6] != -1) ? Tokens[Indexes[6]] : "";
                        T.Mode = (Indexes[7] != -1) ? Tokens[Indexes[7]] : "?";
                        while(T.Name.IndexOf("\"") != -1)
                            T.Name = T.Name.Remove(T.Name.IndexOf("\""), 1);
                        while(T.Desc.IndexOf("\"") != -1)
                            T.Desc = T.Desc.Remove(T.Desc.IndexOf("\""), 1);
                        //check for uniqueness of new target
                        if(TargetIDs.SingleOrDefault(TD => TD.Name == T.Name && TD.Adduct == T.Adduct) != null) {
                            Duplicates += T.Name + " (" + T.Adduct + ")\n";
                        }
                        T.FullRTMax = T.RTMax + Convert.ToDouble(Properties.Settings.Default.RTMargins);
                        T.FullRTMin = Math.Max(0.0, T.RTMin - Convert.ToDouble(Properties.Settings.Default.RTMargins));
                        TargetIDs.Add(T);
                    }
                    catch(IndexOutOfRangeException) {
                        MessageBox.Show("Target text file parsing error - Check column consistency.");
                        return false;
                    }
                    catch {
                        MessageBox.Show("Target text file parsing error - Check data format for string " + LineCount.ToString() + ".");
                        return false;
                    }
                }
                if(Duplicates != "") {
                    MessageBox.Show("Some targets are duplicated in a list:\n" + Duplicates + "Please, remove duplicates.");
                    return false;
                }
                return true;
            }
            catch(Exception e) {
                MessageBox.Show(e.Message, "Target's text file reading");
                return false;
            }
        }

        private bool CheckmzAccessFile(string FileName) {
            string Message;
            double[] RTs = Service.GetRTRange(FileName, false, out Message);
            if(Message != null && Message != "") {
                MessageBox.Show(Message, "mzAccess", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            } else {
                if(RTs[1] > MaxDisplay)
                    MaxDisplay = RTs[1];
                return true;
            }
        }

        private void openText_Click(object sender, EventArgs e) {
            if(OpenTextDialog.ShowDialog() != DialogResult.OK)
                return;
            if(!OpenText(OpenTextDialog.FileName))
                return;
            Text = ProgramCaption + Path.GetFileName(OpenTextDialog.FileName);
            TargetFileName = Path.GetFileName(OpenTextDialog.FileName);
            LFRep(null, null);
            foreach(TargetNum T in TargetIDs) {
                SameMZ(T);
            }
            MRUList.AddRecentFileToMruList(OpenTextDialog.FileName);
            ContentChanged = false;
        }

        private void saveText_Click(object sender, EventArgs e) {
            if(SaveTextDialog.ShowDialog() != DialogResult.OK)
                return;
            SaveText(SaveTextDialog.FileName);
            if (Text.Contains("(Restore Mode)")) {
                File.Delete(TargetFileName);
            }
            Text = ProgramCaption + Path.GetFileName(SaveTextDialog.FileName);
            TargetFileName = SaveTextDialog.FileName;
            ContentChanged = false;
        }


        //Apply for new strings
        private void ValueView_RowValidating(object sender, DataGridViewCellCancelEventArgs e) {
            if(!ValueView.IsCurrentRowDirty)
                return;
            DataGridViewRow Row = ValueView.Rows[e.RowIndex]; 
            //ID have to be unique
            //Isotope - int 
            //MZ, RTMin,RTMax - double
            int Iso;
            double F;
            string ID = (Row.Cells[0].Value ?? "").ToString();
            string Adduct = (Row.Cells[2].Value ?? "").ToString();
            if (String.IsNullOrWhiteSpace(ID)) {
                if(MessageBox.Show("Set Metabolite ID. It can't be empty. \n\n Would you like to continue?", 
                    e.RowIndex == ValueView.RowCount-2?"New compound":"Compound update", 
                    MessageBoxButtons.YesNoCancel) == DialogResult.Yes) {
                    e.Cancel = true;
                    ValueView.CurrentCell = ValueView.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    ValueView.BeginEdit(false);
                } else {
                    if(Row.Cells[0].Value is TargetNum) {
                        RowRefill();
                    } else {
                        BeginInvoke(new MethodInvoker(delegate {
                            ValueView.Rows.RemoveAt(e.RowIndex);
                        }));
                    }
                    e.Cancel = true;
                }
                return;
            }
            if(Row.Cells[3].Value==null || !int.TryParse(Row.Cells[3].Value.ToString(), out Iso) ||
                Row.Cells[5].Value==null ||!double.TryParse(Row.Cells[5].Value.ToString(), out F) ||
                Row.Cells[6].Value==null ||!double.TryParse(Row.Cells[6].Value.ToString(), out F) ||
                Row.Cells[7].Value==null ||!double.TryParse(Row.Cells[7].Value.ToString(), out F) 
                ) {
                if(MessageBox.Show("Check numeric values in a changed row. \n\n Would you like to continue?", 
                    e.RowIndex == ValueView.RowCount-2?"New compound":"Compound update", 
                    MessageBoxButtons.YesNoCancel) == DialogResult.Yes) {
                    e.Cancel = true;
                    ValueView.CurrentCell = ValueView.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    ValueView.BeginEdit(false);
                } else {
                    if(Row.Cells[0].Value is TargetNum) {
                        RowRefill();
                    } else {
                        BeginInvoke(new MethodInvoker(delegate {
                            ValueView.Rows.RemoveAt(e.RowIndex);
                        }));
                    }
                    e.Cancel = true;
                }
                return;
            }
            if(Row.Cells[4].Value==null || !(Row.Cells[4].Value.ToString() == "+" || Row.Cells[4].Value.ToString() == "-")) {
                if(MessageBox.Show("Check ion mode to be \"+\" or \"-\" in a changed row. \n\n Would you like to continue?", 
                    e.RowIndex == ValueView.RowCount-2?"New compound":"Compound update", 
                    MessageBoxButtons.YesNoCancel) == DialogResult.Yes) {
                    e.Cancel = true;
                    ValueView.CurrentCell = ValueView.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    ValueView.BeginEdit(false);
                } else {
                    if(Row.Cells[0].Value is TargetNum) {
                        RowRefill();
                    } else {
                        BeginInvoke(new MethodInvoker(delegate {
                            ValueView.Rows.RemoveAt(e.RowIndex);
                        }));
                    }
                    e.Cancel = true;
                }
                //Check for uniqueness of ID+Adduct
            if (!CheckUnique(ID,Adduct)) {
                if(MessageBox.Show(
                    String.Format("Metabolite \"{0}\" {1} is already there. \n\n Would you like to continue?",
                        ID,String.IsNullOrWhiteSpace(Adduct)?"with no specifiied adduct":"with "+Adduct+" adduct"), 
                    e.RowIndex == ValueView.RowCount-2?"New compound":"Compound update", 
                    MessageBoxButtons.YesNoCancel) == DialogResult.Yes) {
                    e.Cancel = true;
                    ValueView.CurrentCell = ValueView.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    ValueView.BeginEdit(false);
                } else {
                    if(Row.Cells[0].Value is TargetNum) {
                        RowRefill();
                    } else {
                        BeginInvoke(new MethodInvoker(delegate {
                            ValueView.Rows.RemoveAt(e.RowIndex);
                        }));
                    }
                    e.Cancel = true;
                }
                return;
            }


                return;
            }
            e.Cancel = false;
        }

        public void RowRefill() {
            TargetNum T = ValueView.CurrentRow.Cells[0].Value as TargetNum;
            ValueView.CurrentRow.Cells[1].Value = T.Desc;
            ValueView.CurrentRow.Cells[2].Value = T.Adduct;
            ValueView.CurrentRow.Cells[3].Value = T.C13;
            ValueView.CurrentRow.Cells[4].Value = T.Mode;
            ValueView.CurrentRow.Cells[5].Value = T.MZ;
            ValueView.CurrentRow.Cells[6].Value = T.RTMin;
            ValueView.CurrentRow.Cells[7].Value = T.RTMax;
        }

        public bool CheckUnique(string ID, string Adduct) {
            return TargetIDs.Count(a => a.Name == ID && a.Adduct == Adduct) == 0;
        }

        private bool _ContentChanged = false;
        public bool ContentChanged {
            get { return _ContentChanged; }
            set {
                if(value) {
                    if(_ContentChanged != value) {
                        _ContentChanged = value;
                        Text += " - *";
                        AvtoSaveTimer.Enabled = true;
                    }
                }else {
                    if(_ContentChanged != value) 
                        _ContentChanged = value;
                    if(Text.Contains("*"))
                        Text = Text.Substring(0, Text.Length - 3);
                    AvtoSaveTimer.Enabled = false;
                    if (!String.IsNullOrEmpty(TempFileName))
                        File.Delete(TempFileName);
                }
            }
        }

        private void ValueView_RowValidated(object sender, DataGridViewCellEventArgs e) {
            if(!ValueView.IsCurrentCellDirty && SearchEnabled)
                return;
            DataGridViewRow Row = ValueView.Rows[e.RowIndex];
            TargetNum T;
            if(Row.Cells[0].Value is TargetNum) {
                T = Row.Cells[0].Value as TargetNum;
            } else {
                T = new TargetNum();
                TargetIDs.Add(T);
            }
            if (Row.Cells[0].Value == null) {
                return;
            }
            T.Name = Row.Cells[0].Value.ToString();
            T.Desc = (Row.Cells[1].Value??"").ToString();
            T.Adduct = (Row.Cells[2].Value??"").ToString();
            if (int.Parse(Row.Cells[3].Value.ToString()) != 0) {
                T.C13 = int.Parse(Row.Cells[3].Value.ToString());
            }else {
                Row.Cells[3].Value = T.C13;
            }
            T.C13 = int.Parse(Row.Cells[3].Value.ToString());
            T.Mode = Row.Cells[4].Value.ToString();
            T.MZ = double.Parse(Row.Cells[5].Value.ToString());
            T.RTMin = double.Parse(Row.Cells[6].Value.ToString());
            T.RTMax = double.Parse(Row.Cells[7].Value.ToString());
            T.FullRTMax = Math.Max(T.RTMin - Convert.ToDouble(Properties.Settings.Default.RTMargins), 0.0);
            T.FullRTMax = Math.Min(T.RTMax + Convert.ToDouble(Properties.Settings.Default.RTMargins), MaxDisplay);
            T.Row = Row;
            Row.Cells[0].Value = T;
            int ColNumber = 0;
            for(int j = 0 ; j < FilesForm.Pairs.Count ; j++) {
                if(FilesForm.Pairs[j].Reported) {
                    ColNumber++;
                } else {
                    continue;
                }
                TraceNumbers Tr = new TraceNumbers();
                Tr.Target = T;
                Tr.Isotope = "C0N0";
                Tr.Pair = FilesForm.Pairs[j];
                if(T.Mode == "+") {
                    Tr.FileName = Path.GetFileName(FilesForm.Pairs[j].PosFile);
                } else {
                    Tr.FileName = Path.GetFileName(FilesForm.Pairs[j].NegFile);
                }
                Row.Cells[ColNumber + Fixed - 1].Value = Tr;
                Tr.Cell = Row.Cells[ColNumber + Fixed - 1];
                Row.Cells[ColNumber + Fixed - 1].ContextMenuStrip = strip;
            }
            if(T.Row.Index % 2 == 0) {
                T.Row.DefaultCellStyle.BackColor = Color.FromArgb(255, 222, 230, 244);
            } else {
                T.Row.DefaultCellStyle.BackColor = Color.FromArgb(255, 242, 247, 255);
            }
            for(int i = 0 ; i < ValueView.Columns.Count ; i++) {
                ValueView.InvalidateCell(i, T.Row.Index);
            }
            if(T.Row.Index+1<ValueView.RowCount-1 && //last string
                ValueView.Rows[T.Row.Index+1].Cells[0].Value.ToString() == T.Name && 
                ValueView.Rows[T.Row.Index+1].Cells[2].Value.ToString() == T.Adduct ) {
                ChangeIsotopes(T);
            }
            SameMZ(T);
            ContentChanged = true;
            SearchEnabled = true;
        }


        int FrozenRowCount = 0;
        private void FixButton_Click(object sender, EventArgs e) {
            if(!(ValueView.CurrentRow.Cells[0].Value is TargetNum))
                return;
            DataGridViewRow R = ValueView.CurrentRow;
            HideIsotopes(R.Cells[0].Value as TargetNum);
            if(!R.Frozen) {
                FixRow(R);
            }else {//а если отсортировано?
                UnFixRow(R);
            }
            RecolorRows();
        }

        private void FixRow(DataGridViewRow R) {
            if(R.Cells[0].Value is TargetNum) {
                for(int i = R.Index - 1 ; i >= 0 ; i--) { //remember place where it stayed
                    if(ValueView.Rows[i].Cells[0].Value is TargetNum) {
                        (R.Cells[0].Value as TargetNum).Prev = ValueView.Rows[i].Cells[0].Value as TargetNum;
                        break;
                    }
                }
                R.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 248, 207);
            }
            ValueView.Rows.Remove(R);
            ValueView.Rows.Insert(FrozenRowCount, R);
            ValueView.Rows[FrozenRowCount].Frozen = true;
            FrozenRowCount++;
        }

        private void UnFixRow(DataGridViewRow R) {
            ValueView.Rows.Remove(R);
            R.Frozen = false;
            FrozenRowCount--;
            int InsertTo = FrozenRowCount;
            if(R.Cells[0].Value is TargetNum) {
                if((R.Cells[0].Value as TargetNum).Prev != null) {
                    for(int i = 0 ; i < ValueView.RowCount - 1 ; i++) {
                        if(ValueView.Rows[i].Cells[0].Value is TargetNum &&
                            ValueView.Rows[i].Cells[0].Value as TargetNum == (R.Cells[0].Value as TargetNum).Prev) {
                            InsertTo = i + 1;
                            break;
                        }
                    }
                    if(ValueView.Rows[InsertTo].Frozen)
                        InsertTo = FrozenRowCount + 1;
                    while(!(ValueView.Rows[InsertTo].Cells[0].Value is TargetNum)) //if isotopes expanded 
                        InsertTo++;
                }
            (R.Cells[0].Value as TargetNum).Prev = null;
            }
            ValueView.Rows.Insert(InsertTo, R);
        }


        private void RecolorRows() {
            int Counter = 0;
            for (int i = 0 ; i < ValueView.RowCount - 1 ; i++) {
                if (!ValueView.Rows[i].Frozen && (ValueView.Rows[i].Cells[0].Value is TargetNum)) {
                    ValueView.Rows[i].DefaultCellStyle.BackColor = Counter % 2 == 0 ?
                        Color.FromArgb(255, 222, 230, 244):
                        Color.FromArgb(255, 242, 247, 255);
                    Counter++; 
                }
            }

        }

        private void ValueView_DataError(object sender, DataGridViewDataErrorEventArgs e) {
            e.Cancel = true;
        }

        private void ValueView_RowEnter(object sender, DataGridViewCellEventArgs e) {
            if(ValueView.NewRowIndex == e.RowIndex && e.RowIndex != 0) {
                //disable search!!
                SearchEnabled = false;

                ValueView.Rows[e.RowIndex].ReadOnly = false;
                DataGridViewComboBoxCell ModeCell = new DataGridViewComboBoxCell();
                ModeCell.Items.AddRange("+", "-");
                ModeCell.Value = "";
                ValueView.Rows[e.RowIndex].Cells[4] = ModeCell;

                for(int i = 0 ; i < Fixed ; i++)
                    ValueView.Rows[e.RowIndex].Cells[i].ReadOnly = false;
                ValueView.Rows[e.RowIndex].Cells[0].ReadOnly = false;
            }
        }


        private void ValueView_CellEndEdit(object sender, DataGridViewCellEventArgs e) {
            if(SearchEnabled)
                return;
        }

        private void ValueView_RowLeave(object sender, DataGridViewCellEventArgs e) {
            if(SearchEnabled) 
                return;
            if(!ValueView.IsCurrentCellDirty ) {
                SearchEnabled = true;
            }
            if (ValueView.Rows[e.RowIndex].Cells[4] is DataGridViewComboBoxCell) {
                DataGridViewTextBoxCell ModeCell = new DataGridViewTextBoxCell();
                if(ValueView.Rows[e.RowIndex].Cells[4].Value.ToString() == "-" ||
                    ValueView.Rows[e.RowIndex].Cells[4].Value.ToString() == "+") {
                    ModeCell.Value = ValueView.Rows[e.RowIndex].Cells[4].Value.ToString();
                }
                ValueView.Rows[e.RowIndex].Cells[4] = ModeCell;
            }
        }


        private void ValueView_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e) {
            if(SearchEnabled) {
                e.Cancel = true;
                return;
            }
            DataGridViewComboBoxCell ModeCell = new DataGridViewComboBoxCell();
            ModeCell.Items.AddRange("+", "-");
            if(ValueView.Rows[e.RowIndex].Cells[4].Value != null) {
                ModeCell.Value = ValueView.Rows[e.RowIndex].Cells[4].Value.ToString();
            }else {
                ModeCell.Value = "+";
            }
            ValueView.Rows[e.RowIndex].Cells[4] = ModeCell;
        }

        private void ValueView_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e) {
            if(e.KeyCode == Keys.F2)
                SearchEnabled = false;
        }

        private void ValueView_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e) {
            if(ValueView.Rows.Count > 0 && e.Row.Cells[0].Value is TargetNum) {
                //remove all the isotopes too
                while(ValueView.Rows[e.Row.Index + 1].Cells[0].Value != null && ValueView.Rows[e.Row.Index].Cells[0].Value.ToString() == ValueView.Rows[e.Row.Index + 1].Cells[0].Value.ToString() &&
                    ValueView.Rows[e.Row.Index].Cells[2].Value.ToString() == ValueView.Rows[e.Row.Index + 1].Cells[2].Value.ToString()) {
                    ValueView.Rows.RemoveAt(e.Row.Index + 1);
                }
                TargetNum T = e.Row.Cells[0].Value as TargetNum;
                e.Row.Cells[0].Value = e.Row.Cells[0].Value.ToString();
                foreach (TargetNum S in T.SameMZs) {
                    SameMZ(S);
                }
                TargetIDs.Remove(T);
                ContentChanged = true;
            }else {
                e.Cancel = true;
            }
        }

        private void CustomAxisXScale() {
            Axis A = TraceView.ChartAreas[0].AxisX;
            double Range = A.Maximum - A.Minimum;
            double Order = Math.Log10(Range);
            double OrderFloor = Math.Pow(10, Math.Floor(Order));
            //4-6 axis marks 1,2,5 multipliers
            double AxisRange = Math.Pow(10.0,Order - Math.Floor(Order));
           //multiplier 1 for range of 4-6 like (2;3;4;5;6 - range 5 => multiplier 1)
            if (AxisRange > 4.0 && AxisRange <= 7.0) {
                A.Interval = 1.0*OrderFloor;
            }
            //multiplier 5 for range of 2-3 like (1.5;2.0;2.5;3.0;3.5;4.0 - range 2.5 multiplier 5 )
            if (AxisRange > 2.0 && AxisRange <= 4.0) {
                A.Interval = 0.5*OrderFloor;
            }
            //multiplier 2 for range 7-10 like (2;4;6;8;10 - range 8 multiplier 2)
            if (AxisRange > 7.0 ) {
                A.Interval = 2.0*OrderFloor;
            }
            if (AxisRange <= 2.0) {
                A.Interval = 0.2*OrderFloor;
            }
            A.IntervalOffset = -(A.Minimum % A.Interval);
            A.LabelStyle.Format = "f2";
            VDragMax = 0.0;
        }

        private void SameMZ(TargetNum T) {
            T.SameMZs.Clear();
            double MZDev = Convert.ToDouble(Properties.Settings.Default.MZDev) / 1000000.0;
            for (int i = 0 ; i < ValueView.RowCount -1 ; i++) {
                if((ValueView.Rows[i].Cells[0].Value is TargetNum)) { 
                    TargetNum TD = (ValueView.Rows[i].Cells[0].Value as TargetNum);
                    if(TD != T && TD.Mode == T.Mode &&    //have same MZ
                        Math.Abs(T.MZ - TD.MZ) < MZDev*((T.MZ+TD.MZ)/2.0)) {
                        if(!T.SameMZs.Contains(TD))
                            T.SameMZs.Add(TD);
                        if(!TD.SameMZs.Contains(T))
                            TD.SameMZs.Add(T);
                    }else {
                        if(TD.SameMZs.Contains(T)) {
                            TD.SameMZs.Remove(T);
                        }
                    }
                }
            }
            T.SameMZs.Sort((T1, T2) => T1.RTMin.CompareTo(T2.RTMin));
        }


        public bool CancelSignal = false;
        private void saveLabelfreeReport_Click(object sender, EventArgs e) {
            SaveTextDialog.Title = "Save Label-Free Text Report as...";
            if (TargetFileName != "" && 
                Path.GetExtension(TargetFileName) == ".txt") {
                SaveTextDialog.FileName = Path.GetFileNameWithoutExtension(TargetFileName + "-LFReport");
            }
            if(SaveTextDialog.ShowDialog() != DialogResult.OK) return;

            ProgressForm PF = new ProgressForm();
            PF.progressBar1.Value = 0;
            PF.Text = "Label-Free Text Report Progress";
            PF.label1.Text = SaveTextDialog.FileName;
            PF.Show(this);

            StreamWriter sw = new StreamWriter(SaveTextDialog.FileName);
            //Caption
            for (int j = 0 ; j < ValueView.ColumnCount ; j++) {
                sw.Write(ValueView.Columns[j].HeaderText);
                sw.Write("\t");
            }
            sw.WriteLine();
            //Body
            for( int i = 0 ; i < ValueView.RowCount ; i++) {
                if (ValueView.Rows[i].Cells[0].Value is TargetNum) {
                    for (int j = 0 ; j < ValueView.ColumnCount ; j++) {
                        string str = ValueView.Rows[i].Cells[j].Value.ToString();
                        if (str == "" && j >= Fixed) {
                            str = "0.0";
                        }
                        sw.Write(str);
                        sw.Write("\t");
                    }
                    sw.WriteLine();
                }
                Application.DoEvents();
                if(CancelSignal) {
                    CancelSignal = false;
                    break;
                }
                if (PF.progressBar1.Value < (i * 100) / ValueView.RowCount){
                    PF.progressBar1.Value = (i * 100) / ValueView.RowCount;
                }
            }
            sw.Close();
            PF.Close();
        }

        private void saveIsotopeReport_Click(object sender, EventArgs e) {
            SaveTextDialog.Title = "Save Isotope Text Report as...";
            if (TargetFileName != "" && 
                Path.GetExtension(TargetFileName) == ".txt") {
                SaveTextDialog.FileName = Path.GetFileNameWithoutExtension(TargetFileName + "-IsotopeReport");
            }
            if(SaveTextDialog.ShowDialog() != DialogResult.OK) return;

            ProgressForm PF = new ProgressForm();
            PF.progressBar1.Value = 0;
            PF.Text = "Isotope Text Report Progress";
            PF.label1.Text = SaveTextDialog.FileName;
            PF.Show(this);

            StreamWriter sw = new StreamWriter(SaveTextDialog.FileName);
            //Caption
            for (int j = 0 ; j < ValueView.ColumnCount ; j++) {
                sw.Write(ValueView.Columns[j].HeaderText);
                sw.Write("\t");
            }
            sw.WriteLine();
            //Body
            for( int i = 0 ; i < ValueView.RowCount-1 ; i++) {
                int ICounter = ValueView.Rows[i].Cells[0].Value is TargetNum ? Convert.ToInt32(ValueView.Rows[i].Cells[3].Value.ToString()) + 1 : 1;
                if(ICounter>1) { 
                    AddIsotopes(ValueView.Rows[i].Cells[0].Value as TargetNum);
                }
                for(int j = 0 ; j < ICounter ; j++) {
                    for(int k = 0 ; k < ValueView.ColumnCount ; k++) {
                        string str = ValueView.Rows[i+j].Cells[k].Value.ToString();
                        if(str == "" && k >= Fixed) {
                            str = "0.0";
                        }
                        sw.Write(str);
                        sw.Write("\t");
                    }
                    sw.WriteLine();
                }
                if(ICounter>1) {
                    HideIsotopes(ValueView.Rows[i].Cells[0].Value as TargetNum);
                }
                Application.DoEvents();
                if(CancelSignal) {
                    CancelSignal = false;
                    break;
                }
                if (PF.progressBar1.Value < (i * 100) / ValueView.RowCount){
                    PF.progressBar1.Value = (i * 100) / ValueView.RowCount;
                }
            }
            sw.Close();
            PF.Close();
        }

        private void MRUList_RecentFileClicked(object sender, CodeArtEng.Controls.RecentFileClickedEventArgs e) {
            if(Path.GetExtension(e.FileName) == ".txt") {
                if(!OpenText(e.FileName))
                    return;
            }
            if(Path.GetExtension(e.FileName) == ".db3") {
                if(!Open_db3(e.FileName))
                    return;
            }
            Text = ProgramCaption + Path.GetFileName(e.FileName);
            TargetFileName = e.FileName;
            LFRep(null, null);
            foreach(TargetNum T in TargetIDs) {
                SameMZ(T);
            }
        }

        private void MetaRepForm_FormClosing(object sender, FormClosingEventArgs e) {
            if(ContentChanged || Text.Contains("(Restore Mode)")) {
                DialogResult DR = MessageBox.Show("There are unsaved changes in your dataset. Would you like to save it?",
                    "Save file?", MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question,MessageBoxDefaultButton.Button1);
                if(DR == DialogResult.Cancel) {
                    e.Cancel = true;
                    return;
                }
                if(DR == DialogResult.Yes)
                    saveText_Click(null, null);
            }
            if (!String.IsNullOrEmpty(TempFileName))
                File.Delete(TempFileName);
            Properties.Settings.Default.MRUList = String.Join(";", MRUList.RecentFileList);
            Properties.Settings.Default.Save();
        }

        private void SavePropButton_Click(object sender, EventArgs e) { //save current 
            if(Path.GetExtension(TargetFileName) == ".txt") {
                SaveText(TargetFileName);
                ContentChanged = false;
            } else {
                saveText_Click(sender, e);
            }
        }

        string TempFileName = "";
        private void AvtoSaveTimer_Tick(object sender, EventArgs e) {
            if(ContentChanged) {
                if (!this.IsHandleCreated && !this.IsDisposed) return;
                if (TempFileName == "") {
                    TempFileName = Path.GetTempPath();
                    TempFileName += Guid.NewGuid().ToString("D");
                    TempFileName += ".mzAccInspector";
                }
                //save temp file in main thread
                this.Invoke((MethodInvoker) delegate {
                    SaveText(TempFileName, true);
                });
            }
        }

        private void ScreenFitButton_Click(object sender, EventArgs e) {
            ScreenFitButton.Checked = !ScreenFitButton.Checked;
            int W = 0;
            if(ScreenFitButton.Checked) {
                int FixW = 0;
                for (int i = 0 ; i < Fixed ; i++) {
                    FixW += ValueView.Columns[i].Width;
                }
                W = (ValueView.Width - FixW) / (ValueView.ColumnCount - Fixed) -1;
            }else {
                W = 80;
            }
            for (int i = Fixed ; i < ValueView.ColumnCount ; i++) {
                ValueView.Columns[i].Width = W;
            }
        }

        private void ColorIsoButton_Click(object sender, EventArgs e) {
            ColorIsoButton.Checked = !ColorIsoButton.Checked;
            int Counter = 0;
            while(Counter < ValueView.RowCount - 2) {
                if(ValueView.Rows[Counter].Cells[0].Value is TargetNum &&
                    !(ValueView.Rows[Counter + 1].Cells[0].Value is TargetNum)) {
                    TargetNum T = ValueView.Rows[Counter].Cells[0].Value as TargetNum;
                    if(ColorIsoButton.Checked) {
                        for(int i = Fixed ; i < ValueView.ColumnCount ; i++) {
                            double Acc = 0.0;
                            for(int j = T.Row.Index ; j <= T.Row.Index + T.C13 ; j++) {
                                Acc += (ValueView.Rows[j].Cells[i].Value as TraceNumbers).GetValue() ?? 0.0;
                            }
                            if(Acc == 0.0)
                                continue;
                            for(int j = T.Row.Index + 1 ; j <= T.Row.Index + T.C13 ; j++) {
                                double Part = ((ValueView.Rows[j].Cells[i].Value as TraceNumbers).GetValue() ?? 0.0) / Acc;
                                if(Part == 0.0)
                                    continue;
                                Color C = Color.FromArgb(255, 255, (int)(255.0 * (1.0 - Part)), (int)(255.0 * (1.0 - Part)));
                                //Color C = ValueView.Columns[i].HeaderCell.Style.BackColor;
                                ValueView.Rows[j].Cells[i].Style.BackColor = ControlPaint.Light(C, (float)Part);
                            }
                        }
                    }else {
                        for(int i = Fixed ; i < ValueView.ColumnCount ; i++) {
                            for(int j = T.Row.Index + 1 ; j <= T.Row.Index + T.C13 ; j++) {
                                if(ValueView.Rows[j].Index % 2 == 1) {
                                    ValueView.Rows[j].Cells[i].Style.BackColor = ValueView.AlternatingRowsDefaultCellStyle.BackColor;
                                } else {
                                    ValueView.Rows[j].Cells[i].Style.BackColor = ValueView.DefaultCellStyle.BackColor;
                                }
                            }
                        }
                    }
                }
                Counter++;
            }

        }

        bool ShowNeighbours = true;
        private void NeighButton_Click(object sender, EventArgs e) {
            ShowNeighbours = !ShowNeighbours;
            NeighButton.Checked = ShowNeighbours;
            ShowGraphs();
        }

        private void ValueView_SizeChanged(object sender, EventArgs e) {
            if(ScreenFitButton.Checked) {
                int FixW = 0;
                for (int i = 0 ; i < Fixed ; i++) {
                    FixW += ValueView.Columns[i].Width;
                }
                int W = (ValueView.Width - FixW) / (ValueView.ColumnCount - Fixed) -1;
                for (int i = Fixed ; i < ValueView.ColumnCount ; i++) {
                    ValueView.Columns[i].Width = W;
                }
            }
        }
    }
}

