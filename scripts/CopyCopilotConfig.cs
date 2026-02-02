#!/usr/bin/dotnet

using System;
using System.IO;

DirectoryInfo copilotConfigDir = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot"));
FileInfo copilotConfigFile = new(Path.Combine(copilotConfigDir.FullName, "config.json"));
if (!copilotConfigFile.Exists)
{
    Console.WriteLine("No Copilot config file found, You need to run `copilot` and use `/login`.");
    return;
}

DirectoryInfo targetDir = new(Path.Combine(Directory.GetCurrentDirectory(), "out", ".copilot"));
if (!targetDir.Exists)
{
    targetDir.Create();
}

FileInfo targetFile = new(Path.Combine(targetDir.FullName, "config.json"));
copilotConfigFile.CopyTo(targetFile.FullName, true);
Console.WriteLine($"Copied Copilot config to {targetFile.FullName}");
