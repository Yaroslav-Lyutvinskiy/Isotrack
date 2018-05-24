using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms;

namespace Inspector {
    public partial class RawDataForm : Form {

        mzAccess_Service.MSDataService Service = new mzAccess_Service.MSDataService();

        public RawDataForm() {
            InitializeComponent();
        }

        List<double> Intensities = new List<double>();
        List<double> RTTraces = new List<double>();
        List<double> Masses = new List<double>();

        string fileName;
        public string FileName {
            get {
                return fileName;
            }
            set {
                fileName = value;
                label1.Text = "Raw File Name: " + fileName;
            }
        }

        public double TrueMZ;

        string desc;
        public string Desc {
            get {
                return desc;
            }
            set {
                desc = value;
                label6.Text = value;
            }
        }

        double minRT;
        public double MinRT {
            get {
                return minRT;
            }
            set {
                minRT = value;
                MinRTBox.Text = String.Format("{0:f2}",minRT);
            }
        }

        double maxRT;
        public double MaxRT {
            get {
                return maxRT;
            }
            set {
                maxRT = value;
                MaxRTBox.Text = String.Format("{0:f2}",maxRT);
            }
        }

        double minMZ;
        public double MinMZ {
            get {
                return minMZ;
            }
            set {
                minMZ = value;
                MinMZBox.Text = String.Format("{0:f4}",minMZ);
            }
        }

        double maxMZ;
        public double MaxMZ {
            get {
                return maxMZ;
            }
            set {
                maxMZ = value;
                MaxMZBox.Text = String.Format("{0:f4}",maxMZ);
            }
        }

        private void button1_Click(object sender, EventArgs e) {
            SetData();
        }


