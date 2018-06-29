namespace IsoTrack
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.propertyGrid1 = new System.Windows.Forms.PropertyGrid();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.RawList = new System.Windows.Forms.ListView();
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.LogList = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader5 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.panel1 = new System.Windows.Forms.Panel();
            this.ImporButton = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.MainButton = new System.Windows.Forms.Button();
            this.StopButton = new System.Windows.Forms.Button();
            this.OpenStandsFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.DB3SaveDialog = new System.Windows.Forms.SaveFileDialog();
            this.TextSaveDialog = new System.Windows.Forms.SaveFileDialog();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.DB3ImportFileDialog = new System.Windows.Forms.OpenFileDialog();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // propertyGrid1
            // 
            this.propertyGrid1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.propertyGrid1.CategoryForeColor = System.Drawing.SystemColors.InactiveCaptionText;
            this.propertyGrid1.LineColor = System.Drawing.SystemColors.ControlDark;
            this.propertyGrid1.Location = new System.Drawing.Point(0, 0);
            this.propertyGrid1.Margin = new System.Windows.Forms.Padding(2);
            this.propertyGrid1.Name = "propertyGrid1";
            this.propertyGrid1.Size = new System.Drawing.Size(258, 480);
            this.propertyGrid1.TabIndex = 0;
            this.propertyGrid1.ToolbarVisible = false;
            this.propertyGrid1.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.propertyGrid1_PropertyValueChanged);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(2);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.panel1);
            this.splitContainer1.Panel2.Controls.Add(this.propertyGrid1);
            this.splitContainer1.Size = new System.Drawing.Size(735, 518);
            this.splitContainer1.SplitterDistance = 482;
            this.splitContainer1.SplitterWidth = 3;
            this.splitContainer1.TabIndex = 1;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Margin = new System.Windows.Forms.Padding(2);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.RawList);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.splitContainer3);
            this.splitContainer2.Size = new System.Drawing.Size(482, 518);
            this.splitContainer2.SplitterDistance = 185;
            this.splitContainer2.SplitterWidth = 3;
            this.splitContainer2.TabIndex = 0;
            // 
            // RawList
            // 
            this.RawList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader3,
            this.columnHeader4});
            this.RawList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.RawList.Location = new System.Drawing.Point(0, 0);
            this.RawList.Margin = new System.Windows.Forms.Padding(2);
            this.RawList.Name = "RawList";
            this.RawList.ShowItemToolTips = true;
            this.RawList.Size = new System.Drawing.Size(482, 185);
            this.RawList.TabIndex = 0;
            this.RawList.UseCompatibleStateImageBehavior = false;
            this.RawList.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "Input raw files:";
            this.columnHeader3.Width = 335;
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "Message";
            this.columnHeader4.Width = 128;
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.Location = new System.Drawing.Point(0, 0);
            this.splitContainer3.Margin = new System.Windows.Forms.Padding(2);
            this.splitContainer3.Name = "splitContainer3";
            this.splitContainer3.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.LogList);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.AutoScroll = true;
            this.splitContainer3.Size = new System.Drawing.Size(482, 330);
            this.splitContainer3.SplitterDistance = 175;
            this.splitContainer3.SplitterWidth = 3;
            this.splitContainer3.TabIndex = 0;
            // 
            // LogList
            // 
            this.LogList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader5,
            this.columnHeader2});
            this.LogList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.LogList.Location = new System.Drawing.Point(0, 0);
            this.LogList.Margin = new System.Windows.Forms.Padding(2);
            this.LogList.Name = "LogList";
            this.LogList.Size = new System.Drawing.Size(482, 175);
            this.LogList.TabIndex = 0;
            this.LogList.UseCompatibleStateImageBehavior = false;
            this.LogList.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Time:";
            this.columnHeader1.Width = 101;
            // 
            // columnHeader5
            // 
            this.columnHeader5.Text = "File:";
            this.columnHeader5.Width = 147;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Message:";
            this.columnHeader2.Width = 600;
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.ImporButton);
            this.panel1.Controls.Add(this.button2);
            this.panel1.Controls.Add(this.MainButton);
            this.panel1.Controls.Add(this.StopButton);
            this.panel1.Location = new System.Drawing.Point(2, 485);
            this.panel1.Margin = new System.Windows.Forms.Padding(2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(254, 31);
            this.panel1.TabIndex = 1;
            // 
            // ImporButton
            // 
            this.ImporButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ImporButton.Location = new System.Drawing.Point(115, 0);
            this.ImporButton.Margin = new System.Windows.Forms.Padding(2);
            this.ImporButton.Name = "ImporButton";
            this.ImporButton.Size = new System.Drawing.Size(58, 31);
            this.ImporButton.TabIndex = 3;
            this.ImporButton.Text = "Import";
            this.ImporButton.UseVisualStyleBackColor = true;
            this.ImporButton.Click += new System.EventHandler(this.ImportButton_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(83, 0);
            this.button2.Margin = new System.Windows.Forms.Padding(2);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(34, 31);
            this.button2.TabIndex = 2;
            this.button2.Text = "Post";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Visible = false;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // MainButton
            // 
            this.MainButton.Dock = System.Windows.Forms.DockStyle.Right;
            this.MainButton.Location = new System.Drawing.Point(178, 0);
            this.MainButton.Margin = new System.Windows.Forms.Padding(2);
            this.MainButton.Name = "MainButton";
            this.MainButton.Size = new System.Drawing.Size(76, 31);
            this.MainButton.TabIndex = 1;
            this.MainButton.Text = "Go";
            this.MainButton.UseVisualStyleBackColor = true;
            this.MainButton.Click += new System.EventHandler(this.MainButton_Click);
            // 
            // StopButton
            // 
            this.StopButton.Dock = System.Windows.Forms.DockStyle.Left;
            this.StopButton.Enabled = false;
            this.StopButton.Location = new System.Drawing.Point(0, 0);
            this.StopButton.Margin = new System.Windows.Forms.Padding(2);
            this.StopButton.Name = "StopButton";
            this.StopButton.Size = new System.Drawing.Size(79, 31);
            this.StopButton.TabIndex = 0;
            this.StopButton.Text = "Stop";
            this.StopButton.UseVisualStyleBackColor = true;
            this.StopButton.Click += new System.EventHandler(this.StopButton_Click);
            // 
            // OpenStandsFileDialog
            // 
            this.OpenStandsFileDialog.DefaultExt = "txt";
            this.OpenStandsFileDialog.Filter = "Text tab-separated files (*.txt;*.tsv)|*.txt;*.tsv|All Files (*.*)|*.*";
            this.OpenStandsFileDialog.FilterIndex = 0;
            this.OpenStandsFileDialog.RestoreDirectory = true;
            this.OpenStandsFileDialog.ShowReadOnly = true;
            this.OpenStandsFileDialog.Title = "Open Standards File";
            // 
            // DB3SaveDialog
            // 
            this.DB3SaveDialog.DefaultExt = "db3";
            this.DB3SaveDialog.Filter = "SQLite db3 files(*.db3)|*.db3";
            this.DB3SaveDialog.RestoreDirectory = true;
            this.DB3SaveDialog.Title = "Specify output db3 file";
            // 
            // TextSaveDialog
            // 
            this.TextSaveDialog.DefaultExt = "txt";
            this.TextSaveDialog.Filter = "Tab separated text file (*.txt;*.tsv)|*.txt;*.tsv";
            // 
            // timer1
            // 
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // DB3ImportFileDialog
            // 
            this.DB3ImportFileDialog.DefaultExt = "db3";
            this.DB3ImportFileDialog.FileName = "DB3ImportFileDialog";
            this.DB3ImportFileDialog.Filter = "SQLite File (*.db3)|*.db3";
            this.DB3ImportFileDialog.RestoreDirectory = true;
            this.DB3ImportFileDialog.Title = "Select db3 file for settings import...";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(735, 518);
            this.Controls.Add(this.splitContainer1);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "Form1";
            this.Text = "IsoTrack v.1.6.2.0";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.splitContainer3.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
            this.splitContainer3.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PropertyGrid propertyGrid1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        public System.Windows.Forms.OpenFileDialog OpenStandsFileDialog;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button MainButton;
        private System.Windows.Forms.Button StopButton;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.ListView LogList;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ColumnHeader columnHeader4;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        public System.Windows.Forms.SaveFileDialog DB3SaveDialog;
        public System.Windows.Forms.SaveFileDialog TextSaveDialog;
        public System.Windows.Forms.ListView RawList;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.ColumnHeader columnHeader5;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button ImporButton;
        private System.Windows.Forms.OpenFileDialog DB3ImportFileDialog;
    }
}

