using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SQLite;
using RawMSBox;

namespace Targeted_Features
{
    public class Target{
        public double MZ;
        public double RT;
        public int Charge;
        public string Name;
        public string Desc;
        public int ID;
        public int IonID;
        public int C13toCheck;
        public int N15toCheck;
        public double RTMin;
        public double RTMax;
        public double FullRTMin;
        public double FullRTMax;
        public Feature Feature;
        public int Mode;
        public string Adduct;

        //?? string ChemFormulae
        //??Class result
        public static List<Target> ReadTargets(string FileName){
            //tab-separated text file with caption - Order of columns - orbitrary 
            //captions in small or big letters
            //Additional columns are ignopred 
            //Captions Name,MZ,RT,Charge 
            //Charge by default = 1
            //Name by default "MZ-xxx,RT-xxx"
            //empty strings are ignored 
            //not tab separated strings are ignored
            int LineCount = 0;
            //пока табов меньше пяти - это строки заголовка - отматываем их
            StreamReader sr = new StreamReader(FileName);
            List<string> Tokens = new List<string>();
            while( !sr.EndOfStream){
                string str = sr.ReadLine();
                LineCount++;
                Tokens = new List<string>(str.Split(new char[] {'\t'}));
                if (Tokens.Count >= 3 && !Tokens.Contains("")){
                    break;
                }
            }

            if (sr.EndOfStream){
                Exception e = new Exception("Wrong text file format. No data found");
                throw (e);
            }

            //в верхний регистр и обрезать
            for ( int i = 0 ; i < Tokens.Count ; i++){
                Tokens[i] = Tokens[i].ToUpper().Trim();
            }
            //на выходе - заголовок таблицы 
            int[] Indexes = new int[9];
            Indexes[0] = Tokens.IndexOf("NAME");
            Indexes[1] = Tokens.IndexOf("MZ");
            Indexes[2] = Tokens.IndexOf("RT");
            Indexes[3] = Tokens.IndexOf("CHARGE");
            Indexes[4] = Tokens.IndexOf("RTMIN");
            Indexes[5] = Tokens.IndexOf("RTMAX");
            Indexes[6] = Tokens.IndexOf("C13TOCHECK");
            Indexes[7] = Tokens.IndexOf("N15TOCHECK");
            Indexes[8] = Tokens.IndexOf("DESC");
            if (Indexes[1] == -1 ){
                Exception e = new Exception("Insufficient data. No \"MZ\" column");
                throw (e);
            }
            List<Target> Targets = new List<Target>();
            while( !sr.EndOfStream){
                string str = sr.ReadLine();
                LineCount++;
                Tokens = new List<string>(str.Split(new char[] {'\t'}));
                if (Tokens.Count < 2){
                    continue;
                }
                try{
                    Target T = new Target();
                    T.MZ = Convert.ToDouble(Tokens[Indexes[1]]);
                    T.RT = (Indexes[2] != -1) ? Convert.ToDouble(Tokens[Indexes[2]]):0.0;
                    T.Name = (Indexes[0] != -1) ? Tokens[Indexes[0]] : "MZ - " + Tokens[Indexes[1]] + ";RT - " + Tokens[Indexes[2]];
                    T.Charge = (Indexes[3] != -1) ? Convert.ToInt32(Tokens[Indexes[3]]):1;
                    T.RTMin = (Indexes[4] != -1 && Tokens[Indexes[4]].Trim() != "" ) ? Convert.ToDouble(Tokens[Indexes[4]]) : 0.0;
                    T.RTMax = (Indexes[5] != -1 && Tokens[Indexes[5]].Trim() != "" ) ? Convert.ToDouble(Tokens[Indexes[5]]) : 0.0;
                    if (T.RTMin != 0.0 && T.RTMax != 0.0) T.RT = (T.RTMin + T.RTMax) / 2;
                    if (T.RTMin == 0.0) T.RTMin = T.RT - Program.RTError; 
                    if (T.RTMax == 0.0) T.RTMax = T.RT + Program.RTError; 
                    T.C13toCheck = (Indexes[6] != -1 && Tokens[Indexes[6]].Trim() != "" ) ? Convert.ToInt32(Tokens[Indexes[6]]) : Program.C13toCheck;
                    T.N15toCheck = (Indexes[7] != -1 && Tokens[Indexes[7]].Trim() != "" ) ? Convert.ToInt32(Tokens[Indexes[7]]) : Program.N15toCheck;
                    if (Program.C13Only) T.N15toCheck = 0;
                    //if (String.Trim(Tokens[4]))
                    T.Desc = (Indexes[8] != -1) ? Tokens[Indexes[8]]:"";
                    T.ID = Targets.Count;
                    Targets.Add(T);
                }catch(IndexOutOfRangeException){
                    Exception ex = new Exception("File parsing error - Check column consistency.");
                    throw ex;
                }catch{
                    Exception ex = new Exception("File parsing error - Check data format for string "+LineCount.ToString()+".");
                    throw ex;
                }
            }
            return Targets;
        }