        public void SetData() {
            double[] data;
            Intensities = new List<double>();
            RTTraces = new List<double>();
            Masses = new List<double>();
            Series LineSerie = null;
            double MaxIntensity = 0.0;
            string Error;
            elementHost1.Visible = false;
            chart1.ChartAreas[0].AxisX.ScaleView.ZoomReset(0);
            chart1.ChartAreas[0].AxisY.ScaleView.ZoomReset(0);
            chart1.ChartAreas[0].AxisX.ScaleView.Zoomable = true;
            chart1.ChartAreas[0].AxisY.ScaleView.Zoomable = true;
            chart1.ChartAreas[0].CursorY.IsUserSelectionEnabled = true;
            chart1.ChartAreas[0].CursorX.IsUserSelectionEnabled = true;
            chart1.ChartAreas[0].CursorY.Interval = 0.0001;
            chart1.ChartAreas[0].CursorX.Interval = 0.001;


            if(radioButton1.Checked) {//trace
                groupBox2.Visible = false;
                groupBox3.Visible = false;
                groupBox4.Visible = false;
                data = Service.GetChromatogram(fileName, minMZ, maxMZ, minRT, maxRT,radioButton10.Checked,out Error);
                if(data == null) { 
                    MessageBox.Show(Error, "WebService Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                LineSerie = new Series();
                LineSerie.ChartType = SeriesChartType.Line;

                for (int i = 0 ; i < data.Length ; i += 2) {
                    RTTraces.Add(data[i]);
                    Intensities.Add(data[i + 1]);
                    if(data[i + 1] > MaxIntensity)
                        MaxIntensity = data[i + 1];
                }
                for (int j = 0 ; j < Intensities.Count ; j++){
                     LineSerie.Points.AddXY(RTTraces[j],Intensities[j]);
                }
                chart1.Series.Clear();
                chart1.Series.Add(LineSerie);
                chart1.ChartAreas[0].AxisY.Minimum = 0.0;
                chart1.ChartAreas[0].AxisY.Maximum = MaxIntensity*1.1;
                chart1.ChartAreas[0].AxisX.Minimum = MinRT;
                chart1.ChartAreas[0].AxisX.Maximum = MaxRT;
                chart1.ChartAreas[0].AxisX.LabelStyle.Format = "f3";
                chart1.ChartAreas[0].AxisY.LabelStyle.Format = "0.00e+00";
                chart1.Legends[0].Enabled = false;

            }
            if(radioButton2.Checked) {//slice
                groupBox2.Visible = true;
                groupBox3.Visible = true;
                data = Service.GetArea(fileName, minMZ, maxMZ, minRT, maxRT, radioButton10.Checked, radioButton4.Checked, out Error);
                if(data == null) { 
                    MessageBox.Show(Error, "WebService Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                LineSerie = new Series();
                LineSerie.ChartType = SeriesChartType.Point;
                for (int i = 0 ; i < data.Length ; i += 3) {
                    Masses.Add(data[i]);
                    RTTraces.Add(data[i+1]);
                    Intensities.Add(data[i + 2]);
                    if(data[i + 2] > MaxIntensity)
                        MaxIntensity = data[i + 2];
                }
                if(radioButton5.Checked) {
                    groupBox4.Visible = false;
                    if(radioButton6.Checked) {//Mass Scale
                        for(int j = 0 ; j < Intensities.Count ; j++) {
                            LineSerie.Points.AddXY(RTTraces[j], Masses[j]);
                            LineSerie.Points[LineSerie.Points.Count - 1].Color = ColorScale(Intensities[j]/MaxIntensity);
                        }
                        chart1.ChartAreas[0].AxisY.Minimum = MinMZ;
                        chart1.ChartAreas[0].AxisY.Maximum = MaxMZ;
                        chart1.ChartAreas[0].AxisY.LabelStyle.Format = "f4";

                    } else { //PPM Scale
                        for(int j = 0 ; j < Intensities.Count ; j++) {
                            double PPMValue = (Masses[j] - TrueMZ) / (TrueMZ / 1000000.0 );
                            LineSerie.Points.AddXY(RTTraces[j], PPMValue);
                            LineSerie.Points[LineSerie.Points.Count - 1].Color = ColorScale(Intensities[j]/MaxIntensity);
                        }
                        chart1.ChartAreas[0].AxisY.Minimum = (MinMZ - TrueMZ) / (TrueMZ / 1000000.0);
                        chart1.ChartAreas[0].AxisY.Maximum = (MaxMZ - TrueMZ) / (TrueMZ / 1000000.0);
                        chart1.ChartAreas[0].AxisY.LabelStyle.Format = "f2";

                    }
                    chart1.Series.Clear();
                    chart1.Series.Add(LineSerie);
                    //chart1.ChartAreas[0].AxisY.Minimum = MinMZ;
                    //chart1.ChartAreas[0].AxisY.Maximum = MaxMZ;
                    chart1.ChartAreas[0].AxisX.Minimum = MinRT;
                    chart1.ChartAreas[0].AxisX.Maximum = MaxRT;
                    chart1.ChartAreas[0].AxisX.LabelStyle.Format = "f2";
                    chart1.Legends[0].Enabled = false;
                    //chart1.ChartAreas[0].AxisY.LabelStyle.Format = "f4";

                } else {
                    groupBox4.Visible = true;
                    elementHost1.Visible = true;
                    (elementHost1.Child as MS3DPlot.UserControl1).SetupArrays(Masses,RTTraces,Intensities);
                }
            }
            if(radioButton3.Checked) {//spectra
                groupBox2.Visible = true;
                groupBox3.Visible = false;
                groupBox4.Visible = false;
                data = Service.GetAverageSpectrum(
                    fileName, minMZ, maxMZ, minRT, maxRT, radioButton4.Checked,out Error);
                if(data == null) { 
                    MessageBox.Show(Error, "WebService Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                LineSerie = new Series();
                if(radioButton4.Checked) {
                    LineSerie.ChartType = SeriesChartType.Line;
                } else {
                    LineSerie.ChartType = SeriesChartType.Point;
                }
                double MaxInt = 0.0;
                for (int i = 0 ; i < data.Length ; i += 2) {
                    Masses.Add(data[i]);
                    if(data[i+1] > MaxInt)
                        MaxInt = data[i+1];
                    Intensities.Add(data[i + 1]);
                }
                for (int j = 0 ; j < Intensities.Count ; j++){
                     LineSerie.Points.AddXY(Masses[j],Intensities[j]);
                }
                chart1.Series.Clear();
                chart1.Series.Add(LineSerie);
                chart1.ChartAreas[0].AxisX.Minimum = MinMZ;
                chart1.ChartAreas[0].AxisX.Maximum = MaxMZ;
                chart1.ChartAreas[0].AxisY.Minimum = 0.0;
                chart1.ChartAreas[0].AxisY.Maximum = MaxInt*1.1;
                chart1.ChartAreas[0].AxisX.LabelStyle.Format = "f5";
                chart1.ChartAreas[0].AxisY.LabelStyle.Format = "0.00e+00";
                chart1.Legends[0].Enabled = false;
            }
        }

        private void elementHost1_ChildChanged(object sender, System.Windows.Forms.Integration.ChildChangedEventArgs e) {

        }

        private void RawDataForm_Shown(object sender, EventArgs e) {
            SetData();
        }

        private void StateChanged(object sender, EventArgs e) {
            if((sender as RadioButton).Checked == true) {
                SetData();
            }
        }

        private void IntervalsChanged(object sender, EventArgs e) {
            minMZ = Convert.ToDouble(MinMZBox.Text);
            maxMZ = Convert.ToDouble(MaxMZBox.Text);
            minRT = Convert.ToDouble(MinRTBox.Text);
            maxRT = Convert.ToDouble(MaxRTBox.Text);
            SetData();
        }

        private Color ColorScale(double Value) {
            double Red = 0.0;
            double Green = 0.0;
            double Blue = 0.0;
            if (Value>=0 && Value < 0.25) {
                Blue = 1.0;
                Green = Value * 4.0;
            }
            if (Value>=0.25 && Value < 0.5) {
                Green = 1.0;
                Blue = 1-((Value - 0.25) * 4.0);
            }
            if (Value>=0.5 && Value < 0.75) {
                Green = 1.0;
                Red = (Value - 0.5) * 4.0;
            }
            if (Value>=0.75 && Value <=1.0 ) {
                Red = 1.0;
                Green = 1-((Value - 0.75) * 4.0);
            }
            return Color.FromArgb(
                Convert.ToInt32(Math.Ceiling(Red * 255.0)), 
                Convert.ToInt32(Math.Ceiling(Green * 255.0)), 
                Convert.ToInt32(Math.Ceiling(Blue * 255.0)));
        }

        private void button1_Click_1(object sender, EventArgs e) {
            double AveMZ = (MaxMZ + MinMZ) / 2.0;
            MaxMZ = AveMZ + (MaxMZ - AveMZ) * 2.0;
            MinMZ = AveMZ - (AveMZ - MinMZ) * 2.0;
            IntervalsChanged(null,null);
        }

        private void button2_Click(object sender, EventArgs e) {
            double AveRT = (MaxRT + MinRT) / 2.0;
            MaxRT = AveRT + (MaxRT - AveRT) * 2.0;
            MinRT = Math.Max(0.0,AveRT - (AveRT - MinRT) * 2.0);
            IntervalsChanged(null,null);
        }

        private void button5_Click(object sender, EventArgs e) {
            chart1.ChartAreas[0].AxisX.ScaleView.ZoomReset(0);
            chart1.ChartAreas[0].AxisY.ScaleView.ZoomReset(0);
        }
    }
}
