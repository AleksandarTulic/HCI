using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace WhereAmI
{
    internal class InsertingValues
    {
        public static void getObjects(string path)
        {
            List <string> list = FileOperations.getLines(path);

            foreach (string i in list)
            {
                string []l = i.Split(null);

                int CanLeft = 0;
                int CanTop = 0;
                int width = 0;
                int height = 0;

                if (l.Length != 4)
                    return;

                try
                {
                    CanLeft = Int32.Parse (l[0]);
                    CanTop = Int32.Parse (l[1]);
                    width = Int32.Parse (l[2]);
                    height = Int32.Parse (l[3]);
                }catch (FormatException)
                {
                    Console.WriteLine("InsertingValues Class, Can not convert.");
                    return;
                }

                MainWindow.obj.Add(new Rect(CanLeft, CanTop, width, height));
            }
        }

        public static void getTypeOfObjects(string path)
        {
            List <string> list = FileOperations.getLines(path);

            foreach (string i in list)
            {
                if ("reword".Equals(i))
                    MainWindow.type.Add(WhereAmI.TypeOfObjects.reword);
                else if ("rewordBox".Equals(i))
                    MainWindow.type.Add(WhereAmI.TypeOfObjects.rewordBox);
                else if ("enemy".Equals(i))
                    MainWindow.type.Add(WhereAmI.TypeOfObjects.enemy);
                else if ("ground".Equals(i))
                    MainWindow.type.Add(WhereAmI.TypeOfObjects.ground);
                else if ("spikes".Equals(i))
                    MainWindow.type.Add(WhereAmI.TypeOfObjects.spikes);
                else if ("ending".Equals(i))
                    MainWindow.type.Add(WhereAmI.TypeOfObjects.ending);
                else if ("rewordBoxHidden".Equals(i))
                    MainWindow.type.Add(WhereAmI.TypeOfObjects.rewordBoxHidden);
                else
                    return;
            }
        }

        public static void getRewordBoxSize(string path)
        {
            List<string> list = FileOperations.getLines(path);

            int num = 0;

            for (int i = 0; i < MainWindow.type.Count; i++)
            {
                int value = 0;
                if ( MainWindow.type[i] == WhereAmI.TypeOfObjects.rewordBox || MainWindow.type[i] == WhereAmI.TypeOfObjects.rewordBoxHidden )
                {
                    try
                    {
                        value = Int32.Parse(list[num++]);
                    }catch (Exception e)
                    {
                        Console.WriteLine("Class: InsertingValues, Method: getRewordBoxSize, Casting Not Possible.\n" + e.Data);
                    }
                }

                MainWindow.jumpNumCoin.Add(value);
            }
        }

        public static void getEnemies(string path)
        {
            List<string> list = FileOperations.getLines(path);
            int num = 0;

            for (int i = 0; i < MainWindow.type.Count; i++)
            {
                int value = 0;
                string type = "ghost";

                if (MainWindow.type[i] == WhereAmI.TypeOfObjects.enemy)
                {
                    string []sp = list[num++].Split(null);

                    try
                    {
                        value = Int32.Parse(sp[0]);
                        type = sp[1];
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Class: InsertingValues, Method: getRewordBoxSize, Casting Not Possible.\n" + e.Data);
                    }

                    //MainWindow.listEnemy.Add(new Enemy("ghost".Equals(type) ? WhereAmI.typeEnemy.enemy1 : WhereAmI.typeEnemy.enemy2, value, (int)MainWindow.obj[i].X));
                }

                MainWindow.listEnemy.Add(new Enemy("ghost".Equals(type) ? WhereAmI.typeEnemy.enemy1 : WhereAmI.typeEnemy.enemy2, value, (int)MainWindow.obj[i].X));
            }
        }
    }
}
