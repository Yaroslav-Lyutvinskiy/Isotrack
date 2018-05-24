using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using MSFileReaderLib;
using Agilent.MassSpectrometry.DataAnalysis;

//using Quanty.Properties;

namespace RawMSBox{

    public class MZData {
        public double Mass;
        public double Intensity;
        public int Scan;
        public bool Counted = false;
        public object Group = null;
        //public int Scan{
        //    get{
        //        return RawFile.RawSpectra[SpectraIndex].Scan;
        //    }
        //}
        public double TimeCoeff{
            get{
                return RawFile.TimeCoefs[Scan];
            }
        }
        public double RT{
            get{
                return RawFile.RawSpectra[Scan].RT;
            }
        }

        static FileBox RawFile;
        public static void SetRawFile(FileBox FB){
            RawFile = FB;
        }

        public static MZData CreateZero(int Scan){
            MZData Res = new MZData();
            Res.Mass = 0.0;
            Res.Intensity = 0.0;
            Res.Scan = Scan;
            return Res;
        }

    }

    public class RawData{
        public MZData[] Data;
        public double RT;
        public int Scan;
        //для MSX спектров
        public string Filter="";

        public int FindMassBelow(double Mass){
            //запас на оптимизацию - двоичный поиск
            if (Data.GetLength(0) == 0){
                return -1;
            }
            if (Data[0].Mass > Mass){
                return -1;
            }
            for (int i = 1 ; i < Data.GetLength(0) ; i++){
                if (Data[i].Mass > Mass ) {
                    return i-1;
                }
            }
            return Data.GetLength(0)-1;
        }

        public int FindMassAbove(double Mass){
            //запас на оптимизацию - двоичный поиск
            if (Data.GetLength(0) == 0){
                return -1;
            }
            if (Data[Data.GetLength(0)-1].Mass < Mass){
                return -1;
            }
            for (int i = 0 ; i < Data.GetLength(0) ; i++){
                if (Data[i].Mass > Mass ) {
                    return i;
                }
            }
            return 0;
        }

        public double MZPlusPPM(double MZ,double ppm){
            return MZ * ((1000000.0 + ppm) / 1000000.0);
        }

        public MZData FindNearestPeak(double MZ, double Error){
            double LowerMass = MZPlusPPM(MZ, -Error);
            double UpperMass = MZPlusPPM(MZ, Error); 

            int LowerIndex = FindMassAbove(LowerMass);
            int UpperIndex = FindMassBelow(UpperMass);

            if (LowerIndex > UpperIndex || LowerIndex== -1 ){ //ничего не нашли - ничего и не делаем 
                return MZData.CreateZero(Scan);
            }

            while (LowerIndex < UpperIndex) { //если больше одного - ищем самый близкий к целевой массе
                if (LowerIndex == Data.GetLength(0)-1 || 
                    Math.Abs(MZ - Data[LowerIndex].Mass) < Math.Abs(MZ - Data[LowerIndex + 1].Mass)) {
                    break;
                }
		        LowerIndex++;
			}
            return Data[LowerIndex];
        }

        public MZData FindBiggestPeak(double MZ, double Error){
            double LowerMass = MZPlusPPM(MZ, -Error);
            double UpperMass = MZPlusPPM(MZ, Error); 

            int LowerIndex = FindMassAbove(LowerMass);
            int UpperIndex = FindMassBelow(UpperMass);

            if (LowerIndex > UpperIndex || LowerIndex== -1 ){ //ничего не нашли - ничего и не делаем 
                return MZData.CreateZero(Scan);
            }

            double MaxInt = Data[LowerIndex].Intensity;
            int MaxIndex = LowerIndex;
            LowerIndex++;
            while (LowerIndex <= UpperIndex) { //если больше одного - ищем самый близкий к целевой массе
                if (Data[LowerIndex].Intensity > MaxInt ) {
                    MaxInt = Data[LowerIndex].Intensity;
                    MaxIndex = LowerIndex;
                }
		        LowerIndex++;
			}
            return Data[MaxIndex];
        }
    }
    
    public abstract class FileBox{
        public RawData[] RawSpectra; 
        public int Spectra; 
        public string RawFileName;

