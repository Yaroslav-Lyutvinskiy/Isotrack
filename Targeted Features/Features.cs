using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SQLite;

namespace Targeted_Features
{
    public class Feature {
        public LCTrace MainTrace;
        public LCTrace[] Isotopes;
        public TracePeaks TPeaks;
        public bool MainApexOutsideRtWindow = false;
        public bool HasPrevIsotope = false;
        public Target Target;
        private static int LastID =0;
        public int ID;
        public void Write(SQLiteConnection con = null, int FileID = 0){
            if (con != null){
                //check for next avialable Feature ID
                if (LastID == 0){
                    SQLiteCommand Select = new SQLiteCommand(
                        "Select max(FeatureID) From Features ",con);
                    SQLiteDataReader Reader = Select.ExecuteReader();
                    Reader.Read();
                    try {
                        LastID = Reader.GetInt32(0)+1;
                    }
                    catch(Exception){
                        LastID++;
                    }
                }else{
                    LastID++;
                }
                ID = LastID;
                SQLiteCommand Insert = new SQLiteCommand(String.Format(
                    "Insert Into Features (FeatureID, IonID, FileID, ApexOutsideRTWindow, HasPreviousIso) "+
                    "Values ( {0}, {1}, {2}, {3}, {4} )",ID, Target.IonID, FileID, MainApexOutsideRtWindow?1:0, HasPrevIsotope?1:0),con);
                Insert.ExecuteNonQuery();

                //combine and sort list
                List<LCTrace> Traces = new List<LCTrace>();
                for (int C13 = 0 ; C13 < Isotopes.GetLength(0) ; C13++){
                    if (Isotopes[C13] != null){
                        Traces.Add(Isotopes[C13]);
                    }
                }
                Traces.Sort(new LCTrace.byMass());

                for (int i = 0 ; i < Traces.Count ; i++){
                    Traces[i].SaveDB(con, this);
                }
                TPeaks.DBSave(con, this);
            }
            //File output 
        }

        public void ApplyPeak(Peak P){
            MainTrace.ApplyPeak(P, MainTrace);
            foreach(LCTrace Trace in Isotopes){
                if (Trace != null) 
                    Trace.ApplyPeak(P, MainTrace);
            }
        }
    }
}