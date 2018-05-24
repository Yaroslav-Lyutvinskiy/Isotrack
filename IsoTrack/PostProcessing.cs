using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SQLite;
using System.Windows.Forms;

namespace IsoTrack
{

    public enum PeakOrTrace{Peak,Trace}

    public class Peak{
        public int FileID;
        public int TraceID;
        public int PeakID;
        public int CandID;
        public double MeanMass;
        public double Apex;
        public double Left;
        public double Right;
        public int ApexCount;
        public PeakOrTrace Source;
        public List<Target> CandidateFor = new List<Target>();
        public double CandScore;
        public int CompFile;
        public List<Peak> Intersects = new List<Peak>();
        public double ApexIntensity;
        public List<Peak> Paired = new List<Peak>();
        public List<double> Distances = new List<double>();
        public Peak[] Closest;
        public double DistSum;
        public Boolean Used = false;
        public bool Major = false;
        public int Mode;
        public int SameRTID = 0;
        public double SumScore = 0.0;
        public bool Selected = false;


        public class byDistSum : IComparer<Peak> {
            public int Compare(Peak x, Peak y){
                if (x.DistSum<y.DistSum) { return -1;} 
                if (x.DistSum>y.DistSum) { return 1;} 
                return 0;
            }
        }

        public class byScore : IComparer<Peak> {
            public int Compare(Peak x, Peak y){
                if (x.CandScore<y.CandScore) { return 1;} 
                if (x.CandScore>y.CandScore) { return -1;} 
                return 0;
            }
        }

        public class byMZ : IComparer<Peak> {
            public int Compare(Peak x, Peak y){
                if (x.MeanMass<y.MeanMass) { return 1;} 
                if (x.MeanMass>y.MeanMass) { return -1;} 
                return 0;
            }
        }

        public class byRT : IComparer<Peak> {
            public int Compare(Peak x, Peak y){
                if (x.Apex<y.Apex) { return 1;} 
                if (x.Apex>y.Apex) { return -1;} 
                return 0;
            }
        }


    }

    public class PostProcessing{
        List<Peak> Peaks;
        int FileCount;

        void LoadPeaks(SQLiteConnection con){
            Peaks = new List<Peak>();
            SQLiteCommand Select = new SQLiteCommand(
                "Select Features.[FileID], Traces.TraceID, MeanMass, RTPeaks.Apex, RTPeaks.[Left], RTPeaks.[Right], ApexCount, RTPeaks.ApexIntensity, PeakNumber, TracePeak, Files.Mode "+
                "From Features,Traces,RTPeaks,Files "+
                "where [Features].[FeatureID] = [Traces].[onFeatureID] and "+
                "Features.[FileID]=Files.FileIndex and "+
                "[RTPeaks].[TraceID] = Traces.[TraceID] and RTPeaks.Main = 1 ",con);
            SQLiteDataReader Reader = Select.ExecuteReader();

            int IDs = 0;
            while(Reader.Read()){
                Peak P = new Peak();
                P.FileID = Reader.GetInt32(0);
                P.TraceID = Reader.GetInt32(1);
                P.MeanMass = Reader.GetDouble(2);
                P.Apex = Reader.GetDouble(3);
                P.Left = Reader.GetDouble(4);
                P.Right = Reader.GetDouble(5);
                P.ApexCount = Reader.GetInt32(6);
                P.ApexIntensity = Reader.GetDouble(7);
                P.PeakID = Reader.GetInt32(8);
                P.Source = Reader.GetInt32(9)==1?PeakOrTrace.Trace:PeakOrTrace.Peak;
                P.CandID = IDs;
                P.Mode = Reader.GetInt32(10);
                IDs++;
                Peaks.Add(P);
            }
        }

