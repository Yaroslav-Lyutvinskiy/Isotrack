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

        //in full range of all traces
        public double IntensityCorelation(LCTrace Trace, bool Full = false, Peak P = null){
            //sampling 
            //that is inefficient in terms of memory however that's simplier
            List<double> ThisInts = new List<double>();
            List<double> TraceInts = new List<double>();
            int aCounter = 0, bCounter = P==null?0:P.LeftIndex;
            int aEnd = this.Group.Points.Count;
            int bEnd = P==null?Trace.Group.Points.Count:P.RightIndex+1;
            //Restrict to peak area
            if (P != null){
                while (aCounter<this.Group.Points.Count && this.Group.Points[aCounter].Scan < Trace.Group.Points[P.LeftIndex].Scan) aCounter++;
                while (aEnd>0 && this.Group.Points[aEnd - 1].Scan > Trace.Group.Points[P.RightIndex].Scan ) aEnd--;
            }
            while(aCounter < aEnd || bCounter < bEnd){
                if( (bCounter == Trace.Group.Points.Count && aCounter < this.Group.Points.Count )||     //ending Group.Points  
                    (aCounter < this.Group.Points.Count && bCounter < Trace.Group.Points.Count && 
                    this.Group.Points[aCounter].Scan < Trace.Group.Points[bCounter].Scan)){             //starting Group.Points
                    if (Full){
                        ThisInts.Add(this.Group.Points[aCounter].Intensity);
                        TraceInts.Add(0.0);
                    }
                    aCounter++;
                    continue;
                }
                if( (aCounter == this.Group.Points.Count && bCounter < Trace.Group.Points.Count )|| //ending Group.Points 
                    (aCounter < this.Group.Points.Count && bCounter < Trace.Group.Points.Count && 
                    this.Group.Points[aCounter].Scan > Trace.Group.Points[bCounter].Scan) ){        //starting Group.Points
                    if (Full){
                        ThisInts.Add(0.0);
                        TraceInts.Add(Trace.Group.Points[bCounter].Intensity);
                    }
                    bCounter++;
                    continue;
                }
                if (this.Group.Points[aCounter].Scan == Trace.Group.Points[bCounter].Scan){ //co-existing part
                    ThisInts.Add(this.Group.Points[aCounter].Intensity);
                    TraceInts.Add(Trace.Group.Points[bCounter].Intensity);
                    aCounter++; bCounter++;
                }
            }
            //means
            double ThisMean = 0.0, TraceMean = 0.0;
            //shift of starts
            for(int i = 0 ; i < ThisInts.Count; i++){
                ThisMean += ThisInts[i];
                TraceMean += TraceInts[i];
            }
            ThisMean = ThisMean / (double)ThisInts.Count;
            TraceMean = TraceMean / (double)ThisInts.Count;
            //Summs
            double Covariance = 0.0;
            double SigmaA = 0.0;
            double SigmaB = 0.0;
            for(int i = 0 ; i < ThisInts.Count; i++){
                Covariance += (ThisInts[i] - ThisMean) * (TraceInts[i] - TraceMean);
                SigmaA += (ThisInts[i] - ThisMean) * (ThisInts[i] - ThisMean);
                SigmaB += (TraceInts[i] - TraceMean) * (TraceInts[i] - TraceMean);
            }
            if (SigmaA == 0.0 || SigmaB == 0.0) return 0.0;
            return Covariance / Math.Sqrt(SigmaA * SigmaB);
        }

        public double  MZDeviation(out double DivRT){
            int OneSideCount = 1;
            int MaxOneSide = 0;
            int IndexMaxOneSide = 0;
            int Sign = Group.Points[0].Mass > MeanMass ? 1 : -1;
            for (int i = 1 ; i < Group.Points.Count ; i++){
                int NextSign = Group.Points[i].Mass > MeanMass ? 1 : -1;
                if (Sign*NextSign < 0 ) {
                    OneSideCount = 1;
                }else{
                    OneSideCount++;
                }
                if (OneSideCount > MaxOneSide) {
                    MaxOneSide = OneSideCount;
                    IndexMaxOneSide = i;
                }
                Sign = NextSign;
            }
            double MaxE = Math.Pow(0.5, MaxOneSide) * (Group.Points.Count - MaxOneSide);
            DivRT = Group.Points[IndexMaxOneSide - MaxOneSide / 2].RT;
            return MaxE;
        }

        public double RatioDeviation(LCTrace Trace, out double DivRT, out double MeanRatio){
            //Mean Ratio - for overlapping Group.Points
            int aCounter = 0, bCounter = 0;
            double aSum = 0.0, bSum = 0.0;
            while(aCounter < this.Group.Points.Count && bCounter < Trace.Group.Points.Count){
                if (this.Group.Points[aCounter].Scan == Trace.Group.Points[bCounter].Scan){ //co-existing part
                    aSum += this.Group.Points[aCounter].Intensity;
                    bSum += Trace.Group.Points[bCounter].Intensity;
                    aCounter++; bCounter++; continue;
                }
                if( this.Group.Points[aCounter].Scan < Trace.Group.Points[bCounter].Scan){  //starting Group.Points
                    aCounter++;
                    continue;
                }
                if( this.Group.Points[aCounter].Scan > Trace.Group.Points[bCounter].Scan ){ //starting Group.Points
                    bCounter++;
                }
            }
            int Dif = Math.Min(aCounter, bCounter);
            aCounter = aCounter - Dif;
            bCounter = bCounter - Dif;
            if (bSum > 0.0){
                MeanRatio = aSum / bSum;
            }else{
                MeanRatio = 0.0;
            }
            int OneSideCount = 1;
            int MaxOneSide = 0;
            int IndexMaxOneSide = 0;
            int Sign = 1; 

            while(aCounter < this.Group.Points.Count && bCounter < Trace.Group.Points.Count){
                int NextSign = Group.Points[aCounter].Intensity/Trace.Group.Points[bCounter].Intensity > MeanRatio ? 1 : -1;
                if (aCounter == 0 || bCounter == 0){
                    Sign = NextSign;
                    OneSideCount = 1;
                    aCounter++; bCounter++;
                    continue;
                }
                if (Sign*NextSign < 0 ) {
                    OneSideCount = 1;
                }else{
                    OneSideCount++;
                }
                if (OneSideCount > MaxOneSide) {
                    MaxOneSide = OneSideCount;
                    IndexMaxOneSide = aCounter;
                }
                Sign = NextSign;
                aCounter++; bCounter++;
            }
            double MaxE = Math.Pow(0.5, MaxOneSide) * (Group.Points.Count - MaxOneSide);
            DivRT = Group.Points[IndexMaxOneSide - MaxOneSide / 2].RT;
            return MaxE;
        }

        public int ApexCount(){
            List<int> Apexes = new List<int>();
            for (int i = 1 ; i < Group.Points.Count-1 ; i++){
                if ( Group.Points[i].Intensity > Group.Points[i-1].Intensity && Group.Points[i].Intensity > Group.Points[i+1].Intensity){
                    Apexes.Add(i);
                }
            }
            for ( int i = Apexes.Count-1 ; i >= 0 ; i--){
                double MinInt = Group.Points[Apexes[i]].Intensity;
                bool Flag = false;
                //look for bigger point, register lowest boint
                for (int j = Apexes[i]-1; j>0 ; j--){
                    if (Group.Points[j].Intensity<MinInt){
                        MinInt = Group.Points[j].Intensity;
                    }
                    if(Group.Points[j].Intensity > Group.Points[Apexes[i]].Intensity){
                        if ( Group.Points[Apexes[i]].RT - Group.Points[j].RT < 0.4 || MinInt>Group.Points[Apexes[i]].Intensity*0.5){
                            Apexes.RemoveAt(i);
                            Flag = true;
                            break;
                        }
                    }
                }
                if (Flag) continue;
                MinInt = Group.Points[Apexes[i]].Intensity;
                for (int j = Apexes[i]+1; j<Group.Points.Count ; j++){
                    if (Group.Points[j].Intensity<MinInt){
                        MinInt = Group.Points[j].Intensity;
                    }
                    if(Group.Points[j].Intensity > Group.Points[Apexes[i]].Intensity){
                        if ( Group.Points[j].RT - Group.Points[Apexes[i]].RT < 0.4 || MinInt>Group.Points[Apexes[i]].Intensity*0.5){
                            Apexes.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
            return Apexes.Count;
        }

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
                MZDevMaxE,MaxMZDevRT,RatioDevMaxE,RatioMaxDevRT,ApexCount(),F.ID,InnerIntensity(F),Program.PPMError(MeanMass,F.Target,Attribution), PeakCorrelation, PeakTotal, PeakRatio, PeakMeanMass,LCMSGroup.Global.IndexOf(Group)+LCMSGroup.GroupBase),con);
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
            PeakCorrelation = IntensityCorelation(Trace, true, P);
            //!!YL this is written in non-effective way it has O(n2) running time where IntensityCorelation has O(n)
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

        //Isotope attribution
        //place for flags 
        //Correlartions
        //Deviations
        //Apexes 
        //Models
    }
}