        public static List<Target> ReadTargets(SQLiteConnection con){
            List<Target> Targets = new List<Target>();
            SQLiteCommand Select = new SQLiteCommand(
                "Select Targets.TargetID, Name, Desc, Ions.MZ, RT, RTMin, RTMax, FullRTMin, FullRTMax, C13ToCheck, N15ToCheck, Adduct, Mode, IonID  "+
                "From Targets, Ions Where Targets.TargetID=Ions.TargetID",con);
            SQLiteDataReader Reader = Select.ExecuteReader();

            while(Reader.Read()){
                Target T = new Target();
                T.ID = Reader.GetInt32(0);
                T.Name = Reader[1].ToString();
                T.Desc = Reader[2].ToString();
                T.MZ = Reader.GetDouble(3);
                T.RT = Reader.GetDouble(4);
                T.RTMin = Reader.GetDouble(5);
                T.RTMax = Reader.GetDouble(6);
                T.FullRTMin = Reader.IsDBNull(7) ? 0.0 : Reader.GetDouble(7);
                T.FullRTMax = Reader.IsDBNull(8) ? 0.0 : Reader.GetDouble(8);
                T.C13toCheck = Reader.GetInt32(9);
                T.N15toCheck = Reader.GetInt32(10);
                T.Adduct = Reader.GetString(11);
                T.Mode = (Reader.GetString(12) == "+")?1:((Reader.GetString(12) == "-")?-1:0);
                T.IonID = Reader.GetInt32(13);
                T.Charge = 1;
                if (T.FullRTMax > 0.0) {
                    Program.BackSP = true;
                    Program.Simple = true;
                }
                Targets.Add(T);
            }
            //Фильтруем не найденные в первом проходе
            if(Program.BackSP) {
                for(int i = Targets.Count -1 ; i >= 0 ; i--) {
                    if (Targets[i].FullRTMax == 0.0) {
                        Targets.RemoveAt(i);
                    }
                }
            }

            return Targets;
        }

        public static Target TargetFromPoint(MZData Point, int ID){
            if (Point == null) return null;
            Target T = new Target();
            T.ID = ID;
            T.Name = String.Format("Target #{0}", ID);
            T.Desc = String.Format("Target #{0}, Init abund:{1}, MZ :{2}, RT :{3}", ID,Point.Intensity,Point.Mass,Point.RT);
            T.MZ = Point.Mass;
            T.RT = Point.RT;
            T.RTMin = Point.RT-Program.RTError;
            T.RTMax = Point.RT+Program.RTError;
            T.C13toCheck = Program.C13toCheck;
            T.N15toCheck = Program.N15toCheck;
            T.Charge = 1;
            return T;
        }

        public static Target TargetFromPeak(Peak P, int ID){
            if (P == null) return null;
            Target T = new Target();
            T.ID = ID;
            T.Name = String.Format("Target #{0}", ID);
            T.Desc = String.Format("Target #{0}, Init abund:{1}, MZ :{2}, RT :{3}", ID,P.ApexIntensity,P.ApexMass,P.Apex);
            T.MZ = P.ApexMass;
            T.RT = P.Apex;
            T.RTMin = P.Left;
            T.RTMax = P.Right;
            T.C13toCheck = Program.C13toCheck;
            T.N15toCheck = Program.N15toCheck;
            T.Charge = 1;
            return T;
        }

        public void SaveDB(SQLiteConnection con){
            SQLiteCommand Insert = new SQLiteCommand(
                String.Format("INSERT INTO Targets (TargetID, Name, MZ, RT, Charge, Desc, RTMin, RTMax, C13ToCheck, N15ToCheck) "+
                "Values ( {0}, \"{1}\", {2} , {3}, {4}, \"{5}\" , {6}, {7}, {8}, {9} )",
                ID, Name, MZ, RT, Charge, Desc, RTMin, RTMax, C13toCheck, N15toCheck),con);
            Insert.ExecuteNonQuery();
        }
        
        public class byMZ : IComparer<Target> {
            public int Compare(Target x, Target y){
                if (x.MZ<y.MZ) { return -1;} 
                if (x.MZ>y.MZ) { return 1;} 
                return 0;
            }
        }

    }
}