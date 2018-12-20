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
    public class LCMSGroup{
        public List<MZData> Points = new List<MZData>();
        int GroupID;
        public static List<LCMSGroup> Global = new List<LCMSGroup>();
        public static int GroupBase;


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
                double MinMz = 1000000.0;
                double MaxMz = 0.0;
                for(int j = 0 ; j < Global[i].Points.Count ; j++) {
                    if(Global[i].Points[j].Mass > 0.0 && Global[i].Points[j].Mass < MinMz)
                        MinMz = Global[i].Points[j].Mass;
                    if(Global[i].Points[j].Mass > MaxMz)
                        MaxMz = Global[i].Points[j].Mass;
                }
                _Points.Value = null;
                _MinMZ.Value = MinMz;
                _MaxMZ.Value = MaxMz;
                Insert.ExecuteNonQuery();
            }
        }

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
