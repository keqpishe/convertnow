namespace ConvertNow.App.Services;

/// <summary>
/// Parses command-line arguments passed to Convert.Now to extract an optional input file path.
/// </summary>
public static class CliArgumentParser
{
    private const string FileFlag = "--file";
    private const string FileFlagPrefix = "--file=";

    /// <summary>
    /// Extracts an input file path from process command-line arguments (such as <see cref="Environment.GetCommandLineArgs"/>),
    /// skipping the leading executable path at index <c>0</c>.
    /// </summary>
    /// <param name="processArgs">The full process argument array including the executable path.</param>
    /// <returns>The parsed file path, or <see langword="null"/> if no file argument was provided.</returns>
    public static string? ParseProcessArgs(IReadOnlyList<string>? processArgs)
    {
        if (processArgs is null || processArgs.Count <= 1)
        {
            return null;
        }

        return Parse(processArgs.Skip(1));
    }

    /// <summary>
    /// Extracts an input file path from raw command-line arguments (excluding the executable name).
    /// Supports <c>--file &lt;path&gt;</c>, <c>--file=&lt;path&gt;</c>, and a positional <c>&lt;path&gt;</c>.
    /// </summary>
    /// <param name="args">The command-line arguments to parse.</param>
    /// <returns>The parsed file path, or <see langword="null"/> if no valid file argument was provided.</returns>
    public static string? Parse(IEnumerable<string>? args)
    {
        if (args is null)
        {
            return null;
        }

        List<string> argList = args as List<string> ?? args.ToList();
        if (argList.Count == 0)
        {
            return null;
        }

        string? positionalCandidate = null;

        for (int i = 0; i < argList.Count; i++)
        {
            string? current = argList[i];
            if (string.IsNullOrWhiteSpace(current))
            {
                continue;
            }

            string trimmed = current.Trim();

            if (string.Equals(trimmed, FileFlag, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= argList.Count)
                {
                    return null;
                }

                return NormalizePathToken(argList[i + 1]);
            }

            if (trimmed.StartsWith(FileFlagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string rawValue = trimmed[FileFlagPrefix.Length..];
                return NormalizePathToken(rawValue);
            }

            if (positionalCandidate is null && !trimmed.StartsWith('-'))
            {
                positionalCandidate = NormalizePathToken(trimmed);
            }
        }

        return positionalCandidate;
    }

    private static string? NormalizePathToken(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        string cleaned = rawValue.Trim().Trim('"').Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }
}
