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
        static string Task;

        static public double MassError; //in ppm - there can be 3 kinds of errors - general error, 
                                            //intensity dependent scan-to-scan error, local error inside of one feature
                                            // Instrument Resolution can also be important as minimal peak distance corresponds to resolution
        static public double RTError; //in minutes
        static public int C13toCheck;
        static public int N15toCheck;
        static public double MinRTWidth;
        static public bool Simple = false; //can be transferred to parameters
        static public bool BackSP = false;
        static public bool C13Only;

        static public bool RawLabel = true; //seems always to be true 
        static public bool StickMode = true;

        static public double PeakMinWidth;
        static public double PeakMaxWidth;
        static public double PeakMinIntensity;
        static public double PeakbaselineRatio;

        static public double DataThres;

        //??have to be setup to false for Standarts and Targeted and true for Untargeted 
        static bool CountSecondaries = false;
        static bool ArbTrace = false;
        static bool LowSignals = false;
        static public int ZeroScans = 0;

        static public bool SaveProfile;
        static bool WriteTexts;
        static public bool IgnoreRT;


        //Constants
        static public double C13Shift = 1.003354838;
        static public double N15Shift = 0.997035;

        //Processing switches

        static public List<Target> Targets;
        static public FileService RawFileService;
        static public DBInterface DBInt;

        static int Main(string[] args){
            //Data Sources
            //Args[0] - database
            //args[1] - Raw File Name
            //Args[2] - ID for file
            try{

                if (args[0] == "null") {
                    Caching(args[1],Convert.ToDouble(args[2]));
                    return 0;
                }

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
                        Targets = Target.ReadTargets(DBInt.con); 
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

                StreamWriter sw = null;
                if (WriteTexts){
                    string OutFileName = Path.ChangeExtension(args[1],"txt");
                    sw = new StreamWriter(OutFileName);
                }

                //filter targets to raw file mode 
                for (int t = Targets.Count - 1; t >= 0; t--){
                    if (Targets[t].Mode != 0 && Targets[t].Mode != RawFileService.RawFile.Mode){
                        Targets.RemoveAt(t);
                    }
                }


                Target T = null;
                MZData P = null;
                double MaxI = 0;

                switch (Task){
                    case "Untargeted Analysis":{
                        CountSecondaries = true;
                        Console.WriteLine("Information: Building data map");
                        RawFileService.BuildDataMap();
                        P = RawFileService.BiggestUnused();
                        T = Target.TargetFromPoint(P,0);
                        MaxI = P.Intensity;
                        Targets = null;
                        break;
                    } 
                    case "Standards Refine":{
                        Console.WriteLine("Information: Building data map");
                        RawFileService.BuildDataMap();
                        Console.WriteLine("Information: Excluding not-target masses... ");
                        ExcludeNonTargets();
                        //check as used all non-targeted mass
                        Targets = null;
                        P = RawFileService.BiggestUnused();
                        if (P!=null){
                            T = Target.TargetFromPoint(P,0);
                            MaxI = P.Intensity;
                        }
                        break;
                    }
                    case "Targeted Analysis": {
                        T = Targets[0];
                        break;
                    }
                }

                Console.WriteLine("Information: Targets processing...", args[1]);

                List<Feature> ReadyFeatures = new List<Feature>();
                int i = 0;
                int ICount = 1;
                while (T != null ){
                    if (Task == "Targeted Analysis") {
                        if (i / 20 > ICount){
                            Console.WriteLine("{0}%...", (100*i)/Targets.Count);
                            ICount++;
                        }
                    }else{
                        if ((int)Math.Pow(100,(Math.Log10(MaxI/DataThres)-Math.Log10(P.Intensity/DataThres))/(Math.Log10(MaxI/DataThres))) > ICount ){
                            Console.WriteLine("Information: Targets - {0}; Intensity - {1} ",i,P.Intensity);
                            Console.WriteLine("{0}%...",(int)Math.Pow(100,(Math.Log10(MaxI/DataThres)-Math.Log10(P.Intensity/DataThres))/(Math.Log10(MaxI/DataThres))));
                            ICount++;
                        }
                    }
                    Feature F = FeatureForTarget(T,P);
                    if (F!=null) ReadyFeatures.Add(F);
                    i++;
                    if (Targets!=null){//Targeted analysis 
                        T = i<Targets.Count?Targets[i]:null;
                    }else{//Untargeted analysis 
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
                }

                RepeatCount = 0; 
                while(RepeatCount < 10000){
                    try{
                        DBInt.tr = DBInt.con.BeginTransaction();
                        DBInt.SaveFile(args[1], FileID,RawFileService.RawFile.Mode);
                        LCMSGroup.GroupBase = DBInt.GetGroupBase();
                        foreach (Feature F in ReadyFeatures){
                            if (F != null && DBInt != null){
                                F.Write(sw, DBInt.con, FileID);
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
                if (sw != null){
                    sw.Close();
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

        static void ExcludeNonTargets(){
            for(int i = 0 ; i >=0 && i < RawFileService.RawFile.RawSpectra.Length-1 ; i=RawFileService.RawFile.IndexDir[i]){
                for(int j = 0 ; j < RawFileService.RawFile.RawSpectra[i].Data.Length; j++){
                    RawFileService.RawFile.RawSpectra[i].Data[j].Counted = true;
                }
            }
            Targets.Sort(new Target.byMZ());
            for(int i = 0 ; i >=0 && i < RawFileService.RawFile.RawSpectra.Length-1 ; i=RawFileService.RawFile.IndexDir[i]){
                int Current = 0;
                RawData Sp = RawFileService.RawFile.RawSpectra[i];
                for (int j = 0 ; j < Targets.Count ; j++){
                    for (int k=Current ; k < Sp.Data.Length ; k++){
                        double MassDiff = Math.Abs(((Targets[j].MZ-Sp.Data[k].Mass)/(Targets[j].MZ/2.0+Sp.Data[k].Mass/2.0))*1000000.0);
                        if (MassDiff < MassError) {
                            Current = k;
                            break;
                        }
                    }
                    for (int k=Current ; k < Sp.Data.Length ; k++){
                        double MassDiff = Math.Abs(((Targets[j].MZ-Sp.Data[k].Mass)/(Targets[j].MZ/2.0+Sp.Data[k].Mass/2.0))*1000000.0);
                        if (MassDiff > MassError) break;
                        Sp.Data[k].Counted = false;
                    }
                }
            }
        }

        static void CheckPointforTargets(MZData D){
            D.Counted = true;
            foreach(Target T in Targets){
                if (MassDiff(D.Mass,T.MZ)<MassError && 
                    (T.RT==0 || Program.IgnoreRT || 
                        (RawFileService.RawFile.RawSpectra[D.Scan].RT <T.RTMax && 
                        RawFileService.RawFile.RawSpectra[D.Scan].RT > T.RTMin))){
                    D.Counted = false;
                    return;
                }
            }
        }

        static double MassDiff(double mz1, double mz2){
            return (Math.Abs(mz1 - mz2) * 1000000.0) / ((mz1 + mz2) / 2.0);
        }

        static Feature FeatureForTarget(Target T, MZData P = null){
            Feature F = new Feature();
            T.Feature = F;
            F.Target = T;

            //Monoisotopic trace
            MZData Apex = null;
            int MainTraceC13 = 0;
            int MainTraceN15 = 0;
            if (P==null){//Targeted analysis 
                if(!ArbTrace){
                    Apex = RawFileService.SearchForApex(T.MZ, T.RT, T.RTMin, T.RTMax);
                }else{
                    MZData IApex = null;
                    double MaxInt = 0.0;
                    for (int C13 = 0 ; C13 <= T.C13toCheck ; C13++){
                        for (int N15 = 0 ; N15 <= T.N15toCheck ; N15++){
                            IApex = RawFileService.SearchForApex(T.MZ+(C13Shift*(double)C13+N15Shift*(double)N15), T.RT, T.RTMin, T.RTMax);
                            if (IApex == null) continue;
                            if (IApex.Intensity>MaxInt){
                                MainTraceC13 = C13;
                                MainTraceN15 = N15;
                                MaxInt = IApex.Intensity;
                                Apex = IApex;
                            }
                        }
                    }
                    //if (MainTraceC13 > 0) Console.WriteLine("");
                }
            }else{//Untargeted analysis 
                Apex = P;
            }
            if (Apex == null) return null;
            if (!Simple){
                F.MainTrace = LCTrace.CreateTrace(RawFileService.GroupFromPoint(Apex),Apex);//main for untargeted analysis 
            }else{
                if(Program.BackSP) {
                    F.MainTrace = LCTrace.CreateTrace(RawFileService.GroupFromArea(T.FullRTMin, T.FullRTMax, Apex.Mass), null);
                } else {
                    F.MainTrace = LCTrace.CreateTrace(RawFileService.GroupFromArea(T.RTMin, T.RTMax, T.MZ), null);
                    Apex.Mass = T.MZ;
                }
            }
            if ( LowSignals &&(F.MainTrace == null || 
                (F.MainTrace.Group.Points[0].RT>T.RTMin && 
                F.MainTrace.Group.Points[F.MainTrace.Group.Points.Count-1].RT<T.RTMax))) {
                F.MainTrace = LCTrace.CreateTrace(RawFileService.GroupFromArea(T.RTMin, T.RTMax, T.MZ),Apex);
            }

            //? gapped for main trace
            if (F.MainTrace == null) return null;
            F.MainTrace.Group.SetCounted(true);
            F.MainTrace.Attribution = "C"+MainTraceC13.ToString()+"N"+MainTraceN15.ToString();

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

            //Potential previous peak at 13C
            //Principally previous peak has to be searched with respect to trace mzs (since it can fluctuate for more then MaqssError)
            //and resolution of mass spectrometer since previous isotope peak can only shift target peak inside of resolution window 
            //for more then MassError but less then Resolution mass (otherwize it will produce one more peak before target)
            //principally the same is applicable for isotopic traces
            MZData Minus13CApex = RawFileService.SearchForApex(T.MZ-C13Shift, 0.0, RTStart, RTEnd);
            if (Minus13CApex != null && !Simple && MainTraceC13 == 0 && MainTraceN15 == 0){
                LCTrace Minus13CTrace =  LCTrace.CreateTrace(RawFileService.GroupFromPoint(Minus13CApex,false),Minus13CApex);
                if (Minus13CTrace!=null && Minus13CTrace.TotalIntensity*100.0 > F.MainTrace.TotalIntensity){
                    Minus13CTrace.Attribution = "C-1N0";
                    F.Minus13CTrace = Minus13CTrace;
                }
            }
            //Potential previous peak at 15N
            MZData Minus15NApex = RawFileService.SearchForApex(T.MZ-N15Shift, 0.0, RTStart, RTEnd);
            if (Minus15NApex != null && !Simple && !C13Only && MainTraceC13 == 0 && MainTraceN15 == 0){
                LCTrace Minus15NTrace =  LCTrace.CreateTrace(RawFileService.GroupFromPoint(Minus15NApex,false),Minus15NApex);
                if (Minus15NTrace != null && Minus15NTrace.TotalIntensity*100.0 > F.MainTrace.TotalIntensity){
                    Minus15NTrace.Attribution = "C0N-1";
                    F.Minus15NTrace = Minus15NTrace;
                }
            }
            F.HasPrevIsotope = (F.Minus13CTrace != null || F.Minus15NTrace != null);
            //End of preisotopes 

            //Checks for isotopic peaks
            F.PureIsotopes = new LCTrace[T.C13toCheck + 1, T.N15toCheck + 1];
            F.MixedIsotopes = new List<LCTrace>();
            F.PureIsotopes[MainTraceC13,MainTraceN15] = F.MainTrace; 
            if(F.MainTrace.Attribution == null) {
                Console.WriteLine("Wow!");
            }
            //Pure isotopes
            for (int C13 = 0 ; C13 <= T.C13toCheck ; C13++){
                for (int N15 = 0 ; N15 <= T.N15toCheck ; N15++){
                    if (C13==MainTraceC13 && N15 == MainTraceN15) {//main trace case 
                        continue; 
                    }
                    double TargetMass = Apex.Mass + (C13Shift * (double)(C13-MainTraceC13) + N15Shift * (double)(N15-MainTraceN15))/T.Charge;
                    if (!Simple){
                        MZData D = RawFileService.RawFile.RawSpectra[Apex.Scan].FindNearestPeak(TargetMass, MassError);
                        if (D.Mass > 0.0){
                            F.PureIsotopes[C13,N15] = LCTrace.CreateTrace(RawFileService.GroupFromPoint(D,CountSecondaries),D);
                        }else{
                            F.PureIsotopes[C13, N15] = null;
                        }
                        //Gapped trace
                        if ( LowSignals &&(F.PureIsotopes[C13, N15] == null || 
                            (F.PureIsotopes[C13, N15].Group.Points[0].RT>T.RTMin && 
                            F.PureIsotopes[C13, N15].Group.Points[F.PureIsotopes[C13, N15].Group.Points.Count-1].RT<T.RTMax))) {

                            F.PureIsotopes[C13, N15] = LCTrace.CreateTrace(RawFileService.GroupFromArea(T.RTMin, T.RTMax, TargetMass),D.Mass==0.0?null:D);

                        }
                    }else{
                        if(Program.BackSP) {
                            F.PureIsotopes[C13, N15] = LCTrace.CreateTrace(RawFileService.GroupFromArea(T.FullRTMin, T.FullRTMax, TargetMass), null);
                        } else {
                            F.PureIsotopes[C13, N15]= LCTrace.CreateTrace(RawFileService.GroupFromArea(T.RTMin, T.RTMax, TargetMass), null);
                        }
                    }
                    if (F.PureIsotopes[C13,N15] != null)
                        F.PureIsotopes[C13, N15].Attribution = String.Format("C{0}N{1}", C13, N15);
                }
            }

            //mixed isotopes
            for (int C13 = 0 ; C13 < T.C13toCheck ; C13++){
                for (int N15 = 0 ; N15 < T.N15toCheck ; N15++){
                    if (F.PureIsotopes[C13+1,N15] == null && F.PureIsotopes[C13,N15+1] == null){
                        //Search boundaries
                        double MZStart = Apex.Mass + (C13Shift * (double)(C13) + N15Shift * (double)(N15+1))/T.Charge;
                        MZStart += (MZStart * MassError) / 1000000.0;
                        double MZEnd = Apex.Mass + (C13Shift * (double)(C13+1) + N15Shift * (double)(N15))/T.Charge;
                        MZEnd -= (MZEnd * MassError) / 1000000.0;
                        if (MZStart >= MZEnd) { 
                            continue;
                        }
                        double MZMean = (MZStart + MZEnd) / 2;
                        double Interval = ((MZMean - MZStart) / MZMean) * 1000000.0; //in ppms
                        LCTrace Trace = null;
                        if (!Simple){
                            MZData D = RawFileService.RawFile.RawSpectra[Apex.Scan].FindBiggestPeak(MZMean, Interval);
                            if (D.Mass > 0.0){
                                Trace = LCTrace.CreateTrace(RawFileService.GroupFromPoint(D,CountSecondaries),D);
                            }
                        }
                        if (Trace != null) { 
                            Trace.Attribution = String.Format("C{0}N{1}/C{2}N{3}-between", C13 + 1, N15, C13, N15 + 1);
                            F.MixedIsotopes.Add(Trace);
                        }
                    }
                }
            }
            //factually mixed isotopes from first matrix
            List<LCTrace> NominMass = new List<LCTrace>();
            for (int Shift = 1 ; Shift <= T.C13toCheck+T.N15toCheck ; Shift++){
                for (int C13 = 0 ; C13 <= T.C13toCheck ; C13++){
                    for (int N15 = 0 ; N15 <= T.N15toCheck ; N15++){
                        if (C13+N15 == Shift && F.PureIsotopes[C13,N15] != null){
                            NominMass.Add(F.PureIsotopes[C13,N15]);
                        }
                    }
                }
                    
                for(int j = NominMass.Count-2 ; j >= 0 ; j--){
                    if (NominMass[j].StartPoint.Mass == NominMass[j+1].StartPoint.Mass){
                        //clear first matrix
                        int N = Convert.ToInt32(NominMass[j].Attribution.Substring(NominMass[j].Attribution.LastIndexOf("N")+1));
                        F.PureIsotopes[Shift - N, N] = null;
                        if (!NominMass[j+1].Attribution.Contains("/")){
                            N = Convert.ToInt32(NominMass[j+1].Attribution.Substring(NominMass[j+1].Attribution.LastIndexOf("N")+1));
                            F.PureIsotopes[Shift - N, N] = null;
                        }
                        NominMass[j].Attribution = NominMass[j+1].Attribution + "/" + NominMass[j].Attribution;
                        NominMass.RemoveAt(j + 1);
                    }
                }
                for ( int j = 0 ; j < NominMass.Count ; j++){
                    if (NominMass[j].Attribution.Contains("/")){
                        NominMass[j].Attribution += "- unresolved";
                        F.MixedIsotopes.Add(NominMass[j]);
                    }
                }
                NominMass.Clear();
            }

            //??Outer isotopes - in case if something mixed isotope can be shifted out of interval N15..C13 then we need to 
            //search peaks outside this window defined by resolution of Mass spec

            //Correlations and deviations
            for (int C13 = 0 ; C13 <= T.C13toCheck ; C13++){
                for (int N15 = 0 ; N15 <= T.N15toCheck ; N15++){
                    if (F.PureIsotopes[C13,N15] != null){
                        F.PureIsotopes[C13, N15].FullCorrelation = F.PureIsotopes[C13, N15].IntensityCorelation(F.MainTrace, true);
                        F.PureIsotopes[C13, N15].CorrelationFrom = F.MainTrace.IntensityCorelation(F.PureIsotopes[C13, N15]);
                        F.PureIsotopes[C13, N15].CorrelationTo   = F.PureIsotopes[C13, N15].IntensityCorelation(F.MainTrace, false);
                        F.PureIsotopes[C13, N15].MZDevMaxE   = F.PureIsotopes[C13, N15].MZDeviation(out F.PureIsotopes[C13, N15].MaxMZDevRT);
                        F.PureIsotopes[C13, N15].RatioDevMaxE   = F.PureIsotopes[C13, N15].RatioDeviation(F.MainTrace, 
                            out F.PureIsotopes[C13, N15].RatioMaxDevRT, 
                            out F.PureIsotopes[C13, N15].MeanRatioToMono);
                        F.PureIsotopes[C13, N15].ApexNumber = F.PureIsotopes[C13, N15].ApexCount();
                    }
                }
            }
            for (int j = 0 ; j < F.MixedIsotopes.Count ; j++){
                F.MixedIsotopes[j].FullCorrelation = F.MixedIsotopes[j].IntensityCorelation(F.MainTrace, true);
                F.MixedIsotopes[j].CorrelationFrom = F.MainTrace.IntensityCorelation(F.MixedIsotopes[j]);
                F.MixedIsotopes[j].CorrelationTo   = F.MixedIsotopes[j].IntensityCorelation(F.MainTrace, false);
                F.MixedIsotopes[j].MZDevMaxE   = F.MixedIsotopes[j].MZDeviation(out F.MixedIsotopes[j].MaxMZDevRT);
                F.MixedIsotopes[j].RatioDevMaxE   = F.MixedIsotopes[j].RatioDeviation(F.MainTrace, 
                    out F.MixedIsotopes[j].RatioMaxDevRT, 
                    out F.MixedIsotopes[j].MeanRatioToMono);
                F.MixedIsotopes[j].ApexNumber = F.MixedIsotopes[j].ApexCount();
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
            Task = DBInt.GetParameter("Task");
            
            MassError = Convert.ToDouble(DBInt.GetParameter("Mass_Accuracy"));
            RTError = Convert.ToDouble(DBInt.GetParameter("RTError"));
            ZeroScans = Convert.ToInt32(DBInt.GetParameter("Gap_Scans_Max"));

            C13toCheck = Convert.ToInt32(DBInt.GetParameter("C13_to_Check"));
            N15toCheck = Convert.ToInt32(DBInt.GetParameter("N15_to_Check")); 
            MinRTWidth = Convert.ToDouble(DBInt.GetParameter("MinRTWidth"));
            Simple = false;
            C13Only = Convert.ToBoolean(DBInt.GetParameter("C13Only"));

            RawLabel = true;
            StickMode = !Convert.ToBoolean(DBInt.GetParameter("Profile"));
            ArbTrace = (Task == "Targeted Analysis") ? Convert.ToBoolean(DBInt.GetParameter("ArbMainTrace")) : false ;
            LowSignals = (Task == "Targeted Analysis") ? Convert.ToBoolean(DBInt.GetParameter("Low_signals")) : false ;


            PeakMinWidth = Convert.ToDouble(DBInt.GetParameter("PeakMinWidth"));
            PeakMaxWidth = Convert.ToDouble(DBInt.GetParameter("PeakMaxWidth"));
            PeakMinIntensity = Convert.ToDouble(DBInt.GetParameter("MinIntensity"));
            PeakbaselineRatio = Convert.ToDouble(DBInt.GetParameter("BaselineRatio"));

            DataThres = Convert.ToDouble(DBInt.GetParameter("IntensityThreshold"));
            IgnoreRT = Convert.ToBoolean(DBInt.GetParameter("IgnoreRT"));

            WriteTexts = Convert.ToBoolean(DBInt.GetParameter("WriteTexts"));
            SaveProfile = Convert.ToBoolean(DBInt.GetParameter("SaveProfile"));

        }

        //for second pass
        static int Main1(string[] args){
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
                        Targets = Target.ReadTargets(DBInt.con); 
                        break;
                    }catch(System.Data.SQLite.SQLiteException sqle){
                        if (sqle.ErrorCode == System.Data.SQLite.SQLiteErrorCode.Busy && RepeatCount<10){
                            Console.WriteLine("Warning: {0}",sqle.Message);
                            System.Threading.Thread.Sleep(10000);
                            RepeatCount++;
                        }else{
                            throw sqle;
                        }
                    }
                }

                RawFileService = new FileService(args[1]);
                Console.WriteLine("Information: Building data map");
                RawFileService.BuildDataMap();
                LCMSGroup.LoadGroups(DBInt.con, RawFileService.DataMap, FileID);
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

        // raw file caching
        static int Caching(string FileName, double DataThres) {
            // args[0] - filename
            // args[1] - threshold

            Console.ReadLine();
            Console.WriteLine("Information: Loading...");

            RawFileService = new FileService(FileName);
            BinaryWriter sw = null;
            string OutFileName = Path.ChangeExtension(FileName,"rch");
            FileStream fs = new FileStream(OutFileName,FileMode.Create,FileAccess.ReadWrite);
            sw = new BinaryWriter(fs);
            //signature
            sw.Write("RCH0");
            Console.WriteLine("Information: Building data map");
            RawFileService.BuildMZMap();
            Console.WriteLine("Information: Saving {0} points",RawFileService.DataMap.Count);
            //Записать сканы
            //число сканов
            int ScanCount = 1;
            int i;
            for(i = 0 ; RawFileService.RawFile.IndexDir[i] == 0 ; i++);
            while (RawFileService.RawFile.IndexDir[i] != -1) {
                i = RawFileService.RawFile.IndexDir[i];
                ScanCount++;
            }
            ScanCount--;
            sw.Write(ScanCount);
            //пары [scan,RT]
            for(i = 0 ; RawFileService.RawFile.IndexDir[i] == 0 ; i++);
            do {
                sw.Write(i);
                sw.Write((float)RawFileService.RawFile.RawSpectra[i].RT);
                i = RawFileService.RawFile.IndexDir[i];
            }while(RawFileService.RawFile.IndexDir[i]!=-1) ;
            //записать индекс масс (64кб на страницу, 16 байт на точку, 4096 точек на страницу)
            //число страниц
            int Len = RawFileService.DataMap.Count;
            if (Len%4096 == 0) {
                Len = Len / 4096;
            } else {
                Len = Len / 4096 + 1;
            }
            sw.Write(Len);
            //записать индекс масс
            for ( i = 0 ; i < Len ; i++) {
                sw.Write(RawFileService.DataMap[i*4096].Mass);
            }
            //записать данные
            for(i = 0 ; i < RawFileService.DataMap.Count ; i++) {
                sw.Write(RawFileService.DataMap[i].Mass);
                sw.Write((float)RawFileService.DataMap[i].Intensity);
                sw.Write((float)RawFileService.DataMap[i].Scan);
                if (i % (RawFileService.DataMap.Count / 100) == 0) {
                    if(i != 0) {
                        Console.WriteLine("{0}%...", (i*100)/RawFileService.DataMap.Count);
                    } else {
                        Console.WriteLine("0%...");
                    }
                }
            }
            sw.Close();
            Console.WriteLine("Completed");
//            Console.ReadLine();
            return 0;
        }

    }
}