        public void SelectTarget(SQLiteConnection con){
            Program.MainForm.Log("Loading Peaks..");
            LoadPeaks(con);
            //Number of files
            SQLiteCommand Select = new SQLiteCommand("Select Count(*) from Files", con);
            SQLiteDataReader Reader = Select.ExecuteReader();
            Reader.Read();
            FileCount = Reader.GetInt32(0);
            Reader.Close();
            Select = new SQLiteCommand("Select Distinct Mode from Files", con);
            Reader = Select.ExecuteReader();
            Reader.Read();
            string Mode = Reader.GetInt32(0) > 0 ? "+" : "-";

            List<Peak[]> Rec = new List<Peak[]>();

            Program.MainForm.Log("Pairing Peaks..");
            //Pairing 
            Peaks.Sort(new Peak.byMZ());
            for( int i = 0 ; i < Peaks.Count ; i++){
                for( int j = i ; j >= 0  ; j--){ //пейрить нужно и с собой тоже!!
                    if (Peaks[i].Mode != Peaks[j].Mode) continue;
                    double MassDiff = Math.Abs(((Peaks[i].MeanMass-Peaks[j].MeanMass)/(Peaks[i].MeanMass/2.0+Peaks[j].MeanMass/2.0))*1000000.0);
                    if (MassDiff > Properties.Settings.Default.Mass_Accuracy) break;
                    double RTDiff = Math.Abs(Peaks[i].Apex-Peaks[j].Apex);
                    if (RTDiff<Properties.Settings.Default.RTError){
                        Peaks[i].Paired.Add(Peaks[j]);
                        Peaks[j].Paired.Add(Peaks[i]);
                        Peaks[i].Distances.Add(RTDiff);
                        Peaks[j].Distances.Add(RTDiff);
                    }
                }
                for( int j = i + 1 ; j < Peaks.Count ; j++){
                    if (Peaks[i].Mode != Peaks[j].Mode) continue;
                    double MassDiff = Math.Abs(((Peaks[i].MeanMass-Peaks[j].MeanMass)/(Peaks[i].MeanMass/2.0+Peaks[j].MeanMass/2.0))*1000000.0);
                    if (MassDiff > Properties.Settings.Default.Mass_Accuracy) break;
                    double RTDiff = Math.Abs(Peaks[i].Apex-Peaks[j].Apex);
                    if (RTDiff<Properties.Settings.Default.RTError){
                        Peaks[i].Paired.Add(Peaks[j]);
                        Peaks[j].Paired.Add(Peaks[i]);
                        Peaks[i].Distances.Add(RTDiff);
                        Peaks[j].Distances.Add(RTDiff);
                    }
                }

                Application.DoEvents();
            }
            Program.MainForm.Log("Grouping Peaks..");
            //grouping to isolated subsets
            List<List<Peak>> Subsets = new List<List<Peak>>();
            while(Peaks.Count > 0){
                if (Peaks[0].Paired==null || Peaks[0].Paired.Count == 0){
                    Peaks.RemoveAt(0);
                    continue;
                }
                List<Peak> Subset = new List<Peak>();
                Subset.Add(Peaks[0]);
                Peaks.RemoveAt(0);
                for(int i = 0 ; i<Subset.Count ; i++){
                    for (int j = 0 ; j < Subset[i].Paired.Count ; j++){
                        if (!Subset.Contains(Subset[i].Paired[j])){
                            Subset.Add(Subset[i].Paired[j]);
                            Peaks.Remove(Subset[i].Paired[j]);
                        }
                    }
                }
                //for speed optimization a number of Peaks.remove can be 
                //substituted with single copying peaks to another list with exclusion to peaks of subset
                //subsets can be processed in paralel 
                Subsets.Add(Subset);
            }

            Program.MainForm.Log(String.Format("Grouping Peaks - {0} groups found",Subsets.Count));

            Program.MainForm.Log("Define Targets...");
            List<Peak[]> Targets = new List<Peak[]>();
            for (int m = 0; m < Subsets.Count; m++ ){
                List<Peak> Subset = Subsets[m];
                //Distance sorting
                DistanceSorting(Subset);

                //Targets Selection
                while(Subset.Count > 0){
                    Application.DoEvents();
                    for (int i = 0 ; i < FileCount ; i++){
                        if (Subset[0].Closest[i] != null && Subset[0].Closest[i].Used){
                            DistanceSorting(Subsets[m]);
                            break;
                        }
                    }
                    if (Subset.Count == 0) break;
                    Peak[] T = new Peak[FileCount];
                    int NullCount = 0;
                    for (int i = 0 ; i < FileCount ; i++){
                        T[i] = Subset[0].Closest[i];
                        if (Subset[0].Closest[i] == null){
                            NullCount++;
                        }else{
                            Subset[0].Closest[i].Used = true;
                        }
                    }
                    if (FileCount - NullCount < Properties.Settings.Default.Commons) break;
                    Targets.Add(T);
                    Subset.RemoveAt(0);
                }
            }

            SQLiteCommand Command = new SQLiteCommand("Update Features Set IonID = NULL",con);
            Command.ExecuteNonQuery();
            Program.MainForm.Log("Save Targets...");

            //Save Targets
            SQLiteCommand Insert = new SQLiteCommand(
                "INSERT INTO Targets (TargetID, Name, Desc, MZ, RT, RTMin, RTMax, C13ToCheck, N15ToCheck, Candidates, Ambigous) "+
                "Values ( @ID, @Name, @Desc, @MZ, @RT, @RTMin, @RTMax,"+
                Properties.Settings.Default.C13_to_Check.ToString()+", "+Properties.Settings.Default.N15_to_Check.ToString()+", 0, 0 ); "+
                "INSERT INTO Ions (TargetID, IonID, Adduct, Mode, MZ, Strongs, Candidates, Ambigous) "+
                "Values ( @ID, @ID, \"\", @Mode, @MZ, 0, 0, 0);",con);

            SQLiteParameter ID = new SQLiteParameter("@ID");
            Insert.Parameters.Add(ID);
            SQLiteParameter Name = new SQLiteParameter("@Name");
            Insert.Parameters.Add(Name);
            SQLiteParameter Desc = new SQLiteParameter("@Desc");
            Insert.Parameters.Add(Desc);
            SQLiteParameter MZP = new SQLiteParameter("@MZ");
            Insert.Parameters.Add(MZP);
            SQLiteParameter RTP = new SQLiteParameter("@RT");
            Insert.Parameters.Add(RTP);
            SQLiteParameter RTMinP = new SQLiteParameter("@RTMin");
            Insert.Parameters.Add(RTMinP);
            SQLiteParameter RtMaxP = new SQLiteParameter("@RTMax");
            Insert.Parameters.Add(RtMaxP);
            SQLiteParameter ModeP = new SQLiteParameter("@Mode");
            Insert.Parameters.Add(ModeP);

            SQLiteTransaction tr = con.BeginTransaction();

            for(int i=0 ; i<Targets.Count ; i++){
                //benefit to recognised peaks 
                int Count = 0;
                double MZ = 0.0;
                List<Peak> PeaksForTarget = new List<Peak>();
                List<Peak> TracesForTarget= new List<Peak>();
                for (int j = 0 ; j < FileCount ; j++){
                    if (Targets[i][j]!=null){
                        MZ += Targets[i][j].MeanMass;
                        Count++;
                        if (Targets[i][j].Source == PeakOrTrace.Trace){
                            TracesForTarget.Add(Targets[i][j]);
                        }else{
                            PeaksForTarget.Add(Targets[i][j]);
                        }
                    }
                }
                MZ = MZ / (double)Count;
                if (PeaksForTarget.Count==0){
                    if (Properties.Settings.Default.PeaksOnly){
                        continue;
                    }else{
                        PeaksForTarget = TracesForTarget;
                    }
                }
                double RT = 0.0, RTLeft = 1000000.0, RTRight = 0.0;
                for (int j = 0 ; j < PeaksForTarget.Count ; j++){
                    RT += PeaksForTarget[j].Apex / ((double)PeaksForTarget.Count);
                    if (PeaksForTarget[j].Left < RTLeft) RTLeft = PeaksForTarget[j].Left;
                    if (PeaksForTarget[j].Right > RTRight) RTRight = PeaksForTarget[j].Right;
                }

                ID.Value = i;
                Name.Value = String.Format("Target #{0}", i);
                Desc.Value = String.Format("RT - {0}, MZ - {1}", RT, MZ);
                MZP.Value = MZ;
                RTP.Value = RT;
                RTMinP.Value = RTLeft;
                RtMaxP.Value = RTRight;
                ModeP.Value = Mode;
                Insert.ExecuteNonQuery();
                //change peaks and traces to this query
                Target TargetForSave = new Target();
                TargetForSave.ID = i;
                TargetForSave.IonID = i;
                for (int j = 0 ; j < PeaksForTarget.Count ; j++){
                    TargetForSave.SetPeakToTarget(PeaksForTarget[j],con);
                }
                for (int j = 0 ; j < TracesForTarget.Count ; j++){
                    TargetForSave.SetPeakToTarget(TracesForTarget[j],con);
                }
            }
            tr.Commit();
            Program.MainForm.Log("Targets definition has been finished...");
        }

