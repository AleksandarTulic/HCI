using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhereAmI
{
    public class Pair
    {
        private int x;
        private int y;
        public Pair(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
        public void setX(int x)
        {
            this.x = x;
        }

        public int getX()
        {
            return x;
        }

        public int getY()
        {
            return y;
        }

        public void setY(int y)
        {
            this.y = y;
        }
    }
}
