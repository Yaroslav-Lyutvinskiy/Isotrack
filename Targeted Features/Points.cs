using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SQLite;
using RawMSBox;

namespace Targeted_Features
{
    public class LCMSGroup{
        public List<MZData> Points = new List<MZData>();
        int GroupID;
        public static List<LCMSGroup> Global = new List<LCMSGroup>();
        public static int GroupBase;

        public void SetCounted(bool Counted){
            for ( int i = 0 ; i < Points.Count ; i++){
                Points[i].Counted = Counted;
            }
        }

        public static void SaveGroups(SQLiteConnection con, int FileID){
            SQLiteCommand Insert = new SQLiteCommand(
                "INSERT INTO PointGroups (GroupID, FileID, StartScan, EndScan, Points, MinMZ, MaxMZ) "+
                "Values ( @GroupID, @FileID, @StartScan, @EndScan, @Points, @MinMZ, @MaxMZ ) ",con);
            SQLiteParameter _GroupID = new SQLiteParameter("@GroupID");
            Insert.Parameters.Add(_GroupID );
            SQLiteParameter _FileID = new SQLiteParameter("@FileID");
            Insert.Parameters.Add(_FileID );
            SQLiteParameter _StartScan = new SQLiteParameter("@StartScan");
            Insert.Parameters.Add(_StartScan);
            SQLiteParameter _EndScan = new SQLiteParameter("@EndScan");
            Insert.Parameters.Add(_EndScan);
            SQLiteParameter _Points = new SQLiteParameter("@Points");
            Insert.Parameters.Add(_Points);
            SQLiteParameter _MinMZ = new SQLiteParameter("@MinMZ");
            Insert.Parameters.Add(_MinMZ);
            SQLiteParameter _MaxMZ = new SQLiteParameter("@MaxMZ");
            Insert.Parameters.Add(_MaxMZ);

            _FileID.Value = FileID;
            for ( int i = 0 ; i < Global.Count ; i++){
                _GroupID.Value = GroupBase + i;
                _StartScan.Value = Global[i].Points[0].Scan;
                _EndScan.Value = Global[i].Points[Global[i].Points.Count-1].Scan;
                List<byte> Bytes = new List<byte>();
                double MinMz = 1000000.0;
                double MaxMz = 0.0;
                for(int j = 0 ; j < Global[i].Points.Count ; j++) {
                    byte[] db = BitConverter.GetBytes(Global[i].Points[j].Mass);
                    Bytes.AddRange(db);
                    if(Global[i].Points[j].Mass > 0.0 && Global[i].Points[j].Mass < MinMz)
                        MinMz = Global[i].Points[j].Mass;
                    if(Global[i].Points[j].Mass > MaxMz)
                        MaxMz = Global[i].Points[j].Mass;
                    byte[] fb = BitConverter.GetBytes((float)Global[i].Points[j].Intensity);
                    Bytes.AddRange(fb);
                }
                if(Program.SaveProfile) {
                    _Points.Value = Convert.ToBase64String(Bytes.ToArray());
                }else {
                    _Points.Value = null;
                }
                _MinMZ.Value = MinMz;
                _MaxMZ.Value = MaxMz;
                Insert.ExecuteNonQuery();
            }
        }

