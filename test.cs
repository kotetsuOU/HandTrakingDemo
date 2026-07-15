using System;
using System.Collections.Generic;

public class Program
{
    public static void Main()
    {
        var list = new List<(int, float)>();
        list.Add((
            1,
            2f
        ));
        Console.WriteLine("OK");
    }
}
