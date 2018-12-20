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
using System.Data.SQLite;

namespace Targeted_Features
{
    public class Peak{
        public double Apex;
        public double Right;
        public double Left;
        public double ApexIntensity;
        public double ApexMass;
        LCTrace Trace;
        public int LeftIndex;
        public int RightIndex;
        public bool TargetPeak = false;
        public bool TracePeak = false;
        public double SN;

        public Peak(LCTrace Trace, int ApexIndex, int LeftIndex, int RightIndex){
            this.Trace = Trace;
            this.Apex = ApexIndex;
            this.LeftIndex = Math.Min(LeftIndex, RightIndex);
            this.RightIndex = Math.Max(LeftIndex, RightIndex);
            Left = Trace.Group.Points[LeftIndex].RT;
            Right = Trace.Group.Points[RightIndex].RT;
            Apex = Trace.Group.Points[ApexIndex].RT;
            ApexMass = Trace.Group.Points[ApexIndex].Mass;
            ApexIntensity = Trace.Group.Points[ApexIndex].Intensity;
        }

        public double getMaxIntensity()
        {
            double Max = 0.0;
            for (int i = LeftIndex; i <= RightIndex; i++){
                Max = Math.Max(Max,Trace.Group.Points[i].Intensity);
            }
            return Max;
        }

        public double baselineIncrease(){
            return getMaxIntensity()/(Math.Max(Math.Max(Trace.Group.Points[LeftIndex].Intensity,Trace.Group.Points[RightIndex].Intensity),1.0));
        }
    }

    public class TracePeaks
    {
        LCTrace Trace;
        public List<Peak> Peaks = new List<Peak>();
        public Peak TargetPeak = null;

        /**
         * Wavelet-based peak detection.
         * Returns a list of peaks in this chromatogram satisfying the given criteria
         * Here min and max peak width are given in the units of scan time.
         * (Note that this assumes scans are even in time.)
         */
        public TracePeaks(LCTrace Trace){
            this.Trace = Trace;
        }

