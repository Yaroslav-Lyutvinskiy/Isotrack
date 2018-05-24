using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.IO;
using System;


namespace MS3DPlot
{
    public class TextureMapping
    {
        public DiffuseMaterial m_material;
        private double pixelHeight = 0;

        public TextureMapping()
        {
            SetRGBMaping();
        }

        public void SetRGBMaping(){
            BitmapImage bmp = new BitmapImage(new Uri(Directory.GetCurrentDirectory() + "\\RainbowHM.bmp",UriKind.Absolute));
            //BitmapImage bmp = new BitmapImage(new Uri(Directory.GetCurrentDirectory() + "\\BWHM.bmp",UriKind.Absolute));

            pixelHeight = bmp.PixelHeight;

            ImageBrush imageBrush = new ImageBrush(bmp);
            imageBrush.ViewportUnits = BrushMappingMode.Absolute;
            m_material = new DiffuseMaterial();
            m_material.Brush = imageBrush;
        }


        // color according to the z value
        public Point GetCoord(double k){
            return new Point(0.5,1-Math.Abs(k));
        }
    }
}
