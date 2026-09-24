using System.Text.Json.Serialization;

namespace Observation.Core;

internal static class ConfidenceValidator
{
    public static double Validate(double confidence)
    {
        if (!double.IsFinite(confidence) || confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confidence must be finite and within [0, 1].");
        }

        return confidence;
    }
}

/// <summary>
/// A domain-neutral observation with provenance and bounded confidence.
/// </summary>
/// <typeparam name="T">The observed value type.</typeparam>
public sealed record Observation<T>
{
    /// <summary>
    /// Creates and validates an observation.
    /// </summary>
    /// <param name="id">The unique evidence identifier.</param>
    /// <param name="scope">The scope in which the observation applies.</param>
    /// <param name="subject">The observed subject.</param>
    /// <param name="predicate">The observed predicate.</param>
    /// <param name="value">The observed value.</param>
    /// <param name="sourceId">The source identifier.</param>
    /// <param name="sourceKind">The opaque caller-defined source kind.</param>
    /// <param name="confidence">The confidence, from zero through one.</param>
    /// <param name="observedAt">The observation timestamp.</param>
    /// <exception cref="ArgumentException">Thrown when a string is blank, the identifier is empty, or the timestamp is default.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scope"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="confidence"/> is not finite or is outside [0, 1].</exception>
    [JsonConstructor]
    public Observation(
        Guid id,
        ObservationScope scope,
        string subject,
        string predicate,
        T value,
        string sourceId,
        string sourceKind,
        double confidence,
        DateTimeOffset observedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Observation id cannot be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(value);
        ValidateNonBlank(subject, nameof(subject));
        ValidateNonBlank(predicate, nameof(predicate));
        ValidateNonBlank(sourceId, nameof(sourceId));
        ValidateNonBlank(sourceKind, nameof(sourceKind));
        ConfidenceValidator.Validate(confidence);
        if (observedAt == default)
        {
            throw new ArgumentException("Observation timestamp cannot be default.", nameof(observedAt));
        }

        Id = id;
        Scope = scope;
        Subject = subject;
        Predicate = predicate;
        Value = value;
        SourceId = sourceId;
        SourceKind = sourceKind;
        Confidence = confidence;
        ObservedAt = observedAt;
    }

    /// <summary>Gets the unique evidence identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the scope in which the observation applies.</summary>
    public ObservationScope Scope { get; }

    /// <summary>Gets the observed subject.</summary>
    public string Subject { get; }

    /// <summary>Gets the observed predicate.</summary>
    public string Predicate { get; }

    /// <summary>Gets the observed value.</summary>
    public T Value { get; }

    /// <summary>Gets the source identifier.</summary>
    public string SourceId { get; }

    /// <summary>Gets the opaque caller-defined source kind.</summary>
    public string SourceKind { get; }

    /// <summary>Gets the confidence, from zero through one.</summary>
    public double Confidence { get; }

    /// <summary>Gets the timestamp at which the observation was made.</summary>
    public DateTimeOffset ObservedAt { get; }

    private static void ValidateNonBlank(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must be non-blank.", parameterName);
        }
    }
}
