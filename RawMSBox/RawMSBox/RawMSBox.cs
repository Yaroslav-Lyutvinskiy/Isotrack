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
using MSFileReaderLib;
using Agilent.MassSpectrometry.DataAnalysis;

namespace RawMSBox{

    /// <summary>
    /// Representation of single data point in mass spectra.
    /// </summary>
    public class MZData { 
        public double Mass;
        public double Intensity;
        public int Scan;
        public object Group = null; //reference to trace to which data point belong to (actual type LCMSGroup)

        public double TimeCoeff{    //time scale irregularity information located in raw file spectra collection  
            get{
                return RawFile.TimeCoefs[Scan];
            }
        }
        public double RT{           //actual retention time information located in raw file spectra collection
            get{
                return RawFile.RawSpectra[Scan].RT;
            }
        }
        static FileBox RawFile;     //since it guess that there only one raw file open in this process 
                                    //we can have reference to that file as a static member and save some memory

        public static void SetRawFile(FileBox FB){
            RawFile = FB;
        }

        /// <summary>
        /// Creates zero intesity fake data point
        /// That is necessary for chromatograms leading/trailing zeroes and so on
        /// </summary>
        public static MZData CreateZero(int Scan){
            MZData Res = new MZData();
            Res.Mass = 0.0;
            Res.Intensity = 0.0;
            Res.Scan = Scan;
            return Res;
        }

        //Comparer for Binary Search
        public class byMass: IComparer<MZData>
        {
            public int Compare(MZData x, MZData y)
            {
                return x.Mass.CompareTo(y.Mass);
            }
        }

        static public byMass MassComparer = new byMass();

    }

    /// <summary>
    /// Representation of spectra in a raw data file
    /// </summary>
    public class RawSpectrum{ 

        public MZData[] Data;   //Array of data points in the spectra sorted by mass
        public double RT;       //Retention time where spectrum has been taken
        public int Scan;        //Scan number - identifier of spectra inside of LC-MS run

        MZData forSearch = new MZData();

        /// <summary>
        /// Primitive function to search first data point with mass less then specified in spectrum
        /// </summary>
        /// <param name="Mass">Mass, to be serched from</param>
        /// <returns>index of data point in Data array, if not found return zero </returns>
        public int FindMassBelow(double Mass){
            //could be optimized with binary search
            if (Data.GetLength(0) == 0){
                return -1;
            }
            if (Data[0].Mass > Mass){
                return -1;
            }
            forSearch.Mass = Mass;
            int Index = Array.BinarySearch(Data, forSearch, MZData.MassComparer);
            if(Index >= 0)  return Index;
            return (~Index)-1;
        }

        /// <summary>
        /// Primitive function to search first data point with mass more then specified in spectrum
        /// </summary>
        /// <param name="Mass">Mass, to be serched from</param>
        /// <returns>index of data point in Data array, if not found return zero </returns>
        public int FindMassAbove(double Mass){
            //could be optimized with binary search
            if (Data.GetLength(0) == 0){
                return -1;
            }
            if (Data[Data.GetLength(0)-1].Mass < Mass){
                return -1;
            }
            forSearch.Mass = Mass;
            int Index = Array.BinarySearch(Data, forSearch, MZData.MassComparer);
            if(Index >= 0)  return Index;
            return ~Index;
        }

        /// <summary>
        /// Service static function to change mass to certain ppm (part per million) value
        /// </summary>
        /// <param name="MZ">Mass to be adjusted</param>
        /// <param name="ppm">ppm shift value</param>
        /// <returns>Adjusted mass</returns>
        public static double MZPlusPPM(double MZ,double ppm){
            return MZ * ((1000000.0 + ppm) / 1000000.0);
        }