        public List<Peak> waveletPeakDetection(double minWidthTime, double maxWidthTime, double minIntensity, double baselineRatio)
        {
            int nStepsPerDoubling = 4;		// #steps in log-scale per scale doubling

            List<Peak> peaks = new List<Peak>();
            int nEvents = Trace.Group.Points.Count;

            double[] weights = new double[nEvents];		// for suppressing already found peaks
            for (int i = 0; i < weights.Length; i++) weights[i] = 1.0;

            double timeInc = (Trace.Group.Points[Trace.Group.Points.Count - 1].RT - Trace.Group.Points[0].RT) / (double)(Trace.Group.Points.Count - 1);
            //generates division by zero on starting irregularities 
            //int minWidth = Math.Min((int) (minWidthTime / timeInc / 2), nEvents / 4);
            int minWidth = Math.Min((int) (minWidthTime / timeInc / 2), nEvents / 4);
            if (minWidth == 0) {
                Peaks.Add(new Peak(Trace,Trace.Group.Points.IndexOf(Trace.Apex),0,Trace.Group.Points.Count-1));
                Peaks[0].TracePeak = true;
                return peaks;
            }
            int maxWidth = Math.Min((int) (maxWidthTime / timeInc / 2), nEvents / 2);

            while (true) {
                // do wavelet transform
                //wavelet's wave lenghts are limited to interval of PeakMinWidth, PeakMaxWidth 
                //therefore there is no high-freq wavelets
                double[] scales = getWaveletScales(minWidth, maxWidth, nStepsPerDoubling);
                int nScales = scales.Length;
                double[][] transform = waveletTransform(scales, weights);

                //YL scale transforms to fit the data 
                double DataMax = Trace.Group.Points.Max(E => E.Intensity);

                double[] ScaleMax = new double[nScales];
                for (int i = 0; i < nScales; i++) {
                    ScaleMax[i] = transform[i].Max();
                }

                double[] Factor = new double[nScales];
                for (int i = 0; i < nScales; i++) {
                    for (int j = 0; j < nEvents; j++) {
                        double Ratio=(transform[i][j]/ScaleMax[i])/(Trace.Group.Points[j].Intensity/DataMax);
                        if (Ratio>Factor[i]){
                            Factor[i] = Ratio;
                        }
                    }
                }


                // find maximum in the transformed matrix
                int bestScale = 0, bestEvent = 0;
                double maxt = 0.0;
                for (int i = 0; i < nScales; i++) {
                    for (int j = 0; j < nEvents; j++) {
                        if(Trace.Group.Points[j].Intensity > 0) {
                            if(transform[i][j] / Factor[i] > maxt) {
                                maxt = transform[i][j] / Factor[i];
                                bestScale = i;
                                bestEvent = j;
                            }
                        }
                    }
                }

                // find neighboring minima = peak boundaries
                double minRight = maxt;
                int rightBoundary = bestEvent;
                while (rightBoundary < nEvents - 1 && transform[bestScale][rightBoundary + 1]/Factor[bestScale] < minRight) {
                    rightBoundary++;
                    minRight = transform[bestScale][rightBoundary]/Factor[bestScale];
                }

                double minLeft = maxt;
                int leftBoundary = bestEvent;
                while (leftBoundary > 0 && transform[bestScale][leftBoundary - 1]/Factor[bestScale] < minLeft) {
                    leftBoundary--;
                    minLeft = transform[bestScale][leftBoundary]/Factor[bestScale];
                }

                Peak p = new Peak(Trace, bestEvent, leftBoundary, rightBoundary);

                // check peak criteria
                if (p.getMaxIntensity() < minIntensity || p.baselineIncrease() < baselineRatio) {
                    break;
                }

                //check if peak underlies one of the exsisting peaks
                int ibreak = 0;
                for (ibreak = 0; ibreak < peaks.Count; ibreak++ ){
                    if (p.Apex>=peaks[ibreak].Left && p.Apex<=peaks[ibreak].Right){
                        break;
                    }
                }
                if (ibreak < peaks.Count) break;

                // add to peak list
                peaks.Add(p);

                // zero out this peak from weights vector
                // TO DO: it would be better to set these points to a baseline, not zero - YL -  done
                double LeftValue = Trace.Group.Points[leftBoundary].Intensity;
                for (int j = leftBoundary; j <= rightBoundary; j++) {
                    weights[j] = LeftValue/Trace.Group.Points[j].Intensity;
                }
            }
            Peaks = peaks;
            if (Peaks.Count == 0 && Trace.Group.Points[Trace.Group.Points.Count - 1].RT - Trace.Group.Points[0].RT < maxWidthTime){
                Peaks.Add(new Peak(Trace,Trace.Group.Points.IndexOf(Trace.Apex),0,Trace.Group.Points.Count-1));
                Peaks[0].TracePeak = true;
            }
            return peaks;
        }
        /**
         * Calculate the wavelet transform with the "mexican hat" wavelet
         * at logarithmically spaced scales, from min to max
         */
        public double[][] waveletTransform(int minWidth, int maxWidth, int nStepsPerDoubling) {
            // largest scale should be less than half of trace length
            double[] scales = getWaveletScales(minWidth, maxWidth, nStepsPerDoubling);
            return waveletTransform(scales);
        }

        public double[][] waveletTransform(int minWidth, int maxWidth, int nStepsPerDoubling, double[] weights) {
            // largest scale should be less than half of trace length
            double[] scales = getWaveletScales(minWidth, maxWidth, nStepsPerDoubling);
            return waveletTransform(scales, weights);
        }

        private double[][] waveletTransform(double[] scales) {
            int nScales = scales.Length;
            double[][] transform = new double[nScales][];
            for (int i = 0; i < nScales; i++) {
                transform[i] = waveletTransform(scales[i]);
            }
            return transform;
        }

        private double[][] waveletTransform(double[] scales, double[] weights) {
            int nScales = scales.Length;
            double[][] transform = new double[nScales][];
            for (int i = 0; i < nScales; i++) {
                transform[i] = waveletTransform(scales[i], weights);
            }
            return transform;
        }

        private double[] getWaveletScales(int minWidth, int maxWidth, int nStepsPerDoubling) {
            int nEvents = Trace.Group.Points.Count;
            double ln2 = Math.Log(2.0);
            int nScales = (int) Math.Floor(
                    Math.Log(Math.Min(maxWidth, nEvents / 2) / minWidth) / ln2 * nStepsPerDoubling);
            double[] scales = new double[nScales];
            for (int i = 0; i < nScales; i++) {
                scales[i] = minWidth * Math.Exp((ln2 * i) / nStepsPerDoubling);
            }
            return scales;
        }