        public void DistanceSorting(List<Peak> Peaks)
        {
            for( int i = Peaks.Count-1 ; i >=0 ; i--){
                if (Peaks[i].Used){
                    Peaks.RemoveAt(i);
                }
            }

            for( int i = 0 ; i < Peaks.Count ; i++){
                Peaks[i].Closest = new Peak[FileCount];
                for(int j = 0 ; j < FileCount ; j++){
                    Peaks[i].Closest[j] = null;
                }

                for(int j = 0 ; j < FileCount ; j++){
                    double Min = Properties.Settings.Default.RTError * 2.0;
                    for(int k = 0 ; k < Peaks[i].Distances.Count ; k++){
                        if (Peaks[i].Paired[k].FileID == j && !Peaks[i].Paired[k].Used){
                            if(Peaks[i].Distances[k] < Min){
                                Peaks[i].Closest[j] = Peaks[i].Paired[k];
                                Min = Peaks[i].Distances[k];
                            }
                        }
                    }
                }

                Peaks[i].DistSum = 0.0;
                for(int j = 0 ; j < FileCount ; j++){
                    Peaks[i].DistSum += Peaks[i].Closest[j] != null ? 
                        Math.Abs(Peaks[i].Closest[j].Apex - Peaks[i].Apex):
                        Properties.Settings.Default.RTError*(double)(FileCount-1);
                }
            }
            Peaks.Sort(new Peak.byDistSum());
        }

        double SelectivityThres = 3.0;
        List<Target> Targets;
        List<Target> Standards;

