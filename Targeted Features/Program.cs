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
using RawMSBox;

namespace Targeted_Features
{
    class Program
    {
        //Parameters
        static public double MassError; //in ppm - there can be 3 kinds of errors - general error, 
                                            //intensity dependent scan-to-scan error, local error inside of one feature
                                            // Instrument Resolution can also be important as minimal peak distance corresponds to resolution
        static public double RTError; //in minutes
        static public int C13toCheck;

        static public double PeakMinWidth;
        static public double PeakMaxWidth;
        static public double PeakMinIntensity;
        static public double PeakbaselineRatio;

        static public double DataThres;

        static public int ZeroScans = 0;


        //Constants
        static public double C13Shift = 1.003354838;
        static public double N15Shift = 0.997035;

        //Processing switches

        static public FileService RawFileService;
        static public DBInterface DBInt;

        static int Main(string[] args){
            //Data Sources
            //args[0] - database
            //args[1] - Raw File Name
            //args[2] - ID for file
            try{

                Console.ReadLine();
                Console.WriteLine("Information: Loading...", args[1]);

                //Load Parameters

                string DBName = args[0];
                int FileID = Convert.ToInt32(args[2]);
                //against database locking at start time

                int RepeatCount = 0; 
                while(RepeatCount < 10){
                    try{
                        DBInt = new DBInterface();
                        DBInt.InitDB(DBName);
                        LoadParameters();
                        break;
                    }catch(System.Data.SQLite.SQLiteException sqle){
                        if (sqle.ErrorCode == System.Data.SQLite.SQLiteErrorCode.Busy && RepeatCount<10){
                            //здесь возможна генерация идентичных features (- с точностью до границ пика)
                            Console.WriteLine("Warning: {0}",sqle.Message);
                            System.Threading.Thread.Sleep(10000);
                            RepeatCount++;
                        }else{
                            throw sqle;
                        }
                    }
                }

                RawFileService = new FileService(args[1]);

                Target T = null;
                MZData P = null;
                double MaxI = 0;

                Console.WriteLine("Information: Building data map");
                RawFileService.BuildDataMap();
                P = RawFileService.BiggestUnused();
                T = Target.TargetFromPoint(P,0);
                MaxI = P.Intensity;

                Console.WriteLine("Information: Targets processing...", args[1]);

                List<Feature> ReadyFeatures = new List<Feature>();
                int i = 0;
                int ICount = 1;
                while (T != null ){
                    //Progress indicator
                    if ((int)Math.Pow(100,(Math.Log10(MaxI/DataThres)-Math.Log10(P.Intensity/DataThres))/(Math.Log10(MaxI/DataThres))) > ICount ){
                        Console.WriteLine("Information: Targets - {0}; Intensity - {1} ",i,P.Intensity);
                        Console.WriteLine("{0}%...",(int)Math.Pow(100,(Math.Log10(MaxI/DataThres)-Math.Log10(P.Intensity/DataThres))/(Math.Log10(MaxI/DataThres))));
                        ICount++;
                    }
                    Feature F = FeatureForTarget(T,P);
                    if (F!=null) ReadyFeatures.Add(F);
                    i++;

                    //make a features from additional peaks
                    if (F!=null){
                        for (int j = 0; j < F.TPeaks.Peaks.Count; j++){
                            if (F.TPeaks.TargetPeak != F.TPeaks.Peaks[j]){
                                T = Target.TargetFromPeak(F.TPeaks.Peaks[j],i);
                                Feature NF = FeatureForTarget(T,P);
                                ReadyFeatures.Add(NF); //!!!!! ReadyFeatures.Add(F); 
                                i++;
                            }
                        }
                    }
                    P = RawFileService.BiggestUnused();
                    if(P == null)
                        break;
                    T = Target.TargetFromPoint(P,i);

                }

                RepeatCount = 0; 
                while(RepeatCount < 10000){
                    try{
                        DBInt.tr = DBInt.con.BeginTransaction();
                        DBInt.SaveFile(args[1], FileID,RawFileService.RawFile.Mode);
                        LCMSGroup.GroupBase = DBInt.GetGroupBase();
                        foreach (Feature F in ReadyFeatures){
                            if (F != null && DBInt != null){
                                F.Write(DBInt.con, FileID);
                            }
                        }
                        RawFileService.SaveRTs(DBInt.con, FileID);
                        LCMSGroup.SaveGroups(DBInt.con,FileID);
                        DBInt.tr.Commit();
                        break;
                    }catch(System.Data.SQLite.SQLiteException sqle){
                        if (sqle.ErrorCode == System.Data.SQLite.SQLiteErrorCode.Busy && RepeatCount<1000){
                            Console.WriteLine("Warning: {0}",sqle.Message);
                            System.Threading.Thread.Sleep(1000);
                            RepeatCount++;
                        }else{
                            throw sqle;
                        }
                    }
                }
                Console.WriteLine("Completed");
                Console.ReadLine();
            }catch(Exception e){
                Console.Write("Error:");
                Console.Write(e.Message);
                Console.WriteLine("STACKINFO:"+e.StackTrace);
                Console.WriteLine("Completed");
                Console.ReadLine();
                return 1;
            }
            return 0;
        }

