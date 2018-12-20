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
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System.Diagnostics;
using System.Threading;

namespace IsoTrack
{
    class ProcessesHolder
    {
        private ListView RawFiles;
        private ILogAndProgress iLog;
        private Panel Panel;

        ProcessDriver[] PDrivers;

        public ProcessesHolder(ListView RawFiles, ILogAndProgress iLog, Panel Panel){
            this.RawFiles = RawFiles;
            this.iLog = iLog;
            this.Panel = Panel;
        }

        public int CreateProcesses(int NumberOfTasks){

            NextAvialable = 0;
            Processed = 0;


            int PCount = Math.Min(Properties.Settings.Default.Processes, NumberOfTasks);
			PDrivers = new ProcessDriver[PCount];

            int PosShift = 38;
            Panel.ResumeLayout(false);
           
            int i;
            for ( i = 0 ; i < PCount ; i++ ){
				PDrivers[i] = new ProcessDriver();
				PDrivers[i].CreateControls(Panel,12,PosShift*i);
            }

            Panel.PerformLayout();
            return PCount;
        }

        int NextAvialable;
        int Processed;

        private void RunProcesses(){
            int PCount = PDrivers.GetLength(0);               

            //проверяем есть ли свободные процессы 
            if (NextAvialable < RawFiles.Items.Count){
                for(int i = 0 ; i < PCount ; i++){
                    if ( PDrivers[i].Proc == null ){
                        SetFileStatus(RawFiles.Items[NextAvialable].Text, FileStatus.Processing);
                        //!!RUN PROCESS
                        PDrivers[i].StartProcess(Properties.Settings.Default.Out_dbfile, RawFiles.Items[NextAvialable].Text, NextAvialable);
                        NextAvialable++;
                        break;
                    }
                }
            }else{
                for(int i = 0 ; i < PCount ; i++){
                    if ( PDrivers[i].Proc == null && PDrivers[i].PLabel != null && PDrivers[i].Finished ){
                        DeleteProcess(i);
                    }
                }
            }
            for(int i = 0 ; i < PCount ; i++){
                if (PDrivers[i].Proc!=null && PDrivers[i].Proc.HasExited && PDrivers[i].Finished){
					if ( PDrivers[i].Proc.ExitCode==0){
                        SetFileStatus(PDrivers[i].FileName,FileStatus.Processed);
                        iLog.Log("File has been successfully processed.",Path.GetFileName(PDrivers[i].FileName),MessageBoxIcon.Information,null);
					}else{
                        if (PDrivers[i].ErrorMessage == ""){
                            continue;
                        }else{
                            SetFileStatus(PDrivers[i].FileName,FileStatus.Failed);
                        }
					}
                    PDrivers[i].Proc = null;
                    Processed++;
                }
            }
        }

        private void DeleteProcess(int PNumber){
            int PosShift = 38;
            if (PDrivers[PNumber] == null || PDrivers[PNumber].Proc != null ) return;
            Panel.SuspendLayout();
            for (int i = 0 ; i<PDrivers.GetLength(0) ; i++){
                if (PDrivers[i].PLabel == null) continue;
                if (PDrivers[i].PBar.Top <= PDrivers[PNumber].PBar.Top) continue;
                PDrivers[i].PBar.Top -= PosShift; 
                PDrivers[i].PLabel.Top -= PosShift; 
            }
            Panel.Height -= PosShift;
           /*tabControl1.Height += PosShift;
            label3.Top += PosShift;
            OutPathName.Top += PosShift;
            OutPathButton.Top += PosShift;
            groupBox1.Top += PosShift;
            groupBox2.Top += PosShift;
            groupBox3.Top += PosShift;
            ProgressLabel.Top += PosShift;
            progressBar1.Top += PosShift;*/

            PDrivers[PNumber].RemoveControls();
            Panel.ResumeLayout(false);
        }

        enum FileStatus{
            Neutral,
            Processing,
            Processed,
            Failed
        }

        private void SetFileStatus(string FileName, FileStatus Status){
            ListViewItem LItem = null;
            foreach (ListViewItem L in RawFiles.Items){
                if (L.Text == FileName) LItem = L;
            }
            LItem.SubItems.Clear();
			LItem.Text = FileName;
            switch (Status) {
                case FileStatus.Neutral: {
                	LItem.SubItems.Add("File is not processed yet.");
                    LItem.BackColor = Color.White;
                    break;
                }
                case FileStatus.Processing: {
                	LItem.SubItems.Add("File is being processed...");
                	LItem.BackColor = Color.LightGoldenrodYellow;
                    break;
                }
                case FileStatus.Processed: {
                	LItem.SubItems.Add("Processed. Fine.");
                	LItem.BackColor = Color.FromArgb(214,253,200);
                    break;
                }
                case FileStatus.Failed: {
                    //здесь должно быть сообщение об ошибке 
                	LItem.SubItems.Add("Failed.");
			        LItem.BackColor = Color.LightCoral;
                    break;
                }
            }
            int FileNumber = LItem.Index;
			RawFiles.Items.RemoveAt(FileNumber);
			RawFiles.Items.Insert(FileNumber,LItem);
			RawFiles.EnsureVisible(FileNumber);                
        }

        public bool Continue(){
            if (Processed<RawFiles.Items.Count){
                RunProcesses();
                return true;
            }else{
            //Delete last process
                int PCount = PDrivers.GetLength(0);               
                for(int i = 0 ; i < PCount ; i++){
                    if ( PDrivers[i].Proc == null && PDrivers[i].PLabel != null && PDrivers[i].Finished){
                        DeleteProcess(i);
                    }
                }
                return false;
            }
        }

