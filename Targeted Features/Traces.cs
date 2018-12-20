using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SQLite;
using RawMSBox;

namespace Targeted_Features
{
    public class LCTrace{
        //public List<LCMSPoint> Group.Points = new List<LCMSPoint>();
        public LCMSGroup Group;

        LCTrace(LCMSGroup Group, MZData Start){
            this.Group = Group;
            this.StartPoint = Start;
        }

        public static LCTrace CreateTrace(LCMSGroup Group, MZData Start){
            if (Group == null){
                return null;
            }
            if (Start == null){
                Start = Group.Points[0];
                foreach(MZData P in Group.Points) {
                    if (P.Intensity > Start.Intensity) {
                        Start = P;
                    }
                }
            }
            return new LCTrace(Group, Start);
        }

        public class byMass:IComparer<LCTrace>{
            public int Compare(LCTrace x, LCTrace y){
                if (x.MeanMass == y.MeanMass) return 0;
                if (x.MeanMass > y.MeanMass) return 1;
                return -1;
            }
        }

        private double TotalIntensity_ = 0.0;
        public double TotalIntensity {
            get {
                if (TotalIntensity_ != 0.0) return TotalIntensity_;
                foreach(MZData P in Group.Points)
                    TotalIntensity_ += P.Intensity*P.TimeCoeff;
                return TotalIntensity_;
            }
        }
        private MZData Apex_;
        public MZData Apex {
            get {
                if (Apex_ != null) return Apex_;
                Apex_ = Group.Points[0];
                foreach(MZData P in Group.Points){
                    if (P.Intensity > Apex_.Intensity){
                        Apex_ = P;
                    }
                }
                return Apex_;
            }
        }

        public double InnerIntensity(Feature F) {
            Double Res = 0.0;
            foreach(MZData P in Group.Points){
                if (P.RT >= F.Target.RTMin && P.RT <= F.Target.RTMax){
                    Res += P.Intensity*P.TimeCoeff;
                }
            }
            return Res;
        }


        public MZData StartPoint = null;
        private double MeanMass_ = 0.0; //weighted average
        public double MeanMass {
            get {
                if (MeanMass_ != 0.0) return MeanMass_;
                double Sum = 0.0;
                foreach ( MZData P in Group.Points){
                    Sum += P.Mass * P.Intensity*P.TimeCoeff;
                }
                MeanMass_ = Sum / TotalIntensity;
                return MeanMass_;
            }
        }

        public string Attribution;
        public static int LastID;
        public int ID;

        public void SaveDB(SQLiteConnection con, Feature F){
            if (LastID == 0){
                SQLiteCommand Select = new SQLiteCommand(
                    "Select max(TraceID) From Traces ",con);
                SQLiteDataReader Reader = Select.ExecuteReader();
                Reader.Read();
                try {
                    LastID = Reader.GetInt32(0)+1;
                }
                catch(Exception){
                    //LastID++;
                }
            }
            LastID++;
            ID = LastID;
            SQLiteCommand Insert = new SQLiteCommand(String.Format(
                "Insert Into Traces (TraceID, onFeatureID, GroupID ,IsotopeAttribution , TotalIntensity, MeanMass, FullCorrelation, CorrelationTo, "+
                "MinRT, MaxRT, StartRT, StartIntensity, StartMass, ApexRT, ApexIntensity, ApexMass, MeanRatio, "+ 
                "MZDeviationE, MZDeviationRT, RatioDeviationE, RatioDeviationRT, ApexCount, InnerIntensity,PPMError, PeakCorrelation, PeakTotal, PeakRatio, PeakMeanMass )"+
                "Values ( {0}, {20}, {27}, \"{1}\", {2}, {3}, {4}, {5}, "+
                "{6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, "+
                "{15}, {16}, {17}, {18}, {19}, {21}, {22}, {23}, {24}, {25}, {26} );",
                LastID, Attribution , TotalIntensity, MeanMass, FullCorrelation, CorrelationTo,
                Group.Points[0].RT,Group.Points[Group.Points.Count-1].RT,StartPoint!=null?StartPoint.RT:0.0,StartPoint!=null?StartPoint.Intensity:0.0,StartPoint!=null?StartPoint.Mass:0.0,Apex.RT,Apex.Intensity,Apex.Mass,MeanRatioToMono,
                MZDevMaxE,MaxMZDevRT,RatioDevMaxE,RatioMaxDevRT,0,F.ID,InnerIntensity(F),Program.PPMError(MeanMass,F.Target,Attribution), PeakCorrelation, PeakTotal, PeakRatio, PeakMeanMass,LCMSGroup.Global.IndexOf(Group)+LCMSGroup.GroupBase),con);
            Insert.ExecuteNonQuery();
        }

        public double FullCorrelation = 0.0;
        public double CorrelationTo = 0.0;
        public double CorrelationFrom = 0.0;
        public double MZDevMaxE = 0.0;
        public double MaxMZDevRT = 0.0;
        public double MeanRatioToMono = 0.0;
        public double RatioDevMaxE = 0.0;
        public double RatioMaxDevRT = 0.0;
        public int ApexNumber = 0;

        public double PeakCorrelation = 0.0;
        public double PeakTotal = 0.0;
        public double PeakRatio = 0.0;
        public double PeakMeanMass = 0.0;

        private MZData PointForScan(int ScanNumber){
            foreach(MZData P in Group.Points){
                if (P.Scan == ScanNumber){
                    return P;
                }
            }
            return null;
        }

        public void ApplyPeak(Peak P, LCTrace Trace){
            MZData thisPoint = null;
            MZData TracePoint = null;
            double MSum = 0.0;
            double ISum = 0.0;
            double aSum = 0.0;
            double bSum = 0.0;
            for (int i = Trace.Group.Points[P.LeftIndex].Scan ; i <= Trace.Group.Points[P.RightIndex].Scan ; i++){
                thisPoint = PointForScan(i);
                TracePoint = Trace.PointForScan(i);
                MSum += thisPoint == null ? 0.0 : thisPoint.Mass * thisPoint.Intensity * thisPoint.TimeCoeff;
                ISum += thisPoint == null ? 0.0 : thisPoint.Intensity;
                aSum += (thisPoint != null && TracePoint != null)?thisPoint.Intensity:0.0;
                bSum += (thisPoint != null && TracePoint != null)?TracePoint.Intensity:0.0;
            }
            PeakTotal = ISum;
            PeakMeanMass = ISum != 0.0 ? MSum/ISum : 0.0;
            PeakRatio = bSum != 0.0 ? aSum/bSum : 0.0;
        }

    }
}