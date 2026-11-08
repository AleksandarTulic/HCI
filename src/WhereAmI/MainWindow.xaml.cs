using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WhereAmI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public enum TypeOfObjects
    {
        ground,
        reword,
        spikes,
        rewordBox,
        enemy,
        ending,
        rewordBoxHidden
    }
    public partial class MainWindow : Window
    {
        public static readonly string assetDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets");
        public static Pair resolution = ImageProcessing.getScreenResolution();

        //for moving left and right
        int speedMoving = 0;
        //for moving up and down
        int speedJumping = 0;

        //speed for enemies
        //static because i use it in class Enemy
        public static int speedEnemy = 2;

        //moving user
        DispatcherTimer moveUser = new DispatcherTimer();
        //checking if player is out of map
        DispatcherTimer outOfMap = new DispatcherTimer();
        //coin animation
        DispatcherTimer coinMovement = new DispatcherTimer();
        //show coin jumping from the box
        DispatcherTimer coinJump = new DispatcherTimer();
        //showing how long time you have
        DispatcherTimer timeRemaining = new DispatcherTimer();
        //for moving enemies
        DispatcherTimer dtEnemy = new DispatcherTimer();

        //when you kill enemy -> how much points do you get
        Label enemyPointsLabel = new Label();

        //time left for playing this level
        int timeRem = 180;

        //we have only one player
        Rect playerHitBox = new Rect(64, 64, 100, 100);

        //if we are jumping nothing else can be done
        bool flagJump = false;

        //how much are we going up
        int numJump = 0;

        //list of object that we can interact as a player
        //in this we include: enemies, ground, reword, rewordBox and so on
        public static List<Rect> obj = new List<Rect>();

        //we want to know which object is which type
        //because enemies are moving they will not always be in the same position
        public static List<TypeOfObjects> type = new List<TypeOfObjects> ();

        //which picture to show for sound on and off
        int numMusic = 2;

        //Brushes for objects
        //this value are contant threw the game
        ImageBrush soundOnOff = new ImageBrush();
        ImageBrush backGround = new ImageBrush();
        ImageBrush player = new ImageBrush();

        //img brushes used for all object
        public static List<ImageBrush> imgBrush = new List<ImageBrush>();
        //it is filled with previous imgBrush values
        public static List <Rectangle> rect = new List<Rectangle>();

        //for showing i-th picture of coin moving
        int numCoin = 1;

        //current map playing
        public static int currMap = 0;
        int maxMap = 1;

        //rectangles that we always use
        public static Rectangle rectBack = new Rectangle();
        public static Rectangle rectUser = new Rectangle();
        public static Rectangle rectMusic = new Rectangle();

        //labels for showing information
        public static Label pointsLabel = new Label();
        public static Label timeLabel = new Label();
        public static Label lifeLabel = new Label();

        //tutorial/info of the game appears only for the first level
        Label leftLabel = new Label();
        Label rightLabel = new Label();
        Label stopLabel = new Label();
        Label jumpLabel = new Label();
        Label addInfo = new Label();
        Rectangle leftRect = new Rectangle();
        Rectangle rightRect = new Rectangle();
        Rectangle stopRect = new Rectangle();
        Rectangle jumpRect = new Rectangle();

        //for playing music
        MediaPlayer media = new MediaPlayer();

        //medi for effects
        MediaPlayer mediaEffect = new MediaPlayer();

        //number of lifes
        int numLifes = 3;

        //points collected
        int points = 0;

        //rectangle for showing jumping peny
        //because i can not trust time in programming i have a list of rectangles ---> more detail below
        //this will probably not be a problem because the time it takes a rectangle of jumping coin to disappear is 200ms but as i said we can not count on time
        //imagebrush that i used in the code below is not a problem because they all have the same picture
        List<Rectangle> jumpCoin = new List<Rectangle> ();
        public static List<int> jumpNumCoin = new List<int>();

        //enemy
        public static List<Enemy> listEnemy = new List<Enemy> ();

        //which operation is the player currently doing so that we would know which type of picture to show
        string operation = "idle";

        //which picture of player to show
        int numsprite = 0;
        public MainWindow()
        {
            InitializeComponent();

            play(currMap);

            moveUser.Tick += userMoving;
            moveUser.Interval = TimeSpan.FromMilliseconds(10);

            coinMovement.Tick += coinMoving;
            coinMovement.Interval = TimeSpan.FromMilliseconds(40);

            coinJump.Tick += coinJumping;
            coinJump.Interval = TimeSpan.FromMilliseconds(200);

            timeRemaining.Tick += timeLeft;
            timeRemaining.Interval = TimeSpan.FromMilliseconds(1000);

            dtEnemy.Tick += enemyMoving;
            dtEnemy.Interval += TimeSpan.FromMilliseconds(30);
        }

        private void coinJumping(object sender, EventArgs e)
        {
            foreach (Rectangle i in jumpCoin)
                myCanvas.Children.Remove(i);
        }

        private void coinMoving(object sender, EventArgs e)
        {
            for (int i = 0; i < obj.Count; i++ )
                if ( type[i] == TypeOfObjects.reword)
                    ImageProcessing.changeSprite(i, "images\\reword\\" + numCoin + ".png");

            numCoin++;

            if ( numCoin == 17)
                numCoin = 1;
        }

        private void timeLeft(object sender, EventArgs e)
        {
            timeRem--;

            if (timeRem == 0)
            {
                death();
                ((DispatcherTimer)sender).Stop();
            }
            else
                timeLabel.Content = "Time: " + timeRem;
        }

        private void enemyMoving(object sender, EventArgs e)
        {
            for (int i=0;i<obj.Count();i++)
            {
                if ( type[i] == TypeOfObjects.enemy && rect[i].Visibility != Visibility.Hidden)
                {
                    Canvas.SetLeft(rect[i], Canvas.GetLeft(rect[i]) + listEnemy[i].getSpeed());
                    obj[i] = new Rect(Canvas.GetLeft(rect[i]), Canvas.GetTop(rect[i]), obj[i].Width, obj[i].Height);
                    ImageProcessing.changeSprite(i, "images\\enemy\\" + listEnemy[i].getType() + "\\" + listEnemy[i].getNumPic() + ".png");
                    listEnemy[i].setXnow((int)Canvas.GetLeft(rect[i]));
                    listEnemy[i].incDecSprite();
                }
            }
        }

        private void userMoving(object sender, EventArgs e)
        {
            int flagGravity = 0;
            int previousSpeed = speedMoving;
            for (int i = 0; i < obj.Count; i++)
            {
                if (obj[i].IntersectsWith(playerHitBox))
                {
                    if ( type[i] == TypeOfObjects.ground || type[i] == TypeOfObjects.rewordBox )
                    {
                        TypeOfDirection dir = HelpFunctions.getDirection(obj[i], playerHitBox);
                        Console.WriteLine(dir + " " + speedMoving + " " + previousSpeed + " " + i);
                        if (dir == TypeOfDirection.left)
                        {
                            if (previousSpeed > 0)
                                speedMoving = previousSpeed;
                            else
                                speedMoving = 0;

                            flagGravity = 1;
                            break;
                        }else if ( dir == TypeOfDirection.right)
                        {
                            if (previousSpeed < 0)
                                speedMoving = previousSpeed;
                            else
                                speedMoving = 0;

                            flagGravity = 1;
                            break;
                        }

                        if (flagJump)
                        {
                            if (speedJumping > 0)
                            {
                                if (dir == TypeOfDirection.top)
                                {
                                    speedJumping = 0;
                                    speedMoving = 0;
                                    flagJump = false;
                                }
                                else
                                {
                                    speedMoving = 0;
                                    speedJumping = 15;
                                }
                            }
                            else if (dir == TypeOfDirection.bottom)
                            {
                                if ( type[i] == TypeOfObjects.rewordBox)
                                {
                                    if (jumpNumCoin[i] > 0)
                                    {
                                        if (rect[i].Visibility == Visibility.Hidden)
                                            rect[i].Visibility = Visibility.Visible;

                                        Rectangle r = new Rectangle
                                        {
                                            Width = 32,
                                            Height = 32,
                                            Fill = Brushes.Green,
                                        };

                                        jumpCoin.Add(r);
                                        myCanvas.Children.Add(r);

                                        Canvas.SetLeft(r, obj[i].Left + 16); //16px is used because the picture is known size of 32*32px so it will be centered, and i know the box is 64*64px
                                        Canvas.SetTop(r, obj[i].Top - 55);
                                        ImageBrush im = new ImageBrush();
                                        ImageProcessing.changeSprite(ref im, ref r, "images\\reword\\0.png");
                                        jumpNumCoin[i]--;
                                        coinJump.Start();


                                        playEffectMusic("music\\effect\\coinTaken.wav");
                                        points++;
                                        changeContentLabel(ref pointsLabel, "Points: " + points);

                                        if (jumpNumCoin[i] == 0)
                                            ImageProcessing.changeSprite(i, "images\\icons\\empty.png");
                                    }
                                    else
                                    {
                                        playEffectMusic("music\\effect\\blank.mp3");
                                    }
                                }

                                speedJumping = 15;
                                numJump = -1;
                            }
                        }
                        else if (dir == TypeOfDirection.bottom)
                            speedMoving = 0;
                    }
                    else if ( type[i] == TypeOfObjects.reword)
                    {
                        if (rect[i].Visibility != Visibility.Hidden)
                        {
                            playEffectMusic("music\\effect\\coinTaken.wav");
                            rect[i].Visibility = Visibility.Hidden;
                            points++;
                            changeContentLabel(ref pointsLabel, "Points: " + points);
                        }
                    } 
                    else if (type[i] == TypeOfObjects.spikes)
                    {
                        death();
                    }
                    else if (type[i] == TypeOfObjects.enemy)
                    {
                        if (rect[i].Visibility != Visibility.Hidden)
                        {
                            if (HelpFunctions.getDirection(obj[i], playerHitBox) == WhereAmI.TypeOfDirection.top)
                            {
                                rect[i].Visibility = Visibility.Hidden;
                                playEffectMusic("music\\effect\\blank.mp3");

                                if (listEnemy[i].getType() == typeEnemy.enemy1)
                                {
                                    points += 100;
                                    enemyPointsLabel.Content = 100;
                                }
                                else
                                {
                                    points += 200;
                                    enemyPointsLabel.Content = 200;
                                }

                                HelpFunctions.setStyle(ref enemyPointsLabel, new FontFamily("Parsons"), 18);

                                Canvas.SetLeft(enemyPointsLabel, Canvas.GetLeft(rect[i]));
                                Canvas.SetTop(enemyPointsLabel, Canvas.GetTop(rect[i]));
                                changeContentLabel(ref pointsLabel, "Points: " + points);
                            }
                            else
                            {
                                death();
                            }
                        }
                    }
                    else if (type[i] == TypeOfObjects.ending)
                        play(++currMap);

                    flagGravity++;
                }
            }

            if (flagGravity == 0 && !flagJump)
            {
                speedJumping = 10;
                numJump = -1;
                flagJump = true;
            }

            if (playerHitBox.Y + speedJumping <= 0)
                numJump = 1;

            if (playerHitBox.X + Canvas.GetLeft(myCanvas) + speedMoving <= 0)
                speedMoving = 0;

            Canvas.SetLeft(rectUser, Canvas.GetLeft(rectUser) + speedMoving);
            Canvas.SetTop(rectUser, Canvas.GetTop(rectUser) + speedJumping);
            Canvas.SetLeft(rectMusic, Canvas.GetLeft(rectMusic) + (playerHitBox.Right >= resolution.getX() / 2 + Math.Abs(Canvas.GetLeft(myCanvas)) && speedMoving > 0 ? speedMoving : 0));
            Canvas.SetLeft(pointsLabel, Canvas.GetLeft(pointsLabel) + (playerHitBox.Right >= resolution.getX() / 2 + Math.Abs(Canvas.GetLeft(myCanvas)) && speedMoving > 0 ? speedMoving : 0));
            Canvas.SetLeft(lifeLabel, Canvas.GetLeft(lifeLabel) + (playerHitBox.Right >= resolution.getX() / 2 + Math.Abs(Canvas.GetLeft(myCanvas)) && speedMoving > 0 ? speedMoving : 0));
            Canvas.SetLeft(timeLabel, Canvas.GetLeft(timeLabel) + (playerHitBox.Right >= resolution.getX() / 2 + Math.Abs(Canvas.GetLeft(myCanvas)) && speedMoving > 0 ? speedMoving : 0));
            Canvas.SetLeft(myCanvas, Canvas.GetLeft(myCanvas) - (playerHitBox.Right >= resolution.getX() / 2 + Math.Abs(Canvas.GetLeft(myCanvas)) && speedMoving > 0 ? speedMoving : 0));
            playerHitBox.X = Canvas.GetLeft(rectUser);
            playerHitBox.Y = Canvas.GetTop(rectUser);

            if (flagJump)
            {
                numJump--;

                if (numJump == 0)
                    speedJumping = 10;
            }
            else
                if (!"walk".Equals(operation))
                    operation = "idle";


            if ( "jump".Equals(operation))
            {
                ImageProcessing.changeSprite(ref player, ref rectUser, "images\\character\\jump\\" + HelpFunctions.leadingZeros(numsprite) + ".png");
                numsprite++;
                numsprite %= 8;
            }
            else if ( "walk".Equals(operation))
            {
                ImageProcessing.changeSprite(ref player, ref rectUser, "images\\character\\walk\\" + HelpFunctions.leadingZeros(numsprite) + ".png");
                numsprite++;
                numsprite %= 26;
            }
            else if ( "idle".Equals(operation))
            {
                ImageProcessing.changeSprite(ref player, ref rectUser, "images\\character\\idle\\" + HelpFunctions.leadingZeros(numsprite) + ".png");
                numsprite++;
                numsprite %= 36;
            }

            if (playerHitBox.Y > resolution.getY())
                death();
        }

        private void Canvas_KeyUp(object sender, KeyEventArgs e)
        {

            if (!flagJump)
            {
                if (e.Key == Key.Right)
                {
                    operation = "walk";
                    speedMoving = 7;
                }
                else if (e.Key == Key.Left)
                {
                    operation = "walk";
                    speedMoving = -7;
                }
                else if (e.Key == Key.Space)
                {
                    if (!moveUser.IsEnabled)
                    {
                        numLifes = 3;
                        play(currMap = 0);
                    }

                    operation = "idle";
                    speedMoving = 0;
                }
                else if (e.Key == Key.Up && Keyboard.IsKeyDown(Key.Right))
                {
                    operation = "jump";
                    speedMoving = 3;
                    speedJumping = -10;
                    numJump = 40;
                    flagJump = true;
                }
                else if (e.Key == Key.Up && Keyboard.IsKeyDown(Key.Left))
                {
                    operation = "jump";
                    speedMoving = -3;
                    speedJumping = -10;
                    numJump = 40;
                    flagJump = true;
                }
                else if (e.Key == Key.Up)
                {
                    operation = "jump";
                    speedMoving = 0;
                    speedJumping = -10;
                    numJump = 40;
                    flagJump = true;
                }

                numsprite = 0;
            }

            if ( e.Key == Key.Escape) 
                Close();
        }

        private void Music_SoundOnOff(object sender, MouseButtonEventArgs e)
        {
            if (numMusic == 1)
            {
                media.Play();
                ImageProcessing.changeSprite(ref soundOnOff, ref rectMusic, "images\\icons\\on.png");
            }
            else
            {
                media.Pause();
                ImageProcessing.changeSprite(ref soundOnOff, ref rectMusic, "images\\icons\\off.png");
            }

            numMusic = numMusic == 1 ? 2 : 1;
        }

        private void death()
        {
            numLifes--;

            changeContentLabel(ref lifeLabel, "Lives: " + numLifes);

            if (numLifes <= 0)
            {
                flagJump = false;
                moveUser.Stop();
                outOfMap.Stop();

                addInfo.Content = "Esc - For EXIT\n\nSpace - For Restarting the Game";
                Canvas.SetLeft(addInfo, Math.Abs(Canvas.GetLeft(myCanvas)) + 200);
                Canvas.SetTop(addInfo, 128);
                playEffectMusic("music\\effect\\gameOver.wav");
            }
            else if (numLifes > 0)
            {
                playEffectMusic("music\\effect\\death.wav");
                play(currMap);
            }
        }

        private void play(int level)
        {
            if ( level > maxMap)
            {
                numLifes = 1;
                death();
                return;
            }

            resetAllValues();

            //here we need to input objects
            InsertingValues.getObjects(assetDir + "\\level\\" + level + ".txt");

            if ( obj.Count == 0 )
            {
                MessageBox.Show("Configuration is not valid!!!");
                Close();
            }

            //we need to input type of the objects
            InsertingValues.getTypeOfObjects(assetDir + "\\level\\" + level + "Type.txt");
            InsertingValues.getRewordBoxSize(assetDir + "\\level\\" + level + "jumpNumCoin.txt");
            InsertingValues.getEnemies(assetDir + "\\level\\" + level + "enemy.txt");
            if ( type.Count == 0 || type.Count != obj.Count || type.Count != jumpNumCoin.Count )
            {
                MessageBox.Show("Configuration is not valid!!!");
                Close();
            }

            //we will probaably not use even half but it's small size array so who cares
            for (int i = 0; i < obj.Count; i++)
                imgBrush.Add(new ImageBrush());

            //we need to set canvas with specific objects
            HelpFunctions.configureCanvas(ref myCanvas);

            //fill constatnt sprites

            ImageProcessing.changeSprite(ref soundOnOff, ref rectMusic, "images\\icons\\on.png");
            ImageProcessing.changeSprite(ref backGround, ref rectBack, "images\\background\\map" + currMap + ".png");
            ImageProcessing.changeSprite(ref player, ref rectUser, "images\\character\\idle\\000.png");

            playerHitBox.X = Canvas.GetLeft(rectUser);
            playerHitBox.Y = Canvas.GetTop(rectUser);
            playerHitBox.Width = rectUser.Width;
            playerHitBox.Height = rectUser.Height;

            rectMusic.MouseLeftButtonUp += Music_SoundOnOff;
            myCanvas.KeyUp += Canvas_KeyUp;
            myCanvas.Focus();

            Canvas.SetLeft(myCanvas, 0);
            Canvas.SetTop(myCanvas, 0);

            lifeLabel.Content = "Lives: " + numLifes;
            pointsLabel.Content = "Points: " + points;
            timeLabel.Content = "Time: " + timeRem;

            showTutorial(currMap);

            media.Open(new Uri(assetDir + "\\music\\magic.mp3"));
            media.MediaEnded += new EventHandler(Media_Ended);
            media.Play();
            media.Volume = 0.5;

            moveUser.Start();
            coinMovement.Start();
            timeRemaining.Start();
            dtEnemy.Start();

            myCanvas.Children.Add(enemyPointsLabel);
        }

        //only is used for first level
        private void showTutorial(int level)
        {
            myCanvas.Children.Add(leftRect);
            myCanvas.Children.Add(rightRect);
            myCanvas.Children.Add(stopRect);
            myCanvas.Children.Add(jumpRect);
            myCanvas.Children.Add(leftLabel);
            myCanvas.Children.Add(rightLabel);
            myCanvas.Children.Add(stopLabel);
            myCanvas.Children.Add(jumpLabel);
            myCanvas.Children.Add(addInfo);

            ImageBrush[] a = new ImageBrush[4];
            for (int i = 0; i < 4; i++)
                a[i] = new ImageBrush();

            leftRect.Width = 32;
            leftRect.Height = 32;
            rightRect.Width = 32;
            rightRect.Height = 32;
            stopRect.Width = 32;
            stopRect.Height = 32;
            jumpRect.Width = 32;
            jumpRect.Height = 32;
            ImageProcessing.changeSprite(ref a[0], ref leftRect, "images\\icons\\left.png");
            ImageProcessing.changeSprite(ref a[1], ref rightRect, "images\\icons\\right.png");
            ImageProcessing.changeSprite(ref a[2], ref stopRect, "images\\icons\\stop.png");
            ImageProcessing.changeSprite(ref a[3], ref jumpRect, "images\\icons\\jump.png");

            Canvas.SetLeft(leftRect, 20);
            Canvas.SetLeft(rightRect, 20);
            Canvas.SetLeft(stopRect, 20);
            Canvas.SetLeft(jumpRect, 20);

            Canvas.SetTop(leftRect, 128);
            Canvas.SetTop(rightRect, 192);
            Canvas.SetTop(stopRect, 256);
            Canvas.SetTop(jumpRect, 312);

            HelpFunctions.setStyle(ref leftLabel, new FontFamily("Parsons"), 20);
            HelpFunctions.setStyle(ref rightLabel, new FontFamily("Parsons"), 20);
            HelpFunctions.setStyle(ref stopLabel, new FontFamily("Parsons"), 20);
            HelpFunctions.setStyle(ref jumpLabel, new FontFamily("Parsons"), 20);
            HelpFunctions.setStyle(ref addInfo, new FontFamily("Parsons"), 20);

            Canvas.SetLeft(leftLabel, 60);
            Canvas.SetLeft(rightLabel, 60);
            Canvas.SetLeft(stopLabel, 60);
            Canvas.SetLeft(jumpLabel, 60);
            Canvas.SetLeft(addInfo, 190);

            Canvas.SetTop(leftLabel, 128);
            Canvas.SetTop(rightLabel, 192);
            Canvas.SetTop(stopLabel, 256);
            Canvas.SetTop(jumpLabel, 312);
            Canvas.SetTop(addInfo, 192);

            leftLabel.Content = "Left arrow";
            rightLabel.Content = "Right arrow";
            stopLabel.Content = "Space";
            jumpLabel.Content = "Up arrow";
            addInfo.Content = "To move sideways use combination of \n\nleft or right arrow with up arrow.";

            if ( level != 0)
            {
                leftLabel.Visibility = Visibility.Hidden;
                rightLabel.Visibility = Visibility.Hidden;
                stopLabel.Visibility = Visibility.Hidden;
                jumpLabel.Visibility = Visibility.Hidden;
                leftRect.Visibility = Visibility.Hidden;
                rightRect.Visibility = Visibility.Hidden;
                stopRect.Visibility = Visibility.Hidden;
                jumpRect.Visibility = Visibility.Hidden;
                addInfo.Content = "";
            }
        }

        private void resetAllValues()
        {
            flagJump = false;
            timeRem = 180;
            speedMoving = 0;
            speedJumping = 0;
            speedEnemy = 2;
            numJump = 0;
            numMusic = 2;
            numCoin = 1;
            numsprite = 0;

            //all Dispatchers
            dtEnemy.Stop();
            moveUser.Stop();
            coinMovement.Stop();
            coinJump.Stop();
            timeRemaining.Stop();

            enemyPointsLabel.Content = "";

            jumpCoin.Clear();
            jumpNumCoin.Clear();
            listEnemy.Clear();
            rect.Clear();
            imgBrush.Clear();
            obj.Clear();
            type.Clear();

            operation = "idle";
        }

        //when music ends we want it to begin playing again
        private void Media_Ended(object sender, EventArgs e)
        {
            media.Position = TimeSpan.Zero;
            media.Play();
        }

        private void changeContentLabel(ref Label a, string text)
        {
            a.Content = text;
        }

        private void playEffectMusic(string path)
        {
            mediaEffect.Open(new Uri(assetDir + "\\" + path));
            mediaEffect.Volume = 0.8;
            mediaEffect.Play();
        }
    }
}