        public void Stop(){
            for ( int i = PDrivers.Length-1 ; i>=0 ; i--){
                PDrivers[i].Stop();
                if (PDrivers[i].PLabel != null ){
                    DeleteProcess(i);
                }
            }
            for(int i = RawFiles.Items.Count-1 ; i>=0 ; i--){
                SetFileStatus(RawFiles.Items[i].Text, FileStatus.Neutral);
            }
        }


    }

		class ProcessDriver {
			public Label PLabel;
			public ProgressBar PBar;
			public Process Proc;
			Panel Target;
			public int Progress;
            public string FileName;
			public string ErrorMessage;
            public StreamWriter Input;
            public bool Finished;

			public ProcessDriver(){
				PLabel = new Label();
				PBar = new ProgressBar();
				Proc = null;
			}

			public void CreateControls(Panel Target, int X, int Y){
				this.Target = Target;
                PLabel.AutoSize = false;
                PLabel.Location = new System.Drawing.Point(X+7, Y);
                PLabel.Name = "Plabel"+Convert.ToString(X*Y);
                PLabel.Size = new System.Drawing.Size(Target.Width-34, 15);
                PLabel.Text = "Thread progress";
				PLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));


                Target.Controls.Add(PLabel);

                PBar = new ProgressBar();
                PBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                            | System.Windows.Forms.AnchorStyles.Right)));
                PBar.Location = new System.Drawing.Point(X, Y+19);
                PBar.Name = "PBar"+Convert.ToString(X*Y);
                PBar.Size = new System.Drawing.Size(Target.Width-40, 15);

                Target.Controls.Add(PBar);
			}

            public void RemoveControls(){
                Target.Controls.Remove(PLabel);
                Target.Controls.Remove(PBar);
                PLabel = null;
                PBar = null;
            }

			public void StartProcess(string DBName, string FileName,int FileNumber){
				Progress = 0;
				ErrorMessage = "";
                this.FileName = FileName;
                Proc = new Process();
                ProcessStartInfo PSI = new ProcessStartInfo(Environment.CurrentDirectory + "\\Process\\Targeted Features.exe");
                PSI.UseShellExecute = false;
                PSI.RedirectStandardOutput = true;
                PSI.RedirectStandardInput = true;
                PSI.CreateNoWindow = true;
                if(DBName != null) {
                    PSI.Arguments = "\"" + DBName + "\" \"" + FileName + "\" " + FileNumber.ToString();
                } else {
                    PSI.Arguments = " null \"" + FileName + "\" " + Properties.Settings.Default.IntensityThreshold;
                }
                PSI.WorkingDirectory = Environment.CurrentDirectory + "\\Process";
				PLabel.Text = FileName;
                Proc.StartInfo = PSI;
				Proc.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
                Finished = false;
                Proc.Start();
                Input = Proc.StandardInput;
				Proc.BeginOutputReadLine();
                Input.WriteLine(" ");
			}

            public void Stop(){
                if (Proc!=null && !Proc.HasExited){
                    Proc.Kill();
                    while(!Proc.HasExited){
                        Proc.WaitForExit(100);
                    }
                    Proc = null;
                }
            }

			public void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)	{
				// Collect the sort command output.
			   if (Program.MainForm.InvokeRequired) {
					// We're not in the UI thread, so we need to call BeginInvoke
					Program.MainForm.Invoke( new DataReceivedEventHandler(OutputHandler), new object[]{sendingProcess,outLine});
					return;
				}
				if (!String.IsNullOrEmpty(outLine.Data)){
					int Perc;
					if (outLine.Data.Contains("%...")){
                        Perc = Convert.ToInt32(outLine.Data.Substring(0,outLine.Data.IndexOf("%...")));
                        if (PBar != null){
						    PBar.Value = Perc;
                        }
						Progress = Perc; 
					}else{
						//PLabel.Text = outLine.Data;
						//error propcessing
                        if (outLine.Data.IndexOf("Completed") != -1){
                            Input.WriteLine(" ");
                            Finished = true;
                            PLabel.Text = FileName + " " + outLine.Data;
                        }
                        if (outLine.Data.IndexOf("Information:") == 0){
                            if (PLabel != null){
                                PLabel.Text = FileName + " " + outLine.Data.Substring(12);
                            }
                            Program.MainForm.Log(outLine.Data.Substring(12),Path.GetFileName(FileName),MessageBoxIcon.Information,null);
                        }
                        if (outLine.Data.IndexOf("Warning:") == 0){
                            if (PLabel != null){
                                PLabel.Text = FileName + " " + outLine.Data.Substring(8);
                            }
                            Program.MainForm.Log(outLine.Data.Substring(8),Path.GetFileName(FileName),MessageBoxIcon.Warning,null);
                        }
                        if (outLine.Data.IndexOf("Error:") == 0){
                            int StackInfoPos = outLine.Data.IndexOf("STACKINFO:");
                            if (StackInfoPos == -1 ){
                                if (PLabel != null){
                                    PLabel.Text = FileName + " " + outLine.Data.Substring(6);
                                }
                                Program.MainForm.Log(outLine.Data.Substring(6),Path.GetFileName(FileName),MessageBoxIcon.Error,null);
                            }else{
                                if (PLabel != null){
                                    PLabel.Text = FileName + " " + outLine.Data.Substring(6,StackInfoPos-6);
                                }
                                Program.MainForm.Log(outLine.Data.Substring(6,StackInfoPos-6),Path.GetFileName(FileName),MessageBoxIcon.Error,outLine.Data.Substring(StackInfoPos+10));
                            }
    						ErrorMessage = outLine.Data;
                        }
					}
				}
			}
		}
}
