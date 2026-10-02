namespace Observation.Core;

/// <summary>Wraps a parsed value with the timestamp and optional evidence identifier supplied by its producer.</summary>
/// <typeparam name="TResult">The parsed value type.</typeparam>
public sealed record ObservationCacheValue<TResult>(TResult Value, DateTimeOffset ObservedAt, Guid? EvidenceId = null);

/// <summary>Describes the input and freshness policy for one cache lookup.</summary>
/// <typeparam name="TInput">The parser input type.</typeparam>
public sealed record ObservationCacheRequest<TInput>
{
    /// <summary>Creates a validated cache request.</summary>
    public ObservationCacheRequest(
        TInput input,
        ObservationScope scope,
        string fingerprint,
        string parserVersion,
        TimeSpan maxAge,
        IReadOnlySet<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            throw new ArgumentException("A fingerprint is required.", nameof(fingerprint));
        }

        if (string.IsNullOrWhiteSpace(parserVersion))
        {
            throw new ArgumentException("A parser version is required.", nameof(parserVersion));
        }

        if (maxAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAge));
        }

        Input = input;
        Scope = scope;
        Fingerprint = fingerprint;
        ParserVersion = parserVersion;
        MaxAge = maxAge;
        Tags = new HashSet<string>(tags ?? new HashSet<string>(), StringComparer.Ordinal);
    }

    /// <summary>Gets the parser input.</summary>
    public TInput Input { get; init; }

    /// <summary>Gets the typed observation scope.</summary>
    public ObservationScope Scope { get; init; }

    /// <summary>Gets the input fingerprint.</summary>
    public string Fingerprint { get; init; }

    /// <summary>Gets the parser version.</summary>
    public string ParserVersion { get; init; }

    /// <summary>Gets the maximum permitted evidence age.</summary>
    public TimeSpan MaxAge { get; init; }

    /// <summary>Gets the defensively copied, ordinal tag set.</summary>
    public IReadOnlySet<string> Tags { get; init; }
}

/// <summary>Explains the outcome of an observation cache lookup.</summary>
public enum ObservationCacheDisposition
{
    /// <summary>An unexpired cached value was returned.</summary>
    Hit,
    /// <summary>A parser produced and cached new evidence.</summary>
    Miss,
    /// <summary>No usable evidence was available.</summary>
    Expired,
    /// <summary>Previously usable evidence was invalidated.</summary>
    Invalidated,
    /// <summary>The request or parser produced no usable evidence.</summary>
    Unknown
}

/// <summary>Evidence about a cache lookup outcome.</summary>
public sealed record ObservationCacheEvidence(
    ObservationCacheDisposition Disposition,
    int Count,
    string? Reason = null,
    Guid? EvidenceId = null);

/// <summary>Returns a cache value together with its lookup evidence.</summary>
/// <typeparam name="T">The cached value type.</typeparam>
public sealed record ObservationCacheResult<T>(T? Value, ObservationCacheEvidence Evidence);