        public int[] ms2index; //для каждого спектра дает номер скана последнего full-MS спектра
        //заполнены только сканов соответствующих full-MS спектрам
        public int[] IndexDir; //указывает на номер скана следующего full-MS спектра
        public int[] IndexRev; //указывает на номер скана предидущего full-MS спектра

        public double[] ESICurrents; //значения тока элекстроспрея 
        public double[] TimeStamps; //промежуток после предыдущего MS-only спектра - в минутах
        public double[] TimeCoefs;
        protected double AverageTimeStamp;

        public bool RTCorrection;

        protected MZData[] Buf;
        protected double LowRT;
        protected double HighRT;
        protected double TotalRT;
        public bool StickMode;
        public bool RawLabel;
        public int Mode; //+1 - positive, -1 - negative

        //поменять на делегата
        public delegate void Progress(int Perc);
        public static Progress RepProgress;

        public FileBox(){
            StickMode = true;
            RawLabel = true;
        }

        public int ScanNumFromRT(double RT){
            for( int i = 0 ; i < RawSpectra.GetLength(0) ; i++){
                if (RawSpectra[i].RT >= RT) return i; 
            }
            return RawSpectra.Length-1;
        }

        public void LoadInterval(double MinRT, double MaxRT)
        {
            int Index = 0;
            //по границам плюс один спектр 
            while (RawSpectra[IndexDir[Index]].RT<MinRT){
                RawSpectra[Index].Data = null;
                Index = IndexDir[Index];
            }
            int Progress = 0; 
            while (RawSpectra[IndexRev[Index]].RT<MaxRT){
                if(IndexDir[Index] == -1) {
                    break;
                }
                if (RawSpectra[Index].Data == null) {
                    ReadMS(Index);
                    for (int i = 0 ; i < RawSpectra[Index].Data.Length ; i++){
                        if (RawSpectra[Index].Data[i] != null) {
                            RawSpectra[Index].Data[i].Scan = Index;
                        }
                        RawSpectra[Index].Scan = Index;
                    }
                }
                if ((int)((RawSpectra[IndexRev[Index]].RT/MaxRT)*100) > Progress) {
                    Progress = (int)((RawSpectra[IndexRev[Index]].RT / MaxRT) * 100);
                    RepProgress(Progress);
                }
                Index = IndexDir[Index];
            }
            //while (RawSpectra[Index].RT<TotalRT){
            //    RawSpectra[Index].Data = null;
            //    if (IndexDir[Index] == -1) break;
            //    Index = IndexDir[Index];
            //}
            LowRT = MinRT;
            HighRT = MaxRT;
        }

        public MZData[] Centroid(MZData[] Data,int Len, bool StickMode /* former "in" */)
        {
	        int total = 0, u;
	        int o = 0, i = 0, count = Len;
	        double sumIi, sumI, last = 0.0;
            double du = 0.0;
	        bool goingdown = false;
            MZData[] OutData;

            if (StickMode) {
                //считаем пока не начнутся нули или пока следующий не станет меньше помассе 
                for ( i = 1 ; i<count ; i++){
                    if (Data[i].Mass < Data[i-1].Mass || Data[i].Mass == 0){
                        break;
                    }
                }
                OutData = new MZData[i];
                count = i;
                for (i=0; i<count ; i++){
                    OutData[i] = new MZData();
                    OutData[i].Intensity = Data[i].Intensity;
                    OutData[i].Mass = Data[i].Mass;
                }
                return OutData;
            }


            //пропуск начальных нулей
	        while(i < count && Data[i].Intensity == 0.0) ++i;

	        //считает области больше нуля 
	        while(i < count)
	        {
		        while(i < count && Data[i].Intensity != 0.0)
		        {
			        if(last > Data[i].Intensity) {
                        goingdown = true;
                    }else{
                        if(goingdown) {
				            ++total;
				            goingdown = false;
    			        }
                    }

			        last = Data[i].Intensity;
			        ++i;
		        }

		        last = 0.0;
		        goingdown = false;

		        while(i < count && Data[i].Intensity == 0.0) 
                    i++;

		        total++;
	        }

	        //запасает память на подсчитанные области 
	        OutData = new MZData[total];
            for (i = 0; i < total; i++) OutData[i] = new MZData();
	        i = 0; o = 0; total = 0; last = 0.0; goingdown = false;

	        while(i < count && Data[i].Intensity == 0.0) i++;

	        while(i < count)
	        {
		        sumIi = sumI = 0.0;
		        o = i -1;
		        while(i < count && Data[i].Intensity != 0.0){

			        //если пошло на спад
			        if(last > Data[i].Intensity) {
                        goingdown = true;
                    }else{
                        if(goingdown) {
				            u = Convert.ToInt32((sumIi / sumI));
				            OutData[total].Intensity = sumI;
				            OutData[total].Mass = Data[o+u].Mass;
				            ++total;

				            sumIi = sumI = 0.0;
				            o = i -1;
				            goingdown = false;
    			        }
                    }

			        sumIi += Data[i].Intensity*(i-o);
			        sumI += Data[i].Intensity;

			        last = Data[i].Intensity;
			        i++;
		        }

		        u = Convert.ToInt32((sumIi / sumI) /*+0.5*/ );
                du = sumIi / sumI - (double)u;
		        //интенсивность по интегралу 
		        OutData[total].Intensity = sumI;
		        //сентроид - по апексу 
		        //OutData[total].Mass = Data[o+u].Mass;
                //центроид по центру
                OutData[total].Mass = Data[o+u].Mass*(1-du) + Data[o+u+1].Mass*du;

		        last = 0.0;
		        goingdown = false;

		        while(i < count && Data[i].Intensity == 0.0) i++;

                //if (OutData[total].Intensity > 3.0) 
                    total++;
	        }
            return OutData;
        }
        //data format dependent
        abstract public void ReadMS(int Scan);
        abstract public int LoadIndex(string FileName);
        abstract public double GetTIC(int Scan);
    }

