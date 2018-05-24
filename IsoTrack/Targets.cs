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
        public int N15toCheck;
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

        public List<Target> AdductTargets = null; //only for standards

        //public Feature Feature;
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
                Tokens = new List<string>(str.Split(new char[] {'\t'},StringSplitOptions.RemoveEmptyEntries));
                if (Tokens.Count >= 3 && !Tokens.Contains("")){
                    break;
                }
            }

            if (sr.EndOfStream){
                Exception e = new Exception("Target text file wrong format. No data found");
                throw (e);
            }

            //в верхний регистр и обрезать
            for ( int i = 0 ; i < Tokens.Count ; i++){
                Tokens[i] = Tokens[i].ToUpper().Trim();
            }
            //на выходе - заголовок таблицы 
            int[] Indexes = new int[11];
            Indexes[0] = Tokens.IndexOf("NAME");
            Indexes[1] = Tokens.IndexOf("MZ");
            Indexes[2] = Tokens.IndexOf("RT");
            Indexes[3] = Tokens.IndexOf("CHARGE");
            Indexes[4] = Tokens.IndexOf("RTMIN");
            Indexes[5] = Tokens.IndexOf("RTMAX");
            Indexes[6] = Tokens.IndexOf("C13TOCHECK");
            Indexes[7] = Tokens.IndexOf("N15TOCHECK");
            Indexes[8] = Tokens.IndexOf("DESC");
            Indexes[9] = Tokens.IndexOf("ADDUCT");
            Indexes[10] = Tokens.IndexOf("MODE");
            if (Indexes[1] == -1 ){
                Exception e = new Exception("Target text file insufficient data. No \"MZ\" column");
                throw (e);
            }

            List<Target> Targets = new List<Target>();
            Properties.Settings Settings = Properties.Settings.Default;
            int TargetCounter = 0;
            string Duplicates = "";
            while( !sr.EndOfStream){
                string str = sr.ReadLine();
                LineCount++;
                Tokens = new List<string>(str.Split(new char[] {'\t'},StringSplitOptions.RemoveEmptyEntries));
                for(int i = 0 ; i < Tokens.Count ; i++)
                    Tokens[i] = Tokens[i].Trim();
                if (Tokens.Count < 2){
                    continue;
                }
                try{
                    Target T = new Target();
                    T.MZ = Convert.ToDouble(Tokens[Indexes[1]]);
                    T.RT = (Indexes[2] != -1) ? Convert.ToDouble(Tokens[Indexes[2]]):0.0;
                    T.Name = (Indexes[0] != -1) ? Tokens[Indexes[0]].Trim() : "MZ - " + Tokens[Indexes[1]] + ";RT - " + Tokens[Indexes[2]];
                    T.Charge = (Indexes[3] != -1) ? Convert.ToInt32(Tokens[Indexes[3]]):1;
                    T.RTMin = (Indexes[4] != -1 && Tokens[Indexes[4]].Trim() != "" ) ? Convert.ToDouble(Tokens[Indexes[4]]) : 0.0;
                    T.RTMax = (Indexes[5] != -1 && Tokens[Indexes[5]].Trim() != "" ) ? Convert.ToDouble(Tokens[Indexes[5]]) : 0.0;
                    if (T.RTMin != 0.0 && T.RTMax != 0.0) T.RT = (T.RTMin + T.RTMax) / 2;
                    if (T.RTMin == 0.0) T.RTMin = T.RT - Settings.RTError; 
                    if (T.RTMax == 0.0) T.RTMax = T.RT + Settings.RTError; 
                    T.C13toCheck = (Indexes[6] != -1 && Tokens[Indexes[6]].Trim() != "" ) ? Convert.ToInt32(Tokens[Indexes[6]]) : Settings.C13_to_Check;
                    T.N15toCheck = (Indexes[7] != -1 && Tokens[Indexes[7]].Trim() != "" ) ? Convert.ToInt32(Tokens[Indexes[7]]) : Settings.N15_to_Check;
                    if (Settings.C13Only) T.N15toCheck = 0;
                    //if (String.Trim(Tokens[4]))
                    T.Desc = (Indexes[8] != -1) ? Tokens[Indexes[8]]:"";
                    T.Adduct = (Indexes[9] != -1) ? Tokens[Indexes[9]] : "";
                    T.Mode = (Indexes[10] != -1) ? (Tokens[Indexes[10]] == "-"?-1:1) : 0;

                    //TargetID and IonID
                    T.ID = -1;
                    foreach(Target RT in Targets){
                        if (RT.Name == T.Name){
                            T.ID = RT.ID;
                            break;
                        }
                    }
                    if (T.ID == -1){
                        T.ID = TargetCounter;
                        TargetCounter++;
                    }
                    T.IonID = Targets.Count;

                    while (T.Name.IndexOf("\"") != -1)
                        T.Name = T.Name.Remove(T.Name.IndexOf("\""), 1);
                    while (T.Desc.IndexOf("\"") != -1)
                        T.Desc = T.Desc.Remove(T.Desc.IndexOf("\""), 1);
                    //check for uniqueness of new target
                    if (Targets.SingleOrDefault(TD => TD.Name == T.Name && TD.Adduct == T.Adduct) != null){
                        Duplicates+=T.Name + " (" + T.Adduct + ")\n";
                    }
                    Targets.Add(T);
                }catch(IndexOutOfRangeException){
                    Exception ex = new Exception("Target text file parsing error - Check column consistency.");
                    throw ex;
                }catch{
                    Exception ex = new Exception("Target text file parsing error - Check data format for string "+LineCount.ToString()+".");
                    throw ex;
                }
            }
            if (Duplicates != ""){
                    Exception ex = new Exception("Some targets are duplicated in a list:\n"+Duplicates+"Please, remove duplicates.");
                    throw ex;
            }
            return Targets;
        }

        public static List<Target> ReadTargets(SQLiteConnection con, bool Custom = false){
            List<Target> Targets = new List<Target>();
            SQLiteCommand Select = new SQLiteCommand(
                "Select Targets.TargetID, Name, Desc, Ions.MZ, RT, RTMin, RTMax, C13ToCheck, N15ToCheck, Adduct, Mode, IonID, CustomRTMin, CustomRTMax "+
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
                T.N15toCheck = Reader.GetInt32(8);
                if (Settings.C13Only) T.N15toCheck = 0;
                //T.Charge = Reader.GetInt32(9);
                T.Adduct = Reader[9].ToString();
                switch (Reader.GetString(10)){
                    case "+": T.Mode = 1; break;
                    case "-": T.Mode = -1; break;
                    default: T.Mode = 0; break;
                }
                T.IonID = Reader.GetInt32(11);
                if (Custom && !Reader.IsDBNull(12)){
                    T.RTMin = Reader.GetDouble(12);
                    T.RTMax = Reader.GetDouble(13);
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
            String InsertTarget = String.Format("INSERT INTO Targets (TargetID, Name, RT, Desc, RTMin, RTMax, C13ToCheck, N15ToCheck, Candidates, Ambigous, MZ ) " +
                "Values ( {0}, \"{1}\", {2} , \"{3}\", {4}, {5} , {6}, {7}, {8}, {9}, {10} )",
                ID, Name, RT, Desc, RTMin, RTMax, C13toCheck, N15toCheck, Candidates.Count, TAmb, MZ);
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

        public void ChangeDB(SQLiteConnection con){
            String Query = String.Format("Update Targets Set Name = \"{1}\", MZ = {2}, RT = {3}, "+
                "Charge = {4}, Desc = \"{5}\", RTMin = {6}, RTMax = {7}, C13ToCheck = {8}, N15ToCheck = {9}) " +
                "Where TargetID = {0} ",
                ID, Name, MZ, RT, Charge, Desc, RTMin, RTMax, C13toCheck, N15toCheck);
            SQLiteCommand Insert = new SQLiteCommand(Query,con);
            Insert.ExecuteNonQuery();
        }

        public void SetPeakToTarget(Peak P, SQLiteConnection con){
            string Query = String.Format("Update Features Set IonID = {0} " +
                "Where FeatureID = (Select onFeatureID from Traces where TraceID = {1} ) ",
                IonID, P.TraceID);
            SQLiteCommand Update = new SQLiteCommand(Query, con);
            Update.ExecuteNonQuery();
        }

        public static void SaveToFile(List<Target> Targets, string FileName){
            StreamWriter sw = new StreamWriter(FileName);
            sw.WriteLine("NAME\tDESC\tMZ\tRTMIN\tRT\tRTMAX\tCHARGE\tC13TOCHECK\tN15TOCHECK\t");
            foreach(Target T in Targets){
                sw.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t",
                    T.Name,T.Desc,T.MZ,T.RTMin,T.RT,T.RTMax,T.Charge,T.C13toCheck,T.N15toCheck);
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
            T.N15toCheck = this.N15toCheck;
            T.Charge = this.Charge;
            T.FileID = this.FileID;
            T.Ambigous = this.Ambigous;
            T.IonID = IonCounter;
            IonCounter++;
            return T;
        }


    }
}
