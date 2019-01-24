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
                    //MassDiff is mass difference in ppm
                    double MassDiff = Math.Abs(((Peaks[i].MeanMass-Peaks[j].MeanMass)/(Peaks[i].MeanMass/2.0+Peaks[j].MeanMass/2.0))*1000000.0);
                    if (MassDiff > Properties.Settings.Default.Mass_Accuracy) break;
                    //RTDiff is retention time difference 
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
                //Take current subset
                List<Peak> Subset = Subsets[m];
                //Distance sorting
                DistanceSorting(Subset);

                //Targets Selection
                while(Subset.Count > 0){
                    Application.DoEvents();
                    //That is a peak array serves to build a target
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
                    for(int i = Subset.Count - 1 ; i >= 0 ; i--) {
                        if(Subset[i].Used) {
                            Subset.RemoveAt(i);
                        }
                    }
                    DistanceSorting(Subset);
                    
                }
            }

            SQLiteCommand Command = new SQLiteCommand("Update Features Set IonID = NULL",con);
            Command.ExecuteNonQuery();
            Program.MainForm.Log("Save Targets...");

            //Save Targets
            SQLiteCommand Insert = new SQLiteCommand(
                "INSERT INTO Targets (TargetID, Name, Desc, MZ, RT, RTMin, RTMax, C13ToCheck, N15ToCheck, Candidates, Ambigous) "+
                "Values ( @ID, @Name, @Desc, @MZ, @RT, @RTMin, @RTMax,"+
                Properties.Settings.Default.C13_to_Check.ToString()+", 0, 0, 0 ); "+
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
                    PeaksForTarget = TracesForTarget;
                }
                double RT = 0.0, RTLeft = 1000000.0, RTRight = 0.0;
                for (int j = 0 ; j < PeaksForTarget.Count ; j++){
                    RT += PeaksForTarget[j].Apex / ((double)PeaksForTarget.Count);
                    if (PeaksForTarget[j].Left < RTLeft) RTLeft = PeaksForTarget[j].Left;
                    if (PeaksForTarget[j].Right > RTRight) RTRight = PeaksForTarget[j].Right;
                }

                ID.Value = i;
                Name.Value = String.Format("Target #{0}", i);
                //introdused for benchmarking 
                //Desc.Value = String.Format("RT - {0}, MZ - {1}", RT, MZ);
                string Inc="";
                for(int j = 0 ; j < FileCount ; j++) {
                    Inc+= (Targets[i][j] == null)?"0;":"1;";
                }
                Desc.Value = "Found in files: "+Inc;
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

            for( int i = 0 ; i < Peaks.Count ; i++){
                //Closest initialization
                Peaks[i].Closest = new Peak[FileCount];
                for(int j = 0 ; j < FileCount ; j++){
                    Peaks[i].Closest[j] = null;
                }

                //Looking for closest peak in every file
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

                //Fill DistSum by distances of closest peaks
                Peaks[i].DistSum = 0.0;
                for(int j = 0 ; j < FileCount ; j++){
                    Peaks[i].DistSum += Peaks[i].Closest[j] != null ? 
                        Math.Abs(Peaks[i].Closest[j].Apex - Peaks[i].Apex):
                        Properties.Settings.Default.RTError*(double)(FileCount-1);
                }
            }
            Peaks.Sort(new Peak.byDistSum());
        }

    }
}
