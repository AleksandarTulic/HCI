using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhereAmI
{
    public enum typeEnemy
    {
        enemy1,
        enemy2
    }
    public class Enemy
    {
        private typeEnemy type;
        private int walkSize;
        private int speed = MainWindow.speedEnemy;

        private int XNow;
        private int leftMaks;
        private int rightMaks;

        private int numPic = 0; //picture
        private readonly int maks;
        private int INC_DEC = 1;

        public Enemy(typeEnemy type, int walkSize, int XNow)
        {
            Console.WriteLine(numPic + " " + INC_DEC);
            this.type = type;
            this.walkSize = walkSize;
            this.XNow = XNow;

            leftMaks = XNow - walkSize / 2;
            rightMaks = XNow + walkSize / 2;

            if (type == typeEnemy.enemy1)
                maks = 11;
            else
                maks = 4;
        }

        public void setWalkSize(int walkSize) => this.walkSize = walkSize;

        public void setType(typeEnemy type) => this.type = type;

        public void setNumPic(int numPic) => this.numPic = numPic;

        public void setSpeed(int speed) => this.speed = speed;

        public void setXnow(int XNow) => this.XNow = XNow;

        public int getXNow() => XNow;

        public int getSpeed() => speed;

        public int getNumPic() => numPic;

        public typeEnemy getType() => type;

        public int getWalkSize() => walkSize;

        public void incDecSprite()
        {
            numPic += INC_DEC;
            INC_DEC = numPic == -1 ? 1 : numPic == maks ? -1 : INC_DEC;
            numPic = numPic == -1 ? 0 : numPic == maks ? maks - 1 : numPic;

            if (XNow + speed <= leftMaks)
                speed = MainWindow.speedEnemy;
            else if (XNow + speed >= rightMaks)
                speed = -MainWindow.speedEnemy;
        }
    }
}
