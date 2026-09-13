using System;
using System.Linq;
using System.Reflection;
using System.Threading;

// Runs the repo's eval-style rules checks (Tests/*.cs wrapped as static methods by wrap.py) outside
// Unity. Only checks that never touch native Unity (Resources, Debug, GameObjects) can pass here.
static class Program
{
    static int Main(string[] args)
    {
        int failures = 0;
        var methods = typeof(Program).Assembly.GetTypes().Where(t => t.Name.StartsWith("Check_"))
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public)).Where(m => m.Name == "Run")
            .OrderBy(m => m.DeclaringType.Name).ToList();
        foreach (var method in methods)
        {
            if (args.Length > 0 && !args.Any(a => method.DeclaringType.Name.Contains(a))) continue;
            string result = null; Exception error = null;
            var thread = new Thread(() => { try { result = (string)method.Invoke(null, null); } catch (TargetInvocationException e) { error = e.InnerException; } });
            thread.IsBackground = true; thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(15)))
            {
                failures++;
                Console.WriteLine(method.DeclaringType.Name + ": FAIL hung for 15s (a loop that never ends)");
                Environment.Exit(failures);
            }
            if (error != null)
            {
                failures++;
                Console.WriteLine(method.DeclaringType.Name + ": FAIL " + error.GetType().Name + ": " + error.Message);
                if (error.StackTrace != null) Console.WriteLine("   " + error.StackTrace.Split('\n').FirstOrDefault()?.Trim());
            }
            else Console.WriteLine(method.DeclaringType.Name + ": " + result);
        }
        return failures;
    }
}