    public class RawFileBox : FileBox{
        
        public MSFileReader_XRawfile RawFile;

        int[] SimtoAdd;

        public override int LoadIndex(string FileName){

            this.RawFileName = FileName;

            RawFile = new MSFileReader_XRawfile();

            RawFile.Open(FileName);
            RawFile.SetCurrentController(0, 1);

            Spectra = 0;
            RawFile.GetNumSpectra(ref Spectra);

            if( Spectra <= 0) 
                return 0;

	        int i, lastfull = 0, total = 0;
            double TotalEsi = 0.0;

            //fake [0] spectra with no data and fake last spectra with no data 
	        ms2index = new int[Spectra+2];
            IndexDir = new int[Spectra+2];
            IndexRev = new int[Spectra+2];
            SimtoAdd = new int[Spectra + 2];
            RawSpectra = new RawData[Spectra+2];
            for(int j = 0 ; j <= Spectra+1 ; j++){
                RawSpectra[j] = new RawData();
            }
            Buf = new MZData[1000000];
            for (int ini = 0; ini < 1000000; ini++ ) Buf[ini] = new MZData();

            ESICurrents = new double[Spectra + 2];
            TimeStamps = new double[Spectra+2];
            TimeCoefs = new double[Spectra+2];

            string Filter = null;
            bool PosMode = false, NegMode = false;

            LowRT = 0.0;
            HighRT = 0.0;

            int Progress = 0; 
            for(i = 1; i <= Spectra; i++){

                if ((int)(100.0*((double)i/(double)Spectra)) > Progress) {
                    Progress = (int)(100.0*((double)i/(double)Spectra));
                    if (RepProgress != null){
                        RepProgress(Progress);
                    }
                }

		        RawFile.GetFilterForScanNum(i, ref Filter);

		        //YL - для спектров ms-only
                //Заплатка для MSX спектров
		        if(Filter.Contains(" Full ") &&  Filter.Contains(" ms ")  && Filter.Contains("FTMS") ) { //is a FULL MS

                    PosMode |= Filter.Contains(" + ");
                    NegMode |= Filter.Contains(" - ");

			        TimeStamps[i] = RawSpectra[lastfull].RT;
                    
                    IndexDir[lastfull] = i;
			        IndexRev[i] = lastfull;

			        lastfull = i;
			        ms2index[i] = lastfull;

			        ++total;

				    RawFile.RTFromScanNum(i, ref RawSpectra[i].RT);
                    RawSpectra[i].Filter = Filter;
                    TotalRT = RawSpectra[i].RT;

                    TimeStamps[i] = RawSpectra[i].RT - TimeStamps[i];

                    object Labels = null;
                    object Values = null;
                    int ArraySize = 0;
                    double RT = 0.0; 

                    RawFile.GetStatusLogForScanNum(i, ref RT, ref Labels, ref Values , ref ArraySize);

                    for (int k = 0 ; k < ArraySize ; k++ ){
                        if ((Labels as Array).GetValue(k).ToString().Contains("Source Current")){
                            ESICurrents[i] = Convert.ToDouble((Values as Array).GetValue(k).ToString());
                            TotalEsi+=ESICurrents[i];
                        }
                    }


		        } else {
			        ms2index[i] = lastfull;
                    if (Filter.Contains(" SIM ms ")){
                        SimtoAdd[lastfull] = i;
                    }
		        }
		        Filter = null ;
	        }
            IndexDir[lastfull] = Spectra +1;
            IndexDir[Spectra +1] = -1;
            IndexRev[Spectra + 1] = lastfull;

            TotalRT = RawSpectra[lastfull].RT;
            AverageTimeStamp = TotalRT/total;

            //пересчитаем временные коэффициэнты 
            for (i = IndexDir[0] ; IndexDir[i] != -1 ; i = IndexDir[i]) {

                TimeCoefs[i] = (TimeStamps[i]+TimeStamps[IndexDir[i]])/(2.0*AverageTimeStamp);

                ESICurrents[i] = ESICurrents[i]/(TotalEsi/(double)total);
            }
            TimeCoefs[i] = 1.0;
            //Spectra number 0 has to have RT at the same distance as others
            double FRT = RawSpectra[IndexDir[0]].RT;
            double SRT = RawSpectra[IndexDir[IndexDir[0]]].RT;
            RawSpectra[0].RT=Math.Max(0,FRT-(SRT-FRT));
            FRT = RawSpectra[lastfull].RT;
            SRT = RawSpectra[IndexRev[lastfull]].RT;
            //FRT = RawSpectra[IndexRev[lastfull]].RT;
            //SRT = RawSpectra[IndexRev[IndexRev[lastfull]]].RT;
            RawSpectra[Spectra + 1].RT = FRT + (FRT - SRT);
            RawSpectra[0].Data = new MZData[0];
            RawSpectra[Spectra + 1].Data = new MZData[0];

            if (PosMode && !NegMode) Mode = 1;
            if (!PosMode && NegMode) Mode = -1;
            
            return Spectra;
        }