        public static void LoadGroups(SQLiteConnection con, List<MZData> DataMap, int FileID){

            DataMap.Sort(new FileService.MZDatabyMZ());

            //select RTs from Database
            SQLiteCommand RTQuery = new SQLiteCommand(
                String.Format(
                    "Select ScanNumber, RT from Spectra  where FileIndex={0} and MSOnly = 1 order by ScanNumber", FileID),
                con);
            Dictionary<int, double> RTs = new Dictionary<int, double>();
            SQLiteDataReader Reader = RTQuery.ExecuteReader();
            while(Reader.Read()){
                RTs.Add(Reader.GetInt32(0), Reader.GetDouble(1));
            }
            Reader.Close();

            //Select GroupID range for particular file
            SQLiteCommand GroupRange = new SQLiteCommand(
                String.Format("Select Min(PointGroups.GroupID), Max(PointGroups.GroupID) from PointGroups,Traces,Features "+
                    "Where Traces.GroupID = PointGroups.GroupID and "+
                    "Features.FeatureID = Traces.onFeatureID and FileID = {0} ", FileID),
                con);
            Reader = GroupRange.ExecuteReader();
            Reader.Read();
            int GroupMin = Reader.GetInt32(0);
            int GroupMax = Reader.GetInt32(1);

            //select actual points
            SQLiteCommand Data = new SQLiteCommand(
                String.Format("Select GroupID,StartScan,EndScan,Points From PointGroups "+
                    "Where GroupID>={0} and GroupID<={1}",GroupMin,GroupMax),con);
            Reader = Data.ExecuteReader();
            while(Reader.Read()){
                LCMSGroup Group = new LCMSGroup();
                Group.GroupID = Reader.GetInt32(0);
                int StartScan = Reader.GetInt32(1);
                int EndScan =  Reader.GetInt32(2);
                string PStr = Reader.GetString(3);
                byte[] Pbyte = Convert.FromBase64String(PStr);
                int ByteCounter=0;
                double MassMin = 1000000.0;
                double MassMax = 0.0;
                //search for minimum and maximum values in Masses
                while(ByteCounter<Pbyte.Length){
                    double Mass = BitConverter.ToDouble(Pbyte,ByteCounter);
                    ByteCounter += 8;
                    float Int = BitConverter.ToSingle(Pbyte,ByteCounter);
                    ByteCounter += 4;
                    if (Mass>MassMax) MassMax=Mass;
                    if (Mass<MassMin && Mass>0.0) MassMin=Mass;
                }
                //take a range from DataMap
                MZData P = new MZData();
                P.Mass = MassMin-1.0e-5;
                int MinIndex = DataMap.BinarySearch(P,new FileService.MZDatabyMZ());//первый элемент в интервале
                if (MinIndex < 0) MinIndex = ~ MinIndex;
                P.Mass = MassMax+1.0e-5;
                int MaxIndex = DataMap.BinarySearch(P,new FileService.MZDatabyMZ()); //первый элемент за пределами интервала
                if (MaxIndex < 0) MaxIndex = ~ MaxIndex;

                //Map points to DataMap
                int RTCount = StartScan;
                double RT = RTs[RTCount];
                ByteCounter = 0;
                while(ByteCounter<Pbyte.Length){
                    P.Mass = BitConverter.ToDouble(Pbyte,ByteCounter);
                    ByteCounter += 8;
                    P.Intensity = BitConverter.ToSingle(Pbyte,ByteCounter);
                    ByteCounter += 4;
                    P.Scan = RTCount;
                    if (P.Intensity != 0.0){
                        int Index = DataMap.BinarySearch(MinIndex, MaxIndex - MinIndex, P, new FileService.MZDatabyMZ());
                        if (Index < 0) Index = ~ Index;
                        for (int i = 0 ; ; i++){
                            if ( Math.Abs(DataMap[Index+i].RT-RT) < 1.0e-5 ){
                                DataMap[Index + i].Group = Group;
                                DataMap[Index + i].Counted = true;
                                Group.Points.Add(DataMap[Index + i]);
                                break;
                            }
                            if ( Math.Abs(RT-DataMap[Index-i].RT) < 1.0e-5 ){
                                DataMap[Index - i].Group = Group;
                                DataMap[Index - i].Counted = true;
                                Group.Points.Add(DataMap[Index - i]);
                                break;
                            }
                            if (DataMap[Index+i].Mass-P.Mass > 1.0e-5 && P.Mass-DataMap[Index-i].Mass > 1.0e-5){
                                Console.WriteLine("Warning: Orphaned point - Group:{0}, RT:{1}, Mass:{2}",Group.GroupID,RT,P.Mass);
                                break;
                            }
                        }
                    }else{
                        Group.Points.Add(MZData.CreateZero(RTCount));
                    }
                    do{
                        RTCount++;
                    }while( !RTs.TryGetValue(RTCount, out RT) && RTCount<=EndScan);
                }
                Global.Add(Group);
            }
        }


        /* Old Version
         * public static void SaveGroups(SQLiteConnection con)
        {
            SQLiteCommand Insert = new SQLiteCommand(
                "INSERT INTO Points (GroupID, MZ, RT, TimeCoeff, Intensity, Scan) "+
                "Values ( @GroupID, @MZ, @RT, @TimeCoeff, @Intensity, @Scan ) ",con);
            SQLiteParameter _GroupID = new SQLiteParameter("@GroupID");
            Insert.Parameters.Add(_GroupID);
            SQLiteParameter _MZ = new SQLiteParameter("@MZ");
            Insert.Parameters.Add(_MZ);
            SQLiteParameter _RT = new SQLiteParameter("@RT");
            Insert.Parameters.Add(_RT);
            SQLiteParameter _TimeCoeff = new SQLiteParameter("@TimeCoeff");
            Insert.Parameters.Add(_TimeCoeff);
            SQLiteParameter _Intensity = new SQLiteParameter("@Intensity");
            Insert.Parameters.Add(_Intensity);
            SQLiteParameter _Scan = new SQLiteParameter("@Scan");
            Insert.Parameters.Add(_Scan);
            for ( int i = 0 ; i < Global.Count ; i++){
                _GroupID.Value = GroupBase + i;
                for ( int j = 0 ; j < Global[i].Points.Count ; j++){
                    _MZ.Value = Global[i].Points[j].Mass;
                    _RT.Value = Global[i].Points[j].RT;
                    _TimeCoeff.Value = Global[i].Points[j].TimeCoeff;
                    _Intensity.Value = Global[i].Points[j].Intensity;
                    _Scan.Value = Global[i].Points[j].Scan;
                    Insert.ExecuteNonQuery();
                }
            }
        }*/

    }

    
    public class LCMSPoint{
        public double MZ;
        public double RT;
        public double Intensity;
        public int Scan;
        public double TimeCoeff;
        public LCMSPoint(double MZ = 0.0, double Intensity = 0.0, double RT = 0.0, int Scan = 0, double TimeCoeff = 1.0){
            this.MZ = MZ;
            this.RT = RT;
            this.Intensity = Intensity;
            this.TimeCoeff = TimeCoeff;
            this.Scan = Scan;
        }
        public LCMSPoint(LCMSPoint Point){
            this.MZ = Point.MZ;
            this.RT = Point.RT;
            this.Intensity = Point.Intensity;
            this.Scan = Point.Scan;
            this.TimeCoeff = Point.TimeCoeff;
        }
        public void SaveDB(SQLiteConnection con, int TraceID){
            SQLiteCommand Insert = new SQLiteCommand(String.Format(
                "Insert Into Points (TraceID, MZ, RT, TimeCoeff, Intensity, Scan) "+
                "Values ( {0}, {1}, {2}, {3}, {4}, {5})",TraceID,MZ,RT,TimeCoeff,Intensity,Scan),con);
            Insert.ExecuteNonQuery();
        }
    }
}
