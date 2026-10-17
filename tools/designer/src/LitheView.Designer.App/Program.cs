// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            DesignerRepositoryContext repository = DesignerRepositoryContext.Load(
                RepositoryLocator.Find(args));
            string? projectPath = CommandLine.ProjectPath(args);
            Application.Run(new DesignerMainForm(repository, projectPath));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or
                ArgumentException)
        {
            MessageBox.Show(
                exception.Message,
                "LitheView Designer could not start",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

internal static class CommandLine
{
    public static string? ProjectPath(string[] args)
    {
        for (int index = 0; index + 1 < args.Length; ++index)
        {
            if (string.Equals(args[index], "--project", StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }
        return null;
    }
}
