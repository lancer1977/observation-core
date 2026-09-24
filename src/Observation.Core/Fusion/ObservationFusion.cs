namespace Observation.Core;

/// <summary>Describes the outcome of deterministic observation fusion.</summary>
public enum FusionStatus
{
    /// <summary>No usable evidence contributed to the result.</summary>
    Unknown,

    /// <summary>All usable evidence supports one value.</summary>
    Agreed,

    /// <summary>One value leads by at least the configured confidence margin.</summary>
    Resolved,

    /// <summary>Usable evidence conflicts without a sufficient margin.</summary>
    Disputed
}

/// <summary>One distinct value and the evidence that supports it.</summary>
/// <typeparam name="T">The consumer-defined observation value type.</typeparam>
public sealed record FusionCandidate<T>(T Value, double Confidence, IReadOnlyList<Guid> EvidenceIds);

/// <summary>The deterministic result of fusing observations for one subject and predicate.</summary>
/// <typeparam name="T">The consumer-defined observation value type.</typeparam>
public sealed record FusionResult<T>(
    ObservationScope Scope,
    string Subject,
    string Predicate,
    T? Value,
    double Confidence,
    FusionStatus Status,
    IReadOnlyList<Guid> EvidenceIds,
    IReadOnlyList<FusionCandidate<T>> Candidates,
    IReadOnlyList<Guid> ExcludedEvidenceIds);

/// <summary>Configures confidence, expiry, and value comparison for observation fusion.</summary>
/// <typeparam name="T">The consumer-defined observation value type.</typeparam>
public sealed class ObservationFusionOptions<T>
{
    /// <summary>Initializes options with the documented defaults.</summary>
    /// <param name="minimumConfidenceMargin">The minimum leader margin required to resolve conflict.</param>
    /// <param name="maxAge">The optional maximum inclusive-exclusive age of evidence.</param>
    /// <param name="clock">The clock used for expiry checks.</param>
    /// <param name="valueEquality">The equality comparer used to group values.</param>
    /// <param name="valueOrder">The deterministic comparer used to order candidate values.</param>
    public ObservationFusionOptions(
        double minimumConfidenceMargin = 0.2,
        TimeSpan? maxAge = null,
        Func<DateTimeOffset>? clock = null,
        IEqualityComparer<T>? valueEquality = null,
        IComparer<T>? valueOrder = null)
    {
        if (!double.IsFinite(minimumConfidenceMargin) || minimumConfidenceMargin is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumConfidenceMargin));
        }

        if (maxAge is { } age && age < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAge));
        }

        if (maxAge is not null && clock is null)
        {
            clock = static () => DateTimeOffset.UtcNow;
        }

        MinimumConfidenceMargin = minimumConfidenceMargin;
        MaxAge = maxAge;
        Clock = clock;
        ValueEquality = valueEquality ?? EqualityComparer<T>.Default;
        ValueOrder = valueOrder ?? Comparer<T>.Default;
        UsesDefaultValueOrder = valueOrder is null;
    }

    /// <summary>Gets the minimum confidence difference required to resolve conflict.</summary>
    public double MinimumConfidenceMargin { get; }

    /// <summary>Gets the optional maximum age of usable evidence.</summary>
    public TimeSpan? MaxAge { get; }

    /// <summary>Gets the expiry clock, or <see langword="null"/> when expiry is disabled.</summary>
    public Func<DateTimeOffset>? Clock { get; }

    /// <summary>Gets the equality comparer used to group values.</summary>
    public IEqualityComparer<T> ValueEquality { get; }

    /// <summary>Gets the ordering comparer used to sort candidates.</summary>
    public IComparer<T> ValueOrder { get; }

    internal bool UsesDefaultValueOrder { get; }
}

/// <summary>Fuses observations into deterministic, scope-isolated results.</summary>
/// <typeparam name="T">The consumer-defined observation value type.</typeparam>
public sealed class ObservationFusion<T>
{
    private readonly ObservationFusionOptions<T> _options;

