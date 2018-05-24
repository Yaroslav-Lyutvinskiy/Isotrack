using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
//using MySql.Data.MySqlClient;

namespace IsoTrack.MasterForms
{
    public partial class LoadTargetListForm : Form
    {
        public LoadTargetListForm()
        {
            InitializeComponent();
        }

        private void TextButton_Enter(object sender, EventArgs e)
        {
            label1.Text = "Load target list from predesigned tab-separated text file";
        }

        private void DB3Button_Enter(object sender, EventArgs e)
        {
            label1.Text = "Load target list from db3 filel formed in one of previous sessions";
        }

        private void MySQLButton_Enter(object sender, EventArgs e)
        {
            label1.Text = "Load target list from MySQL data repository";
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (TextFileDialog.ShowDialog()==DialogResult.OK){
                textBox1.Text = TextFileDialog.FileName;
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (DB3FileDialog.ShowDialog()==DialogResult.OK){
                textBox2.Text = DB3FileDialog.FileName;
            }
        }

        public bool Setup(string Prop){
            //parse incoming string of format file|db3|mysql:FileName|(Method|FileSet:Name)
            try{
                string[] Tokens = Prop.Split(new char[] { '|' });
                switch (Tokens[0]){
                    case "file":{
                        TextButton.Checked = true;
                        textBox1.Text = Tokens[1];
                        return true;
                    }
                    case "db3":{
                        DB3Button.Checked = true;
                        textBox2.Text = Tokens[1];
                        return true;
                    }
                    case "mysql":{
                        DB3Button.Checked = true;
                        comboBox1.Text = Tokens[1];
                        comboBox2.Text = Tokens[2];
                        return true;
                    }
                    default: return false;
                }
            }catch(Exception){
                return false;
            }
        }

        public string GetTargets(){
            if (TextButton.Checked) return "file|" + textBox1.Text;
            if (DB3Button.Checked) return "db3|" + textBox2.Text;
            if (MySQLButton.Checked) return "mysql|" + comboBox1.Text+"|"+comboBox2.Text;
            return "";
        }

        public void FromMaster(){
            button2.Enabled = false;
            button2.Visible = false;
        }

        public void FromGrid(){
            button3.Enabled = false;
            button3.Visible = false;
            button4.Enabled = false;
            button4.Visible = false;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!Validate()) return;
            DialogResult = DialogResult.OK;
            Close();
        }

        private bool Validate(){
            if (TextButton.Checked){
                if (!File.Exists(textBox1.Text)){
                    MessageBox.Show("Existing text file has to be selected if text file source has been choosen",Text);
                    return false;
                }else{
                    return true;
                }
            }
            if (DB3Button.Checked){
                if (!File.Exists(textBox2.Text)){
                    MessageBox.Show("Existing db3 file has to be selected if db3 file source has been choosen",Text);
                    return false;
                }else{
                    return true;
                }
            }
            if (MySQLButton.Checked){
                if (comboBox1.Text == "" && comboBox2.Text == ""){
                    MessageBox.Show("Method or FileSet has to be selected if mySQL database source has been choosen",Text);
                    return false;
                }else{
                    return true;
                }
            }
            return true;
        }

        private void LoadTargetListForm_Load(object sender, EventArgs e)
        {
            try{
                LoadMySQL();
            }catch(Exception){
                MySQLButton.Enabled=false;
            }
        }

        void LoadMySQL(){
            string ConnStr = IsoTrack.Properties.Settings.Default.MySQLConnString;
            //MySqlConnection conn = new MySqlConnection(ConnStr);
            //conn.Open();
            //MySqlCommand com = new MySqlCommand("SELECT id FROM lcms.method",conn);
            //MySqlDataReader Reader = com.ExecuteReader();
            //while(Reader.Read()){
            //    comboBox1.Items.Add(Reader.GetString(0));
            //}
            //Reader.Close();
            //com = new MySqlCommand("SELECT id FROM lcms.mass_spec_dataset",conn);
            //Reader = com.ExecuteReader();
            //while(Reader.Read()){
            //    comboBox2.Items.Add(Reader.GetString(0));
            //}                
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBox2.Text = "";
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBox1.Text = "";
        }


        public bool NextOrPrev = true;

        private void button4_Click(object sender, EventArgs e)
        {
            if (!Validate()) return;
            DialogResult = DialogResult.OK;
            NextOrPrev = false;
            Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (!Validate()) return;
            DialogResult = DialogResult.OK;
            NextOrPrev = true;
            Close();
        }


    }
}
