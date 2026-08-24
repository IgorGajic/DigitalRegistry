using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalRegistry.Api.Shared.Serialization;

/// <summary>
/// Writes every <see cref="DateTime"/> as UTC, with the trailing <c>Z</c>.
/// </summary>
/// <remarks>
/// SQL Server's <c>datetime2</c> stores no offset, so EF Core hands back
/// <see cref="DateTimeKind.Unspecified"/> however the value went in. Serialised as-is that loses the
/// <c>Z</c>, and a browser reads a licence expiring at midnight UTC as midnight local — every
/// timestamp in the API shifted by the client's offset.
/// <para>
/// Fixed here rather than with an EF value converter, which was tried first: converting the property
/// stops EF translating <c>DateTime.Year</c> and <c>DateTime.Month</c>, so reports that group by month
/// fall back to client evaluation or fail outright. The defect is in how the value is written to the
/// wire, so that is where it is corrected.
/// </para>
/// <para>
/// Everything this system stores is produced in UTC, so an unspecified kind is read as UTC rather than
/// converted from local time — converting would silently move timestamps by the server's offset.
/// </para>
/// </remarks>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTime().ToUniversalTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        writer.WriteStringValue(utc);
    }
}

/// <summary>The nullable counterpart, which JSON handles as a separate converter.</summary>
public sealed class NullableUtcDateTimeJsonConverter : JsonConverter<DateTime?>
{
    private static readonly UtcDateTimeJsonConverter Inner = new();

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : Inner.Read(ref reader, typeof(DateTime), options);

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        Inner.Write(writer, value.Value, options);
    }
}