        public override void ReadMS(int Scan){
	        int ArraySize = 0;
            Object MassList = null, EmptyRef=null;
            double temp=0.0;

            try {
                if(StickMode && Scan > 0 ){
                    if (RawLabel){
                        (RawFile as IXRawfile2).GetLabelData(ref MassList, ref EmptyRef, ref  Scan);
                        ArraySize = (MassList as Array).GetLength(1); 
                        RawSpectra[Scan].Data = new MZData[ArraySize];
                        for (int k = 0 ; k<ArraySize ; k++ ){
                            RawSpectra[Scan].Data[k] = new MZData();
                            RawSpectra[Scan].Data[k].Mass = (double)(MassList as Array).GetValue(0, k);
                            RawSpectra[Scan].Data[k].Intensity = (double)(MassList as Array).GetValue(1, k);
                        }
                        if (SimtoAdd[Scan] != 0){
                            int SimScan = SimtoAdd[Scan];
                            MassList = null;
                            EmptyRef = null;
                            (RawFile as IXRawfile2).GetLabelData(ref MassList, ref EmptyRef, ref  SimScan);
                            int SimSize = (MassList as Array).GetLength(1);
                            MZData[] NewData = new MZData[ArraySize + SimSize];
                            for (int k = 0 ; k<SimSize ; k++ ){
                                NewData[k] = new MZData();
                                NewData[k].Mass = (double)(MassList as Array).GetValue(0, k);
                                NewData[k].Intensity = (double)(MassList as Array).GetValue(1, k);
                            }
                            for (int k = 0 ; k<ArraySize ; k++ ){
                                NewData[k + SimSize] = RawSpectra[Scan].Data[k];
                            }
                            RawSpectra[Scan].Data = NewData;
                        }
                    }else{
                        double PeakWidth = 0.0;
                        object PeakFlags = null;
                        string Filter = RawSpectra[Scan].Filter;
                        string MassRange = Filter.Substring(Filter.IndexOf("[")+1,Filter.IndexOf("]")-Filter.IndexOf("[")-1);
                        (RawFile as IXRawfile3).GetMassListRangeFromScanNum(
                            ref Scan, null, 0, 0, 0, 1, ref PeakWidth,ref MassList , ref PeakFlags, MassRange, ref ArraySize);
                        RawSpectra[Scan].Data = new MZData[ArraySize];
                        for (int k = 0 ; k<ArraySize ; k++ ){
                            RawSpectra[Scan].Data[k] = new MZData();
                            RawSpectra[Scan].Data[k].Mass = (double)(MassList as Array).GetValue(0, k);
                            RawSpectra[Scan].Data[k].Intensity = (double)(MassList as Array).GetValue(1, k);
                        }
                        if (SimtoAdd[Scan] != 0){
                            int SimScan = SimtoAdd[Scan];
                            Filter = null; 
                            RawFile.GetFilterForScanNum(SimScan, ref Filter);
                            MassRange = Filter.Substring(Filter.IndexOf("[")+1,Filter.IndexOf("]")-Filter.IndexOf("[")-1);
                            PeakWidth = 0.0;
                            PeakFlags = null;
                            MassList = null;
                            EmptyRef = null;
                            int SimSize = 0;
                            (RawFile as IXRawfile3).GetMassListRangeFromScanNum(
                                ref SimScan, null, 0, 0, 0, 1, ref PeakWidth,ref MassList , ref PeakFlags, MassRange, ref SimSize);
                            MZData[] NewData = new MZData[ArraySize + SimSize];
                            for (int k = 0 ; k<SimSize ; k++ ){
                                NewData[k].Mass = (double)(MassList as Array).GetValue(0, k);
                                NewData[k].Intensity = (double)(MassList as Array).GetValue(1, k);
                            }
                            for (int k = 0 ; k<ArraySize ; k++ ){
                                NewData[k+SimSize].Mass = RawSpectra[Scan].Data[k].Mass;
                                NewData[k+SimSize].Intensity = RawSpectra[Scan].Data[k].Intensity;
                            }
                            RawSpectra[Scan].Data = NewData;
                        }
                    }
                    return;
                }else{
	                RawFile.GetMassListFromScanNum(ref Scan, null, 
		                0, //type
                        0, //value
                        0, //peaks
                        0, //centeroid
                        ref temp,
		                ref MassList, 
                        ref EmptyRef, 
                        ref ArraySize);
                }
            }
            catch{
                Exception e = new Exception(string.Format("Scan #{0} cannot be loaded, probably RAW file is corrupted!",Scan));
                throw e;
            }
			
            //RawSpectra[Scan].Data = new MZData[ArraySize];

            if (SimtoAdd[Scan] == 0){
                for ( int j = 0 ; j<ArraySize ; j++){
                    Buf[j].Mass = (double)(MassList as Array).GetValue(0,j);
                    Buf[j].Intensity =  (double)(MassList as Array).GetValue(1,j);
                }
            }else{
                int SimSize = 0;
                Object SimMassList = null, SimEmptyRef=null;
                temp = 0.0;
                try{
	                RawFile.GetMassListFromScanNum(ref Scan, null, 
		                0, //type
                        0, //value
                        0, //peaks
                        0, //centeroid
                        ref temp,
		                ref SimMassList, 
                        ref SimEmptyRef, 
                        ref SimSize);
                    for ( int j = 0 ; j<SimSize ; j++){
                        Buf[j].Mass = (double)(SimMassList as Array).GetValue(0,j);
                        Buf[j].Intensity =  (double)(SimMassList as Array).GetValue(1,j);
                    }
                    for ( int j = 0 ; j<ArraySize ; j++){
                        Buf[j+SimSize].Mass = (double)(MassList as Array).GetValue(0,j);
                        Buf[j+SimSize].Intensity =  (double)(MassList as Array).GetValue(1,j);
                    }
                    SimMassList = null;
                }
                catch{
                    Exception e = new Exception(string.Format("Scan #{0} cannot be loaded, probably RAW file is corrupted!",Scan));
                    throw e;
                }
            }


            MassList = null;
            GC.Collect(2);

            int isCentroided = 0;

            RawFile.IsCentroidScanForScanNum(Scan,ref isCentroided);

            RawSpectra[Scan].Data = Centroid(Buf, ArraySize, isCentroided != 0);
        }

/*        public MZData[] PeakDetect(MZData[] Data ){

            PeakDetecting.PeakDetector pd = new PeakDetecting.PeakDetector();
            PeakDetecting.peakinside[] Peaks = new PeakDetecting.peakinside[1];
            pd.PeaksDetecting(ref Data, ref Peaks);
	        MZData[] OutData = new MZData[Peaks.GetLength(0)];
            for (int i = 0 ; i < Peaks.GetLength(0) ; i++){
                OutData[i].Intensity = Peaks[i].Value;
                OutData[i].Mass = Peaks[i].Center;
            }
            return OutData;
        }*/

