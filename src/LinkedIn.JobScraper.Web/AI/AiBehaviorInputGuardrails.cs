using System.Text;
using System.Text.RegularExpressions;

namespace LinkedIn.JobScraper.Web.AI;

public static partial class AiBehaviorInputGuardrails
{
    private const int BehavioralInstructionsMaxLength = 4000;
    private const int PrioritySignalsMaxLength = 3000;
    private const int ExclusionSignalsMaxLength = 3000;
    private const int CombinedMaxLength = 9000;
    private const int SoftWarningLongTextThreshold = 2000;
    private const int SoftWarningBehaviorLengthThreshold = 80;
    private const int SoftWarningSignalCountThreshold = 3;

    private static readonly Regex[] InstructionOverridePatterns =
    [
        new(
            @"\b(ignore|disregard|forget)\b.{0,30}\b(previous|prior)\b.{0,20}\b(instruction|prompt)s?\b",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled),
        new(
            @"\b(ignore|disregard|override)\b.{0,30}\b(system|developer)\b.{0,20}\b(instruction|prompt)s?\b",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled)
    ];

    private static readonly Regex[] OutputContractBypassPatterns =
    [
        new(
            @"\b(do\s*not|don't|never)\b.{0,30}\b(return|output|respond)\b.{0,20}\bjson\b",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled),
        new(
            @"\brespond\b.{0,20}\bplain\s*text\b",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled),
        new(
            @"\b(ignore|skip)\b.{0,20}\bjson\b",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled)
    ];

    private static readonly Regex[] SensitiveDisclosurePatterns =
    [
        new(
            @"\b(reveal|show|print|expose|leak)\b.{0,40}\b(api\s*key|secret|system\s*prompt|developer\s*prompt)\b",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled)
    ];

    public static AiBehaviorGuardrailEvaluation Evaluate(
        string? behavioralInstructions,
        string? prioritySignals,
        string? exclusionSignals)
    {
        var normalizedBehavioralInstructions = NormalizeText(behavioralInstructions);
        var normalizedPrioritySignals = NormalizeText(prioritySignals);
        var normalizedExclusionSignals = NormalizeText(exclusionSignals);

        var blockingErrors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var warnings = new List<string>();

        ValidateRequiredAndLength(
            normalizedBehavioralInstructions,
            "BehavioralInstructions",
            "Behavioral Instructions",
            BehavioralInstructionsMaxLength,
            blockingErrors);
        ValidateRequiredAndLength(
            normalizedPrioritySignals,
            "PrioritySignals",
            "Priority Signals",
            PrioritySignalsMaxLength,
            blockingErrors);
        ValidateRequiredAndLength(
            normalizedExclusionSignals,
            "ExclusionSignals",
            "Exclusion Signals",
            ExclusionSignalsMaxLength,
            blockingErrors);

        var combinedLength = normalizedBehavioralInstructions.Length +
                             normalizedPrioritySignals.Length +
                             normalizedExclusionSignals.Length;

        if (combinedLength > CombinedMaxLength)
        {
            AddBlockingError(
                blockingErrors,
                "BehavioralInstructions",
                $"Combined AI behavior input is too long. Keep total content at {CombinedMaxLength} characters or fewer.");
        }

        ValidateCriticalPatternBlocks(normalizedBehavioralInstructions, "BehavioralInstructions", blockingErrors);
        ValidateCriticalPatternBlocks(normalizedPrioritySignals, "PrioritySignals", blockingErrors);
        ValidateCriticalPatternBlocks(normalizedExclusionSignals, "ExclusionSignals", blockingErrors);

        if (normalizedBehavioralInstructions.Length < SoftWarningBehaviorLengthThreshold)
        {
            warnings.Add(
                "Behavioral Instructions are very short; scoring quality may be unstable without clearer decision policy.");
        }

        if (CountSignalItems(normalizedPrioritySignals) < SoftWarningSignalCountThreshold)
        {
            warnings.Add(
                "Priority Signals contain very few concrete items; score boosts may be too weak or noisy.");
        }

        if (CountSignalItems(normalizedExclusionSignals) < SoftWarningSignalCountThreshold)
        {
            warnings.Add(
                "Exclusion Signals contain very few blocker items; skip/review behavior may be less reliable.");
        }

        if (normalizedBehavioralInstructions.Length > SoftWarningLongTextThreshold ||
            normalizedPrioritySignals.Length > SoftWarningLongTextThreshold ||
            normalizedExclusionSignals.Length > SoftWarningLongTextThreshold)
        {
            warnings.Add(
                "One or more AI behavior fields are very long; this can increase token usage and latency.");
        }

        return new AiBehaviorGuardrailEvaluation(
            normalizedBehavioralInstructions,
            normalizedPrioritySignals,
            normalizedExclusionSignals,
            blockingErrors.ToDictionary(
                static entry => entry.Key,
                static entry => (IReadOnlyList<string>)entry.Value),
            warnings);
    }

