﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace IsoTrack.MasterForms
{
    public partial class LoadFileList : Form
    {
        public LoadFileList()
        {
            InitializeComponent();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!Validate()) return;
            DialogResult = DialogResult.OK;
            Close();
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

        private void button6_Click(object sender, EventArgs e)
        {
            try{
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                dialog.Multiselect = true;
                dialog.RestoreDirectory = true;
                dialog.DefaultExtension = "raw";
                dialog.Filters.Add(new CommonFileDialogFilter("Thermo Raw Files (*.raw)","*.raw"));
                Enabled = false;
                CommonFileDialogResult result = dialog.ShowDialog();
                Enabled = true;
                Focus();
                foreach (string FileName in dialog.FileNames){
                    bool Flag = false;
                    for (int j = 0; j < FileList.Items.Count; j++ ){
                        if ( FileList.Items[j].ToString() == FileName) {
                            Flag = true;
                            break;
                        }
                    }
                    if (!Flag){
                        FileList.Items.Add(FileName);
                    }
                }
            }catch(Exception){}
        }

        private void button8_Click(object sender, EventArgs e)
        {
            try{
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                dialog.IsFolderPicker = true;
                dialog.Multiselect = true;
                dialog.RestoreDirectory = true;
                Enabled = false;
                CommonFileDialogResult result = dialog.ShowDialog();
                Enabled = true;
                Focus();
                foreach (string FileName in dialog.FileNames){
                    bool Flag = !FileName.EndsWith(".d");
                    for (int j = 0; j < FileList.Items.Count; j++ ){
                        if ( FileList.Items[j].ToString() == FileName) {
                            Flag = true;
                            break;
                        }
                    }
                    if (!Flag){
                        FileList.Items.Add(FileName);
                    }
                }
            }catch(Exception){}
        }

        private void button7_Click(object sender, EventArgs e)
        {
            int SI = FileList.SelectedIndex;
            if (SI < 0) return;
            FileList.Items.RemoveAt(FileList.SelectedIndex);
            if (SI >= FileList.Items.Count) return;
            FileList.SelectedIndex = SI;
        }

        private void UpButton_Click(object sender, EventArgs e)
        {
            int SI = FileList.SelectedIndex;
            if (SI <= 0) return;
            object Selected = FileList.Items[FileList.SelectedIndex];
            FileList.Items.RemoveAt(SI);
            FileList.Items.Insert(SI - 1, Selected);
            FileList.SelectedIndex = SI - 1;
        }

        private void DownButton_Click(object sender, EventArgs e)
        {
            int SI = FileList.SelectedIndex;
            if (SI >= FileList.Items.Count-1) return;
            object Selected = FileList.Items[FileList.SelectedIndex];
            FileList.Items.RemoveAt(SI);
            FileList.Items.Insert(SI + 1, Selected);
            FileList.SelectedIndex = SI + 1;
        }

        public bool Setup(){
            //
            ListView RawList = Program.MainForm.RawList;
            FileList.Items.Clear();
            for (int i = 0 ; i < RawList.Items.Count ; i++){
                FileList.Items.Add(RawList.Items[i].Text);
            }
            return true;
        }

        public string GetProperty(){
            ListView RawList = Program.MainForm.RawList;
            //RawList.Clear();
            RawList.Items.Clear();
            for (int i = 0 ; i < FileList.Items.Count ; i++){
                ListViewItem LItem = new ListViewItem();
                LItem.Text = FileList.Items[i].ToString();
                LItem.SubItems.Add("Not processed yet.");
                RawList.Items.Add(LItem);
            }
            if (FileList.Items.Count > 0){
                return "Filled";
            }else{
                return "Empty";
            }
        }

    }
}