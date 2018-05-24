namespace Standard_Inspector
{
    partial class SettingsForm
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.SettingsView = new System.Windows.Forms.DataGridView();
            this.SettingsName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.SettingsValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.button1 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.SettingsView)).BeginInit();
            this.SuspendLayout();
            // 
            // SettingsView
            // 
            this.SettingsView.AllowUserToAddRows = false;
            this.SettingsView.AllowUserToDeleteRows = false;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.SettingsView.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle2;
            this.SettingsView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.SettingsView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.SettingsView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.SettingsName,
            this.SettingsValue});
            this.SettingsView.Location = new System.Drawing.Point(12, 12);
            this.SettingsView.Name = "SettingsView";
            this.SettingsView.ReadOnly = true;
            this.SettingsView.RowTemplate.Height = 24;
            this.SettingsView.Size = new System.Drawing.Size(407, 401);
            this.SettingsView.TabIndex = 1;
            // 
            // SettingsName
            // 
            this.SettingsName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.SettingsName.HeaderText = "Name:";
            this.SettingsName.Name = "SettingsName";
            this.SettingsName.ReadOnly = true;
            // 
            // SettingsValue
            // 
            this.SettingsValue.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.SettingsValue.HeaderText = "Value:";
            this.SettingsValue.Name = "SettingsValue";
            this.SettingsValue.ReadOnly = true;
            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button1.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button1.Location = new System.Drawing.Point(344, 419);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 31);
            this.button1.TabIndex = 2;
            this.button1.Text = "Ok";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // SettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(431, 462);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.SettingsView);
            this.Name = "SettingsForm";
            this.Text = "Settings:";
            ((System.ComponentModel.ISupportInitialize)(this.SettingsView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridViewTextBoxColumn SettingsName;
        private System.Windows.Forms.DataGridViewTextBoxColumn SettingsValue;
        private System.Windows.Forms.Button button1;
        public System.Windows.Forms.DataGridView SettingsView;
    }
}