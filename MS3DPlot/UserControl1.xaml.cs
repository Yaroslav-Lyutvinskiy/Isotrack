using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MIConvexHull;

namespace MS3DPlot
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class UserControl1 : UserControl
    {
        private TextureMapping m_mapping;

        public UserControl1()
        {
            InitializeComponent();

            // set up the trackball
            var trackball = new Trackball();
            trackball.EventSource = background;
            PlotPort.Camera.Transform = trackball.Transform;
            light.Transform = trackball.RotateTransform;

        }

        //MS Arrays 
        List<double> Masses;
        List<double> RTs;
        List<double> Intensities;
        //scaleFactors
        double MassMax = 0.0; //to +1
        double MassMin = 0.0; //to -1
        double RTMax = 0.0;   //to +1
        double RTMin = 0.0;   //to -1
        double IntScale = 0.0; //to +1

        public void SetupArrays(List<double> Masses,List<double> RTs,List<double> Intensities) {
            this.Masses = Masses;
            this.RTs = RTs;
            this.Intensities = Intensities;
            MassMax = Masses.Max();
            MassMin = Masses.Min();
            RTMax = RTs.Max();
            RTMin = RTs.Min();
            IntScale = Intensities.Max();
            m_mapping = new TextureMapping();

            //adding zeroes
            //Split by RTs
            List<List<double>> MassesSplit = new List<List<double>>();
            List<List<double>> IntSplit = new List<List<double>>();
            List<double> RTforSplit = new List<double>();
            int Count = 0; 
            while(Count < Intensities.Count) {
                List<double> MassRow = new List<double>();
                List<double> IntRow = new List<double>();
                MassesSplit.Add(MassRow);
                IntSplit.Add(IntRow);
                RTforSplit.Add(RTs[Count]);
                while(RTs[Count] == RTforSplit[RTforSplit.Count - 1]) {
                    MassRow.Add(Masses[Count]);
                    IntRow.Add(Intensities[Count]);
                    Count++;
                    if(Count == RTs.Count)
                        break;
                }
            }
            //Make Ranges
            List<List<double>> MassesRanges = new List<List<double>>();
            for(int i = 0 ; i < RTforSplit.Count ; i++) {
                List<double> Ranges = new List<double>();
                for(int j = 0 ; j < MassesSplit[i].Count ; j++) {
                    if (j==0) {
                        if (IntSplit[i][j] > 0.0)
                            Ranges.Add(MassesSplit[i][j]);
                        continue;
                    }
                    if (j==MassesSplit[i].Count-1) {
                        if (IntSplit[i][j] > 0.0)
                            Ranges.Add(MassesSplit[i][j]);
                        continue;
                    }

                    if(IntSplit[i][j] == 0.0 && IntSplit[i][j + 1] > 0.0) {
                        Ranges.Add(MassesSplit[i][j]);
                        continue;
                    }
                    if(IntSplit[i][j] == 0.0 && IntSplit[i][j - 1] > 0.0) {
                        Ranges.Add(MassesSplit[i][j]);
                    }
                }
                MassesRanges.Add(Ranges);
            }


            for(int i = 0 ; i < RTforSplit.Count ; i++) {
                //Add leading and tailing zeroes
                if(MassesSplit[i][0] > MassMin && IntSplit[i][0] == 0.0) {
                    MassesSplit[i].Insert(0, MassMin);
                    IntSplit[i].Insert(0, 0.0);
                }
                if(MassesSplit[i][MassesSplit[i].Count - 1] < MassMax && IntSplit[i][IntSplit[i].Count-1] == 0.0) {
                    MassesSplit[i].Add(MassMax);
                    IntSplit[i].Add(0.0);
                }
                //Prev Spectra (not for first)
                for (int j = 0 ; i>0 && j < MassesRanges[i-1].Count-1 ; j+=2) {
                    IEnumerable<double> Res = MassesSplit[i].Where(n => (n > MassesRanges[i - 1][j] && n < MassesRanges[i - 1][j + 1]));
                    if (Res.Sum() == 0.0) {
                        Res = MassesSplit[i-1].Where(n => (n >= MassesRanges[i - 1][j] && n <= MassesRanges[i - 1][j + 1]));
                        int Index = MassesSplit[i].FindIndex(n => n>Res.First());
                        MassesSplit[i].InsertRange(Index, Res);
                        foreach(double d in Res){
                            IntSplit[i].Insert(Index, 0.0);
                        }
                    }
                }
                //Next spectra
                for (int j = 0 ; i<RTforSplit.Count-1 && j < MassesRanges[i+1].Count-1 ; j+=2) {
                    IEnumerable<double> Res = MassesSplit[i].Where(n => (n > MassesRanges[i + 1][j] && n < MassesRanges[i + 1][j + 1]));
                    if (Res.Sum() == 0.0) {
                        Res = MassesSplit[i+1].Where(n => (n >= MassesRanges[i + 1][j] && n <= MassesRanges[i + 1][j + 1]));
                        int Index = MassesSplit[i].FindIndex(n => n>Res.First());
                        MassesSplit[i].InsertRange(Index, Res);
                        foreach(double d in Res){
                            IntSplit[i].Insert(Index, 0.0);
                        }
                    }
                }
            }
                


            List<Vertex> vertices = new List<Vertex>();
            //fill verteces 
            for(int i = 0 ; i < RTforSplit.Count ; i++) {
                for(int j = 0 ; j < MassesSplit[i].Count ; j++) {
                    vertices.Add(
                        new Vertex((((RTforSplit[i] - RTMin) / (RTMax - RTMin)) - 0.5) * 2.0,
                        (((MassesSplit[i][j] - MassMin) / (MassMax - MassMin)) - 0.5) * 2.0,
                        IntSplit[i][j] / IntScale));
                }
            }

            var Triangles = Triangulation.CreateDelaunay(vertices).Cells;


            Material frontMaterial = m_mapping.m_material;

            foreach(var t in Triangles) {
                MeshGeometry3D Tr = new MeshGeometry3D();
                Tr.Positions.Add(t.Vertices[0].ToPoint3D());
                Tr.Positions.Add(t.Vertices[1].ToPoint3D());
                Tr.Positions.Add(t.Vertices[2].ToPoint3D());
                Tr.TriangleIndices.Add(2);
                Tr.TriangleIndices.Add(1);
                Tr.TriangleIndices.Add(0);
                Vector3D norma = new Vector3D(0, 0, 1);
                Tr.Normals.Add(norma);
                Tr.Normals.Add(norma);
                Tr.Normals.Add(norma);
                Point TextPt = m_mapping.GetCoord(t.Vertices[0].ToPoint3D().Z);
                Tr.TextureCoordinates.Add(TextPt);
                Tr.TextureCoordinates.Add(m_mapping.GetCoord(t.Vertices[1].ToPoint3D().Z));
                Tr.TextureCoordinates.Add(m_mapping.GetCoord(t.Vertices[2].ToPoint3D().Z));
                GeometryModel3D trianglModel =
                    new GeometryModel3D(Tr, frontMaterial);
                trianglModel.Transform = new Transform3DGroup();

                ModelVisual3D visModel = new ModelVisual3D();
                visModel.Content = trianglModel;
                PlotPort.Children.Add(visModel);
            }
        }

    public class Vertex : IVertex
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="Vertex"/> class.
        /// </summary>
        /// <param name="x">The x position.</param>
        /// <param name="y">The y position.</param>
        public Vertex(double x, double y, double z){
            Position = new double[] { x, y };
                Z = z;        
        }

        public Point ToPoint()
        {
            return new Point(Position[0], Position[1]);
        }
        public Point3D ToPoint3D()
        {
            return new Point3D(Position[0], Position[1],Z);
        }

        /// <summary>
        /// Gets or sets the Z. Not used by MIConvexHull2D.
        /// </summary>
        /// <value>The Z position.</value>
        private double Z { get; set; }

        /// <summary>
        /// Gets or sets the coordinates.
        /// </summary>
        /// <value>The coordinates.</value>
        public double[] Position { get; set; }
    }


    }


}
