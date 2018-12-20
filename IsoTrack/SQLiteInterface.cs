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
    class SQLiteInterface{
        public SQLiteConnection con;
        public SQLiteTransaction tr;

        public void InitDB(string DBName){
            con = new SQLiteConnection(String.Format("Data Source = {0}",DBName));
            con.Open();
        }

        public SQLiteConnection CreateDB(string DBName){
            SQLiteConnection.CreateFile(DBName);
            con = new SQLiteConnection(String.Format("Data Source ={0}",DBName));
            con.Open();
            SQLiteCommand com = new SQLiteCommand(
                "CREATE TABLE [Features] ("+
                "  [FeatureID] INT, "+
                "  [IonID] INT, "+
                "  [FileID] INT, "+
                "  [HasPreviousIso] BOOLEAN, "+
                "  [ApexOutsideRTWindow] BOOLEAN);",con);
            com.ExecuteNonQuery();
            com = new SQLiteCommand(
                "CREATE TABLE [Files] ("+
                "  [FileIndex] INT, "+
                "  [Mode] INT, "+
                "  [FileName] VARCHAR(256));",con);
            com.ExecuteNonQuery();
            //com = new SQLiteCommand(
            //    "CREATE TABLE [Points] ("+
            //    "  [GroupID] INT, "+
            //    "  [MZ] DOUBLE, "+
            //    "  [RT] DOUBLE, "+
            //    "  [Intensity] DOUBLE, "+
            //    "  [TimeCoeff] DOUBLE, "+
            //    "  [Scan] INT);",con);
            //com.ExecuteNonQuery();
            com = new SQLiteCommand(
                "CREATE TABLE [Settings] ("+
                "  [Name] VARCHAR(30), "+
                "  [Value] VARCHAR(30));",con);
            com.ExecuteNonQuery();
            com = new SQLiteCommand(
                "CREATE TABLE [Traces] ("+
                "  [TraceID] INT, "+
                "  [onFeatureID] INT, "+
                "  [GroupID] INT, "+
                "  [IsotopeAttribution] VARCHAR(30), "+
                "  [Mono] BOOL, "+
                "  [TotalIntensity] DOUBLE, "+
                "  [MeanMass] DOUBLE, "+
                "  [FullCorrelation] DOUBLE, "+
                "  [CorrelationTo] CHAR, "+
                "  [PPMError] DOUBLE, "+
                "  [MinRT] DOUBLE, "+
                "  [MaxRT] DOUBLE, "+
                "  [StartRT] DOUBLE, "+
                "  [StartIntensity] CHAR, "+
                "  [StartMass] DOUBLE, "+
                "  [ApexRT] DOUBLE, "+
                "  [ApexIntensity] DOUBLE, "+
                "  [ApexMass] DOUBLE, "+
                "  [ApexCount] INT, "+
                "  [MeanRatio] DOUBLE, "+
                "  [MZDeviationE] CHAR, "+
                "  [MZDeviationRT] CHAR, "+
                "  [RatioDeviationE] DOUBLE, "+
                "  [RatioDeviationRT] DOUBLE, "+
                "  [PeakCorrelation] DOUBLE, "+
                "  [PeakTotal] DOUBLE, "+
                "  [PeakRatio] DOUBLE, "+
                "  [PeakMeanMass] DOUBLE, "+
                "  [InnerIntensity] DOUBLE);",con);
            com.ExecuteNonQuery();

            com = new SQLiteCommand(
                "CREATE TABLE [RTPeaks] ("+
                "  [TraceID] INT, "+
                "  [PeakNumber] INT, "+
                "  [Main] INT, "+
                "  [TracePeak] INT, "+
                "  [Left] DOUBLE, "+
                "  [Right] DOUBLE, "+
                "  [SNRatio] DOUBLE, "+
                "  [ApexIntensity] DOUBLE, "+
                "  [Apex] DOUBLE);",con);
            com.ExecuteNonQuery();

            com = new SQLiteCommand(
                "CREATE TABLE [PointGroups] ("+
                "  [GroupID] INT, "+
                "  [FileID] INT, "+
                "  [StartScan] INT, "+
                "  [EndScan] INT, "+
                "  [MinMZ] DOUBLE, "+
                "  [MaxMZ] DOUBLE, "+
                "  [Points] TEXT);",con);
            com.ExecuteNonQuery();

            com = new SQLiteCommand(
                "CREATE TABLE [Spectra] ("+
                "  [ScanNumber] INT, "+
                "  [FileIndex] INT, "+
                "  [RT] DOUBLE, "+
                "  [MSOnly] INT, "+
                "  [TimeCoef] DOUBLE);",con);
            com.ExecuteNonQuery();

            //Reselect Target to top table (with meaning of standards)
            com = new SQLiteCommand(
                "CREATE TABLE [Targets] ("+ 
                "  [TargetID] INT, "+
                "  [Name] VARCHAR(50), "+  
                "  [Desc] VARCHAR(128), "+ 
                "  [MZ] DOUBLE, "+
                "  [RT] DOUBLE, "+      
                "  [RTMin] DOUBLE, "+   
                "  [RTMax] DOUBLE, "+   
                "  [CustomRTMin] DOUBLE, "+   
                "  [CustomRTMax] DOUBLE, "+   
                "  [FullRTMin] DOUBLE, "+   
                "  [FullRTMax] DOUBLE, "+   
                "  [C13ToCheck] INT, "+ 
                "  [N15ToCheck] INT, "+ 
                "  [Candidates] INT, "+ //Standard refinement specific attr - here is a number of same-RT groups
                "  [Ambigous] INT); ",con);   //on MZ without attention to adducts - Standard refinement specific attr 
            com.ExecuteNonQuery();

            com = new SQLiteCommand(
                "CREATE TABLE [Ions] ("+ 
                "  [TargetID] INT, "+
                "  [IonID] INT, "+
                "  [Adduct] VARCHAR(10), "+ 
                "  [ToShow] VARCHAR(10), "+ 
                "  [Mode] CHAR(1), "+       
                "  [MZ] DOUBLE, "+          //is not Adduct attr if MZ fact    
                "  [Strongs] INT, "+   //Standard refinement specific attr - statistics
                "  [Candidates] INT, "+ //Standard refinement specific attr - statistics 
                "  [Ambigous] INT) ",con);   //on MZ with attention to adducts - Standard refinement specific attr  - statistics
            com.ExecuteNonQuery();
               
            //Can be one more entity for RT candidate 
            //with sum score - (based on number of MZ candidates)

            com = new SQLiteCommand(
                "CREATE TABLE [Candidates] ("+
                "  [IonID] INT, "+ //Link to target 
                "  [CandID] INT, "+
                "  [MZ] DOUBLE, "+ 
                "  [FileID] INT, "+     //Link to factual signal + uniqueness 
                "  [TraceID] INT, "+    //Link to factual signal
                "  [PeakNumber] INT, "+ //Link to factual signal
                "  [Selected] INT, "+ //currenlty - prefered for standard with particular MZ, should be - for RTGroup 
                "  [Recomended] INT, "+ //+ recomended ?? (one in group of MZ for the same RT)
                "  [SameRTID] int, "+ //?? group of signals with the same RT (different only by adducts)
                "  [Major] INT, "+   //most intensive on particular MZ in standard File
                "  [SumScore] double, "+
                "  [Score] double );",con);//??
            com.ExecuteNonQuery();

            // Here are two hidden entities 
            // Same-RT groups - candidates for the same standard with the same RT (differs only by adducts)
            // Adducts 
            //Relation Targets-FileIDs

            //Or it is better to define on the fly??
            com = new SQLiteCommand(
                "CREATE TABLE [Intersects] ("+
                "  [LeftStandardID] INT, "+
                "  [LeftCandID] INT, "+
                "  [RightStandardID] INT, "+
                "  [RightCandID] INT);",con);
            com.ExecuteNonQuery();

            com = new SQLiteCommand("CREATE TABLE [Report] ("+
                " OrderID INT, "+
                " PosFile INT, "+
                " NegFile  INT, "+
                " Reported Int, "+
                " ShortName VARCHAR(64), "+
                " Color Int );",con);
            com.ExecuteNonQuery();

            return con;
        }

        public void CreateIndexes(){
            //SQLiteCommand com = new SQLiteCommand("CREATE INDEX [iTraceRT] ON [Points] ([GroupID], [RT])",con);
            //com.ExecuteNonQuery();
            try{
                SQLiteCommand com = new SQLiteCommand("CREATE INDEX [onFeature] ON [Traces] ([onFeatureID]);",con);
                com.ExecuteNonQuery();
                com = new SQLiteCommand("CREATE INDEX [onID] ON [Features] ([FeatureID]);",con);
                com.ExecuteNonQuery();
                com = new SQLiteCommand("CREATE INDEX [onTraceID] ON [Traces] ([TraceID]);",con);
                com.ExecuteNonQuery();
                com = new SQLiteCommand("CREATE INDEX [OnTracePeakNum] ON [RTPeaks] ([TraceID], [PeakNumber]);",con);
                com.ExecuteNonQuery();
                com = new SQLiteCommand("CREATE INDEX [OnFileScan] ON [Spectra] ([FileIndex], [ScanNumber]);",con);
                com.ExecuteNonQuery();
                com = new SQLiteCommand("CREATE INDEX [onGroupID] ON [PointGroups] ([GroupID]);",con);
                com.ExecuteNonQuery();
                com = new SQLiteCommand("CREATE INDEX [onTargetID] ON [Targets] ([TargetID]);",con);
                com.ExecuteNonQuery();
                com = new SQLiteCommand("CREATE INDEX [onFileMZ] ON [PointGroups] ([FileID],[MinMZ]);",con);
                com.ExecuteNonQuery();
                com = new SQLiteCommand("CREATE INDEX [onMZ] ON [PointGroups] ([MinMZ]);",con);
                com.ExecuteNonQuery();
            }
            catch (Exception) { };
        }

        public void SaveSetting(string Name, string Value){
            SQLiteCommand Insert = new SQLiteCommand(
                "INSERT INTO Settings (Name, Value) "+
                "Values ( @Name, @Value)",con);
            SQLiteParameter _Name = new SQLiteParameter("@Name");
            Insert.Parameters.Add(_Name);
            SQLiteParameter _Value = new SQLiteParameter("@Value");
            Insert.Parameters.Add(_Value);
            _Name.Value = Name;
            _Value.Value = Value;
            Insert.ExecuteNonQuery();
        }
    }
}