        /**
         * Calculate the wavelet transform with the "mexican hat" wavelet at a given
         * scale Here scale is the scale parameter s for the mexican hat function
         * phi(t/s) It should be roughly half the peak width for best matching The
         * intensity signal is multiplied by the weights vector to suppress peaks
         * (default is unit vector)
         */

        public double[] waveletTransform(double scale) {
            double[] weights = new double[Trace.Group.Points.Count];
            for(int i = 0 ; i < weights.Length ; i++) 
                weights[i] = 1.0;
            return waveletTransform(scale, weights);
        }

        public double[] waveletTransform(double scale, double[] weights) {
            double waveletSupport = 5;		// effective support from -5 to 5, gives about 4 correct decimal- YL - what is that?
        
            int length = Trace.Group.Points.Count;
            double[] transform = new double[length];

            int nHalfWavelet = (int) Math.Round(waveletSupport * scale);
            int nWaveletPoints = 2 * nHalfWavelet + 1;
            double[] waveLet = new double[nWaveletPoints];

	    // Precalculate the values of the scaled wavelet phi(t / s) over the effective support
            // TO DO: this could be catched once-and-for-all for all scales used
            for (int j = 0; j <= nHalfWavelet; j++) {
                waveLet[nHalfWavelet + j] = mexicanHatWavelet(j, scale);
                waveLet[nHalfWavelet - j] = waveLet[nHalfWavelet + j];	// wavelet function is symmetric
            }

            // loop over length of mass trace
            // System.out.print("Transform = ");
            for (int k = 0; k < length; k++) {
                transform[k] = 0;
                // we assume that the MS signal is zero outside the mass trace (so that the terms phi * x are zero too) 
                // TO DO: if mass trace is close to start/end of MS run we might want to clip the convolution
                int a = Math.Max(0, k - nHalfWavelet);
                int b = Math.Min(length - 1, k + nHalfWavelet);
                int m = b - a + 1;
                int c = Math.Max(0, nHalfWavelet - k);
    //          System.out.print("a = " + a + ", b =  " + b  + ", m = " + m + ", c = " + c + ": ");

                // Perform convolution at point k
                for (int i = 0; i < m; i++) {
                    transform[k] += weights[a + i] * Trace.Group.Points[a + i].Intensity * waveLet[c + i];
                }
    //          System.out.println(transform[k]);
            }
    //	System.out.println();
            return transform;
        }

        /**
         * Calculate the "mexican hat" wavelet function phi(t |s) Where t is the
         * wavelet time and s is the scale factor Note that this function is already
         * scaled by 1/sqrt(s), so that int_R phi^2(t|s) dt = 1 for all s
         */
        private static double mexicanHatWavelet(double t/*points from centre*/, double s/*scale or sigma*/) {
            double c = 0.8673250705840776;			// c = 2 / ( sqrt(3) * pi^(1/4) )
            double x = t / s;
            double x2 = x * x;
            return c * (1.0 - x2) * Math.Exp(-x2 / 2) / Math.Sqrt(s);
        }

        private static double[] InverseCWT(double[][] CWT, double[] Scales) {
            double[] Res = new double[CWT[0].Length];
            for (int i = 0 ; i < Res.Length ; i++) {
                Res[i] = 0.0;
                for(int j = 0 ; j < Scales.Length ; j++) {
                    Res[i] += CWT[j][i] / Math.Sqrt(Scales[j]);
                }
            }
            return Res;
        }


        public void SelectClosestAsTarget(Target T){
            int Closest = 0;
            for (int i = 1 ; i < Peaks.Count ; i++){
                if ( Math.Abs(Peaks[Closest].Apex - T.RT) > Math.Abs(Peaks[i].Apex - T.RT)){
                    Closest = i;
                }
            }
            if (Peaks.Count > 0){
                Peaks[Closest].TargetPeak = true;
                TargetPeak = Peaks[Closest];
            }
        }

        public void DBSave(SQLiteConnection con, Feature F)
        {
            for(int i = 0 ; i < Peaks.Count ; i++){
                Peak P = Peaks[i];
                SQLiteCommand Insert = new SQLiteCommand(String.Format(
                    "Insert Into RTPeaks (TraceID, PeakNumber, Main, TracePeak, Left, Right, ApexIntensity, Apex) "+
                     "Values ( {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7} )",F.MainTrace.ID, i, P.TargetPeak?1:0, P.TracePeak?1:0, P.Left, P.Right, P.ApexIntensity, P.Apex),con);
                Insert.ExecuteNonQuery();
            }
        }
    }
}
