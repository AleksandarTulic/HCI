
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WhereAmI
{
    internal class ImageProcessing
    {
        public static void changeSprite(int i, string path)
        {
            MainWindow.imgBrush[i].ImageSource = new BitmapImage(new Uri(MainWindow.assetDir + "\\" + path));
            Pair s = getPictureSize(MainWindow.assetDir + "\\" + path);
            MainWindow.rect[i].Width = s.getX();
            MainWindow.rect[i].Height = s.getY();
            MainWindow.rect[i].Fill = MainWindow.imgBrush[i];
        }

        public static void changeSprite(ref ImageBrush img, ref Rectangle rect, string path)
        {
            Console.WriteLine(MainWindow.assetDir + "\\" + path);
            img.ImageSource = new BitmapImage(new Uri(MainWindow.assetDir + "\\" + path));
            rect.Fill = img;
        }

        public static Pair getPictureSize(string path)
        {
            // Reads only the image header; no external imaging library needed.
            BitmapFrame frame = BitmapDecoder.Create(
                new Uri(path), BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];

            return new Pair(frame.PixelWidth, frame.PixelHeight);
        }

        public static Pair getScreenResolution()
        {
            return new Pair((int)System.Windows.SystemParameters.PrimaryScreenWidth, (int)System.Windows.SystemParameters.PrimaryScreenHeight);
        }
    }
}
