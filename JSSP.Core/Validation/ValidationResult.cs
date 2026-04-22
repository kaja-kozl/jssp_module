namespace JSSP.Core.Validation;

/// <summary>
/// Immutable result returned by <see cref="ScheduleValidator.Validate"/>.
/// Carries a pass/fail flag and the full list of constraint violations found,
/// one message per violation.
/// </summary>
/// <param name="IsValid">
/// <see langword="true"/> when no constraint violations were detected.
/// </param>
/// <param name="Violations">
/// Human-readable descriptions of every violation found, in the order they
/// were detected. Empty when <paramref name="IsValid"/> is <see langword="true"/>.
/// </param>
public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Violations);
