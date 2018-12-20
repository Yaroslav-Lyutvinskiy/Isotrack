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
    class FileService{

        static public void RepProgress(int Perc){
            Console.WriteLine("{0}%...",Perc);
        }

        public FileBox RawFile;
        public FileService(string FileName){
            FileBox.RepProgress = RepProgress;
            string Ext = Path.GetExtension(FileName);
            if (Ext == ".raw"){
                RawFile = new RawFileBox();
            }else{
                RawFile = new AgilentFileBox();
            }
            RawFile.LoadIndex(FileName);
            //RawFile.RTCorrection = true;
            MZData.SetRawFile(RawFile);
            double EndRT=0.0;
            //pass some last MSMS spectra?
            for(int i = RawFile.RawSpectra.Length-1;i>0;i--){
                EndRT= RawFile.RawSpectra[i].RT;
                if (EndRT>0.0)break;
            }
            RawFile.LoadInterval(0.0, EndRT);
        }

        /// <summary>
        /// Main Trace building method 
        /// </summary>
        /// <param name="Point"></param>
        /// <param name="MassError"></param>
        /// <returns></returns>

        static int ConflictCounter = 0;

        private MZData TakeFreePoint(int Scan, double Mass, LCMSGroup Group) {
            MZData Point = RawFile.RawSpectra[Scan].FindNearestPeak(Mass, Program.MassError);
            if(Point.Mass != 0.0) {
                if(Point.Group == null) {
                    Point.Group = Group;
                    Group.Points.Add(Point);
                    return Point;
                }else {
                    if(Point.Group != Group) {
                        //Console.WriteLine("Warning: Group conflict {0}",ConflictCounter++);
                        ConflictCounter++;
                    }
                }
            }
            return null;
        }

        public LCMSGroup GroupFromPoint(MZData Point) {

            if (Point.Group != null) return Point.Group as LCMSGroup;

            LCMSGroup Group = new LCMSGroup();

            Point.Group = Group;
            Group.Points.Add(Point);
            Queue<MZData> QPoints = new Queue<MZData>();
            QPoints.Enqueue(Point);
            do {
                MZData CurrentPoint = QPoints.Dequeue();
                int ForwardScan = RawFile.IndexDir[CurrentPoint.Scan];
                int BackwardScan = RawFile.IndexRev[CurrentPoint.Scan];
                for(int i = 0 ; i < Program.ZeroScans+1 ; i++) {
                    if (ForwardScan > 0) {
                        MZData NextPoint = TakeFreePoint(ForwardScan, CurrentPoint.Mass, Group);
                        if (NextPoint != null) QPoints.Enqueue(NextPoint);
                        ForwardScan = RawFile.IndexDir[ForwardScan];
                    }
                    if (BackwardScan > 0) {
                        MZData NextPoint = TakeFreePoint(BackwardScan, CurrentPoint.Mass, Group);
                        if (NextPoint != null) QPoints.Enqueue(NextPoint);
                        BackwardScan = RawFile.IndexRev[BackwardScan];
                    }
                }
            } while(QPoints.Count > 0);

            Group.Points.Sort((p1,p2)=>p1.Scan.CompareTo(p2.Scan));
            //Add zero points
            Group.Points.Add(MZData.CreateZero(RawFile.IndexDir[Group.Points[Group.Points.Count - 1].Scan]));
            Group.Points.Add(MZData.CreateZero(RawFile.IndexRev[Group.Points[0].Scan]));
            for( int i = Group.Points.Count - 3 ; i > 0 ; i--) {
                int CurrentScan = RawFile.IndexRev[Group.Points[i].Scan];
                while(CurrentScan > Group.Points[i-1].Scan) {
                    Group.Points.Add(MZData.CreateZero(CurrentScan));
                    CurrentScan = RawFile.IndexRev[CurrentScan];
                }
            }
            Group.Points.Sort((p1,p2)=>p1.Scan.CompareTo(p2.Scan));

            if (Group.Points[Group.Points.Count - 1].RT - Group.Points[0].RT < Program.PeakMinWidth) {
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

        public void BuildDataMap(){
            //int EndScan = RawFile.ScanNumFromRT(EndRT);
            for(int i = 0 ; i >= 0 ; i=RawFile.IndexDir[i]){
                for(int j = 0 ; j < RawFile.RawSpectra[i].Data.Length ; j++) {
                    DataMap.Add(RawFile.RawSpectra[i].Data[j]);
                }
            }
            DataMap.Sort(CompMZDatabyIntensity);
        }

        int LastUsed = -1;

        public MZData BiggestUnused(){
            //LastUsed++; //??!!
            for( LastUsed++ ; LastUsed < DataMap.Count ; LastUsed++){
                if (DataMap[LastUsed].Group != null )
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