        /// <summary>
        /// Function to search nearest peak in spectra around MZ mass, interval of search is specified in ppm by Error parameter
        /// </summary>
        /// <param name="MZ">Mass, to search around of</param>
        /// <param name="Error">Interval of +/- ppm to be searched in</param>
        /// <returns>If found returns data point, otherwise returns zero data point</returns>
        public MZData FindNearestPeak(double MZ, double Error){
            double LowerMass = MZPlusPPM(MZ, -Error);
            double UpperMass = MZPlusPPM(MZ, Error); 

            int LowerIndex = FindMassAbove(LowerMass);
            int UpperIndex = FindMassBelow(UpperMass);

            if (LowerIndex > UpperIndex || LowerIndex== -1 ){ //if nothing if found - return zero data point
                return MZData.CreateZero(Scan);
            }

            while (LowerIndex < UpperIndex) { //if there are more then one data point - return closest by mass 
                //Console.WriteLine("Warning: Two peaks at distanse of {0} ppm in scan {1} mass {2}",
                //    ((Data[LowerIndex].Mass - Data[LowerIndex+1].Mass) * 500000.0) / (Data[LowerIndex].Mass + Data[LowerIndex+1].Mass),
                //    Scan, Data[LowerIndex].Mass);
                if (LowerIndex == Data.GetLength(0)-1 || 
                    Math.Abs(MZ - Data[LowerIndex].Mass) < Math.Abs(MZ - Data[LowerIndex + 1].Mass)) {
                    break;
                }
		        LowerIndex++;
			}
            return Data[LowerIndex];
        }

        /// <summary>
        /// Function to search bigest peak in spectra around MZ mass, interval of search is specified in ppm by Error parameter
        /// </summary>
        /// <param name="MZ">Mass, to search around of</param>
        /// <param name="Error">Interval of +/- ppm to be searched in</param>
        /// <returns>If found returns data point, otherwise returns zero data point</returns>
        public MZData FindBiggestPeak(double MZ, double Error){
            double LowerMass = MZPlusPPM(MZ, -Error);
            double UpperMass = MZPlusPPM(MZ, Error); 

            int LowerIndex = FindMassAbove(LowerMass);
            int UpperIndex = FindMassBelow(UpperMass);

            if (LowerIndex > UpperIndex || LowerIndex== -1 ){ //if nothing if found - return zero data point
                return MZData.CreateZero(Scan);
            }

            double MaxInt = Data[LowerIndex].Intensity;
            int MaxIndex = LowerIndex;
            LowerIndex++;
            while (LowerIndex <= UpperIndex) { //if there are more then one data point - return most intensive
                if (Data[LowerIndex].Intensity > MaxInt ) {
                    MaxInt = Data[LowerIndex].Intensity;
                    MaxIndex = LowerIndex;
                }
		        LowerIndex++;
			}
            return Data[MaxIndex];
        }
    }
    
    /// <summary>
    /// LC-MS run representation
    /// </summary>
    public abstract class FileBox{
        public RawSpectrum[] RawSpectra;    //Array of spectra in the raw data file, 
                                            //when filled contains references to ms-only spectra, 
                                            //references corresponded to ms2 spectra stays null

        public int Spectra;                 //Total number of spectra in the file 
        public string RawFileName;          //Raw file name 

        //Navigation arrays
        public int[] IndexDir;              //Array to navigate forward in retention time, each element of this array points to the next ms-only spectra in the run 
        public int[] IndexRev;              //Array to navigate backward in retention time, each element of this array points to the previous ms-only spectra in the run 
                                            //if I is index of ms+only spectrum in RawSpectrum collection then 
                                            //IndexDir[I] will be an index of next ms-only spectra in collection
                                            //IndexRev[I] will be an index of previous ms-only spectra in collection

        //Arrays to compensate time interval bias between sequential spectra in LC-MS run
        //Since time interval between MS+only spectra can be unequal due to MSMS events and other instrument related issues
        //it should be compensated in LC peak area calculation
        public double[] TimeStamps;         //Time interval in minutes between current and previous ms-only spectra  
        public double[] TimeCoefs;          //Coefficient to multiply spectra intensity (TimeStamp[i]/Average)
        protected double AverageTimeStamp;  //Average time interval in minutes between ms-only spectra in LC-MS run 