    /// <summary>Initializes a fusion engine.</summary>
    /// <param name="options">Optional fusion configuration.</param>
    public ObservationFusion(ObservationFusionOptions<T>? options = null)
    {
        _options = options ?? new ObservationFusionOptions<T>();
        if (_options.UsesDefaultValueOrder && !SupportsDefaultOrdering())
        {
            throw new InvalidOperationException(
                $"Type '{typeof(T).FullName}' has no default ordering. Supply a ValueOrder comparer.");
        }
    }

    /// <summary>Fuses observations without performing I/O or mutating the input.</summary>
    /// <param name="observations">The observations to group and resolve.</param>
    /// <returns>One result for each distinct scope, subject, and predicate.</returns>
    public IReadOnlyList<FusionResult<T>> Resolve(IEnumerable<Observation<T>> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);

        var input = observations.Select(observation => observation ?? throw new ArgumentException(
            "Observations cannot contain null values.", nameof(observations))).ToArray();
        var now = _options.MaxAge is not null ? _options.Clock!() : default;

        return input
            .GroupBy(observation => (observation.Scope, observation.Subject, observation.Predicate))
            .OrderBy(group => group.Key.Scope)
            .ThenBy(group => group.Key.Subject, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Predicate, StringComparer.Ordinal)
            .Select(group => ResolveGroup(group, now))
            .ToArray();
    }

    private FusionResult<T> ResolveGroup(
        IGrouping<(ObservationScope Scope, string Subject, string Predicate), Observation<T>> group,
        DateTimeOffset now)
    {
        var ordered = group.OrderBy(observation => observation.Id).ToArray();
        var included = ordered.Where(observation => !IsExpired(observation, now)).ToArray();
        var excludedIds = ordered
            .Where(observation => IsExpired(observation, now))
            .Select(observation => observation.Id)
            .Order()
            .ToArray();

        if (included.Length == 0)
        {
            return new(group.Key.Scope, group.Key.Subject, group.Key.Predicate, default, 0,
                FusionStatus.Unknown, Array.Empty<Guid>(), Array.Empty<FusionCandidate<T>>(), excludedIds);
        }

        var candidates = included
            .GroupBy(observation => observation.Value, _options.ValueEquality)
            .Select(candidate => new CandidateWork(
                candidate.First().Value,
                candidate.Average(observation => observation.Confidence),
                candidate.Select(observation => observation.Id).Order().ToArray()))
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.Value, _options.ValueOrder)
            .ThenBy(candidate => candidate.EvidenceIds[0])
            .Select(candidate => new FusionCandidate<T>(candidate.Value, candidate.Confidence, candidate.EvidenceIds))
            .ToArray();

        var evidenceIds = candidates.SelectMany(candidate => candidate.EvidenceIds).Order().ToArray();
        if (candidates.Length == 1)
        {
            return new(group.Key.Scope, group.Key.Subject, group.Key.Predicate, candidates[0].Value,
                candidates[0].Confidence, FusionStatus.Agreed, evidenceIds, candidates, excludedIds);
        }

        var resolved = candidates[0].Confidence - candidates[1].Confidence >= _options.MinimumConfidenceMargin;
        return new(
            group.Key.Scope,
            group.Key.Subject,
            group.Key.Predicate,
            resolved ? candidates[0].Value : default,
            candidates[0].Confidence,
            resolved ? FusionStatus.Resolved : FusionStatus.Disputed,
            evidenceIds,
            candidates,
            excludedIds);
    }

    private bool IsExpired(Observation<T> observation, DateTimeOffset now) =>
        _options.MaxAge is { } maxAge && (observation.ObservedAt > now || now - observation.ObservedAt >= maxAge);

    private static bool SupportsDefaultOrdering()
    {
        var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return typeof(IComparable<T>).IsAssignableFrom(type) || typeof(IComparable).IsAssignableFrom(type);
    }

    private sealed record CandidateWork(T Value, double Confidence, IReadOnlyList<Guid> EvidenceIds);
}
