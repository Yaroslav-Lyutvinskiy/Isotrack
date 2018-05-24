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
