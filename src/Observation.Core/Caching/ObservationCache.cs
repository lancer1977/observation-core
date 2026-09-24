namespace Observation.Core;

/// <summary>Provides bounded, freshness-aware reuse of caller-supplied parsed observations.</summary>
/// <typeparam name="TInput">The parser input type.</typeparam>
/// <typeparam name="TResult">The parsed result type.</typeparam>
public sealed class ObservationCache<TInput, TResult>
{
    private readonly record struct ScopeKey(ObservationScope Scope);
    private readonly record struct VersionToken(string ParserVersion, string Fingerprint);
    private readonly record struct CacheKey(ScopeKey Scope, VersionToken Version);
    private sealed record Entry(ObservationCacheValue<TResult> Value, long Sequence, long Version, IReadOnlySet<string> Tags);
    private sealed record Pending(ScopeKey Scope, long Version, IReadOnlySet<string> Tags, CancellationTokenSource Cancellation);

    private readonly object gate = new();
    private readonly Dictionary<CacheKey, Entry> entries = [];
    private readonly Queue<(CacheKey Key, long Sequence)> order = [];
    private readonly Dictionary<ScopeKey, long> versions = [];
    private readonly Dictionary<ScopeKey, VersionToken> fingerprints = [];
    private readonly List<Pending> pending = [];
    private readonly int capacity;
    private readonly Func<DateTimeOffset> clock;
    private long nextSequence;

    /// <summary>Creates a cache with a bounded entry and parser capacity.</summary>
    /// <param name="capacity">The maximum number of cached entries and concurrent parsers.</param>
    /// <param name="clock">The clock used for freshness checks.</param>
    public ObservationCache(int capacity = 128, Func<DateTimeOffset>? clock = null)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        this.capacity = capacity;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Gets or creates a value, suppressing stale, future, cancelled, or invalidated results.</summary>
    public async Task<ObservationCacheResult<TResult>> GetOrCreateAsync(
        ObservationCacheRequest<TInput> request,
        Func<TInput, CancellationToken, Task<ObservationCacheValue<TResult>?>> parse,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(parse);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request.Scope);
        if (string.IsNullOrWhiteSpace(request.Fingerprint) || string.IsNullOrWhiteSpace(request.ParserVersion) || request.MaxAge < TimeSpan.Zero)
        {
            throw new ArgumentException("Cache request metadata is invalid.", nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.Tags);

        var key = new CacheKey(new ScopeKey(request.Scope), new VersionToken(request.ParserVersion, request.Fingerprint));
        var tags = new HashSet<string>(request.Tags, StringComparer.Ordinal);
        Pending mine;

        lock (gate)
        {
            if (request.Input is null)
            {
                AdvanceScope(key.Scope);
                PruneMetadata();
                return Unknown("Input is required.");
            }

            // A change of parser version or fingerprint for the same scope invalidates everything earlier for it.
            if (fingerprints.TryGetValue(key.Scope, out var current) && current != key.Version)
            {
                AdvanceScope(key.Scope);
            }

            fingerprints[key.Scope] = key.Version;
            var version = versions.GetValueOrDefault(key.Scope);
            if (entries.TryGetValue(key, out var entry))
            {
                if (entry.Version == version && IsFresh(entry.Value, request.MaxAge, clock()))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    entries[key] = entry with { Tags = entry.Tags.Concat(tags).ToHashSet(StringComparer.Ordinal) };
                    return Result(entry.Value, ObservationCacheDisposition.Hit);
                }

                entries.Remove(key);
            }

            if (pending.Count >= capacity)
            {
                PruneMetadata();
                return Unknown("Parser capacity is exhausted.");
            }

            mine = new(key.Scope, version, tags, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
            pending.Add(mine);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            mine.Cancellation.Token.ThrowIfCancellationRequested();
            var value = await parse(request.Input, mine.Cancellation.Token).WaitAsync(mine.Cancellation.Token).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            lock (gate)
            {
                if (mine.Cancellation.IsCancellationRequested || mine.Version != versions.GetValueOrDefault(key.Scope))
                {
                    return Invalidated("Relevant state changed while parsing.");
                }

                if (value is null || value.Value is null)
                {
                    return Unknown("No evidence produced.");
                }

                if (!IsFresh(value, request.MaxAge, clock()))
                {
                    return Unknown("Evidence is stale or from the future.");
                }

                var sequence = ++nextSequence;
                entries[key] = new(value, sequence, mine.Version, tags);
                order.Enqueue((key, sequence));
                while (order.Count > capacity)
                {
                    var old = order.Dequeue();
                    if (entries.TryGetValue(old.Key, out var oldEntry) && oldEntry.Sequence == old.Sequence)
                    {
                        entries.Remove(old.Key);
                    }
                }

                return Result(value, ObservationCacheDisposition.Miss);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (mine.Cancellation.IsCancellationRequested)
        {
            return Invalidated("Parsing was cancelled by invalidation.");
        }
        finally
        {
            lock (gate)
            {
                pending.Remove(mine);
                PruneMetadata();
            }

            mine.Cancellation.Dispose();
        }
    }

    /// <summary>Invalidates entries and pending parsers in the specified scope and its descendants.</summary>
    /// <param name="scope">The scope prefix to invalidate.</param>
    public void InvalidateScope(ObservationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        lock (gate)
        {
            foreach (var affected in LiveScopes().Where(item => item.Scope.IsPrefixedBy(scope)).Distinct().ToArray())
            {
                AdvanceScope(affected);
            }

            PruneMetadata();
        }
    }

    /// <summary>Invalidates entries and pending parsers whose request contains the specified tag.</summary>
    /// <param name="tag">The non-blank tag to invalidate.</param>
    public void InvalidateTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("A tag is required.", nameof(tag));
        }

        lock (gate)
        {
            foreach (var affected in entries.Where(item => item.Value.Tags.Contains(tag)).Select(item => item.Key.Scope)
                .Concat(pending.Where(item => item.Tags.Contains(tag)).Select(item => item.Scope)).Distinct().ToArray())
            {
                AdvanceScope(affected);
            }

            PruneMetadata();
        }
    }

