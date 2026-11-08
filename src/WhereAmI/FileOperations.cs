using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WhereAmI
{
    internal class FileOperations
    {
        public static List<string> getLines(string path)
        {
            List<string> res = new List<string>();

            foreach (string line in System.IO.File.ReadLines(path))
                res.Add(line);

            return res;
        }
    }
}
