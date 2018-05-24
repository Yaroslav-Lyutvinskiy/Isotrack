using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SQLite;
using RawMSBox;

namespace Targeted_Features
{
    class FileService{

        static public void RepProgress(int Perc){
            Console.WriteLine("{0}%...",Perc);
        }

        public FileBox RawFile;
        public FileService(string FileName){
            RawMSBox.FileBox.RepProgress = RepProgress;
            string Ext = Path.GetExtension(FileName);
            if (Ext == ".raw"){
                RawFile = new RawMSBox.RawFileBox();
            }else{
                RawFile = new RawMSBox.AgilentFileBox();
            }
            RawFile.StickMode = Program.StickMode;
            RawFile.RawLabel = Program.RawLabel;
            RawFile.LoadIndex(FileName);
            RawFile.RTCorrection = true;
            MZData.SetRawFile(RawFile);
            double EndRT=0.0;
            for(int i = RawFile.RawSpectra.Length-1;i>0;i--){
                EndRT= RawFile.RawSpectra[i].RT;
                if (EndRT>0.0)break;
            }
            RawFile.LoadInterval(0.0, EndRT);
        }

        public MZData SearchForApex(double MZ, double RT, double RTStart = 0.0, double RTEnd = 0.0, double MassError = 0.0 ){
            if (MassError == 0.0){
                //MassError should not be attribute of programm
                MassError = Program.MassError;
            }
            if (RTStart == 0.0) {
                RTStart = RT - Program.RTError;
            }
            if (RTEnd == 0.0 ){
                RTEnd = RT + Program.RTError;
            }
            int ScanStart = RawFile.ScanNumFromRT(RTStart);
            int ScanEnd = RawFile.ScanNumFromRT(RTEnd);
            MZData Apex = null;
            for(int i = ScanStart ; i<= ScanEnd ; i++){
                if (RawFile.RawSpectra[i].Data != null){
                    RawData Spec = RawFile.RawSpectra[i];
                    MZData D = RawFile.RawSpectra[i].FindBiggestPeak(MZ, MassError);
                    if (D.Mass != 0.0 && (Apex == null || Apex.Intensity < D.Intensity)){
                        Apex = D;
                    }
                }
            }
            return Apex;
        }