        public void RefineStands(SQLiteConnection con){

            Program.MainForm.Log("Loading Peaks..");
            LoadPeaks(con);
            //Targets = Target.ReadTargets(con);
            Targets = Program.MainForm.Targets;
            Standards = Program.MainForm.Standards;
            SelectivityThres = Properties.Settings.Default.SelectivityThreshold;

            //if RTs for target == 
            double MinRT = 10000.0;
            double MaxRT = 0.0;
            if (!Properties.Settings.Default.IgnoreRT){
                for(int i = 0 ; i < Targets.Count ; i++){
                    Targets[i].RT = 0.0;
                }
            }
            for( int j = 0 ; j < Peaks.Count ; j++){
                MinRT = Peaks[j].Left < MinRT ? Peaks[j].Left : MinRT;
                MaxRT = Peaks[j].Right > MaxRT ? Peaks[j].Right : MaxRT;
            }
            for(int i = 0 ; i < Targets.Count ; i++){
                if ((Targets[i].RTMin == 0.0 && Targets[i].RTMax == 0.0) || Targets[i].RT == 0.0){
                    Targets[i].RTMin = MinRT - 0.5;
                    Targets[i].RTMax = MaxRT + 0.5;
                }
            }

            
            List<Peak[]> Rec = new List<Peak[]>();

            Program.MainForm.Log("Pairing Peaks..");
            //Pairing 
            Peaks.Sort(new Peak.byMZ());
            for( int i = 0 ; i < Peaks.Count ; i++){
                for( int j = i - 1 ; j >= 0  ; j--){
                    double MassDiff = Math.Abs(((Peaks[i].MeanMass-Peaks[j].MeanMass)/(Peaks[i].MeanMass/2.0+Peaks[j].MeanMass/2.0))*1000000.0);
                    if (MassDiff > Properties.Settings.Default.Mass_Accuracy) break;
                    double RTDiff = Math.Abs(Peaks[i].Apex-Peaks[j].Apex);
                    if (RTDiff<Properties.Settings.Default.RTError){
                        Peaks[i].Paired.Add(Peaks[j]);
                        Peaks[j].Paired.Add(Peaks[i]);
                        Peaks[i].Distances.Add(RTDiff);
                        Peaks[j].Distances.Add(RTDiff);
                    }
                }
                for( int j = i + 1 ; j < Peaks.Count ; j++){
                    double MassDiff = Math.Abs(((Peaks[i].MeanMass-Peaks[j].MeanMass)/(Peaks[i].MeanMass/2.0+Peaks[j].MeanMass/2.0))*1000000.0);
                    if (MassDiff > Properties.Settings.Default.Mass_Accuracy) break;
                    double RTDiff = Math.Abs(Peaks[i].Apex-Peaks[j].Apex);
                    if (RTDiff<Properties.Settings.Default.RTError){
                        Peaks[i].Paired.Add(Peaks[j]);
                        Peaks[j].Paired.Add(Peaks[i]);
                        Peaks[i].Distances.Add(RTDiff);
                        Peaks[j].Distances.Add(RTDiff);
                    }
                }
                //for( int j = 0 ; j < Peaks.Count ; j++){
                //    double RTDiff = Math.Abs(Peaks[i].Apex-Peaks[j].Apex);
                //    double MassDiff = Math.Abs(((Peaks[i].MeanMass-Peaks[j].MeanMass)/(Peaks[i].MeanMass/2.0+Peaks[j].MeanMass/2.0))*1000000.0);
                //    if (RTDiff<Properties.Settings.Default.RTError && MassDiff<Properties.Settings.Default.Mass_Accuracy){
                //        Peaks[i].Paired.Add(Peaks[j]);
                //        Peaks[j].Paired.Add(Peaks[i]);
                //        Peaks[i].Distances.Add(RTDiff);
                //        Peaks[j].Distances.Add(RTDiff);
                //    }
                //}
                Application.DoEvents();
            }

            //Distance sorting
            //DistanceSorting();
            
            Program.MainForm.Log("Candidates selection...");
            StreamWriter sw = new StreamWriter(Properties.Settings.Default.StandardsReport);
            sw.WriteLine("No candidates standarts:");


            //Peaks are sorted by mz now - so could be optimized 
            Targets.Sort(new Target.byMZ());
            int Current = 0;
            for(int i =  Targets.Count-1 ; i >= 0 ; i--){
                for( int j = Current ; j < Peaks.Count ; j++){
                    double MassDiff = Math.Abs(((Targets[i].MZ-Peaks[j].MeanMass)/(Targets[i].MZ/2.0+Peaks[j].MeanMass/2.0))*1000000.0);
                    if (MassDiff<Properties.Settings.Default.Mass_Accuracy){
                        Current = j;
                        break;
                    }
                }
                for( int j = Current ; j < Peaks.Count ; j++){
                    double MassDiff = Math.Abs(((Targets[i].MZ-Peaks[j].MeanMass)/(Targets[i].MZ/2.0+Peaks[j].MeanMass/2.0))*1000000.0);
                    if (MassDiff>Properties.Settings.Default.Mass_Accuracy) {
                        break;
                    }
                    if (Targets[i].FileID == Peaks[j].FileID && 
                        Peaks[j].Apex > Targets[i].RTMin && 
                        Peaks[j].Apex < Targets[i].RTMax &&
                        Peaks[j].Mode == Targets[i].Mode ){
                        Targets[i].Candidates.Add(Peaks[j]);
                        Peaks[j].CandidateFor.Add(Targets[i]);
                    }
                }

                //no candidates for target
                if (Targets[i].Candidates.Count == 0 ){
                    sw.WriteLine("Target - {0} MZ-{1} FileID-{2} :- No candidates",Targets[i].Name,Targets[i].MZ, Targets[i].FileID);
                    //Targets.RemoveAt(i);
                }
                Application.DoEvents();
            }

            //report writing
        
            sw.WriteLine("Ambigous peaks:");

            //Check for Targets, ambigous by definition
            for (int i = 0 ; i < Targets.Count ; i++){
                for (int j = 0 ; j < Targets.Count ; j++){
                    if (Targets[i].FileID == Targets[j].FileID && i!=j){
                        double PPMDiff = (Math.Abs(Targets[i].MZ - Targets[j].MZ) * 1000000.0) / ((Targets[i].MZ + Targets[j].MZ) / 2.0);
                        bool RTOver = Math.Min(Targets[i].RTMax - Targets[j].RTMin, Targets[j].RTMax - Targets[i].RTMin) > 0;
                        if (PPMDiff<Properties.Settings.Default.Mass_Accuracy && RTOver && 
                            Targets[i].Mode == Targets[j].Mode && Targets[i].FileID == Targets[j].FileID){
                            Targets[i].Ambigous = (Targets[i].Adduct == Targets[j].Adduct) ? 2 : 1; 
                            Targets[j].Ambigous = (Targets[i].Adduct == Targets[j].Adduct) ? 2 : 1; 
                        }
                    }
                }
            }

            //Check for double target peaks - Targets amb by peaks
            List<double> NoiseInts = new List<double>();
            for( int j = 0 ; j < Peaks.Count ; j++){
                if (Peaks[j].CandidateFor.Count > 1){
                    sw.WriteLine("Ambigous peak: MZ-{0}, RT-{1}", Peaks[j].MeanMass, Peaks[j].Apex);
                    for (int k = 0 ; k < Peaks[j].CandidateFor.Count ; k++){
                        sw.WriteLine("For Target - {0} MZ-{1} FileID-{2} Adduct-{3}",Peaks[j].CandidateFor[k].Name,Peaks[j].CandidateFor[k].MZ, Peaks[j].CandidateFor[k].FileID, Peaks[j].CandidateFor[k].Adduct);
                        if (Peaks[j].CandidateFor[k].Ambigous % 2 == 1){
                            Peaks[j].CandidateFor[k].Ambigous = 3;
                        }else{
                            Peaks[j].CandidateFor[k].Ambigous = 4;
                        }
                    }
                }
            }
            LoadRTs(con);
            //load file List 
            SQLiteCommand FilesQuery = new SQLiteCommand("Select FileIndex, Mode From Files",con);
            SQLiteDataReader Reader = FilesQuery.ExecuteReader();
            List<int> FileIDs = new List<int>();
            while(Reader.Read())
                //+1 to resolve ID=0 which is not negative or positive 
                FileIDs.Add((Reader.GetInt32(0)+1) * Reader.GetInt32(1));

            Program.MainForm.Log("Scoring candidates...");
            foreach (Target T in Targets){
                foreach(Peak C in T.Candidates){
                    //Аll the paired peaks for particular candidate
                    //List<Peak> AllPaired = new List<Peak>();
                    //AllPaired.Add(C);
                    //for (int k = 0 ; k < AllPaired.Count ; k++){
                    //    for ( int l = 0 ; l < AllPaired[k].Paired.Count ;  l++){
                    //        if (!AllPaired.Contains(AllPaired[k].Paired[l])){
                    //            AllPaired.Add(AllPaired[k].Paired[l]);
                    //        }
                    //    }
                    //}
                    ////Calc candidate score 
                    ////combine list of intensities
                    //List<double> Ints = new List<double>();
                    double MaxInt = 0.0;
                    int MaxFile = -1;
                    //for (int k = 0 ; k < AllPaired.Count ; k++){
                    //    if (AllPaired[k].CandidateFor.Count == 0){
                    //        if (AllPaired[k].ApexIntensity>MaxInt){
                    //            MaxInt = AllPaired[k].ApexIntensity;
                    //            MaxFile = AllPaired[k].FileID;
                    //        }
                    //    }else{
                    //        if (!AllPaired[k].CandidateFor.Contains(T)){
                    //            C.Intersects.AddRange(AllPaired[k].CandidateFor);
                    //        }
                    //    }
                    //}
                    List<int> BackGroundFiles = new List<int>();
                    BackGroundFiles.AddRange(FileIDs);
                    if(BackGroundFiles.Contains(C.FileID+1)) {
                        BackGroundFiles = BackGroundFiles.Where(ID => ID > 0).ToList();
                        BackGroundFiles.Remove(C.FileID + 1);
                    } else {
                        BackGroundFiles = BackGroundFiles.Where(ID => ID < 0).ToList();
                        BackGroundFiles.Remove(-C.FileID - 1);
                    }

                    for (int k = 0 ; k < C.Paired.Count ; k++){
                        if (C.Paired[k].CandidateFor.Count == 0){
                            if (C.Paired[k].ApexIntensity>MaxInt){
                                MaxInt = C.Paired[k].ApexIntensity;
                                MaxFile = C.Paired[k].FileID;
                            }
                        }else{
                            if (!C.Paired[k].CandidateFor.Contains(T)){
                                C.Intersects.Add(C.Paired[k]);
                            }
                        }
                        if(BackGroundFiles.Contains(C.Paired[k].FileID+1)) 
                            BackGroundFiles.Remove(C.Paired[k].FileID+1);
                        if(BackGroundFiles.Contains(-C.Paired[k].FileID-1)) 
                            BackGroundFiles.Remove(-C.Paired[k].FileID-1);
                    }

                    C.CompFile = MaxFile;
                    //!! нужно проверять на background тоже
                    double Backgr = CheckBackground(BackGroundFiles, C.Left, C.Right, C.MeanMass, con);
                    if(Backgr > MaxInt) MaxInt = Backgr;
                    if(MaxInt>0.0){
                        C.CandScore = C.ApexIntensity/MaxInt;
                    }else{
                        C.CandScore = C.ApexIntensity/Properties.Settings.Default.IntensityThreshold;
                    }
                    C.SumScore = C.CandScore;
                }

            }
            //Grouping Same RT candidates
            int GroupCount = 0; 
            foreach(Target S in Standards){
                List<Peak> StandCands = new List<Peak>();
                foreach (Target T in S.AdductTargets){
                    T.Candidates.Sort(new Peak.byScore());
                    //there are artifact candidates with the same RT from the same file - Have to filter them 
                    for (int i = 0; i < T.Candidates.Count; i++ ){
                        bool flag = true;
                        for (int j = 0 ; j < i; j++) {
                            if (!((T.Candidates[i].Right <= T.Candidates[j].Left) || (T.Candidates[i].Left >= T.Candidates[j].Right))) { //if intersecting
                                flag = false; break;
                            }
                        }
                        if (flag) {
                            StandCands.Add(T.Candidates[i]);
                        }

                    }
                }
                foreach (Peak P1 in StandCands){
                    double SumScore = 0.0;
                    if (P1.SameRTID != 0) continue;
                    foreach (Peak P2 in StandCands){
                        if (P1 == P2) continue;
                        if (Math.Abs(P1.Apex-P2.Apex)<Properties.Settings.Default.RTError){
                            if (P1.SameRTID == 0 ){
                                GroupCount++;
                                P1.SameRTID = GroupCount;
                                SumScore += P1.CandScore;
                            }
                            P2.SameRTID = GroupCount;
                            SumScore += P2.CandScore;
                        }
                    }
                    //there are candidates with the same RT from the same file - Have to filter them 
                    foreach (Peak P2 in StandCands){
                        if (P2.SameRTID == P1.SameRTID && P1.SameRTID!=0){
                            P2.SumScore = SumScore;
                        }
                    }
                }
                int MaxScoreInd = 0;
                for (int i = 1 ; i < StandCands.Count ; i++){
                    if (StandCands[i].SumScore > StandCands[MaxScoreInd].SumScore){
                        MaxScoreInd = i;
                    }
                }
                if (StandCands.Count>0 && StandCands[MaxScoreInd].SameRTID == 0){
                    if (StandCands[MaxScoreInd].CandScore > Properties.Settings.Default.SelectivityThreshold){
                        StandCands[MaxScoreInd].Selected = true;
                    }
                }else{
                    foreach(Peak C in StandCands){
                        if (StandCands[MaxScoreInd].CandScore > Properties.Settings.Default.SelectivityThreshold && 
                            C.SameRTID == StandCands[MaxScoreInd].SameRTID ){
                            C.Selected = true;
                        }
                    }
                }
            }


            //Calc strongs and major candidates
            foreach (Target T in Targets){
                if (T.Candidates.Count > 0){
                    //strong candidates
                    if (T.Candidates[0].CandScore < SelectivityThres) continue;
                    T.Strongs = 1;
                    int MaxIndex = 0;
                    //bool Ambig = T.Candidates[0].CandidateFor.Count > 1;
                    //Strongs and Majors
                    for (int i = 1; i < T.Candidates.Count; i++) {
                        //Strong definition
                        if (T.Candidates[0].CandScore - T.Candidates[i].CandScore < 10.0 && Math.Abs(T.Candidates[0].Apex-T.Candidates[i].Apex)>0.01){
                            T.Strongs++;
                        }
                        if (T.Candidates[MaxIndex].ApexIntensity < T.Candidates[i].ApexIntensity ){
                            MaxIndex = i;
                        }
                    }
                    T.Candidates[MaxIndex].Major = true;
                    T.RT = T.Candidates[0].Apex;
                    T.RTMin = T.Candidates[0].Left;
                    T.RTMax = T.Candidates[0].Right;
                }
                Application.DoEvents();
            }

            //Save to DB
            Program.MainForm.Log("Write targets...");
            SQLiteTransaction tr = con.BeginTransaction();
            //SQLiteCommand Command = new SQLiteCommand("Update Features Set TargetID = NULL",con);
            //Command.ExecuteNonQuery();
            SQLiteCommand Command = new SQLiteCommand("Delete from Targets",con);
            Command.ExecuteNonQuery();
            Command = new SQLiteCommand("Delete from Ions",con);
            Command.ExecuteNonQuery();
            foreach (Target S in Standards){
                S.SaveDB(con, false);
                foreach( Target T in S.AdductTargets){
                    if (T.Candidates.Count == 0) continue;
                    T.SaveDB(con); 
                    T.SetPeakToTarget(T.Candidates[0],con);
                    T.Candidates.Sort(new Peak.byScore());
                    foreach(Peak C in T.Candidates){
                        //save candidate to DB
                        SQLiteCommand CandidateInsert = 
                            new SQLiteCommand("INSERT INTO Candidates ( IonID, CandID, FileID, MZ, TraceID, PeakNumber, Score, Selected, Major, SameRTID, SumScore) "+
                            String.Format("Values ( {0}, {1}, {2}, {3:f6}, {4}, {5},  {6:f2}, {7}, {8}, {9}, {10} ) ",
                            T.IonID,C.CandID, C.FileID, C.MeanMass, C.TraceID, C.PeakID, C.CandScore, (C.Selected?1:0), C.Major?1:0, C.SameRTID, C.SumScore), con);
                        CandidateInsert.ExecuteNonQuery();

                        //save intersects to DB 
                        foreach(Peak InterCand in C.Intersects){
                            foreach(Target InterTarg in InterCand.CandidateFor){
                                SQLiteCommand IntersectsInsert = new SQLiteCommand("INSERT INTO InterSects(LeftStandardID, LeftCandID, RightStandardID, RightCandID) "+
                                    String.Format("Values ( {0}, {1}, {2}, {3}) ",
                                    T.ID,C.CandID,InterTarg.ID,InterCand.CandID),con);
                                IntersectsInsert.ExecuteNonQuery();
                            }
                        }
                    }
                }
                Application.DoEvents();
            }
            tr.Commit();
            //output - candidates

            //Save to File
            sw.WriteLine("Name\tAdduct\tMZ\tFact. MZ\tFileID\tRT\tRTMin\tRTMax\tIntens ity\tScore\tPeakOrTrace\tComp. File ID");
            foreach (Target T in Targets){
                foreach(Peak C in T.Candidates){
                    sw.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}", 
                        T.Name, T.Adduct, T.MZ, C.MeanMass, T.FileID, C.Apex, C.Left, C.Right, C.ApexIntensity, C.CandScore, C.Source, C.CompFile);
                }
            }
            sw.WriteLine("Main standarts report:");
            sw.WriteLine("Name\tAdduct\tDesc\tMZ\tMZ Fact\tRT\tRTMin\tRTMax\tC13TOCHECK\tN15TOCHECK\tFileID\tIntensity\tScore\tCandidates\tStrongs\tAmbigous\tMajor\tIntersect");

