using System.Reflection;
using System.IO;
using System;

string resonitePath = Environment.GetEnvironmentVariable("ResonitePath");
if (resonitePath == null )
{
    Console.Error.WriteLine("Environment variable ResonitePath is missing!");
    Environment.Exit(1);
}

string frooxEnginePath = Path.Combine(resonitePath, "FrooxEngine.dll");

try
{
    AssemblyName frooxEngineAssembly = AssemblyName.GetAssemblyName(frooxEnginePath);
    Console.WriteLine(frooxEngineAssembly.Version);
}
catch(Exception e)
{
    Console.Error.WriteLine($"Error reading from {frooxEnginePath}:\n{e}");
    Environment.Exit(2);
}