        public LCMSGroup GroupFromPoint(MZData Point, bool Check = true ,double MassError = 0.0){

            if (Point.Group != null) return Point.Group as LCMSGroup;

            LCMSGroup Group = new LCMSGroup();
            if (MassError == 0.0){
                //MassError should not be attribute of programm
                MassError = Program.MassError;
            }

            int Scan = Point.Scan;

            MZData CurrentPoint = Point;

            //MZData RawPoint = RawFile.RawSpectra[Scan].FindNearestPeak(Point.MZ,MassError);
            Point.Counted = Point.Counted || Check;
            Point.Group = Group;
            Group.Points.Add(Point);

            //!!вынести на уровень выше - К созданию трейса
            //Trace.StartPoint = Point;

            //backward pass
            bool Flag = false;
            double LastMass = 0.0;
            do {
                Scan = RawFile.IndexRev[Scan];
                if (CurrentPoint.Mass > 0.0)
                    LastMass = CurrentPoint.Mass;
                //MassError can be adotted to measured normal scan-to-scan jumps 
                MZData Next = RawFile.RawSpectra[Scan].FindNearestPeak(LastMass,MassError);
                //if (Next.Counted) {
                //    for (int i = 0 ; i < Group.Points.Count ; i++){
                //        Group.Points[i].Group = null;
                //    }
                //    return null;
                //}
                if(Next.Group != null)
                    break;
                Next.Counted = Next.Counted || Check;
                Next.Group = Group;
                CurrentPoint = Next;
                //here can be code to support gaps
                //here can be code to check abnormal scan-to-scan jumps
                Group.Points.Insert(0,CurrentPoint);
                if (CurrentPoint.Intensity == 0){
                    Flag = true;
                    int GapScan = Scan;
                    for (int i = 1 ; i <= Program.ZeroScans ; i++){
                        GapScan = RawFile.IndexRev[GapScan];
                        if (GapScan == 0) break;
                        MZData GapNext = RawFile.RawSpectra[GapScan].FindNearestPeak(LastMass,MassError);
                        if (GapNext.Intensity > 0.0) {
                            Flag = false;
                            break;
                        }
                    }
                }
            } while (!Flag && Scan > 0); //or can be tested against certain threshold
            //direct pass
            CurrentPoint = Point;
            Scan = Point.Scan;
            Flag = false;
            do {
                Scan = RawFile.IndexDir[Scan];
                if (CurrentPoint.Mass > 0.0)
                    LastMass = CurrentPoint.Mass;
                if (Scan == -1 ){
                    Group.Points[Group.Points.Count - 1].Intensity = 0.0;
                    break;
                }
                //MassError can be adotted to measured normal scan-to-scan jumps 
                MZData Next = RawFile.RawSpectra[Scan].FindNearestPeak(LastMass,MassError);
                //if (Next.Counted) {
                //    for (int i = 0 ; i < Group.Points.Count ; i++){
                //        Group.Points[i].Group = null;
                //    }
                //    return null;
                //}
                if(Next.Group != null)
                    break;
                Next.Counted = Next.Counted || Check;
                Next.Group = Group;
                CurrentPoint = Next;
                //here can be code to support gaps
                //here can be code to check abnormal scan-to-scan jumps
                Group.Points.Add(CurrentPoint);
                if (CurrentPoint.Intensity == 0){
                    Flag = true;
                    int GapScan = Scan;
                    for (int i = 1 ; i <= Program.ZeroScans ; i++){
                        GapScan = RawFile.IndexDir[GapScan];
                        if (GapScan == -1) break;
                        MZData GapNext = RawFile.RawSpectra[GapScan].FindNearestPeak(LastMass,MassError);
                        if (GapNext.Intensity > 0.0) {
                            Flag = false;
                            break;
                        }
                    }
                }

            } while (!Flag); //or can be tested against certain threshold
            //Check for conditions here (for example min RT or number of scans interval for a trace)
            //check gaps for missed points
            for (int i = 0 ; i < Group.Points.Count ; i++) {
                if (Group.Points[i].Intensity == 0.0) {
                    int FirstPoint = (i - Program.ZeroScans > 0) ? i - Program.ZeroScans : 0;
                    int LastPoint = (i + Program.ZeroScans < Group.Points.Count) ? i + Program.ZeroScans : Group.Points.Count - 1;
                    double MaxMass = 0.0;
                    double MinMass = 1000000.0;
                    for(int j = FirstPoint ; j <= LastPoint ; j++) {
                        if(Group.Points[j].Mass > MaxMass)
                            MaxMass = Group.Points[j].Mass;
                        if (Group.Points[j].Mass < MinMass && Group.Points[j].Mass > 0.0)
                            MinMass = Group.Points[j].Mass;
                    }
                    MZData ForGap = RawFile.RawSpectra[Group.Points[i].Scan].FindNearestPeak(
                        (MaxMass + MinMass) / 2.0,
                        ((MaxMass - MinMass) * 500000.0) / MinMass + MassError);
                    if (ForGap.Intensity > 0.0) {
                        Group.Points[i] = ForGap;
                        ForGap.Counted = ForGap.Counted || Check;
                        ForGap.Group = Group;
                    }
                }
            }

            if (Group.Points[Group.Points.Count - 1].RT - Group.Points[0].RT < Program.MinRTWidth) {
                for (int i = 0 ; i < Group.Points.Count ; i++){
                    Group.Points[i].Group = null;
                }
                return null;
            }
            LCMSGroup.Global.Add(Group);
            return Group;
        }

        public LCMSGroup GroupFromArea(double StartRT,double EndRT, double MZ, double MassError = 0.0){

            LCMSGroup Group = new LCMSGroup();
            if (MassError == 0.0){
                MassError = Program.MassError;
            }
            int StartScan = RawFile.ScanNumFromRT(StartRT);
            int EndScan = RawFile.ScanNumFromRT(EndRT);
            for(int Scan = RawFile.IndexDir[StartScan] ; Scan <= EndScan ; Scan = RawFile.IndexDir[Scan]){
                if (Scan == -1) break;
                MZData Next = RawFile.RawSpectra[Scan].FindBiggestPeak(MZ,MassError);
                Group.Points.Add(Next);
            }

            //Заполнение дыр
            //из начала в конец 
            int LastIndex = -Program.ZeroScans-1;
            for(int i = 0 ; i < Group.Points.Count ; i++) {
                if(Group.Points[i].Intensity == 0.0) {
                    if(i - LastIndex < Program.ZeroScans) {
                        double LastMZ = Group.Points[LastIndex].Mass;
                        MZData Next = RawFile.RawSpectra[Group.Points[i].Scan].FindBiggestPeak(LastMZ,MassError);
                        Group.Points[i] = Next;
                        if (Next.Intensity != 0.0) {
                            LastIndex = i;
                        }
                    }
                } else {
                    LastIndex = i;
                }
            }
            //из конца в начало
            LastIndex = Group.Points.Count + Program.ZeroScans + 1;
            for(int i = Group.Points.Count-1 ; i >= 0  ; i--) {
                if(Group.Points[i].Intensity == 0.0) {
                    if(LastIndex - i < Program.ZeroScans) {
                        double LastMZ = Group.Points[LastIndex].Mass;
                        MZData Next = RawFile.RawSpectra[Group.Points[i].Scan].FindBiggestPeak(LastMZ,MassError);
                        Group.Points[i] = Next;
                        if (Next.Intensity != 0.0) {
                            LastIndex = i;
                        }
                    }
                } else {
                    LastIndex = i;
                }
            }

            //leading and tailing zeros (except one)
            while (Group.Points.Count > 1 && 
                Group.Points[0].Intensity == 0.0 && 
                Group.Points[1].Intensity == 0.0) 
                Group.Points.RemoveAt(0);
            while (Group.Points.Count > 1 && 
                Group.Points[Group.Points.Count - 1].Intensity == 0.0 && 
                Group.Points[Group.Points.Count - 2].Intensity == 0.0) 
                Group.Points.RemoveAt(Group.Points.Count - 1);

            if (Group.Points.Count <= 1 ){
                return null;
            }

            LCMSGroup.Global.Add(Group);
            return Group;
        }

