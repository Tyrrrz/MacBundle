using System;

namespace MacBundle.Demo.Gui;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("MacBundle demo app");

        if (args.Length > 0)
            Console.WriteLine(string.Join(" ", args));
    }
}