        protected double LowRT;             //Lowest loaded RT
        protected double HighRT;            //Highest loaded RT
        protected double TotalRT;           //Total RT Interval
        public int Mode;                    //+1 - positive, -1 - negative

        public delegate void Progress(int Perc);    //Delegate where to report progress
        public static Progress RepProgress;

        //function is to be removed    
        public int ScanNumFromRT(double RT){
            for( int i = 0 ; i < RawSpectra.GetLength(0) ; i++){
                if (RawSpectra[i].RT >= RT) return i; 
            }
            return RawSpectra.Length-1;
        }


        /// <summary>
        /// Get spectra index in RawSpectra collection where rt is not less then requested 
        /// </summary>
        /// <param name="RT">Retention time</param>
        /// <returns>Index of first spectra with rt not less then requested </returns>
        public int IndexFromRT(double RT){
            for( int i = 0 ; i < RawSpectra.GetLength(0) ; i++){
                if (RawSpectra[i].RT >= RT) return i; 
            }
            return RawSpectra.Length-1;
        }

        /// <summary>
        /// Load to the memory data points for ms-only spectra where RT is in interval between MinRT and MaxRT
        /// </summary>
        /// <param name="MinRT">Minimum RT to load</param>
        /// <param name="MaxRT">Maximum RT to load</param>
        public void LoadInterval(double MinRT, double MaxRT)
        {
            int Index = 0;
            //Navigate forward in RT until MinRT
            while (RawSpectra[IndexDir[Index]].RT<MinRT){
                RawSpectra[Index].Data = null;
                Index = IndexDir[Index]; //go forward 
            }
            int Progress = 0; 
            //load spectra forward in RT until MaxRT 
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
                    }
                }
                //Report progress
                if ((int)((RawSpectra[IndexRev[Index]].RT/MaxRT)*100) > Progress) {
                    Progress = (int)((RawSpectra[IndexRev[Index]].RT / MaxRT) * 100);
                    RepProgress(Progress);
                }
                Index = IndexDir[Index]; //go forward 
            }
            LowRT = MinRT;
            HighRT = MaxRT;
        }

        /// <summary>
        /// Read factual spectrum for scan number 
        /// </summary>
        /// <param name="Scan"> Scan to be read </param>
        abstract public void ReadMS(int Scan);

        /// <summary>
        /// //Read index of ms-only spectra, fills navigation structures
        /// </summary>
        /// <param name="FileName"> File name of LC-MS run </param>
        /// <returns></returns>
        abstract public int LoadIndex(string FileName); 
        
    }

    //Thermo .raw specific FileBox implementation
    public class RawFileBox : FileBox{
        
        public MSFileReader_XRawfile RawFile; //instance of LC-MS run

        public override int LoadIndex(string FileName){

            this.RawFileName = FileName;

            RawFile = new MSFileReader_XRawfile();
            RawFile.Open(FileName);
            RawFile.SetCurrentController(0, 1);
            Spectra = 0;
            RawFile.GetNumSpectra(ref Spectra);

            if( Spectra <= 0) //if there are no spectra available
                return 0;

	        int i, LastFull = 0, Total = 0;

            //there will be fake [0] spectra with no data and fake last spectra with no data 
            //it is made to make any chromatogram start and end with zero
            IndexDir = new int[Spectra+2];
            IndexRev = new int[Spectra+2];
            RawSpectra = new RawSpectrum[Spectra+2];
            for(int j = 0 ; j <= Spectra+1 ; j++){
                RawSpectra[j] = new RawSpectrum();
            }

            TimeStamps = new double[Spectra+2];
            TimeCoefs = new double[Spectra+2];

            string Filter = null;
            bool PosMode = false, NegMode = false;

            LowRT = 0.0;
            HighRT = 0.0;

            int Progress = 0; 
            for(i = 1; i <= Spectra; i++){

                if ((int)(100.0*((double)i/(double)Spectra)) > Progress) { //report progress 
                    Progress = (int)(100.0*((double)i/(double)Spectra));
                    RepProgress?.Invoke(Progress);
                }

		        RawFile.GetFilterForScanNum(i, ref Filter); //to reveal spectra properties we parse filter string

		        if(Filter.Contains(" Full ") &&  Filter.Contains(" ms ")  && Filter.Contains("FTMS") ) { //is a FULL MS

                    PosMode |= Filter.Contains(" + "); 
                    NegMode |= Filter.Contains(" - ");

   				    RawFile.RTFromScanNum(i, ref RawSpectra[i].RT);
                    RawSpectra[i].Scan = i;
                    TimeStamps[i] = RawSpectra[i].RT - RawSpectra[LastFull].RT;

                    IndexDir[LastFull] = i;
			        IndexRev[i] = LastFull;

			        LastFull = i;

			        Total++;
		        } 
		        Filter = null ;
	        }
            IndexDir[LastFull] = Spectra +1; //boundaries of navigation arrays
            IndexDir[Spectra +1] = -1;
            IndexRev[Spectra + 1] = LastFull;

            TotalRT = RawSpectra[LastFull].RT;
            AverageTimeStamp = TotalRT/Total;

            //time interval bias coefficients 
            for (i = IndexDir[0] ; IndexDir[i] != -1 ; i = IndexDir[i]) {
                TimeCoefs[i] = (TimeStamps[i]+TimeStamps[IndexDir[i]])/(2.0*AverageTimeStamp);
            }
            TimeCoefs[i] = 1.0;
            //Fake spectra number 0 has to have reasonable RT 
            double FRT = RawSpectra[IndexDir[0]].RT;            //First full spectra RT
            double SRT = RawSpectra[IndexDir[IndexDir[0]]].RT;  //Second full spectra RT
            RawSpectra[0].RT=Math.Max(0,FRT-(SRT-FRT));         // 0 or make the same distance like between 1-st and 2-nd spectra
            //Last spectra also has to have reasonable RT 
            FRT = RawSpectra[LastFull].RT;                      //the same for last spectra
            SRT = RawSpectra[IndexRev[LastFull]].RT;
            RawSpectra[Spectra + 1].RT = FRT + (FRT - SRT);
            RawSpectra[Spectra + 1].Scan = RawSpectra[Spectra].Scan + 1; 

            RawSpectra[0].Data = new MZData[0];
            RawSpectra[Spectra + 1].Data = new MZData[0];

            if (PosMode && !NegMode) Mode = 1;
            if (!PosMode && NegMode) Mode = -1;
            
            return Spectra;
        }

        public override void ReadMS(int Scan){
	        int ArraySize = 0;
            Object MassList = null, EmptyRef=null;
            try {
                if(Scan > 0 ){
                    (RawFile as IXRawfile2).GetLabelData(ref MassList, ref EmptyRef, ref  Scan);
                    ArraySize = (MassList as Array).GetLength(1); 
                    RawSpectra[Scan].Data = new MZData[ArraySize];
                    for (int k = 0 ; k<ArraySize ; k++ ){
                        RawSpectra[Scan].Data[k] = new MZData();
                        RawSpectra[Scan].Data[k].Mass = (double)(MassList as Array).GetValue(0, k);
                        RawSpectra[Scan].Data[k].Intensity = (double)(MassList as Array).GetValue(1, k);
                    }
                } else {
                    RawSpectra[Scan].Data = new MZData[0];
                }
            }
            catch{
                Exception e = new Exception(string.Format("Scan #{0} cannot be loaded, probably RAW file is corrupted!",Scan));
                throw e;
            }
            MassList = null;
            GC.Collect(2);
        }
    }

    //Agilent .d specific FileBox implementation
    public class AgilentFileBox : FileBox{
        
        public MassSpecDataReader RawFile; 
        private IMsdrDataReader MSReader;
        private IMsdrPeakFilter PeakFilter;

        public override int LoadIndex(string FileName){

            this.RawFileName = FileName;

            RawFile = new MassSpecDataReader();
            MSReader = RawFile;
            MSReader.OpenDataFile(FileName);
            Spectra = (int)(MSReader.MSScanFileInformation.TotalScansPresent);

            bool PosMode = false, NegMode = false;

            if( Spectra <= 0) 
                return 0;

	        int i, LastFull = 0, Total = 0;

            //there will be fake [0] spectra with no data and fake last spectra with no data 
            //it is made to make any chromatogram start and end with zero
            IndexDir = new int[Spectra+2];
            IndexRev = new int[Spectra+2]; 
            RawSpectra = new RawSpectrum[Spectra+2];
            for(int j = 0 ; j <= Spectra+1 ; j++){
                RawSpectra[j] = new RawSpectrum();
            }
            TimeStamps = new double[Spectra+2];
            TimeCoefs = new double[Spectra+2];

            LowRT = 0.0;
            HighRT = 0.0;

            int Progress = 0; 
            for(i = 1; i <= Spectra; i++){

                if ((int)(100.0*((double)i/(double)Spectra)) > Progress) {//report progress 
                    Progress = (int)(100.0*((double)i/(double)Spectra));
                    RepProgress?.Invoke(Progress);
                }

                IMSScanRecord ScanRecord =  MSReader.GetScanRecord(i-1);

		        if(ScanRecord.MSScanType == MSScanType.Scan && ScanRecord.MSLevel == MSLevel.MS && ScanRecord.CollisionEnergy == 0.0) { //if spectra is a FULL MS

                    PosMode |= ScanRecord.IonPolarity == IonPolarity.Positive;
                    NegMode |= ScanRecord.IonPolarity == IonPolarity.Negative;

                    RawSpectra[i].RT = ScanRecord.RetentionTime;
                    TimeStamps[i] = RawSpectra[i].RT - RawSpectra[LastFull].RT;
                    RawSpectra[i].Scan = i;

                    IndexDir[LastFull] = i;
			        IndexRev[i] = LastFull;

			        LastFull = i;

			        Total++;
		        }  
	        }
            IndexDir[LastFull] = Spectra +1;
            IndexDir[Spectra + 1] = -1;
            IndexRev[Spectra + 1] = LastFull;

            TotalRT = RawSpectra[LastFull].RT;
            AverageTimeStamp = TotalRT/Total;

            //time interval bias coefficients 
            for (i = IndexDir[0] ; IndexDir[i] != -1 ; i = IndexDir[i]) {
                TimeCoefs[i] = (TimeStamps[i]+TimeStamps[IndexDir[i]])/(2.0*AverageTimeStamp);
            }
            TimeCoefs[i] = 1.0;

            //Fake spectra number 0 has to have reasonable RT 
            double FRT = RawSpectra[IndexDir[0]].RT;
            double SRT = RawSpectra[IndexDir[IndexDir[0]]].RT;
            RawSpectra[0].RT=Math.Max(0,FRT-(SRT-FRT));
            //Last spectra also has to have reasonable RT 
            FRT = RawSpectra[LastFull].RT;
            SRT = RawSpectra[IndexRev[LastFull]].RT;
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
            IBDASpecData SpecData;
            try {
                //Agilent spectra are enumerated from 0, Thermo spectra + from 1, so here is a shift 
                SpecData = MSReader.GetSpectrum(Scan-1,PeakFilter,PeakFilter,DesiredMSStorageType.Peak);
                RawSpectra[Scan].Data = new MZData[SpecData.TotalDataPoints];
                for (int k = 0 ; k<SpecData.TotalDataPoints ; k++ ){
                    RawSpectra[Scan].Data[k] = new MZData();
                    RawSpectra[Scan].Data[k].Mass = SpecData.XArray[k];
                    RawSpectra[Scan].Data[k].Intensity = SpecData.YArray[k];
                }
            }
            catch{
                Exception e = new Exception(string.Format("Scan #{0} cannot be loaded, probably RAW file is corrupted!",Scan-1));
                throw e;
            }
            SpecData = null;
            GC.Collect(2);
        }
    }

}