    /// <summary>Gets the number of cached entries.</summary>
    public int Count { get { lock (gate) return entries.Count; } }

    /// <summary>Gets the number of parsers currently tracked.</summary>
    public int PendingCount { get { lock (gate) return pending.Count; } }

    /// <summary>Gets the number of scopes retained for version and fingerprint safety.</summary>
    public int TrackedScopeCount { get { lock (gate) return versions.Count; } }

    private IEnumerable<ScopeKey> LiveScopes() => entries.Keys.Select(item => item.Scope).Concat(pending.Select(item => item.Scope)).Concat(fingerprints.Keys).Distinct();

    private void AdvanceScope(ScopeKey scope)
    {
        versions[scope] = versions.GetValueOrDefault(scope) + 1;
        foreach (var key in entries.Keys.Where(item => item.Scope.Equals(scope)).ToArray())
        {
            entries.Remove(key);
        }

        foreach (var item in pending.Where(item => item.Scope.Equals(scope)).ToArray())
        {
            _ = item.Cancellation.CancelAsync();
        }
    }

    private void PruneMetadata()
    {
        var live = entries.Keys.Select(item => item.Scope).Concat(pending.Select(item => item.Scope)).ToHashSet();
        foreach (var scope in versions.Keys.Concat(fingerprints.Keys).Distinct().Where(scope => !live.Contains(scope)).ToArray())
        {
            versions.Remove(scope);
            fingerprints.Remove(scope);
        }
    }

    private static bool IsFresh(ObservationCacheValue<TResult> value, TimeSpan maxAge, DateTimeOffset now)
    {
        var age = now - value.ObservedAt;
        return value.ObservedAt != default && age >= TimeSpan.Zero && age < maxAge;
    }

    private ObservationCacheResult<TResult> Result(ObservationCacheValue<TResult> value, ObservationCacheDisposition disposition) =>
        new(value.Value, new(disposition, entries.Count, EvidenceId: value.EvidenceId));

    private ObservationCacheResult<TResult> Unknown(string reason) => new(default, new(ObservationCacheDisposition.Unknown, entries.Count, reason));

    private ObservationCacheResult<TResult> Invalidated(string reason) => new(default, new(ObservationCacheDisposition.Invalidated, entries.Count, reason));
}