        static double MassDiff(double mz1, double mz2){
            return (Math.Abs(mz1 - mz2) * 1000000.0) / ((mz1 + mz2) / 2.0);
        }

        static Feature FeatureForTarget(Target T, MZData P){
            Feature F = new Feature();
            T.Feature = F;
            F.Target = T;

            //Monoisotopic trace
            MZData Apex = null;

            Apex = P;

            if (Apex == null) return null;

            F.MainTrace = LCTrace.CreateTrace(RawFileService.GroupFromPoint(Apex),Apex);//main for untargeted analysis 

            //? gapped for main trace
            if (F.MainTrace == null) return null;
            F.MainTrace.Attribution = "C0N0";

            double RTStart = F.MainTrace.Group.Points[0].RT;
            double RTEnd = F.MainTrace.Group.Points[F.MainTrace.Group.Points.Count-1].RT;

            //Check if Apex outside of RT Window
            if (F.MainTrace.Apex.RT < T.RTMin || F.MainTrace.Apex.RT> T.RTMax ){
                F.MainApexOutsideRtWindow = true;
            }

            F.TPeaks = new TracePeaks(F.MainTrace);
            F.TPeaks.waveletPeakDetection(PeakMinWidth, PeakMaxWidth, PeakMinIntensity, PeakbaselineRatio);
            F.TPeaks.SelectClosestAsTarget(F.Target);
            //End of monotrace

            F.HasPrevIsotope = false;
            //End of preisotopes 

            //Checks for isotopic peaks
            F.Isotopes = new LCTrace[T.C13toCheck + 1];

            F.Isotopes[0] = F.MainTrace; 

            //Pure isotopes
            for (int C13 = 1 ; C13 <= T.C13toCheck ; C13++){
                double TargetMass = Apex.Mass + (C13Shift * (double)(C13))/T.Charge;
                    MZData D = RawFileService.RawFile.RawSpectra[Apex.Scan].FindNearestPeak(TargetMass, MassError);
                    if (D.Mass > 0.0){
                        F.Isotopes[C13] = LCTrace.CreateTrace(RawFileService.GroupFromPoint(D),D);
                    }else{
                        F.Isotopes[C13] = null;
                    }
                    //Gapped trace (it was only actual if low signals is turned on - may provide some non-obvious errors)
                    if ( F.Isotopes[C13] == null || 
                        (F.Isotopes[C13].Group.Points[0].RT>T.RTMin && 
                        F.Isotopes[C13].Group.Points[F.Isotopes[C13].Group.Points.Count-1].RT<T.RTMax)) {
                            F.Isotopes[C13] = LCTrace.CreateTrace(RawFileService.GroupFromArea(T.RTMin, T.RTMax, TargetMass),D.Mass==0.0?null:D);
                    }
                if (F.Isotopes[C13] != null)
                    F.Isotopes[C13].Attribution = String.Format("C{0}N{1}", C13, 0);
            }

            //Apply peaks
            if (F.TPeaks.TargetPeak != null){
                F.ApplyPeak(F.TPeaks.TargetPeak);
            }
            return F;
        }

        static public double PPMError(double Mass, Target T, string Attr){
            if (Attr.IndexOf("/")!= -1){
                Attr = Attr.Substring(0,Attr.IndexOf("/"));
            }
            int Cs = Convert.ToInt32(Attr.Substring(Attr.IndexOf("C") + 1, Attr.IndexOf("N") - Attr.IndexOf("C") - 1));
            int Ns = Convert.ToInt32(Attr.Substring(Attr.IndexOf("N") + 1));
            double TargetMass = T.MZ+(double)Cs*C13Shift+(double)Ns*N15Shift;
            return (Mass - TargetMass) / (Mass / 1000000);
        }

        static public void LoadParameters(){
           
            MassError = Convert.ToDouble(DBInt.GetParameter("Mass_Accuracy"));
            RTError = Convert.ToDouble(DBInt.GetParameter("RTError"));
            ZeroScans = Convert.ToInt32(DBInt.GetParameter("Gap_Scans_Max"));

            C13toCheck = Convert.ToInt32(DBInt.GetParameter("C13_to_Check"));

            PeakMinWidth = Convert.ToDouble(DBInt.GetParameter("MinRTWidth"));
            PeakMaxWidth = Convert.ToDouble(DBInt.GetParameter("MaxRTWidth"));

            PeakMinIntensity = Convert.ToDouble(DBInt.GetParameter("MinIntensity"));
            PeakbaselineRatio = Convert.ToDouble(DBInt.GetParameter("BaselineRatio"));

            DataThres = Convert.ToDouble(DBInt.GetParameter("IntensityThreshold"));

        }

    }
}