        public List<MZData> DataMap = new List<MZData>();

        public static int CompMZDatabyIntensity(MZData x,MZData y){
            return (x.Intensity==y.Intensity)?0:(x.Intensity>y.Intensity?-1:1);
        }

        public static int CompMZDatabyMZ(MZData x,MZData y){
            return (x.Mass==y.Mass)?((x.RT==y.RT)?0:(x.RT<y.RT?-1:1)):(x.Mass<y.Mass?-1:1);
        }

        public class MZDatabyMZ:IComparer<MZData>{
            public int Compare(MZData x, MZData y){
                return (x.Mass==y.Mass)?
                    (x.RT==y.RT?0:(x.RT>y.RT?1:-1)):
                    (x.Mass>y.Mass?1:-1);
            }

        }

        public void BuildDataMap(){
            //int EndScan = RawFile.ScanNumFromRT(EndRT);
            for(int i = 0 ; i >= 0 ; i=RawFile.IndexDir[i]){
                for(int j = 0 ; j < RawFile.RawSpectra[i].Data.Length ; j++) {
                    DataMap.Add(RawFile.RawSpectra[i].Data[j]);
                }
            }
            DataMap.Sort(CompMZDatabyIntensity);
        }

        public void BuildMZMap(){
            //int EndScan = RawFile.ScanNumFromRT(EndRT);
            for(int i = 0 ; i >= 0 ; i=RawFile.IndexDir[i]){
                for(int j = 0 ; j < RawFile.RawSpectra[i].Data.Length ; j++) { // does not work with agilent
                    if(RawFile.RawSpectra[i].Data[j].Intensity >= Program.DataThres) {
                        DataMap.Add(RawFile.RawSpectra[i].Data[j]);
                    }
                }
            }
            DataMap.Sort(CompMZDatabyMZ);
        }


        int LastUsed = -1;

        public MZData BiggestUnused(){
            //LastUsed++; //??!!
            for( LastUsed++ ; LastUsed < DataMap.Count ; LastUsed++){
                if (DataMap[LastUsed].Counted)
                    continue;
                if (DataMap[LastUsed].Intensity < Program.DataThres && DataMap[LastUsed].Intensity > 0.0)
                    break;
                return DataMap[LastUsed];
            }
            return null;
        }

        public void SaveRTs(SQLiteConnection conn, int FileIndex){

            SQLiteCommand Insert = new SQLiteCommand(
                "INSERT INTO Spectra (ScanNumber, FileIndex, RT, MSOnly, TimeCoef) "+
                "Values ( @ScanNumber, @FileIndex, @RT, @MSOnly, @TimeCoeff ) ",conn);

            SQLiteParameter _ScanNumber = new SQLiteParameter("@ScanNumber");
            Insert.Parameters.Add(_ScanNumber);
            SQLiteParameter _FileIndex = new SQLiteParameter("@FileIndex");
            Insert.Parameters.Add(_FileIndex);
            _FileIndex.Value = FileIndex;
            SQLiteParameter _MSOnly = new SQLiteParameter("@MSOnly");
            Insert.Parameters.Add(_MSOnly);
            SQLiteParameter _RT = new SQLiteParameter("@RT");
            Insert.Parameters.Add(_RT);
            SQLiteParameter _TimeCoeff = new SQLiteParameter("@TimeCoeff");
            Insert.Parameters.Add(_TimeCoeff);

            for(int i = 0; i < RawFile.RawSpectra.Length ; i++){
                _ScanNumber.Value = RawFile.RawSpectra[i].Scan;
                _MSOnly.Value = RawFile.RawSpectra[i].Data == null ? 0 : 1;
                _RT.Value = RawFile.RawSpectra[i].RT;
                _TimeCoeff.Value = RawFile.TimeCoefs[i];
                Insert.ExecuteNonQuery();
            }

        }
    }
}