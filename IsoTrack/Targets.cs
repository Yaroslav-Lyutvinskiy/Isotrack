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

namespace IsoTrack
{
    
    public class Target{
        public double MZ;
        public double RT;
        public int Charge;
        public string Name;
        public string Desc;
        public int ID;
        public int C13toCheck;
        public double RTMin;
        public double RTMax;
        //for standards isolation
        public List<Peak> Candidates = new List<Peak>();
        public int FileID = -1;
        public int Ambigous = 0; //0 - no amb; 1 - amb by definition ; 2 - amb by peak 
        public string Adduct = "";
        public int Mode = 0; //1 - positive; -1 - negative
        public int Strongs = 0;
        public int IonID = 0;
        public static int IonCounter = 0;

        public static List<Target> ReadTargets(SQLiteConnection con, bool Custom = false){
            List<Target> Targets = new List<Target>();
            SQLiteCommand Select = new SQLiteCommand(
                "Select Targets.TargetID, Name, Desc, Ions.MZ, RT, RTMin, RTMax, C13ToCheck, Adduct, Mode, IonID, CustomRTMin, CustomRTMax "+
                "Charge From Targets, Ions Where Ions.TargetID = Targets.TargetID",con);
            SQLiteDataReader Reader = Select.ExecuteReader();
            Properties.Settings Settings = Properties.Settings.Default;

            while(Reader.Read()){
                Target T = new Target();
                T.ID = Reader.GetInt32(0);
                T.Name = Reader[1].ToString();
                T.Desc = Reader[2].ToString();
                T.MZ = Reader.GetDouble(3);
                T.RT = Reader.GetDouble(4);
                T.RTMin = Reader.GetDouble(5);
                T.RTMax = Reader.GetDouble(6);
                T.C13toCheck = Reader.GetInt32(7);
                T.Adduct = Reader[8].ToString();
                switch (Reader.GetString(9)){
                    case "+": T.Mode = 1; break;
                    case "-": T.Mode = -1; break;
                    default: T.Mode = 0; break;
                }
                T.IonID = Reader.GetInt32(10);
                if (Custom && !Reader.IsDBNull(11)){
                    T.RTMin = Reader.GetDouble(11);
                    T.RTMax = Reader.GetDouble(12);
                    T.RT = (T.RTMin + T.RTMax) / 2.0;
                }
                Targets.Add(T);
            }
            return Targets;
        }


        public void SaveDB(SQLiteConnection con, bool SaveIons = true){
            //check for ID exsistance
            SQLiteCommand Check = new SQLiteCommand(String.Format("Select TargetID From Targets where TargetID = {0}",ID),con);
            SQLiteDataReader Reader = Check.ExecuteReader();
            SQLiteCommand Insert;
            int TAmb = (Ambigous % 2 == 0) ? 1 : 0;
            if (!Reader.Read()){
            String InsertTarget = String.Format("INSERT INTO Targets (TargetID, Name, RT, Desc, RTMin, RTMax, C13ToCheck, Candidates, Ambigous, MZ ) " +
                "Values ( {0}, \"{1}\", {2} , \"{3}\", {4}, {5} , {6}, {7}, {8}, {9}, {10} )",
                ID, Name, RT, Desc, RTMin, RTMax, C13toCheck, Candidates.Count, TAmb, MZ);
                Insert = new SQLiteCommand(InsertTarget,con);
                Insert.ExecuteNonQuery();
            }
            Reader.Close();
            if (SaveIons){
                String InsertIon = String.Format("INSERT INTO Ions (TargetID, IonID , MZ, Adduct, Mode, Strongs, Candidates, Ambigous ) " +
                        "Values ( {0}, {1}, {2}, \"{3}\", \"{4}\", {5} , {6}, {7} )",
                        ID, IonID, MZ, Adduct, Mode==1?'+':(Mode==-1?'-':'?'), Strongs, Candidates.Count, Ambigous);
                Insert = new SQLiteCommand(InsertIon,con);
                Insert.ExecuteNonQuery();
            }
        }

        public void SetPeakToTarget(Peak P, SQLiteConnection con){
            string Query = String.Format("Update Features Set IonID = {0} " +
                "Where FeatureID = (Select onFeatureID from Traces where TraceID = {1} ) ",
                IonID, P.TraceID);
            SQLiteCommand Update = new SQLiteCommand(Query, con);
            Update.ExecuteNonQuery();
        }

        public static void SaveToFile(List<Target> Targets, string FileName){
            StreamWriter sw = new StreamWriter(FileName,true);
            sw.WriteLine("NAME\tADDUCT\tDESC\tMZ\tRTMIN\tRTMAX\tMODE\tC13TOCHECK\t");
            foreach(Target T in Targets){
                sw.WriteLine("{0}\t \t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t",
                    T.Name,T.Desc,T.MZ,T.RTMin,T.RTMax,T.Mode>0?"+":"-",T.C13toCheck);
            }
            sw.Close();
        }

        public class byMZ : IComparer<Target> {
            public int Compare(Target x, Target y){
                if (x.MZ<y.MZ) { return -1;} 
                if (x.MZ>y.MZ) { return 1;} 
                return 0;
            }
        }

        public Target Copy(){
            Target T = new Target();
            T.ID = this.ID;
            T.Name = this.Name;
            T.Desc = this.Desc;
            T.MZ = this.MZ;
            T.RT = this.RT;
            T.RTMin = this.RTMin;
            T.RTMax = this.RTMax;
            T.C13toCheck = this.C13toCheck;
            T.Charge = this.Charge;
            T.FileID = this.FileID;
            T.Ambigous = this.Ambigous;
            T.IonID = IonCounter;
            IonCounter++;
            return T;
        }
    }
}
