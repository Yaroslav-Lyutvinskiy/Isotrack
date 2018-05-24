using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SQLite;

namespace Targeted_Features
{
    public class Feature {
        public LCTrace MainTrace;
        public LCTrace Minus15NTrace;
        public LCTrace Minus13CTrace;
        public LCTrace[,] PureIsotopes;
        public TracePeaks TPeaks;
        public List<LCTrace> MixedIsotopes;
        public bool MainApexOutsideRtWindow = false;
        public bool HasPrevIsotope = false;
        public Target Target;
        private static int LastID =0;
        public int ID;
        public void Write(StreamWriter sw, SQLiteConnection con = null, int FileID = 0){
            if (con != null){
                //check for next avialable Feature ID
                if (LastID == 0){
                    SQLiteCommand Select = new SQLiteCommand(
                        "Select max(FeatureID) From Features ",con);
                    SQLiteDataReader Reader = Select.ExecuteReader();
                    Reader.Read();
                    try {
                        LastID = Reader.GetInt32(0)+1;
                    }
                    catch(Exception){
                        LastID++;
                    }
                }else{
                    LastID++;
                }
                ID = LastID;
                SQLiteCommand Insert = new SQLiteCommand(String.Format(
                    "Insert Into Features (FeatureID, IonID, FileID, ApexOutsideRTWindow, HasPreviousIso) "+
                    "Values ( {0}, {1}, {2}, {3}, {4} )",ID, Target.IonID, FileID, MainApexOutsideRtWindow?1:0, HasPrevIsotope?1:0),con);
                Insert.ExecuteNonQuery();

                if (sw != null ){
                    sw.WriteLine("Target #{4}: {0}\tFull name: {10}\t{1} Da.\t {2} min.\t From {6} to {7} min \t{8} C13 Isotopes \t{9} N15 Isotopes \tExtern Apex - {3}\t HasPrevIsotope - {5}",
                        Target.Name,Target.MZ,Target.RT,MainApexOutsideRtWindow,Target.ID,HasPrevIsotope,Target.RTMin,Target.RTMax,Target.C13toCheck,Target.N15toCheck,Target.Desc);
                    sw.WriteLine("Isotope\tTotal\tTotalInside\tMeanMass\tMeanRatio\tFullCorrelation\tCorrelationTo\tApexCount\tPPMError\t"+
                        "MinRT\tMaxRT\tStartRT\tStartIntensity\tStartMass\tApexRT\tApexIntensity\tApexMass\t"+
                        "MZDeviationE\tMZDeviationRT\tRatioDeviationE\tRatioDeviationRT\t\tPeakCorrelation\tPeakTotal\tPeakRatio\tPeakMeanMass");
                }

                if ( Minus13CTrace != null)
                    Minus13CTrace.SaveDB(con, this);
                if ( Minus15NTrace != null)
                    Minus15NTrace.SaveDB(con, this);
                //combine and sort list
                List<LCTrace> Traces = new List<LCTrace>();
                for (int C13 = 0 ; C13 < PureIsotopes.GetLength(0) ; C13++){
                    for (int N15 = 0 ; N15 < PureIsotopes.GetLength(1) ; N15++){
                        if (PureIsotopes[C13,N15] != null){
                            Traces.Add(PureIsotopes[C13, N15]);
                        }
                    }
                }
                Traces.AddRange(MixedIsotopes);
                Traces.Sort(new LCTrace.byMass());

                for (int i = 0 ; i < Traces.Count ; i++){
                    Traces[i].SaveDB(con, this);
                    if (sw != null){
                        sw.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t"+
                        "{9}\t{10}\t{11}\t{12}\t{13}\t{14}\t{15}\t{16}\t"+
                        "{17}\t{18}\t{19}\t{20}\t\t{21}\t{22}\t{23}\t{24}",
                        Traces[i].Attribution, Traces[i].TotalIntensity, Traces[i].InnerIntensity(this), Traces[i].MeanMass, Traces[i].MeanRatioToMono, Traces[i].FullCorrelation, Traces[i].CorrelationTo,Traces[i].ApexCount(),Program.PPMError(Traces[i].MeanMass,Target,Traces[i].Attribution),
                        Traces[i].Group.Points[0].RT,Traces[i].Group.Points[Traces[i].Group.Points.Count-1].RT,Traces[i].StartPoint!=null?Traces[i].StartPoint.RT:0.0,Traces[i].StartPoint!=null?Traces[i].StartPoint.Intensity:0.0,Traces[i].StartPoint!=null?Traces[i].StartPoint.Mass:0.0,Traces[i].Apex.RT,Traces[i].Apex.Intensity,Traces[i].Apex.Mass,
                        Traces[i].MZDevMaxE,Traces[i].MaxMZDevRT,Traces[i].RatioDevMaxE,Traces[i].RatioMaxDevRT, Traces[i].PeakCorrelation, Traces[i].PeakTotal, Traces[i].PeakRatio, Traces[i].PeakMeanMass);
                    }
                }
                TPeaks.DBSave(con, this);
            }
            //File output 
        }

        public void ApplyPeak(Peak P){
            MainTrace.ApplyPeak(P, MainTrace);
            if (Minus15NTrace != null) Minus15NTrace.ApplyPeak(P, MainTrace);
            if (Minus13CTrace != null) Minus13CTrace.ApplyPeak(P, MainTrace);
            foreach(LCTrace Trace in PureIsotopes){
                if (Trace != null) 
                    Trace.ApplyPeak(P, MainTrace);
            }
            foreach(LCTrace Trace in MixedIsotopes){
                if (Trace != null) 
                    Trace.ApplyPeak(P, MainTrace);
            }
        }
    }
}