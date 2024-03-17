﻿using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Mdk.CommandLine.IngameScript.LegacyConversion;
using Mdk.CommandLine.IngameScript.Restore;
using Mdk.CommandLine.SharedApi;

namespace Mdk.CommandLine.Commands.Restore;

/// <summary>
///     The parameters for the restore-script command.
/// </summary>
public class RestoreParameters : VerbParameters
{
    /// <summary>
    ///     The project file to restore.
    /// </summary>
    public string? ProjectFile { get; set; }

    /// <inheritdoc />
    public override bool TryLoad(Queue<string> args, [MaybeNullWhen(true)] out string failureReason)
    {
        var p = new RestoreParameters();
        while (args.Count > 0)
        {
            if (TryParseGlobalOptions(args, out failureReason))
                continue;

            if (p.ProjectFile is not null)
            {
                failureReason = "Only one project file can be specified.";
                return false;
            }
            p.ProjectFile = args.Dequeue();
        }

        if (p.ProjectFile is null)
        {
            failureReason = "No project file specified.";
            return false;
        }

        if (!File.Exists(p.ProjectFile))
        {
            failureReason = $"The specified project file '{p.ProjectFile}' does not exist.";
            return false;
        }

        failureReason = null;
        return true;
    }

    /// <inheritdoc />
    public override void Help(IConsole console) =>
        console.Print("Usage: mdk restore [options] <project-file>")
            .Print()
            .Print("Checks the script in the specified project file for compatibility with the current version of MDK, "
                   + "checks nuget packages for updates, etcetera.")
            .Print()
            .Print("Options:")
            .Print("  -interactive  Prompt for confirmation before restoring the script.")
            .Print("  -log <file>   Log to the specified file.")
            .Print("  -trace        Enable trace logging.")
            .Print()
            .Print("Example:")
            .Print("  mdk restore -interactive MyProject.csproj");

    /// <inheritdoc />
    public override async Task ExecuteAsync(IConsole console, IHttpClient httpClient, IInteraction interaction)
    {
        if (ProjectFile is null) throw new CommandLineException(-1, "No project file specified.");
        if (!File.Exists(ProjectFile)) throw new CommandLineException(-1, $"The specified project file '{ProjectFile}' does not exist.");

        await foreach (var project in MdkProject.LoadAsync(ProjectFile, console))
        {
            switch (project.Type)
            {
                case MdkProjectType.Mod:
                    console.Print($"Mod projects are not yet implemented: {project.Project.Name}");
                    break;
                
                case MdkProjectType.ProgrammableBlock:
                    console.Print($"Restoring ingame script project: {project.Project.Name}");
                    var restorer = new ScriptRestorer();
                    await restorer.RestoreAsync(this, project, console, httpClient, interaction);
                    break;
                
                case MdkProjectType.LegacyProgrammableBlock:
                    console.Print($"Converting legacy ingame script project: {project.Project.Name}");
                    var converter = new LegacyConverter();
                    await converter.ConvertAsync(this, project, console, httpClient);
                    goto case MdkProjectType.ProgrammableBlock;
                    
                case MdkProjectType.Unknown:
                    console.Print($"The project file {project.Project.Name} does not seem to be an MDK project.");
                    break;
            }
        }
    }
}