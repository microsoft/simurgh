using Azure;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CsvDataUploader;

/// <summary>
/// Copied from https://www.codeproject.com/Tips/5255878/A-Console-Progress-Bar-in-Csharp
/// Thank you honey the codewitch!
/// </summary>
internal static class ConsoleUtility
{
    const char _block = '■';
    const string _back = "\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b";
    const string _twirl = "-\\|/";
    public static void WriteProgressBar(int percent, bool update = false)
    {
        if (update)
            Console.Write(_back);
        Console.Write("[");
        var p = (int)((percent / 10f) + .5f);
        for (var i = 0; i < 10; ++i)
        {
            if (i >= p)
                Console.Write(' ');
            else
                Console.Write(_block);
        }
        Console.Write("] {0,3:##0}%", percent);
    }
    public static void WriteProgress(int progress, bool update = false)
    {
        if (update)
            Console.Write("\b");
        Console.Write(_twirl[progress % _twirl.Length]);
    }

    private static double GetPercentile(int[] sortedData, double percentile)
    {
        int n = sortedData.Length;
        double rank = (percentile / 100.0) * (n - 1);
        int lowerIndex = (int)Math.Floor(rank);
        int upperIndex = (int)Math.Ceiling(rank);
        double weight = rank - lowerIndex;
        return sortedData[lowerIndex] * (1 - weight) + sortedData[upperIndex] * weight;
    }

    // Adjust scale for better visualization
    internal static void DrawBoxAndWhisker(int[] data, int scale = 25)
    {
        Array.Sort(data);
        int n = data.Length;
        double q1 = GetPercentile(data, 25);
        double q2 = GetPercentile(data, 50);
        double q3 = GetPercentile(data, 75);
        int min = data.Min();
        int max = data.Max();

        int minPos = min / scale;
        int q1Pos = (int)q1 / scale;
        int q2Pos = (int)q2 / scale;
        int q3Pos = (int)q3 / scale;
        int maxPos = max / scale;

        Console.WriteLine("Box and Whisker Diagram:");
        Console.WriteLine(new string('-', 50));
        Console.WriteLine($"Min: {min}");
        Console.WriteLine($"Q1: {q1}");
        Console.WriteLine($"Median (Q2): {q2}");
        Console.WriteLine($"Q3: {q3}");
        Console.WriteLine($"Max: {max}");
        Console.WriteLine(new string('-', 50));


        // Draw whiskers
        for (int i = 0; i < minPos; i++) Console.Write(" ");
        Console.Write("|");
        for (int i = minPos + 1; i < q1Pos; i++) Console.Write("-");
        Console.Write("|");
        for (int i = q1Pos + 1; i < q3Pos; i++) Console.Write("-");
        Console.Write("|");
        for (int i = q3Pos + 1; i < maxPos; i++) Console.Write("-");
        Console.Write("|");
        Console.WriteLine();

        // Draw box
        for (int i = 0; i < minPos; i++) Console.Write(" ");
        Console.Write("|");
        for (int i = minPos + 1; i < q1Pos; i++) Console.Write(" ");
        Console.Write("|");
        for (int i = q1Pos + 1; i < q3Pos; i++) Console.Write("=");
        Console.Write("|");
        for (int i = q3Pos + 1; i < maxPos; i++) Console.Write(" ");
        Console.Write("|");
        Console.WriteLine();
    }
}