        public override double GetTIC(int Scan){
            int NumPackets=0 , Сhannels = 0 , UniTime = 0 ;
            double StartTime = 0.0, LowMass = 0.0, HighMass = 0.0, Tic = 0.0, BPMass = 0.0, BPInt = 0.0, Freq = 0.0;
            RawFile.GetScanHeaderInfoForScanNum(
                Scan, ref NumPackets, ref StartTime, ref LowMass, ref HighMass, ref Tic, ref BPMass, ref BPInt, ref Сhannels, ref UniTime, ref Freq);
            return Tic;
        }


    }


    public class AgilentFileBox : FileBox{
        
        public MassSpecDataReader RawFile; 
        private IMsdrDataReader MSReader;
        private IMsdrPeakFilter PeakFilter;

        bool HasProfileData;

        public override int LoadIndex(string FileName){

            this.RawFileName = FileName;



            RawFile = new MassSpecDataReader();
            MSReader = RawFile;

            MSReader.OpenDataFile(FileName);

            HasProfileData = File.Exists(FileName+Path.DirectorySeparatorChar+"AcqData"+Path.DirectorySeparatorChar+"MSProfile.bin");
            if (StickMode) HasProfileData = false;

            Spectra = (int)(MSReader.MSScanFileInformation.TotalScansPresent);

            bool PosMode = false, NegMode = false;

            if( Spectra <= 0) 
                return 0;

	        int i, lastfull = 0, total = 0;
            double TotalEsi = 0.0;

	        ms2index = new int[Spectra+2];
            IndexDir = new int[Spectra+2];
            IndexRev = new int[Spectra+2]; 
            RawSpectra = new RawData[Spectra+2];
            for(int j = 0 ; j <= Spectra+1 ; j++){
                RawSpectra[j] = new RawData();
            }
            Buf = new MZData[500000];
            for (i = 0; i < 500000; i++) Buf[i] = new MZData();
            ESICurrents = new double[Spectra+2];
            TimeStamps = new double[Spectra+2];
            TimeCoefs = new double[Spectra+2];

            LowRT = 0.0;
            HighRT = 0.0;

            int Progress = 0; 
            for(i = 1; i <= Spectra; i++){

                if ((int)(100.0*((double)i/(double)Spectra)) > Progress) {
                    Progress = (int)(100.0*((double)i/(double)Spectra));
                    if (RepProgress != null){
                        RepProgress(Progress);
                    }
                }

                IMSScanRecord ScanRecord =  MSReader.GetScanRecord(i-1);

		        //YL - для спектров ms-only
		        if(ScanRecord.MSScanType == MSScanType.Scan && ScanRecord.MSLevel == MSLevel.MS && ScanRecord.CollisionEnergy == 0.0) { //is a FULL MS

                    PosMode |= ScanRecord.IonPolarity == IonPolarity.Positive;
                    NegMode |= ScanRecord.IonPolarity == IonPolarity.Negative;

			        TimeStamps[i] = RawSpectra[lastfull].RT;
                    
                    IndexDir[lastfull] = i;
			        IndexRev[i] = lastfull;

			        lastfull = i;
			        ms2index[i] = lastfull;

			        ++total;

                    RawSpectra[i].RT = ScanRecord.RetentionTime;

                    TotalRT = RawSpectra[i].RT;

                    TimeStamps[i] = RawSpectra[i].RT - TimeStamps[i];

		        }  else {
			        ms2index[i] = lastfull;
		        }
	        }
            IndexDir[lastfull] = Spectra +1;
            IndexDir[Spectra +1] = -1;
            IndexRev[Spectra + 1] = lastfull;


            //IndexDir[lastfull] = -1;
            TotalRT = RawSpectra[lastfull].RT;
            AverageTimeStamp = TotalRT/total;

            //пересчитаем временные коэффициэнты 
            for (i = IndexDir[0] ; IndexDir[i] != -1 ; i = IndexDir[i]) {

                TimeCoefs[i] = (TimeStamps[i]+TimeStamps[IndexDir[i]])/(2.0*AverageTimeStamp);

                ESICurrents[i] = ESICurrents[i]/(TotalEsi/(double)total);
            }
            TimeCoefs[i] = 1.0;

            //Spectra number 0 has to have RT at the same distance as others
            double FRT = RawSpectra[IndexDir[0]].RT;
            double SRT = RawSpectra[IndexDir[IndexDir[0]]].RT;
            RawSpectra[0].RT=Math.Max(0,FRT-(SRT-FRT));
            FRT = RawSpectra[lastfull].RT;
            SRT = RawSpectra[IndexRev[lastfull]].RT;
            //FRT = RawSpectra[IndexRev[lastfull]].RT;
            //SRT = RawSpectra[IndexRev[IndexRev[lastfull]]].RT;
            RawSpectra[Spectra + 1].RT = FRT + (FRT - SRT);
            RawSpectra[0].Data = new MZData[0];
            RawSpectra[Spectra + 1].Data = new MZData[0];

            PeakFilter = new MsdrPeakFilter();
            PeakFilter.AbsoluteThreshold = 5.0;
            
            if (PosMode && !NegMode) Mode = 1;
            if (!PosMode && NegMode) Mode = -1;

            return Spectra;
        }