    private static void ValidateRequiredAndLength(
        string value,
        string fieldKey,
        string fieldName,
        int maxLength,
        IDictionary<string, List<string>> blockingErrors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddBlockingError(blockingErrors, fieldKey, $"{fieldName} cannot be empty.");
            return;
        }

        if (value.Length > maxLength)
        {
            AddBlockingError(
                blockingErrors,
                fieldKey,
                $"{fieldName} must be {maxLength} characters or fewer.");
        }
    }

    private static void ValidateCriticalPatternBlocks(
        string value,
        string fieldKey,
        IDictionary<string, List<string>> blockingErrors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (MatchesAny(value, InstructionOverridePatterns))
        {
            AddBlockingError(
                blockingErrors,
                fieldKey,
                "Instruction override attempts are not allowed in AI behavior fields.");
        }

        if (MatchesAny(value, OutputContractBypassPatterns))
        {
            AddBlockingError(
                blockingErrors,
                fieldKey,
                "Output contract bypass instructions are not allowed in AI behavior fields.");
        }

        if (MatchesAny(value, SensitiveDisclosurePatterns))
        {
            AddBlockingError(
                blockingErrors,
                fieldKey,
                "Requests to reveal sensitive prompts or secrets are not allowed in AI behavior fields.");
        }
    }

    private static bool MatchesAny(string input, IEnumerable<Regex> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (pattern.IsMatch(input))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalizedNewLines = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var sanitized = new StringBuilder(normalizedNewLines.Length);

        foreach (var character in normalizedNewLines)
        {
            if (char.IsControl(character) && character is not ('\n' or '\t'))
            {
                sanitized.Append(' ');
                continue;
            }

            sanitized.Append(character);
        }

        var lines = sanitized.ToString()
            .Split('\n')
            .Select(static line => MultiWhitespaceRegex().Replace(line.Trim(), " "))
            .ToArray();

        var merged = string.Join('\n', lines);
        var compacted = ThreeOrMoreNewLinesRegex().Replace(merged, "\n\n");
        return compacted.Trim();
    }

    private static int CountSignalItems(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return value.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static item => item.Trim())
            .Where(static item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private static void AddBlockingError(
        IDictionary<string, List<string>> errors,
        string fieldKey,
        string message)
    {
        if (!errors.TryGetValue(fieldKey, out var fieldErrors))
        {
            fieldErrors = [];
            errors[fieldKey] = fieldErrors;
        }

        if (!fieldErrors.Contains(message, StringComparer.Ordinal))
        {
            fieldErrors.Add(message);
        }
    }

    [GeneratedRegex(@"\s{2,}", RegexOptions.Compiled)]
    private static partial Regex MultiWhitespaceRegex();

    [GeneratedRegex(@"\n{3,}", RegexOptions.Compiled)]
    private static partial Regex ThreeOrMoreNewLinesRegex();
}

public sealed record AiBehaviorGuardrailEvaluation(
    string BehavioralInstructions,
    string PrioritySignals,
    string ExclusionSignals,
    IReadOnlyDictionary<string, IReadOnlyList<string>> BlockingErrors,
    IReadOnlyList<string> SoftWarnings)
{
    public bool IsBlocked => BlockingErrors.Count > 0;
}
