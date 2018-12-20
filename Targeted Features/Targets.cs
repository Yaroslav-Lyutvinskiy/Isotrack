using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SQLite;
using RawMSBox;

namespace Targeted_Features
{
    public class Target{
        public double MZ;
        public double RT;
        public int Charge;
        public string Name;
        public string Desc;
        public int ID;
        public int IonID;
        public int C13toCheck;
        public double RTMin;
        public double RTMax;
        public double FullRTMin;
        public double FullRTMax;
        public Feature Feature;
        public int Mode;
        public string Adduct;

        public static Target TargetFromPoint(MZData Point, int ID){
            if (Point == null) return null;
            Target T = new Target();
            T.ID = ID;
            T.Name = String.Format("Target #{0}", ID);
            T.Desc = String.Format("Target #{0}, Init abund:{1}, MZ :{2}, RT :{3}", ID,Point.Intensity,Point.Mass,Point.RT);
            T.MZ = Point.Mass;
            T.RT = Point.RT;
            T.RTMin = Point.RT-Program.RTError;
            T.RTMax = Point.RT+Program.RTError;
            T.C13toCheck = Program.C13toCheck;
            T.Charge = 1;
            return T;
        }

        public static Target TargetFromPeak(Peak P, int ID){
            if (P == null) return null;
            Target T = new Target();
            T.ID = ID;
            T.Name = String.Format("Target #{0}", ID);
            T.Desc = String.Format("Target #{0}, Init abund:{1}, MZ :{2}, RT :{3}", ID,P.ApexIntensity,P.ApexMass,P.Apex);
            T.MZ = P.ApexMass;
            T.RT = P.Apex;
            T.RTMin = P.Left;
            T.RTMax = P.Right;
            T.C13toCheck = Program.C13toCheck;
            T.Charge = 1;
            return T;
        }
       
    }
}