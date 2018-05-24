namespace IsoTrack.MasterForms
{
    partial class AdductsForm
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
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.AdductView = new System.Windows.Forms.DataGridView();
            this.AdductColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Charge = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.Mass = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Included = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.AdductView)).BeginInit();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button1.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button1.Location = new System.Drawing.Point(338, 277);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 30);
            this.button1.TabIndex = 0;
            this.button1.Text = "Ok";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // button2
            // 
            this.button2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button2.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button2.Location = new System.Drawing.Point(419, 277);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 30);
            this.button2.TabIndex = 1;
            this.button2.Text = "Cancel";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // AdductView
            // 
            this.AdductView.AllowUserToDeleteRows = false;
            this.AdductView.AllowUserToOrderColumns = true;
            this.AdductView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.AdductView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.AdductView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.AdductColumn,
            this.Charge,
            this.Mass,
            this.Included});
            this.AdductView.Location = new System.Drawing.Point(12, 12);
            this.AdductView.Name = "AdductView";
            this.AdductView.RowTemplate.Height = 24;
            this.AdductView.Size = new System.Drawing.Size(482, 259);
            this.AdductView.TabIndex = 2;
            this.AdductView.RowsAdded += new System.Windows.Forms.DataGridViewRowsAddedEventHandler(this.AdductView_RowsAdded);
            // 
            // AdductColumn
            // 
            this.AdductColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.AdductColumn.HeaderText = "Adduct";
            this.AdductColumn.Name = "AdductColumn";
            // 
            // Charge
            // 
            this.Charge.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.Charge.HeaderText = "Charge";
            this.Charge.Items.AddRange(new object[] {
            "+",
            "-"});
            this.Charge.Name = "Charge";
            this.Charge.Width = 60;
            // 
            // Mass
            // 
            this.Mass.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Mass.FillWeight = 80F;
            this.Mass.HeaderText = "Mass";
            this.Mass.Name = "Mass";
            // 
            // Included
            // 
            this.Included.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.Included.HeaderText = "To be searched";
            this.Included.Name = "Included";
            this.Included.Width = 120;
            // 
            // AdductsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(506, 319);
            this.Controls.Add(this.AdductView);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Name = "AdductsForm";
            this.Text = "Adducts";
            ((System.ComponentModel.ISupportInitialize)(this.AdductView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.DataGridView AdductView;
        private System.Windows.Forms.DataGridViewTextBoxColumn AdductColumn;
        private System.Windows.Forms.DataGridViewComboBoxColumn Charge;
        private System.Windows.Forms.DataGridViewTextBoxColumn Mass;
        private System.Windows.Forms.DataGridViewCheckBoxColumn Included;
    }
}