        public override void ReadMS(int Scan){

	        int ArraySize = 0;
            IBDASpecData SpecData;
            try {
                if(!HasProfileData || StickMode){
                    SpecData = MSReader.GetSpectrum(Scan-1,PeakFilter,PeakFilter,DesiredMSStorageType.Peak);
                    RawSpectra[Scan].Data = new MZData[SpecData.TotalDataPoints];
                    for (int k = 0 ; k<SpecData.TotalDataPoints ; k++ ){
                        RawSpectra[Scan].Data[k] = new MZData();
                        RawSpectra[Scan].Data[k].Mass = SpecData.XArray[k];
                        RawSpectra[Scan].Data[k].Intensity = SpecData.YArray[k];
                    }
                    return;
                }else{
                    SpecData = MSReader.GetSpectrum(Scan-1,null,null,/*PeakFilter,PeakFilter,*/DesiredMSStorageType.Profile);
                    if (Buf.GetLength(0) < SpecData.TotalDataPoints) { 
                        Buf = new MZData[SpecData.TotalDataPoints + 100];
                        for (int i = 0 ; i<SpecData.TotalDataPoints + 100 ; i++)
                            Buf[i] = new MZData();
                    }
                    ArraySize = SpecData.TotalDataPoints;
                    for ( int j = 0 ; j<ArraySize ; j++){
                        Buf[j].Mass = SpecData.XArray[j];
                        Buf[j].Intensity =  SpecData.YArray[j];
                    }
                }
            }
            catch{
                Exception e = new Exception(string.Format("Scan #{0} cannot be loaded, probably RAW file is corrupted!",Scan-1));
                throw e;
            }
			
            //RawSpectra[Scan].Data = new MZData[ArraySize];

            GC.Collect(2);

            //if (Settings.Default.Centroids){
            //    RawSpectra[Scan].Data = PeakDetect(Buf);
            //}else{
            //    RawSpectra[Scan].Data = Centroid(Buf, ArraySize);
            //}
            RawSpectra[Scan].Data = Centroid(Buf, ArraySize, !HasProfileData);
        }

        public override double GetTIC(int Scan){
            IMSScanRecord ScanRecord =  MSReader.GetScanRecord(Scan);
            return ScanRecord.Tic;
        }


    }

}
