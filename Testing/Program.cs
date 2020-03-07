using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Testing
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                new TriangulationTest().Run();

                Console.WriteLine("Successfully executed tests, press any key to exit.");
            }
            catch(Exception e)
            {
                
                Console.WriteLine(e.Message);
            }

            Console.ReadKey();
        }
    }
}
