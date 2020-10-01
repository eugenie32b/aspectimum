using System;

namespace AspectimumDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            var lib = new Aspectimum.Demo.Lib.ConsoleDemo();

            var jim = lib.FirstDemo("Jim", "Smith", 65);
            var john = lib.FirstDemo("John", "Grey", 55);

            lib.SecondDemo(new[] { jim, john });

            Console.Out.WriteLine(jim.FullName);
        }
    }
}
