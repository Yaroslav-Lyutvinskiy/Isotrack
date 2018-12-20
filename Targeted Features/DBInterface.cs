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

namespace Targeted_Features
{
    class DBInterface
    {
        public SQLiteConnection con;
        public SQLiteTransaction tr;

        public void InitDB(string DBName){
            con = new SQLiteConnection(String.Format("Data Source = {0}",DBName));
            con.Open();
        }


        public void SaveFile(string FileName, int ID, int Mode){
            SQLiteCommand Insert = new SQLiteCommand(
                "INSERT INTO Files (FileName, Mode, FileIndex) "+
                "Values ( @FileName, @Mode, @FileIndex)",con);
            SQLiteParameter _Name = new SQLiteParameter("@FileName");
            Insert.Parameters.Add(_Name);
            SQLiteParameter _Index = new SQLiteParameter("@FileIndex");
            Insert.Parameters.Add(_Index);
            SQLiteParameter _Mode = new SQLiteParameter("@Mode");
            Insert.Parameters.Add(_Mode);
            _Name.Value = FileName;
            _Index.Value = ID;
            _Mode.Value = Mode;
            Insert.ExecuteNonQuery();
        }

        public string GetParameter(string Name){
            SQLiteCommand Select = new SQLiteCommand(
                "Select Value from Settings Where Name = @Name ",con);
            SQLiteParameter _Name = new SQLiteParameter("@Name");
            Select.Parameters.Add(_Name);
            _Name.Value = Name;
            SQLiteDataReader Reader = Select.ExecuteReader();
            Reader.Read();
            return Reader[0].ToString();
        }

        public int GetGroupBase(){
            SQLiteCommand Select = new SQLiteCommand(
                "Select Max(GroupID) from Traces ",con);
            SQLiteDataReader Reader = Select.ExecuteReader();
            Reader.Read();
            if (Reader.IsDBNull(0)){
                return 0;
            }else{
                return Reader.GetInt32(0)+1;
            }
        }


    }
}
