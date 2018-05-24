using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace IsoTrack.MasterForms
{
    public partial class AdductsForm : Form
    {
        public AdductsForm()
        {
            InitializeComponent();
        }

        public class Adduct{
            public string Name;
            public char Mode;
            public double Mass;
        }

        public static List<Adduct> Adducts = new List<Adduct>();

        public static void ReadAdducts(){
            StreamReader sr = new StreamReader("Adducts.txt");
            string[] Tokens;
            try{
                while(!sr.EndOfStream){
                    string S = sr.ReadLine();
                    Tokens = S.Split(new char[] { '\t' },StringSplitOptions.RemoveEmptyEntries);
                    if (Tokens.Length != 3) continue;
                    Adduct A = new Adduct();
                    A.Name = Tokens[0].Trim();
                    A.Mode = Tokens[1].Trim()[0];
                    A.Mass = Convert.ToDouble(Tokens[2].Trim());
                    Adducts.Add(A);
                    RegAdducts++;
                }
            }catch(Exception e){
                MessageBox.Show("Adduct file parsing error: " + e.Message + " Adducts have not been loaded");
            }
            sr.Close();
        }

        static int RegAdducts = 0;

        public void Setup(string AdductsProp){
            //standard adducts
            foreach (Adduct A in Adducts){
                AdductView.Rows.Add(A.Name,A.Mode.ToString(),A.Mass,false);
            }
            for (int i = 0 ; i < AdductView.RowCount-1 ; i++){
                AdductView.Rows[i].ReadOnly = true;
                AdductView.Rows[i].Cells[3].ReadOnly = false;
                AdductView.Rows[i].Cells[3].Value = AdductsProp.Contains(AdductView.Rows[i].Cells[0].Value as string); 
            }
            //custom adducts
            string[] Tokens = AdductsProp.Split(new char[] { ';' });
            for (int i = 0 ; i < Tokens.Length ; i++){
                if (Tokens[i].Trim().IndexOf("Custom")==0){
                    string[] CustAdd = Tokens[i].Split(new char[] { ':', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    AdductView.Rows.Add(CustAdd[1].Trim(), CustAdd[2].Trim(), CustAdd[3].Trim(), true);
                }
            }
        }

        public string GetAdducts(){
            string S = "";
            for ( int i = 0 ; i < RegAdducts ; i++){
                if ((bool)AdductView.Rows[i].Cells[3].Value)
                    S += AdductView.Rows[i].Cells[0].Value + "; ";
            }
            for ( int i = RegAdducts ; i < AdductView.RowCount-1 ; i++){
                if (AdductView.Rows[i].Cells[0].Value != null && (bool)AdductView.Rows[i].Cells[3].Value){
                    S += "Custom:" +
                        AdductView.Rows[i].Cells[0].Value.ToString() + "," +
                        AdductView.Rows[i].Cells[1].Value.ToString() + "," +
                        AdductView.Rows[i].Cells[2].Value.ToString() + "; ";
                }
            }
            return S;
        }

        //default value processing
        private void AdductView_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e){
            if (AdductView.IsCurrentCellInEditMode){
                AdductView.CurrentRow.Cells[1].Value = "+";
                AdductView.CurrentRow.Cells[2].Value = 0.0;
                AdductView.CurrentRow.Cells[3].Value = true;
            }
        }
    }
}
