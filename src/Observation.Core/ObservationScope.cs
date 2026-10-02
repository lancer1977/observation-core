using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Observation.Core;

/// <summary>
/// Identifies an ordered, hierarchical scope using opaque string segments.
/// </summary>
[JsonConverter(typeof(ObservationScopeJsonConverter))]
public sealed class ObservationScope : IEquatable<ObservationScope>, IComparable<ObservationScope>
{
    private readonly ReadOnlyCollection<string> segments;

    private ObservationScope(string[] segments)
    {
        this.segments = Array.AsReadOnly(segments);
    }

    /// <summary>
    /// Creates a scope from one or more non-blank ordered segments.
    /// </summary>
    /// <param name="segments">The ordered scope segments.</param>
    /// <returns>A scope containing a defensive copy of <paramref name="segments"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when no segment is supplied or a segment is blank.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="segments"/> is <see langword="null"/>.</exception>
    public static ObservationScope Of(params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        if (segments.Length == 0)
        {
            throw new ArgumentException("A scope must contain at least one segment.", nameof(segments));
        }

        var copy = new string[segments.Length];
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            if (string.IsNullOrWhiteSpace(segment))
            {
                throw new ArgumentException("Scope segments must be non-blank.", nameof(segments));
            }

            copy[index] = segment;
        }

        return new ObservationScope(copy);
    }

    /// <summary>
    /// Gets the ordered scope segments.
    /// </summary>
    public IReadOnlyList<string> Segments => segments;

    /// <summary>
    /// Determines whether this scope starts with all segments in <paramref name="prefix"/>.
    /// </summary>
    /// <param name="prefix">The candidate leading scope.</param>
    /// <returns><see langword="true"/> when <paramref name="prefix"/> is a leading run of this scope's segments.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="prefix"/> is <see langword="null"/>.</exception>
    public bool IsPrefixedBy(ObservationScope prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        if (prefix.segments.Count > segments.Count)
        {
            return false;
        }

        for (var index = 0; index < prefix.segments.Count; index++)
        {
            if (!string.Equals(segments[index], prefix.segments[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Creates a new scope by appending one non-blank segment.
    /// </summary>
    /// <param name="segment">The segment to append.</param>
    /// <returns>A new scope with <paramref name="segment"/> appended.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="segment"/> is blank.</exception>
    public ObservationScope Append(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            throw new ArgumentException("Scope segments must be non-blank.", nameof(segment));
        }

        var appended = new string[segments.Count + 1];
        segments.CopyTo(appended, 0);
        appended[^1] = segment;
        return new ObservationScope(appended);
    }

    /// <inheritdoc />
    public bool Equals(ObservationScope? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || segments.Count != other.segments.Count)
        {
            return false;
        }

        for (var index = 0; index < segments.Count; index++)
        {
            if (!string.Equals(segments[index], other.segments[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ObservationScope);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var segment in segments)
        {
            hash.Add(segment, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public int CompareTo(ObservationScope? other)
    {
        if (other is null)
        {
            return 1;
        }

        var sharedLength = Math.Min(segments.Count, other.segments.Count);
        for (var index = 0; index < sharedLength; index++)
        {
            var comparison = string.CompareOrdinal(segments[index], other.segments[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return segments.Count.CompareTo(other.segments.Count);
    }

    /// <summary>
    /// Returns a diagnostic representation of the ordered segments.
    /// </summary>
    /// <returns>The segments joined with a slash for diagnostics only.</returns>
    public override string ToString() => string.Join('/', segments);
}

internal sealed class ObservationScopeJsonConverter : JsonConverter<ObservationScope>
{
    public override ObservationScope Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var segments = JsonSerializer.Deserialize<string[]>(ref reader, options);
        return ObservationScope.Of(segments ?? throw new JsonException("Scope segments cannot be null."));
    }

    public override void Write(Utf8JsonWriter writer, ObservationScope value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Segments, options);
    }
}
