using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WhereAmI
{
    public enum TypeOfDirection
    {
        left,
        right,
        top,
        bottom
    }
    internal class HelpFunctions
    {
        //whiy this we add all our object
        public static void configureCanvas(ref Canvas can)
        {
            //we configure it by the list that is given us ===> obj
            can.Children.Clear();

            MainWindow.rectBack.Name = "background";
            Pair s = ImageProcessing.getPictureSize(MainWindow.assetDir + "\\images\\background\\map" + MainWindow.currMap + ".png");
            MainWindow.rectBack.Width = s.getX();
            MainWindow.rectBack.Height = s.getY();

            can.Children.Add(MainWindow.rectBack);
            Canvas.SetLeft(MainWindow.rectBack, 0);
            Canvas.SetTop(MainWindow.rectBack, 0);

            for (int i = 0; i < MainWindow.obj.Count; i++)
            {
                Rectangle a = new Rectangle
                {
                    Width = (int)MainWindow.obj[i].Width,
                    Height = (int)MainWindow.obj[i].Height,
                    Fill = Brushes.Transparent,
                    Name = MainWindow.type[i] + "_" + i,
                };

                MainWindow.rect.Add(a);
                can.Children.Add(a);
                Canvas.SetLeft(a, MainWindow.obj[i].Left);
                Canvas.SetTop(a, MainWindow.obj[i].Top);

                if (MainWindow.type[i] == WhereAmI.TypeOfObjects.rewordBoxHidden)
                {
                    a.Visibility = Visibility.Hidden;
                    MainWindow.type[i] = WhereAmI.TypeOfObjects.rewordBox;
                    ImageProcessing.changeSprite(i, "images\\icons\\full.png");
                }
            }

            MainWindow.rectUser.Name = "player";
            MainWindow.rectUser.Width = 64; //because it is always the same
            MainWindow.rectUser.Height = 64;

            can.Children.Add(MainWindow.rectUser);
            Canvas.SetLeft(MainWindow.rectUser, 100);
            Canvas.SetTop(MainWindow.rectUser, 100);

            MainWindow.rectMusic.Name = "Music";
            s = ImageProcessing.getPictureSize(MainWindow.assetDir + "\\images\\icons\\on.png");
            MainWindow.rectMusic.Width = 40; //because it is always the same
            MainWindow.rectMusic.Height = 40;

            can.Children.Add(MainWindow.rectMusic);
            Canvas.SetLeft(MainWindow.rectMusic, 32);
            Canvas.SetTop(MainWindow.rectMusic, 32);

            can.Children.Add(MainWindow.timeLabel);
            setStyle(ref MainWindow.timeLabel, new FontFamily("Parsons"), 24);
            MainWindow.timeLabel.Foreground = Brushes.Red;
            Canvas.SetLeft(MainWindow.timeLabel, 96);
            Canvas.SetTop(MainWindow.timeLabel, 32);

            can.Children.Add(MainWindow.lifeLabel);
            setStyle(ref MainWindow.lifeLabel, new FontFamily("Parsons"), 24);
            MainWindow.lifeLabel.Foreground = Brushes.Red;
            Canvas.SetLeft(MainWindow.lifeLabel, 256);
            Canvas.SetTop(MainWindow.lifeLabel, 32);

            can.Children.Add(MainWindow.pointsLabel);
            setStyle(ref MainWindow.pointsLabel, new FontFamily("Parsons"), 24);
            MainWindow.pointsLabel.Foreground = Brushes.Red;
            Canvas.SetLeft(MainWindow.pointsLabel, 412);
            Canvas.SetTop(MainWindow.pointsLabel, 32);
        }

        public static string leadingZeros(int num)
        {
            string number = Convert.ToString(num);

            int br = num == 0 ? 1 : 0;
            while (num != 0)
            {
                num /= 10;
                br++;
            }

            for (int i = 0; i < 3 - br; i++)
                number = '0' + number;

            return number;
        }

        public static WhereAmI.TypeOfDirection getDirection(Rect obs, Rect playerHitBox)
        {
            Console.WriteLine(playerHitBox.Bottom + " " + playerHitBox.Top + " " + obs.Bottom + " " + obs.Top);
            if (playerHitBox.Bottom >= obs.Top && playerHitBox.Top < obs.Top)
                return TypeOfDirection.top;
            else if (playerHitBox.Bottom <= obs.Bottom && playerHitBox.Top >= obs.Top)
                if (Math.Abs(playerHitBox.Left - obs.Left) > Math.Abs(playerHitBox.Right - obs.Right))
                    return TypeOfDirection.left;
                else 
                    return TypeOfDirection.right;
            else
                return TypeOfDirection.bottom;
        }
        
        public static void setStyle(ref Label label, FontFamily ff, int fs)
        {
            label.FontFamily = ff;
            label.FontSize = fs;
        }
    }
}