            foreach (Target T in Targets){
                if (T.Candidates.Count > 0){
                    string Intersects = "";
                    for (int i = 0; i < T.Candidates[0].Intersects.Count; i++){
                        double RTDiff = Math.Abs(T.Candidates[0].Intersects[i].Apex - T.Candidates[0].Apex);
                        if (RTDiff <= Properties.Settings.Default.RTError && 
                            !Intersects.Contains(T.Candidates[0].Intersects[i].CandidateFor[0].Name ) &&
                            T.Candidates[0].Intersects[i].CandScore > SelectivityThres){
                            if (T.Candidates[0].Intersects[i].CandidateFor.Count >0 &&
                                T.Candidates[0].Intersects[i].CandidateFor[0].Candidates[0] == T.Candidates[0].Intersects[i] )
                                Intersects += T.Candidates[0].Intersects[i].CandidateFor[0].Name + ";";
                        }
                    }
                    if (T.Candidates[0].CandScore > SelectivityThres){
                        sw.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}\t{12}\t{13}\t{14}\t{15}\t{16}\t{17}",
                            T.Name, T.Adduct, T.Desc, T.MZ, T.Candidates[0].MeanMass, T.Candidates[0].Apex, T.Candidates[0].Left, T.Candidates[0].Right, T.C13toCheck, T.N15toCheck, T.FileID, T.Candidates[0].ApexIntensity, T.Candidates[0].CandScore, T.Candidates.Count, T.Strongs, T.Ambigous>0 ? 1 : 0, T.Candidates[0].Major ? 1 : 0,Intersects);
                    }
                }
                Application.DoEvents();//agilent 6495
            }
            sw.Close();
            Program.MainForm.Log("Standards analysis has been finished.");

        }

        List<Dictionary<int, double>> RTs = null;
        //читаем таблицу возможных RT и сохраняем ее на будущее
        public void LoadRTs(SQLiteConnection con){
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



        private double CheckBackground(List<int> FileIDs, double RTMin, double RTMax, double MZ,SQLiteConnection con){
            //retrieve traces 
            double Error = (Properties.Settings.Default.Mass_Accuracy*MZ)/1000000.0;
            double MinMZ = MZ - Error;
            double MaxMZ = MZ + Error;
            double MaxIntensity = 0.0;
            for(int i = 0 ; i < FileIDs.Count ; i++) {
                SQLiteCommand TraceQuery = new SQLiteCommand(String.Format(
                    "Select Distinct StartScan,EndScan,Points,FileID From PointGroups,Traces,Features " +
                    "Where Traces.[onFeatureID] = [Features].[FeatureID] and " +
                    "Traces.[GroupID] = [PointGroups].[GroupID] and " +
                    "[PointGroups].[MinMZ] < {0} and PointGroups.[MaxMZ] > {1} and " +
                    "FileID = {2} ", MaxMZ, MinMZ, Math.Abs(FileIDs[i])-1), con);
                SQLiteDataReader Reader = TraceQuery.ExecuteReader();
                //Extract traces 
                while(Reader.Read()) {
                    int ScanCounter = Reader.GetInt32(0);
                    int EndScan = Reader.GetInt32(1);
                    string PStr = Reader.GetString(2);
                    int FileForRT = Reader.GetInt32(3);
                    byte[] Pbyte = Convert.FromBase64String(PStr);
                    int ByteCounter = 0;
                    double RT = RTs[FileForRT][ScanCounter];
                    while(ByteCounter < Pbyte.Length) {
                        if(RT > RTMax)
                            break;
                        if(RT > RTMin) {
                            double Mass = BitConverter.ToDouble(Pbyte, ByteCounter);
                            ByteCounter += 8;
                            if(Mass > MinMZ && Mass < MaxMZ) {
                                double Intensity = BitConverter.ToSingle(Pbyte, ByteCounter);
                                if(Intensity > MaxIntensity) {
                                    MaxIntensity = Intensity;
                                }
                            }
                            ByteCounter += 4;
                        } else {
                            ByteCounter += 12;
                        }
                        do {
                            ScanCounter++;
                        } while(!(RTs[FileForRT].TryGetValue(ScanCounter, out RT)) && ScanCounter <= EndScan);
                    }
                }
            }
            return MaxIntensity;
        }

        public void BackFilling(SQLiteConnection con) {
            SQLiteCommand RTs = new SQLiteCommand(
                "Select Min(MinRts.RT),Max(MaxRTs.RT),Targets.[Name], Targets.[TargetID] from "+
                "(Select Traces.TraceID, PointGroups.GroupID, SMin.[RT], FileIndex, Features.FileID, FeatureID "+
                "from PointGroups, Spectra as SMin ,Traces, Features "+
                "where SMin.[MSOnly] = 1 and "+
                "PointGroups.[StartScan] = SMin.[ScanNumber] and "+
                "Traces.[GroupID] = [PointGroups].[GroupID] and "+
                "Features.[FeatureID] = Traces.[onFeatureID] and "+
                "FileIndex = Features.FileID) as MinRts, "+
                "(Select Traces.TraceID, PointGroups.GroupID, SMin.[RT], FileIndex, Features.FileID , Features.IonID "+
                "from PointGroups, Spectra as SMin ,Traces, Features "+
                "where SMin.[MSOnly] = 1 and "+
                "PointGroups.[EndScan] = SMin.[ScanNumber] and "+
                "Traces.[GroupID] = [PointGroups].[GroupID] and "+
                "Features.[FeatureID] = Traces.[onFeatureID] and "+
                "FileIndex = Features.FileID) as MaxRTs, "+
                "Ions, Targets "+
                "where MinRTs.[TraceID] = MaxRTs.[TraceID] and "+
                "MaxRts.[IonID] = Ions.IonID and "+
                "Ions.[TargetID] = Targets.[TargetID] "+
                "Group by 3 ",con);
            SQLiteDataReader Reader = RTs.ExecuteReader();

            while(Reader.Read()) {
                SQLiteCommand Update = new SQLiteCommand(String.Format(
                    "Update Targets Set FullRTMin = {0}, FullRTMax = {1} where TargetID = {2}",
                    Reader.GetDouble(0),Reader.GetDouble(1),Reader.GetInt32(3)),con);
                Update.ExecuteNonQuery();
            }

            SQLiteCommand Delete = new SQLiteCommand("Delete from Spectra",con);
            Delete.ExecuteNonQuery();
            Delete = new SQLiteCommand("Delete from PointGroups",con);
            Delete.ExecuteNonQuery();
            Delete = new SQLiteCommand("Delete from RTPeaks",con);
            Delete.ExecuteNonQuery();
            Delete = new SQLiteCommand("Delete from Traces",con);
            Delete.ExecuteNonQuery();
            Delete = new SQLiteCommand("Delete from Features",con);
            Delete.ExecuteNonQuery();
            Delete = new SQLiteCommand("Delete from Files",con);
            Delete.ExecuteNonQuery();
            Delete = new SQLiteCommand("Vacuum",con);
            Delete.ExecuteNonQuery();
        }
    }

}
