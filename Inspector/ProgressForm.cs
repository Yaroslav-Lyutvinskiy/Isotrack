using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Inspector {
    public partial class ProgressForm : Form {
        public ProgressForm() {
            InitializeComponent();
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            progressBar1.Value = e.ProgressPercentage;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e) {
            (this.Owner as MetaRepForm).CancelSignal = true;
        }

        private void ProgressForm_FormClosed(object sender, FormClosedEventArgs e) {
            this.Owner.Enabled = true; 
        }

        private void ProgressForm_Load(object sender, EventArgs e) {
            this.Owner.Enabled = false; 
        }
    }
}
