using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Test.SingleStepTests;

public enum TestDataBusOperation
{
	None,

	[JsonStringEnumMemberName("read")]
	Read,

	[JsonStringEnumMemberName("write")]
	Write
}

[JsonConverter(typeof(TestDataStateRamValueConverter))]
public sealed class TestDataStateRamValue
{
	public required ushort Address { get; init; }
	public required byte Value { get; init; }
}

public sealed class TestDataState
{
	[JsonPropertyName("a")]
	public required byte RegA { get; init; }

	[JsonPropertyName("x")]
	public required byte RegX { get; init; }

	[JsonPropertyName("y")]
	public required byte RegY { get; init; }

	[JsonPropertyName("pc")]
	public required ushort RegPC { get; init; }

	[JsonPropertyName("s")]
	public required byte RegSPLow { get; init; }

	[JsonPropertyName("p")]
	public required byte RegStatus { get; init; }

	[JsonPropertyName("ram")]
	public required TestDataStateRamValue[] Ram { get; init; }
}

[JsonConverter(typeof(TestDataCycleConverter))]
public sealed class TestDataCycle
{
	public required ushort Address { get; init; }
	public required byte Data { get; init; }
	public TestDataBusOperation Operation { get; init; }
}

public sealed partial class TestDataTest
{
	[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)]
	[JsonSerializable(typeof(TestDataTest[]))]
	private sealed partial class SerializerContext : JsonSerializerContext;

	public static JsonTypeInfo<TestDataTest[]> JsonTypeInfo => SerializerContext.Default.TestDataTestArray;

	[JsonPropertyName("name")]
	public required string Name { get; init; }

	[JsonPropertyName("initial")]
	public required TestDataState InitialState { get; init; }

	[JsonPropertyName("final")]
	public required TestDataState FinalState { get; init; }

	[JsonPropertyName("cycles")]
	public required TestDataCycle[] Cycles { get; init; }

	public static TestDataTest[]? Deserialize(string json) => JsonSerializer.Deserialize<TestDataTest[]>(json, JsonTypeInfo);
	public static TestDataTest[]? Deserialize(Stream utf8Json) => JsonSerializer.Deserialize<TestDataTest[]>(utf8Json, JsonTypeInfo);
}

internal sealed class TestDataCycleConverter : JsonConverter<TestDataCycle>
{
	public override TestDataCycle? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartArray)
			return null;

		if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
			return null;

		var address = reader.GetUInt16();

		if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
			return null;

		var value = reader.GetByte();

		if (!reader.Read() || reader.TokenType != JsonTokenType.String)
			return null;

		var operation = reader.GetString() switch
		{
			"read" => TestDataBusOperation.Read,
			"write" => TestDataBusOperation.Write,
			_ => TestDataBusOperation.None
		};

		if (operation == 0)
			return null;

		if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
			return null;

		return new()
		{
			Address = address,
			Data = value,
			Operation = operation
		};
	}

	public override void Write(Utf8JsonWriter writer, TestDataCycle value, JsonSerializerOptions options) => throw new NotSupportedException();
}

internal sealed class TestDataStateRamValueConverter : JsonConverter<TestDataStateRamValue>
{

	public override TestDataStateRamValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartArray)
			return null;

		if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
			return null;

		var address = reader.GetUInt16();

		if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
			return null;

		var value = reader.GetByte();

		if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
			return null;

		return new()
		{
			Address = address,
			Value = value
		};
	}

	public override void Write(Utf8JsonWriter writer, TestDataStateRamValue value, JsonSerializerOptions options) => throw new NotSupportedException();
}
